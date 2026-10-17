using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.CustomerOrders;

public record OrderServiceMediaDto(Guid ListingId, Guid? ServiceItemId, string Title, string? ImageUrl, List<string> GalleryUrls);
public record OrderMediaDto(string Title, string? ImageUrl, List<string> GalleryUrls, Guid? VendorId, string? VendorName,
    string? VendorLogoUrl, string? VendorCoverImageUrl, Guid? ListingId, List<OrderServiceMediaDto> Items);
public record OrderCapabilitiesDto(bool CanEdit, bool CanWithdraw, bool CanArchive, bool CanRestore,
    bool CanRequestChanges, bool CanStopPending, bool CanRequestChange, bool CanRequestCancellation, string? BlockedReason,
    bool CanConfirm = false, bool CanRespondQuote = false);
public record ChangeRequestDto(Guid Id, string Kind, Guid EntityId, Guid CustomerId, Guid VendorId, string Type, string Status,
    string Reason, string? ProposedChanges, DateTime CreatedAt, DateTime? VendorReviewedAt, string? VendorResponse,
    DateTime? FinanceReviewedAt, string? FinanceResponse, bool RequiresFinance);
public record OrderJourneyItemDto(string Kind, Guid Id, bool IsArchived, DateTime? ArchivedAt, Guid? RevisionOfId, Guid? SupersededById,
    OrderMediaDto Media, OrderCapabilitiesDto Capabilities, List<ChangeRequestDto> Requests,
    Guid? ComboOrderId = null, string? ComboOrderCode = null, string? RevisionReason = null, string? WithdrawalReason = null);

/// <summary>Public image allowlist and capabilities derived from actual records, never the combo item's cached status.</summary>
public static class CustomerOrderJourney
{
    public static string ShortReason(string reason) => reason.Length <= 500 ? reason : reason[..497] + "...";
    public static ChangeRequestDto Map(OrderChangeRequest x) => new(x.Id, x.Kind, x.EntityId, x.CustomerId, x.VendorId, x.Type, x.Status,
        x.Reason, x.ProposedChanges, x.CreatedAt, x.VendorReviewedAt, x.VendorResponse, x.FinanceReviewedAt, x.FinanceResponse, x.RequiresFinance);

    public static void EnsureActiveLead(Lead lead)
    {
        if (lead.Status is LeadStatus.Cancelled or LeadStatus.Expired) throw new DomainException("Yêu cầu tư vấn đã rút/hết hạn hoặc đã được thay thế. Không thể tiếp tục báo giá/hợp đồng cũ.");
    }

    public static async Task<CustomerOrderState> StateAsync(IApplicationDbContext db, Guid customer, string kind, Guid id, CancellationToken ct)
    {
        var local = db.CustomerOrderStates.Local.FirstOrDefault(x => x.CustomerId == customer && x.Kind == kind && x.EntityId == id);
        if (local != null) return local;
        var state = await db.CustomerOrderStates.FirstOrDefaultAsync(x => x.CustomerId == customer && x.Kind == kind && x.EntityId == id, ct);
        if (state != null) return state;
        state = CustomerOrderState.Create(customer, kind, id); db.CustomerOrderStates.Add(state); return state;
    }

    public static async Task<bool> HasMoneyAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> leads, CancellationToken ct)
    {
        if (leads.Count == 0) return false;
        // A reported but unconfirmed external transfer must also be reconciled before withdrawal.
        if (await db.Appointments.AnyAsync(a => a.LeadId.HasValue && leads.Contains(a.LeadId.Value) &&
                (a.DepositPaid || a.DepositReportedAt != null) && a.RefundedAt == null, ct)) return true;
        var contracts = await db.BookingContracts.Where(c => leads.Contains(c.LeadId)).Select(c => c.Id).ToListAsync(ct);
        return await db.Commissions.AnyAsync(c => contracts.Contains(c.ContractId), ct) ||
            await db.AdvancePayments.AnyAsync(a => contracts.Contains(a.ContractId) && a.Status != "Rejected", ct) ||
            await db.BookingContracts.AnyAsync(c => contracts.Contains(c.Id) && (c.ConfirmedAt != null || c.AppointmentDepositCredit > 0), ct);
    }

    public static async Task<string?> WithdrawalBlockAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> leads, CancellationToken ct)
    {
        if (await db.BookingContracts.AnyAsync(c => leads.Contains(c.LeadId) && c.Status != ContractStatus.Cancelled, ct))
            return "Đã có hợp đồng thực tế (kể cả bản nháp/chờ xác nhận). Hãy xử lý từng hợp đồng trước; hợp đồng đã xác nhận chỉ gửi yêu cầu thay đổi/hủy để NCC và Tài chính phối hợp.";
        if (await HasMoneyAsync(db, leads, ct)) return "Đã có cọc/chuyển khoản được báo hoặc nghĩa vụ tài chính. Cần NCC/Tài chính đối soát; không thể tự rút hoặc sửa thay thế.";
        return null;
    }

    public static async Task<bool> RequiresFinanceAsync(IApplicationDbContext db, string kind, Guid id, Guid customer, CancellationToken ct)
    {
        if (kind == "contract")
        {
            var contract = await db.BookingContracts.SingleAsync(c => c.Id == id && c.CustomerId == customer, ct);
            return contract.Status is ContractStatus.Confirmed or ContractStatus.Disputed || await HasMoneyAsync(db, new[] { contract.LeadId }, ct);
        }
        if (kind == "lead") return await HasMoneyAsync(db, new[] { id }, ct);
        var leads = await db.ComboOrderItems.Where(i => i.ComboOrderId == id && i.ComboOrder.CustomerId == customer && i.LeadId != null).Select(i => i.LeadId!.Value).ToListAsync(ct);
        return await HasMoneyAsync(db, leads, ct);
    }

    public static async Task EnsureCanReplaceComboAsync(IApplicationDbContext db, ComboOrder source, CancellationToken ct)
    {
        if (source.Status == ComboOrderStatus.Cancelled) throw new DomainException("Combo đã rút, không thể sửa. Hãy tạo combo mới.");
        if (source.Items.Select(i => i.VendorId).Distinct().Count() != 1) throw new DomainException("Combo cũ có nhiều NCC chỉ được giữ làm lịch sử, không thể gửi lại.");
        var state = await db.CustomerOrderStates.FirstOrDefaultAsync(s => s.CustomerId == source.CustomerId && s.Kind == "combo" && s.EntityId == source.Id, ct);
        if (state?.SupersededById != null) throw new DomainException("Combo đã được thay thế. Vui lòng mở phiên bản mới nhất.");
        var block = await WithdrawalBlockAsync(db, source.Items.Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList(), ct);
        if (block != null) throw new DomainException(block);
    }

    public static async Task WithdrawLeadsAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> ids, string reason, CancellationToken ct)
    {
        foreach (var lead in await db.Leads.Where(l => ids.Contains(l.Id)).ToListAsync(ct))
            if (lead.Status is not (LeadStatus.Cancelled or LeadStatus.Expired)) lead.Cancel(ShortReason(reason));
        foreach (var quote in await db.Quotes.Where(q => ids.Contains(q.LeadId)).ToListAsync(ct)) quote.Withdraw();
        foreach (var voucher in await db.Vouchers.Where(v => ids.Contains(v.LeadId) && v.Status == VoucherStatus.Active).ToListAsync(ct)) voucher.Expire();
        // Withdraw only after the caller has excluded all paid/reported deposits. Close unpaid linked meetings too,
        // so no later deposit report can restart a request that the customer has withdrawn.
        foreach (var appointment in await db.Appointments.Include(a => a.AppointmentType).Where(a => a.LeadId.HasValue && ids.Contains(a.LeadId.Value)).ToListAsync(ct))
            if (appointment.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.AwaitingDeposit or AppointmentStatus.RescheduleProposed)
            {
                if (appointment.DepositPaid || appointment.DepositReportedAt != null) throw new DomainException("Lịch hẹn có giao dịch cần đối soát trước khi rút yêu cầu.");
                // The unprefixed full reason is retained in order state/audit; appointment notes have smaller limits.
                var note = ShortReason(reason);
                appointment.Cancel(note, false, appointment.AppointmentType.FreeCancellationHours);
                db.AppointmentEvents.Add(AppointmentEvent.Create(appointment.Id, null, "lead-withdrawn", note));
            }
    }

    public static async Task<List<OrderJourneyItemDto>> LoadAsync(IApplicationDbContext db, Guid uid, CancellationToken ct)
    {
        var orders = await db.ComboOrders.AsNoTracking().Include(o => o.Items).Where(o => o.CustomerId == uid).ToListAsync(ct);
        var leads = await db.Leads.AsNoTracking().Include(l => l.Vendor).Include(l => l.Listing).ThenInclude(l => l!.Media).Where(l => l.CustomerId == uid).ToListAsync(ct);
        var contracts = await db.BookingContracts.AsNoTracking().Where(c => c.CustomerId == uid).ToListAsync(ct);
        var quotes = await db.Quotes.AsNoTracking().Where(q => q.CustomerId == uid).ToListAsync(ct);
        var states = await db.CustomerOrderStates.AsNoTracking().Where(s => s.CustomerId == uid).ToListAsync(ct);
        var requests = await db.OrderChangeRequests.AsNoTracking().Where(r => r.CustomerId == uid).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        var appointments = await db.Appointments.AsNoTracking().Where(a => a.CustomerId == uid && a.LeadId != null &&
            (a.DepositPaid || a.DepositReportedAt != null) && a.RefundedAt == null).Select(a => a.LeadId!.Value).ToListAsync(ct);
        var contractIds = contracts.Select(c => c.Id).ToList();
        var financialContractIds = await db.Commissions.AsNoTracking().Where(c => contractIds.Contains(c.ContractId)).Select(c => c.ContractId).Distinct().ToListAsync(ct);
        financialContractIds.AddRange(await db.AdvancePayments.AsNoTracking().Where(a => contractIds.Contains(a.ContractId) && a.Status != "Rejected").Select(a => a.ContractId).ToListAsync(ct));
        var moneyLeadIds = appointments.Concat(contracts.Where(c => c.ConfirmedAt != null || c.AppointmentDepositCredit > 0 || financialContractIds.Contains(c.Id)).Select(c => c.LeadId)).ToHashSet();
        var listingIds = orders.SelectMany(o => o.Items).Select(i => i.ListingId).Distinct().ToList();
        var listings = await db.Listings.AsNoTracking().Include(l => l.Media).Include(l => l.Vendor).Where(l => listingIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var result = new List<OrderJourneyItemDto>();
        OrderServiceMediaDto Photo(Listing? listing, Guid listingId, Guid? serviceId, string title)
        {
            var images = listing?.Media.Where(m => m.MediaType == "image").OrderByDescending(m => m.IsFeatured).ThenBy(m => m.DisplayOrder).ToList() ?? new();
            return new(listingId, serviceId, title, images.Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefault(),
                images.Select(m => m.MediaUrl).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Take(12).ToList());
        }
        OrderMediaDto Media(string title, Vendor? vendor, List<OrderServiceMediaDto> items) => new(title, items.Select(i => i.ImageUrl).FirstOrDefault(s => s != null),
            items.SelectMany(i => i.GalleryUrls).Distinct().Take(12).ToList(), vendor?.Id, vendor?.BrandName, vendor?.LogoUrl, vendor?.CoverImageUrl,
            items.Count == 1 ? items[0].ListingId : null, items);
        string? Block(IReadOnlyCollection<Guid> ids)
        {
            if (contracts.Any(c => ids.Contains(c.LeadId) && c.Status != ContractStatus.Cancelled)) return "Đã có hợp đồng thực tế (kể cả nháp/chờ xác nhận). Combo có phần đã chốt không thể sửa toàn bộ; hãy mở từng hợp đồng để đề nghị thay đổi/hủy.";
            return ids.Any(moneyLeadIds.Contains) ? "Đã có cọc/chuyển khoản báo nhận hoặc nghĩa vụ tài chính. Cần NCC/Tài chính đối soát trước khi rút hay sửa thay thế." : null;
        }
        void Add(string kind, Guid id, OrderMediaDto media, bool edit, bool withdraw, bool changes, bool stop, bool changeRequest, bool cancelRequest,
            string? block, Guid? comboId, string? comboCode, bool confirm = false, bool respond = false)
        {
            var s = states.FirstOrDefault(x => x.Kind == kind && x.EntityId == id);
            if (s?.SupersededById != null) { edit = withdraw = changes = stop = changeRequest = cancelRequest = confirm = respond = false; block = "Yêu cầu đã được thay thế. Vui lòng xem phiên bản mới nhất; bản cũ vẫn được giữ trong lịch sử."; }
            var archived = s?.ArchivedAt != null;
            result.Add(new(kind, id, archived, s?.ArchivedAt, s?.RevisionOfId, s?.SupersededById, media,
                new(edit, withdraw, !archived, archived, changes, stop, changeRequest, cancelRequest, block, confirm, respond),
                requests.Where(r => r.Kind == kind && r.EntityId == id).Select(Map).ToList(), comboId, comboCode, s?.RevisionReason, s?.WithdrawalReason));
        }
        foreach (var o in orders)
        {
            var ids = o.Items.Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList();
            var block = Block(ids); var singleVendor = o.Items.Select(i => i.VendorId).Distinct().Count() == 1;
            if (!singleVendor) block = "Combo nhiều NCC cũ chỉ được lưu để tham khảo. Hãy tạo combo riêng của một NCC.";
            var active = o.Status is not (ComboOrderStatus.Cancelled or ComboOrderStatus.Expired);
            var photos = o.Items.Select(i => Photo(listings.GetValueOrDefault(i.ListingId), i.ListingId, i.ServiceItemId, i.Title)).ToList();
            var vendor = singleVendor ? listings.Values.FirstOrDefault(l => l.VendorId == o.Items.First().VendorId)?.Vendor : null;
            Add("combo", o.Id, Media(o.Code, vendor, photos), block == null && o.Status != ComboOrderStatus.Cancelled, active && block == null,
                false, false, active && singleVendor && o.Status != ComboOrderStatus.Draft, active && singleVendor && block != null, block, o.Id, o.Code);
        }
        foreach (var l in leads)
        {
            var block = Block(new[] { l.Id }); var active = l.Status is not (LeadStatus.Cancelled or LeadStatus.Expired);
            if (l.ComboOrderId.HasValue && block == null) block = "Dịch vụ thuộc combo. Hãy sửa/rút tại combo để giữ đồng bộ các hạng mục và ngân sách.";
            var combo = orders.FirstOrDefault(o => o.Id == l.ComboOrderId);
            var selectedLine = combo?.Items.FirstOrDefault(i => i.LeadId == l.Id);
            var title = selectedLine?.Title ?? l.Listing?.Title ?? l.Vendor.BrandName;
            var media = Media(title, l.Vendor, l.ListingId.HasValue ? new() { Photo(l.Listing, l.ListingId.Value, selectedLine?.ServiceItemId, title) } : new());
            Add("lead", l.Id, media, block == null && l.Status != LeadStatus.Cancelled, active && block == null, false, false,
                active && !contracts.Any(c => c.LeadId == l.Id && c.Status != ContractStatus.Cancelled), active && moneyLeadIds.Contains(l.Id), block, combo?.Id, combo?.Code);
        }
        foreach (var c in contracts)
        {
            var l = leads.FirstOrDefault(l => l.Id == c.LeadId); var activeLead = l != null && l.Status is not (LeadStatus.Cancelled or LeadStatus.Expired);
            var pending = c.Status is ContractStatus.Draft or ContractStatus.PendingVerification;
            var canStop = pending && activeLead && !moneyLeadIds.Contains(c.LeadId); var confirmed = c.Status is ContractStatus.Confirmed or ContractStatus.Disputed;
            var combo = orders.FirstOrDefault(o => o.Id == l?.ComboOrderId);
            var selectedLine = combo?.Items.FirstOrDefault(i => i.LeadId == c.LeadId);
            Add("contract", c.Id, Media(c.ContractCode, l?.Vendor, l?.ListingId != null ? new() { Photo(l.Listing, l.ListingId.Value, selectedLine?.ServiceItemId, selectedLine?.Title ?? l.Listing?.Title ?? c.ContractCode) } : new()),
                false, false, c.Status == ContractStatus.PendingVerification && activeLead && !moneyLeadIds.Contains(c.LeadId), canStop,
                activeLead && (pending || confirmed), activeLead && (pending || confirmed),
                !activeLead ? "Yêu cầu tư vấn gốc đã rút/hết hạn, không thể tiếp tục hợp đồng cũ." : confirmed || moneyLeadIds.Contains(c.LeadId) ? "Hợp đồng/cọc đã có hiệu lực: chỉ gửi đề nghị phối hợp; không tự sửa, hủy hoặc hoàn tiền." : null, combo?.Id, combo?.Code,
                confirm: c.Status == ContractStatus.PendingVerification && c.VerificationDeadline > DateTime.UtcNow && activeLead);
        }
        foreach (var q in quotes)
        {
            var l = leads.FirstOrDefault(l => l.Id == q.LeadId); var combo = orders.FirstOrDefault(o => o.Id == l?.ComboOrderId);
            var selectedLine = combo?.Items.FirstOrDefault(i => i.LeadId == q.LeadId);
            Add("quote", q.Id, Media(q.Code, l?.Vendor, l?.ListingId != null ? new() { Photo(l.Listing, l.ListingId.Value, selectedLine?.ServiceItemId, selectedLine?.Title ?? l.Listing?.Title ?? q.Code) } : new()),
                false, false, false, false, false, false, l?.Status is LeadStatus.Cancelled or LeadStatus.Expired ? "Yêu cầu gốc đã rút/hết hạn; báo giá cũ không thể được chấp nhận." : null, combo?.Id, combo?.Code,
                respond: q.Status == QuoteStatus.Sent && q.ValidUntil > DateTime.UtcNow && l != null && l.Status is not (LeadStatus.Cancelled or LeadStatus.Expired) &&
                    !contracts.Any(c => c.LeadId == q.LeadId && c.Status != ContractStatus.Draft));
        }
        return result;
    }
}

public record GetCustomerOrdersQuery : IRequest<List<OrderJourneyItemDto>>;
public class GetCustomerOrdersQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetCustomerOrdersQuery, List<OrderJourneyItemDto>>
{
    public Task<List<OrderJourneyItemDto>> Handle(GetCustomerOrdersQuery r, CancellationToken ct) => CustomerOrderJourney.LoadAsync(db, user.UserId ?? throw new DomainException("Bạn cần đăng nhập."), ct);
}
