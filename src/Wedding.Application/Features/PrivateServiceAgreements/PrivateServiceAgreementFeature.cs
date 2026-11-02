using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.PrivateServiceAgreements;
public record PrivateServiceLine(string Name, int Quantity, string Details);
public record PrivateAgreementDto(Guid Id, int Sequence, string Status, List<PrivateServiceLine> Items, string Note,
    string Terms, DateTime CreatedAt, DateTime? RespondedAt, string? Response);
public record PrivateAgreementLeadOption(Guid LeadId, string ListingTitle);
public record PrivateAgreementWorkspace(string Kind, Guid TargetId, decimal CurrentTotal, string CurrentVersion,
    List<PrivateServiceLine> OriginalContent, List<PrivateServiceLine> EffectiveContent, List<PrivateAgreementDto> History,
    bool CanPropose, bool CanRespond, string? BlockedReason, Guid? LeadId, bool CanWithdraw,
    Guid? SourceComboId = null, List<PrivateServiceLine>? SourceComboContent = null,
    List<PrivateAgreementLeadOption>? LeadOptions = null);
public record GetPrivateAgreementQuery(string Kind, Guid TargetId, Guid? LeadId = null) : IRequest<PrivateAgreementWorkspace>;
public record ProposePrivateAgreementCommand(string Kind, Guid TargetId, string ExpectedVersion,
    List<PrivateServiceLine> Items, string Note, string? Terms) : IRequest<PrivateAgreementWorkspace>;
public record RespondPrivateAgreementCommand(string Kind, Guid TargetId, Guid ProposalId, string ExpectedVersion,
    bool Accept, string? Note, bool Withdraw = false) : IRequest<PrivateAgreementWorkspace>;

internal record PrivateAgreementTarget(string Kind, Guid Id, Guid VendorId, Guid CustomerId, decimal Total,
    string State, string OriginalVersion, bool Open, string? BlockedReason, Guid? LeadId, List<PrivateServiceLine> Original,
    Guid? SourceComboId = null, List<PrivateAgreementLeadOption>? LeadOptions = null);
internal static class PrivateAgreementAccess
{
    // Stored JSON / ordinary DTO text only, never injected as HTML or an inline script.
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static List<PrivateServiceLine> Lines(string? json)
    { try { return JsonSerializer.Deserialize<List<PrivateServiceLine>>(json ?? "[]", Json) ?? []; } catch (JsonException) { return []; } }
    internal static string FrozenDetails(ComboOrderItem item)
    {
        var snapshot = ComboFixedPackages.Read(item.ComponentSnapshotJson);
        if (snapshot == null) return item.CancellationPolicySnapshot ?? "";
        var d = snapshot.ServiceDetails;
        var sections = new (string Label, string? Value)[] { ("Mô tả", snapshot.Description),
            ("Bao gồm", d?.IncludedItems), ("Không bao gồm", d?.ExcludedItems), ("Thời lượng", d?.Duration),
            ("Quy trình", d?.Process), ("Điều kiện", d?.Terms), ("Đổi lịch", d?.ReschedulePolicy),
            ("Phụ phí", d?.AdditionalFees), ("Yêu cầu khách hàng", d?.CustomerRequirements),
            ("Phạm vi trọn gói", d?.FullService?.Scope), ("Các giai đoạn", d?.FullService?.Stages),
            ("Chính sách hủy", snapshot.CancellationPolicy) };
        return string.Join("\n\n", sections.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => $"{x.Label}: {x.Value}"));
    }
    internal static async Task<(User Actor, PrivateAgreementTarget Target)> Target(IApplicationDbContext db, ICurrentUserService user,
        string kind, Guid id, Guid? requestedLeadId, CancellationToken ct)
    {
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == user.UserId && x.IsActive, ct);
        if (actor == null || actor.Role.ToString() != user.Role || actor.Role is not (UserRole.Customer or UserRole.VendorOwner))
            throw new DomainException("Đề xuất riêng chỉ dành cho khách và NCC của giao dịch, không phải hàng chờ kiểm duyệt công khai.");
        Guid vendorId, customerId; decimal total; string state, original; bool open; string? blocked; Guid? leadId;
        Guid? sourceComboId = null; List<PrivateAgreementLeadOption> leadOptions = []; List<PrivateServiceLine> content;
        if (kind == "combo")
        {
            var order = await db.ComboOrders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Không tìm thấy đơn hàng.");
            var vendorIds = order.Items.Select(x => x.VendorId).Distinct().ToList();
            if (vendorIds.Count != 1) throw new DomainException("Đơn cũ nhiều NCC cần xử lý riêng tại từng hợp đồng.");
            vendorId = vendorIds[0]; customerId = order.CustomerId; total = order.Total; state = order.Status.ToString();
            var linkedItems = order.Items.Where(x => x.LeadId.HasValue).ToList();
            var leads = linkedItems.Select(x => x.LeadId!.Value).Distinct().ToList();
            var contracted = await db.BookingContracts.AnyAsync(x => leads.Contains(x.LeadId) && x.Status != ContractStatus.Cancelled, ct);
            var linkedLeads = await db.Leads.AsNoTracking().Where(x => leads.Contains(x.Id)).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.CustomerId, x.VendorId, x.Status }).ToListAsync(ct);
            var validLeadLinks = linkedLeads.Count == leads.Count && linkedLeads.All(x => x.CustomerId == order.CustomerId && x.VendorId == vendorId);
            if (requestedLeadId.HasValue && (!validLeadLinks || !leads.Contains(requestedLeadId.Value)))
                throw new NotFoundException("Không tìm thấy yêu cầu tư vấn thuộc đơn combo này.");
            leadId = requestedLeadId ?? (leads.Count == 1 && validLeadLinks ? leads[0] : null);
            if (validLeadLinks)
                leadOptions = linkedItems.GroupBy(x => x.LeadId!.Value).Select(group =>
                    new PrivateAgreementLeadOption(group.Key, string.Join(", ", group.Select(x => x.Title).Distinct()))).ToList();
            var leadsOpen = leads.Count == order.Items.Count && validLeadLinks && linkedLeads.All(x => x.Status is LeadStatus.New or LeadStatus.Accepted or LeadStatus.Contacted);
            open = order.Status is not (ComboOrderStatus.Draft or ComboOrderStatus.Cancelled or ComboOrderStatus.Completed or ComboOrderStatus.Expired) && !contracted && leadsOpen;
            blocked = contracted ? "Đơn đã có hợp đồng. Hãy đối chiếu nội dung đã thống nhất với báo giá/hợp đồng; thay đổi tiếp cần đề xuất riêng tại đúng hợp đồng." : !leadsOpen ? "Yêu cầu tư vấn đã dừng/hết hạn hoặc thiếu liên kết. Không thể xác nhận thay đổi nội dung của đơn này." : open ? null : "Đơn không còn ở trạng thái nhận đề xuất nội dung.";
            content = order.Items.OrderBy(x => x.Id).Select(x => new PrivateServiceLine(x.Title, x.Quantity, FrozenDetails(x))).ToList();
            original = JsonSerializer.Serialize(new { Leads = linkedLeads.Select(x => new { x.Id, x.Status }), Items = order.Items.OrderBy(x => x.Id).Select(x => new { x.Id, x.Title, x.Quantity, x.Unit, x.ServiceItemId,
                Price = x.Price.ToString("0", CultureInfo.InvariantCulture), Discount = x.DiscountShare.ToString("0", CultureInfo.InvariantCulture),
                x.SelectionJson, x.ComponentSnapshotJson, x.CancellationPolicySnapshot }) }, Json);
        }
        else if (kind == "contract")
        {
            var contract = await db.BookingContracts.AsNoTracking().Include(x => x.Lead).ThenInclude(x => x.Listing).SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Không tìm thấy hợp đồng.");
            if (requestedLeadId.HasValue && requestedLeadId != contract.LeadId)
                throw new NotFoundException("Không tìm thấy yêu cầu tư vấn thuộc hợp đồng này.");
            vendorId = contract.VendorId; customerId = contract.CustomerId; total = contract.ContractValue; state = contract.Status.ToString(); leadId = contract.LeadId;
            leadOptions = [new PrivateAgreementLeadOption(contract.LeadId, contract.Lead.Listing?.Title ?? contract.ContractCode)];
            sourceComboId = contract.Lead.ComboOrderId;
            open = contract.Status == ContractStatus.Confirmed && contract.Lead.Status is not (LeadStatus.Cancelled or LeadStatus.Expired); blocked = open ? null : "Chỉ hợp đồng đã xác nhận, đang thực hiện và không tranh chấp mới nhận đề xuất nội dung riêng.";
            var quoteJson = contract.QuoteId.HasValue ? await db.Quotes.Where(x => x.Id == contract.QuoteId).Select(x => x.ItemsJson).SingleOrDefaultAsync(ct) : null;
            content = Lines(quoteJson); // Quote lines retain their original names and quantities, not today's service catalog.
            content = content.Select(x => x with { Details = x.Details ?? "" }).ToList();
            if (content.Count == 0) content = [new(contract.ContractCode, 1, contract.CancellationPolicySnapshot ?? "Nội dung theo hợp đồng gốc.")];
            original = JsonSerializer.Serialize(new { contract.Version, contract.QuoteId, contract.CommissionRateSnapshot, contract.CancellationPolicySnapshot, LeadStatus = contract.Lead.Status, Items = content }, Json);
        }
        else throw new DomainException("Loại giao dịch không hợp lệ.");
        var vendor = await db.Vendors.AsNoTracking().Include(x => x.User).SingleAsync(x => x.Id == vendorId, ct);
        if (actor.Role == UserRole.Customer ? actor.Id != customerId : vendor.UserId != actor.Id)
            throw new NotFoundException("Không tìm thấy giao dịch thuộc tài khoản của bạn.");
        var activeCustomer = await db.Users.AnyAsync(x => x.Id == customerId && x.IsActive && x.Role == UserRole.Customer, ct);
        if (!vendor.IsActive || !vendor.User.IsActive || vendor.User.Role != UserRole.VendorOwner || !activeCustomer)
        { open = false; blocked = "Một bên giao dịch không còn hoạt động; không thể lập hoặc xác nhận đề xuất mới."; }
        var version = Hash($"{kind}|{id:D}|{vendorId:D}|{customerId:D}|{total.ToString("0", CultureInfo.InvariantCulture)}|{state}|{original}");
        return (actor, new(kind, id, vendorId, customerId, total, state, version, open, blocked, leadId, content, sourceComboId, leadOptions));
    }
    internal static async Task<PrivateAgreementWorkspace> Workspace(IApplicationDbContext db, User actor, PrivateAgreementTarget t, CancellationToken ct)
    {
        var rows = await db.PrivateServiceAgreements.AsNoTracking().Where(x => x.Kind == t.Kind && x.TargetId == t.Id).OrderByDescending(x => x.Sequence).ToListAsync(ct);
        var current = Hash(t.OriginalVersion + "|" + string.Join("|", rows.Select(x => $"{x.Id:D}:{x.Status}")));
        var effective = rows.FirstOrDefault(x => x.Status == "Accepted");
        var history = rows.Select(x => new PrivateAgreementDto(x.Id, x.Sequence, x.Status, Lines(x.ItemsJson), x.Note, x.Terms,
            DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc), x.RespondedAt.HasValue ? DateTime.SpecifyKind(x.RespondedAt.Value, DateTimeKind.Utc) : null, x.Response)).ToList();
        var pending = rows.FirstOrDefault(x => x.Status == "PendingCustomer");
        var stale = pending != null && pending.OriginalVersion != t.OriginalVersion;
        var source = t.SourceComboId.HasValue ? await db.PrivateServiceAgreements.AsNoTracking().Where(x => x.Kind == "combo" && x.TargetId == t.SourceComboId && x.VendorId == t.VendorId && x.CustomerId == t.CustomerId && x.Status == "Accepted").OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct) : null;
        return new(t.Kind, t.Id, t.Total, current, t.Original, effective == null ? t.Original : Lines(effective.ItemsJson), history,
            t.Open && actor.Role == UserRole.VendorOwner && pending == null, t.Open && actor.Role == UserRole.Customer && pending != null && !stale,
            t.BlockedReason ?? (stale ? "Hồ sơ gốc đã thay đổi. NCC cần rút đề xuất cũ và gửi lại." : null), t.LeadId,
            actor.Role == UserRole.VendorOwner && pending != null && pending.CreatedById == actor.Id,
            t.SourceComboId, source == null ? null : Lines(source.ItemsJson), t.LeadOptions);
    }
}
public class GetPrivateAgreementQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetPrivateAgreementQuery, PrivateAgreementWorkspace>
{
    public async Task<PrivateAgreementWorkspace> Handle(GetPrivateAgreementQuery r, CancellationToken ct)
    { var (actor, target) = await PrivateAgreementAccess.Target(db, user, r.Kind, r.TargetId, r.LeadId, ct); return await PrivateAgreementAccess.Workspace(db, actor, target, ct); }
}
public class ProposePrivateAgreementCommandHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<ProposePrivateAgreementCommand, PrivateAgreementWorkspace>
{
    public Task<PrivateAgreementWorkspace> Handle(ProposePrivateAgreementCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var (actor, target) = await PrivateAgreementAccess.Target(db, user, r.Kind, r.TargetId, null, token);
        var current = await PrivateAgreementAccess.Workspace(db, actor, target, token);
        if (!current.CanPropose) throw new DomainException(current.BlockedReason ?? "Chỉ NCC được lập đề xuất; cần xử lý đề xuất đang chờ trước.");
        if (r.ExpectedVersion != current.CurrentVersion) throw new DomainException("Giao dịch đã thay đổi. Tải lại trước khi gửi đề xuất.");
        if (r.Items is not { Count: >= 1 and <= 30 } || r.Items.Any(x => x == null || string.IsNullOrWhiteSpace(x.Name) || x.Name.Length > 200 || x.Quantity is < 1 or > 10000 || x.Details?.Length > 10000) ||
            string.IsNullOrWhiteSpace(r.Note) || r.Note.Trim().Length > 2000 || r.Terms?.Length > 4000)
            throw new DomainException("Nhập 1–30 hạng mục, tên tối đa 200 ký tự, chi tiết 10.000, số lượng 1–10.000 và lý do tối đa 2000 ký tự.");
        var items = r.Items.Select(x => new PrivateServiceLine(x.Name.Trim(), x.Quantity, x.Details?.Trim() ?? "")).ToList();
        var itemsJson = JsonSerializer.Serialize(items, PrivateAgreementAccess.Json);
        if (itemsJson.Length > 40000) throw new DomainException("Tổng nội dung đề xuất quá dài (tối đa 40.000 ký tự JSON). Hãy viết gọn phạm vi đã thống nhất.");
        var row = PrivateServiceAgreement.Create(target.Kind, target.Id, target.VendorId, target.CustomerId,
            (current.History.FirstOrDefault()?.Sequence ?? 0) + 1, actor.Id, target.OriginalVersion, target.Total, itemsJson, r.Note.Trim(), r.Terms?.Trim() ?? "");
        db.PrivateServiceAgreements.Add(row);
        db.AuditLogs.Add(AuditLog.Create(actor.Id, actor.Role.ToString(), "PrivateServices.Propose", nameof(PrivateServiceAgreement), row.Id.ToString()));
        db.Notifications.Add(Notification.Create(target.CustomerId, "InApp", "", "NCC đã gửi đề xuất thay đổi nội dung riêng. Hãy xem và xác nhận; giá và hoa hồng gốc chưa thay đổi.", $"private-services:{row.Id:D}:created", contextKind: target.Kind, contextId: target.Id));
        await db.SaveChangesAsync(token); return await PrivateAgreementAccess.Workspace(db, actor, target, token);
    }, ct);
}
public class RespondPrivateAgreementCommandHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<RespondPrivateAgreementCommand, PrivateAgreementWorkspace>
{
    public Task<PrivateAgreementWorkspace> Handle(RespondPrivateAgreementCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var (actor, target) = await PrivateAgreementAccess.Target(db, user, r.Kind, r.TargetId, null, token);
        var current = await PrivateAgreementAccess.Workspace(db, actor, target, token);
        if (r.ExpectedVersion != current.CurrentVersion || r.Note?.Length > 2000) throw new DomainException("Giao dịch đã đổi trạng thái/phiên bản hoặc phản hồi không hợp lệ. Hãy tải lại.");
        var row = await db.PrivateServiceAgreements.SingleOrDefaultAsync(x => x.Id == r.ProposalId && x.Kind == r.Kind && x.TargetId == r.TargetId, token)
            ?? throw new NotFoundException("Không tìm thấy đề xuất của giao dịch này.");
        if (r.Withdraw) { if (actor.Role != UserRole.VendorOwner) throw new DomainException("Chỉ NCC được rút đề xuất."); row.Withdraw(actor.Id); }
        else
        {
            if (!current.CanRespond || row.OriginalVersion != target.OriginalVersion)
                throw new DomainException(current.BlockedReason ?? "Chỉ khách hàng của giao dịch được phản hồi đề xuất đang chờ.");
            row.Respond(actor.Id, r.Accept, r.Note?.Trim());
        }
        db.AuditLogs.Add(AuditLog.Create(actor.Id, actor.Role.ToString(), "PrivateServices." + row.Status, nameof(PrivateServiceAgreement), row.Id.ToString()));
        var recipient = actor.Role == UserRole.Customer ? await db.Vendors.Where(x => x.Id == target.VendorId).Select(x => x.UserId).SingleAsync(token) : target.CustomerId;
        db.Notifications.Add(Notification.Create(recipient, "InApp", "", $"Đề xuất nội dung riêng #{row.Sequence}: {row.Status}. Không tự thay đổi giá, thanh toán, hoa hồng hoặc combo công khai.", $"private-services:{row.Id:D}:{row.Status}", contextKind: target.Kind, contextId: target.Id));
        await db.SaveChangesAsync(token); return await PrivateAgreementAccess.Workspace(db, actor, target, token);
    }, ct);
}
