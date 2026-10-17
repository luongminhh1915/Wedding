using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.CustomerOrders;

public record CustomerOrderActionCommand(string Kind, Guid Id, string Action, string? Reason = null) : IRequest<OrderJourneyItemDto>;
public class CustomerOrderActionCommandHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<CustomerOrderActionCommand, OrderJourneyItemDto>
{
    public Task<OrderJourneyItemDto> Handle(CustomerOrderActionCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var item = (await CustomerOrderJourney.LoadAsync(db, uid, token)).FirstOrDefault(x => x.Kind == r.Kind && x.Id == r.Id)
            ?? throw new NotFoundException("Không tìm thấy dữ liệu thuộc tài khoản của bạn.");
        var state = await CustomerOrderJourney.StateAsync(db, uid, r.Kind, r.Id, token);
        if (r.Action is "archive" or "restore") state.Archive(r.Action == "archive");
        else
        {
            OrderChangeRequest.ValidateReason(r.Reason);
            if (r.Action == "withdraw")
            {
                if (!item.Capabilities.CanWithdraw) throw new DomainException(item.Capabilities.BlockedReason ?? "Yêu cầu không còn có thể rút.");
                if (r.Kind == "combo")
                {
                    var order = await db.ComboOrders.Include(o => o.Items).SingleAsync(o => o.Id == r.Id && o.CustomerId == uid, token);
                    await CustomerOrderJourney.WithdrawLeadsAsync(db, order.Items.Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList(), r.Reason!, token);
                    order.Cancel(); foreach (var line in order.Items) { line.SetStatus(ComboItemStatus.Cancelled); await ComboBudgetSynchronization.ReleaseAsync(db, line, token); }
                }
                else if (r.Kind == "lead") await CustomerOrderJourney.WithdrawLeadsAsync(db, new[] { r.Id }, r.Reason!, token);
                else throw new DomainException("Chỉ rút combo hoặc yêu cầu tư vấn chưa có hợp đồng/cọc.");
                state.Withdraw(r.Reason!);
            }
            else if (r.Action == "stop-pending" && r.Kind == "contract")
            {
                if (!item.Capabilities.CanStopPending) throw new DomainException(item.Capabilities.BlockedReason ?? "Chỉ dừng hợp đồng nháp/chờ xác nhận chưa có nghĩa vụ tài chính.");
                var contract = await db.BookingContracts.SingleAsync(c => c.Id == r.Id && c.CustomerId == uid, token);
                contract.Cancel(CustomerOrderJourney.ShortReason(r.Reason!));
                await CustomerOrderJourney.WithdrawLeadsAsync(db, new[] { contract.LeadId }, r.Reason!, token);
                state.Withdraw(r.Reason!);
                var line = await db.ComboOrderItems.Include(i => i.ComboOrder).FirstOrDefaultAsync(i => i.LeadId == contract.LeadId, token);
                if (line != null) { line.SetStatus(ComboItemStatus.Cancelled); await ComboBudgetSynchronization.ReleaseAsync(db, line, token); line.ComboOrder.Recalculate(); }
            }
            else throw new DomainException("Thao tác không hợp lệ.");
            var recipients = r.Kind == "combo"
                ? await db.ComboOrderItems.Where(i => i.ComboOrderId == r.Id).Select(i => i.Vendor.UserId).Distinct().ToListAsync(token)
                : item.Media.VendorId.HasValue ? await db.Vendors.Where(v => v.Id == item.Media.VendorId).Select(v => v.UserId).ToListAsync(token) : new List<Guid>();
            foreach (var recipient in recipients)
                db.Notifications.Add(Notification.Create(recipient, "InApp", "", $"Khách đã dừng/rút {item.Media.Title}. Không tiếp tục báo giá/hợp đồng cũ. Lý do: {r.Reason}"));
        }
        db.AuditLogs.Add(AuditLog.Create(uid, user.Role, $"CustomerOrder.{r.Action}", r.Kind, r.Id.ToString(), JsonSerializer.Serialize(new { r.Reason })));
        await db.SaveChangesAsync(token);
        return (await CustomerOrderJourney.LoadAsync(db, uid, token)).Single(x => x.Kind == r.Kind && x.Id == r.Id);
    }, ct);
}

public record ReviseCustomerLeadCommand(Guid Id, string Reason, Guid? ListingId = null, string? PhoneNumber = null,
    DateTime? WeddingDate = null, int? EstimatedGuests = null, decimal? EstimatedBudget = null, string? Notes = null) : IRequest<OrderJourneyItemDto>;
public class ReviseCustomerLeadCommandHandler(IApplicationDbContext db, ICurrentUserService user, INotificationSender notify) : IRequestHandler<ReviseCustomerLeadCommand, OrderJourneyItemDto>
{
    public Task<OrderJourneyItemDto> Handle(ReviseCustomerLeadCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập."); OrderChangeRequest.ValidateReason(r.Reason);
        var meta = (await CustomerOrderJourney.LoadAsync(db, uid, token)).FirstOrDefault(x => x.Kind == "lead" && x.Id == r.Id) ?? throw new NotFoundException("Không tìm thấy yêu cầu tư vấn.");
        if (!meta.Capabilities.CanEdit) throw new DomainException(meta.Capabilities.BlockedReason ?? "Yêu cầu này không thể sửa thay thế.");
        var source = await db.Leads.Include(l => l.Vendor).SingleAsync(l => l.Id == r.Id && l.CustomerId == uid, token);
        if (!source.Vendor.IsActive || !source.Vendor.IsVerified) throw new DomainException("NCC không còn đủ điều kiện nhận yêu cầu mới.");
        var listingId = r.ListingId ?? source.ListingId;
        if (listingId.HasValue && !await db.Listings.AnyAsync(l => l.Id == listingId && l.VendorId == source.VendorId && l.Status == ListingStatus.Active, token))
            throw new DomainException("Chỉ sửa sang dịch vụ đang hoạt động của cùng NCC.");
        var phone = string.IsNullOrWhiteSpace(r.PhoneNumber) ? source.RawPhoneNumber : r.PhoneNumber.Trim();
        if (phone.Length < 9 || phone.Length > 20 || phone.Any(c => !char.IsDigit(c) && c != '+')) throw new DomainException("Số điện thoại không hợp lệ.");
        if (r.EstimatedGuests is <= 0 or > 10000 || r.EstimatedBudget is <= 0 || r.Notes?.Length > 1000 ||
            (r.WeddingDate.HasValue && r.WeddingDate.Value.Date < DateTime.UtcNow.AddHours(7).Date)) throw new DomainException("Ngày cưới, số khách, ngân sách hoặc ghi chú không hợp lệ.");
        var lead = Lead.Create(uid, source.VendorId, listingId, phone, r.WeddingDate ?? source.WeddingDate,
            r.EstimatedGuests ?? source.EstimatedGuests, r.EstimatedBudget ?? source.EstimatedBudget, r.Notes ?? source.Notes);
        lead.AttachSource("Revision"); db.Leads.Add(lead); db.Vouchers.Add(Voucher.Create(lead.Id, uid, Guid.NewGuid().ToString("N")[..8]));
        await CustomerOrderJourney.WithdrawLeadsAsync(db, new[] { source.Id }, $"Thay thế bởi yêu cầu mới: {r.Reason}", token);
        (await CustomerOrderJourney.StateAsync(db, uid, "lead", source.Id, token)).Supersede(lead.Id);
        (await CustomerOrderJourney.StateAsync(db, uid, "lead", lead.Id, token)).RevisionOf(source.Id, r.Reason);
        db.AuditLogs.Add(AuditLog.Create(uid, user.Role, "CustomerOrder.Revise", "lead", source.Id.ToString(), JsonSerializer.Serialize(new { replacementId = lead.Id, r.Reason })));
        await notify.SendAsync("InApp", source.Vendor.UserId.ToString(), "Khách đã gửi phiên bản tư vấn mới; yêu cầu/báo giá cũ đã dừng. Vui lòng xem yêu cầu mới.", token);
        await db.SaveChangesAsync(token);
        return (await CustomerOrderJourney.LoadAsync(db, uid, token)).Single(x => x.Kind == "lead" && x.Id == lead.Id);
    }, ct);
}

public record CreateOrderChangeRequestCommand(string Kind, Guid Id, string Type, string Reason, string? ProposedChanges = null) : IRequest<ChangeRequestDto>;
public class CreateOrderChangeRequestCommandHandler(IApplicationDbContext db, ICurrentUserService user, INotificationSender notify) : IRequestHandler<CreateOrderChangeRequestCommand, ChangeRequestDto>
{
    public Task<ChangeRequestDto> Handle(CreateOrderChangeRequestCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var item = (await CustomerOrderJourney.LoadAsync(db, uid, token)).FirstOrDefault(x => x.Kind == r.Kind && x.Id == r.Id) ?? throw new NotFoundException("Không tìm thấy dữ liệu thuộc tài khoản của bạn.");
        if (!(r.Type == "Change" ? item.Capabilities.CanRequestChange : r.Type == "Cancellation" && item.Capabilities.CanRequestCancellation))
            throw new DomainException(item.Capabilities.BlockedReason ?? "Không thể gửi loại đề nghị này cho trạng thái hiện tại.");
        if (await db.OrderChangeRequests.AnyAsync(x => x.CustomerId == uid && x.Kind == r.Kind && x.EntityId == r.Id &&
            (x.Status == "PendingVendor" || x.Status == "PendingFinance"), token)) throw new DomainException("Đã có đề nghị đang xử lý. Hãy chờ phản hồi trước khi gửi thêm.");
        var vendorId = item.Media.VendorId ?? throw new DomainException("Vui lòng gửi đề nghị riêng tại từng hợp đồng/NCC.");
        var requiresFinance = await CustomerOrderJourney.RequiresFinanceAsync(db, r.Kind, r.Id, uid, token);
        var request = OrderChangeRequest.Create(uid, vendorId, r.Kind, r.Id, r.Type, r.Reason, r.ProposedChanges, requiresFinance);
        db.OrderChangeRequests.Add(request); db.AuditLogs.Add(AuditLog.Create(uid, user.Role, "CustomerOrder.RequestCreated", r.Kind, r.Id.ToString(), JsonSerializer.Serialize(new { request.Id, r.Type })));
        var vendor = await db.Vendors.SingleAsync(v => v.Id == vendorId, token);
        await notify.SendAsync("InApp", vendor.UserId.ToString(), "Khách gửi đề nghị thay đổi/hủy. Tiếp nhận đề nghị không tự sửa hợp đồng hoặc hoàn tiền; cần phối hợp với khách và Tài chính.", token);
        await db.SaveChangesAsync(token); return CustomerOrderJourney.Map(request);
    }, ct);
}

public record GetOrderChangeRequestsQuery(bool Finance) : IRequest<List<ChangeRequestDto>>;
public class GetOrderChangeRequestsQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetOrderChangeRequestsQuery, List<ChangeRequestDto>>
{
    public async Task<List<ChangeRequestDto>> Handle(GetOrderChangeRequestsQuery r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        if (r.Finance && user.Role is not ("Finance" or "SuperAdmin")) throw new DomainException("Chỉ Tài chính được xem hàng đợi này.");
        var q = db.OrderChangeRequests.AsNoTracking();
        if (!r.Finance)
        {
            var vendor = await db.Vendors.SingleOrDefaultAsync(v => v.UserId == uid, ct) ?? throw new DomainException("Không tìm thấy NCC của bạn.");
            q = q.Where(x => x.VendorId == vendor.Id);
        }
        else q = q.Where(x => x.RequiresFinance);
        return (await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct)).Select(CustomerOrderJourney.Map).ToList();
    }
}
public record ReviewOrderChangeRequestCommand(Guid Id, bool Finance, bool Accept, string Response) : IRequest<ChangeRequestDto>;
public class ReviewOrderChangeRequestCommandHandler(IApplicationDbContext db, ICurrentUserService user, INotificationSender notify) : IRequestHandler<ReviewOrderChangeRequestCommand, ChangeRequestDto>
{
    public Task<ChangeRequestDto> Handle(ReviewOrderChangeRequestCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var request = await db.OrderChangeRequests.SingleOrDefaultAsync(x => x.Id == r.Id, token) ?? throw new NotFoundException("Không tìm thấy đề nghị.");
        if (r.Finance)
        {
            if (user.Role is not ("Finance" or "SuperAdmin")) throw new DomainException("Chỉ Tài chính được phản hồi bước này.");
            request.ReviewFinance(uid, r.Accept, r.Response);
        }
        else
        {
            if (user.Role != "VendorOwner" || !await db.Vendors.AnyAsync(v => v.Id == request.VendorId && v.UserId == uid, token)) throw new DomainException("Bạn không phải NCC nhận đề nghị này.");
            // Money/confirmation may have arrived since the request was created. Never rely on a stale snapshot.
            if (r.Accept && await CustomerOrderJourney.RequiresFinanceAsync(db, request.Kind, request.EntityId, request.CustomerId, token)) request.RequireFinance();
            request.ReviewVendor(uid, r.Accept, r.Response);
        }
        db.AuditLogs.Add(AuditLog.Create(uid, user.Role, r.Finance ? "CustomerOrder.FinanceAcknowledgement" : "CustomerOrder.VendorAcknowledgement", "OrderChangeRequest", r.Id.ToString(), JsonSerializer.Serialize(new { r.Accept, r.Response })));
        await notify.SendAsync("InApp", request.CustomerId.ToString(), $"Đề nghị đã có phản hồi: {request.Status}. Đây là tiếp nhận phối hợp; hợp đồng và khoản tiền chưa tự thay đổi. {r.Response}", token);
        if (request.Status == "PendingFinance")
            foreach (var id in await db.Users.Where(u => u.Role == UserRole.Finance || u.Role == UserRole.SuperAdmin).Select(u => u.Id).ToListAsync(token))
                await notify.SendAsync("InApp", id.ToString(), "Có đề nghị hợp đồng/cọc đã được NCC tiếp nhận, cần Tài chính đối soát. Phản hồi đề nghị không tự phát lệnh hoàn tiền.", token);
        await db.SaveChangesAsync(token); return CustomerOrderJourney.Map(request);
    }, ct);
}
