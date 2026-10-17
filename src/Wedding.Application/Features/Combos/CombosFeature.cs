using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using System.Text.Json;
using Wedding.Application.Features.Planner;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.CustomerOrders;

namespace Wedding.Application.Features.Combos;

public record ComboOptionDto(Guid ListingId, Guid VendorId, string VendorName, bool VendorVerified, decimal RatingAvg,
    string Title, decimal Price, decimal MaxPrice, string Location, string? ImageUrl, Guid ServiceItemId,
    string Unit, int MinGuests, int MaxGuests, string CancellationPolicy, List<VariantDto> Variants, List<AddOnDto> AddOns,
    List<string>? GalleryUrls = null, string? Description = null);
/// <summary>Explicit allowlist of public shop details. Never serialize the Vendor/User/tax/document entity graphs.</summary>
public record ComboPublicVendorDto(Guid Id, string Name, string Slug, string? LogoUrl, string? CoverImageUrl,
    string City, string? Address, string? Bio, bool IsVerified, decimal? RatingAvg, int TotalReviews);
public record ComboCategoryDto(Guid CategoryId, string Name, string Slug, string? Icon, List<ComboOptionDto> Options);
public record ComboTemplateItemDto(Guid CategoryId, Guid ListingId, bool IsOptional, Guid? ServiceItemId = null, List<Guid>? AllowedItemIds = null);
public record ComboTemplateDto(Guid Id, string Name, string Tier, string Style, string? Description, string? ImageUrl,
    bool IsActive, decimal EstimatedTotal, List<ComboTemplateItemDto> Items, Guid? OwnerVendorId = null, string? Region = null, string ModerationStatus = "Approved", decimal? SingleVendorDiscount = null, string? OwnerVendorName = null);
public record ComboBuilderDto(List<ComboCategoryDto> Categories, List<ComboTemplateDto> Templates, List<DiscountTierDto> DiscountTiers,
    int PreviewGuestCount = 200, List<ComboPublicVendorDto>? Vendors = null);
public record DiscountTierDto(int MinItems, decimal Percent);

public record ComboOrderItemDto(Guid Id, Guid ListingId, Guid VendorId, string VendorName, string Title, decimal Price,
    decimal DiscountShare, Guid? LeadId, string Status, string? VoucherCode, Guid? ServiceItemId = null, int Quantity = 1, string Unit = "Package", string? CancellationPolicy = null, string? SelectionJson = null, Guid? CategoryId = null);
public record ComboOrderDto(Guid Id, string Code, string Status, DateTime CreatedAt, DateTime? WeddingDate, int? GuestCount,
    decimal Subtotal, decimal DiscountPercent, decimal DiscountAmount, decimal Total, List<ComboOrderItemDto> Items, DateTime? PriceLockUntil = null, string Region = "", Guid? ComboTemplateId = null,
    string? Note = null, Guid? RevisionOfId = null, string? RevisionReason = null);

internal static class ComboMap
{
    public static ComboTemplateDto ToDto(ComboTemplate t) => new(t.Id, t.Name, t.Tier, t.Style, t.Description, t.ImageUrl, t.IsActive,
        t.Items.Where(i => !i.IsOptional).Sum(i => i.Listing?.MinPrice ?? 0),
        t.Items.Select(i => new ComboTemplateItemDto(i.CategoryId, i.ListingId, i.IsOptional, i.ServiceItemId, JsonSerializer.Deserialize<List<Guid>>(i.AllowedItemIdsJson))).ToList(), t.OwnerVendorId, t.Region, t.ModerationStatus, t.SingleVendorDiscount);

    /// <summary>Đồng bộ trạng thái từng dòng combo theo Lead/Hợp đồng thật.</summary>
    public static async Task SyncAsync(IApplicationDbContext ctx, List<ComboOrder> orders, CancellationToken ct)
    {
        var leadIds = orders.SelectMany(o => o.Items).Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList();
        var leads = await ctx.Leads.Where(l => leadIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var quoted = await ctx.Quotes.Where(q => leadIds.Contains(q.LeadId)).Select(q => q.LeadId).Distinct().ToListAsync(ct);
        foreach (var o in orders.Where(o => o.Status != ComboOrderStatus.Cancelled && o.Status != ComboOrderStatus.Draft && o.Status != ComboOrderStatus.Expired))
        {
            foreach (var i in o.Items.Where(i => i.LeadId.HasValue && leads.ContainsKey(i.LeadId.Value)))
            {
                var l = leads[i.LeadId!.Value];
                var s = l.Status switch
                {
                    LeadStatus.Contracted => ComboItemStatus.Contracted,
                    LeadStatus.Cancelled or LeadStatus.Expired => ComboItemStatus.Cancelled,
                    LeadStatus.Accepted or LeadStatus.Contacted => quoted.Contains(l.Id) ? ComboItemStatus.Quoted : ComboItemStatus.Accepted,
                    _ => ComboItemStatus.Sent
                };
                if (s != i.Status) i.SetStatus(s);
            }
            o.Recalculate();
        }
    }
}

public record GetComboBuilderQuery(string? City, int GuestCount = 200, string? Style = null) : IRequest<ComboBuilderDto>;

public class GetComboBuilderQueryHandler : IRequestHandler<GetComboBuilderQuery, ComboBuilderDto>
{
    private readonly IApplicationDbContext _context;
    public GetComboBuilderQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ComboBuilderDto> Handle(GetComboBuilderQuery r, CancellationToken ct)
    {
        var items = await _context.ServiceItems.AsNoTracking().Include(i => i.Vendor).Include(i => i.Listing).ThenInclude(l => l.Category)
            .Include(i => i.Listing).ThenInclude(l => l.Media).Include(i => i.Variants).Include(i => i.AddOns)
            .Where(i => i.IsActive && i.ModerationStatus == "Approved" && i.Listing.Status == ListingStatus.Active && i.Vendor.IsActive && i.Vendor.IsVerified && i.Vendor.AllowInCombos).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(r.City))
            items = items.Where(i => i.Serves(r.City)).ToList();
        var categories = await _context.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync(ct);
        var cats = categories.Select(category => (Cat: category, Items: items.Where(i => i.CategoryId == category.Id)))
            .Select(g => new ComboCategoryDto(g.Cat.Id, g.Cat.Name, g.Cat.Slug, g.Cat.Icon, g.Items.OrderBy(i => i.BasePrice).Select(i =>
                new ComboOptionDto(i.ListingId, i.VendorId, i.Vendor.BrandName, i.Vendor.IsVerified, i.Vendor.TotalReviews >= 3 ? i.Vendor.RatingAvg : 0, i.Name, i.BasePrice, i.BasePrice,
                    i.Regions, i.Listing.Media.Where(m => m.MediaType == "image").OrderByDescending(m => m.IsFeatured).ThenBy(m => m.DisplayOrder).Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefault(), i.Id,
                    i.Unit, i.MinGuests, i.MaxGuests, i.CancellationPolicy, CatalogMap.ToDto(i).Variants, CatalogMap.ToDto(i).AddOns,
                    i.Listing.Media.Where(m => m.MediaType == "image").OrderByDescending(m => m.IsFeatured).ThenBy(m => m.DisplayOrder)
                        .Select(m => m.MediaUrl).Where(url => !string.IsNullOrWhiteSpace(url)).Distinct().Take(12).ToList(), i.Listing.Description)).ToList()))
            .ToList();
        var templates = await _context.ComboTemplates.AsNoTracking().Include(t => t.Items).ThenInclude(i => i.Listing)
            .Where(t => t.IsActive && t.ModerationStatus == "Approved" && t.OwnerVendorId != null).OrderBy(t => t.SortOrder).ToListAsync(ct);
        // Legacy listing-only presets cannot initialize a real service cart. Leave them available
        // to admins for repair, but never present a nonfunctional or artificially priced preset.
        var catalog = items.ToDictionary(i => i.Id);
        templates = templates.Where(t => t.Items.Count > 0 && t.Items.All(slot => slot.ServiceItemId.HasValue && catalog.ContainsKey(slot.ServiceItemId.Value)))
            .Where(t => string.IsNullOrWhiteSpace(r.Style) || t.Style.Equals(r.Style, StringComparison.OrdinalIgnoreCase)).ToList();
        var ownedTemplates = new List<ComboTemplate>();
        foreach (var template in templates)
            if (await ComboVendorPolicy.IsOwnedTemplateAsync(_context, template, ct)) ownedTemplates.Add(template);
        templates = ownedTemplates;
        var guests = Math.Clamp(r.GuestCount, 1, 10000);
        var ownerIds = templates.Where(t => t.OwnerVendorId.HasValue).Select(t => t.OwnerVendorId!.Value).ToList();
        var owners = await _context.Vendors.Where(v => ownerIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.BrandName, ct);
        var visibleVendorIds = cats.SelectMany(c => c.Options).Select(option => option.VendorId).ToHashSet();
        var vendors = items.Where(i => visibleVendorIds.Contains(i.VendorId)).Select(i => i.Vendor).DistinctBy(v => v.Id)
            .OrderBy(v => v.BrandName).Select(v => new ComboPublicVendorDto(v.Id, v.BrandName, v.Slug, v.LogoUrl, v.CoverImageUrl,
                v.City, v.Address, v.Bio, v.IsVerified, v.TotalReviews >= 3 ? v.RatingAvg : null, v.TotalReviews)).ToList();
        return new ComboBuilderDto(cats, templates.Select(t => ComboMap.ToDto(t) with
        {
            OwnerVendorName = t.OwnerVendorId.HasValue && owners.TryGetValue(t.OwnerVendorId.Value, out var name) ? name : null,
            EstimatedTotal = t.Items.Where(slot => !slot.IsOptional).Sum(slot => catalog[slot.ServiceItemId!.Value].BasePrice * ServiceItem.QuantityFor(catalog[slot.ServiceItemId.Value].Unit, guests))
        }).ToList(),
            await new GetBundleRulesQueryHandler(_context).Handle(new(), ct), guests, vendors);
    }
}

public record SubmitComboOrderCommand(Guid? TemplateId, List<Guid> ListingIds, DateTime? WeddingDate, int? GuestCount,
    string? PhoneNumber, string? Note, string Region = "", List<ComboSelection>? Selections = null, decimal? ExpectedTotal = null,
    bool SaveAsDraft = false, Guid? DraftId = null, Guid? RevisionOfId = null, string? RevisionReason = null) : IRequest<ComboOrderDto>;

/// <summary>P5 – Gửi combo: tạo ComboOrder + mỗi dịch vụ sinh 1 Lead (kèm voucher BR-004) tới NCC tương ứng.</summary>
public class SubmitComboOrderCommandHandler : IRequestHandler<SubmitComboOrderCommand, ComboOrderDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly INotificationSender _notify;

    public SubmitComboOrderCommandHandler(IApplicationDbContext context, ICurrentUserService user, INotificationSender notify)
    { _context = context; _user = user; _notify = notify; }

    public async Task<ComboOrderDto> Handle(SubmitComboOrderCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập để gửi combo.");
        var scope = await WeddingAccess.ResolveAsync(_context, _user, ct, write: true);
        return await _context.ExecuteSerializableAsync(async token =>
        {
            ComboOrder? previousDraft = null;
            var revisionId = r.RevisionOfId; var revisionReason = r.RevisionReason;
            if (r.DraftId.HasValue)
            {
                var draft = await _context.ComboOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == r.DraftId && o.CustomerId == uid, token)
                    ?? throw new NotFoundException("Bản nháp không tồn tại.");
                if (draft.Status != ComboOrderStatus.Draft) throw new DomainException("Đơn này không còn là bản nháp, hãy tạo combo mới.");
                if (draft.Items.Select(i => i.VendorId).Distinct().Count() != 1)
                    throw new DomainException("Bản nháp cũ có nhiều NCC được giữ để tham khảo, không thể gửi lại. Hãy tạo combo riêng cho từng NCC.");
                previousDraft = draft;
                var draftState = await _context.CustomerOrderStates.FirstOrDefaultAsync(s => s.CustomerId == uid && s.Kind == "combo" && s.EntityId == draft.Id, token);
                if (draftState?.SupersededById != null) throw new DomainException("Bản nháp đã được thay thế. Hãy mở bản mới nhất.");
                if (draftState?.RevisionOfId != null && revisionId.HasValue && revisionId != draftState.RevisionOfId) throw new DomainException("Không được thay đổi nguồn của bản nháp chỉnh sửa.");
                revisionId ??= draftState?.RevisionOfId; revisionReason ??= draftState?.RevisionReason;
            }
            ComboOrder? source = null;
            if (revisionId.HasValue)
            {
                if (revisionId == r.DraftId) throw new DomainException("Bản nháp không thể tự thay thế chính nó.");
                OrderChangeRequest.ValidateReason(revisionReason);
                source = await _context.ComboOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == revisionId && o.CustomerId == uid, token)
                    ?? throw new NotFoundException("Combo gốc không tồn tại hoặc không thuộc tài khoản của bạn.");
                await CustomerOrderJourney.EnsureCanReplaceComboAsync(_context, source, token);
            }
            var selections = r.Selections ?? throw new DomainException("Vui lòng chọn ServiceItem và tính giá lại trước khi gửi.");
            var price = await ComboPricing.CalculateAsync(_context, new(r.TemplateId, r.Region, r.WeddingDate ?? throw new DomainException("Cần ngày cưới."), r.GuestCount ?? 0, selections, revisionId), uid, token);
            if (source != null && source.Items.First().VendorId != price.Lines.First().VendorId) throw new DomainException("Phiên bản chỉnh sửa chỉ dùng dịch vụ cùng NCC của combo gốc. Đổi NCC cần tạo combo mới.");
            if (r.ExpectedTotal.HasValue && r.ExpectedTotal.Value != price.Total) throw new DomainException("Giá đã thay đổi. Vui lòng tính lại và xác nhận tổng mới.");
            var customer = await _context.Users.FirstAsync(u => u.Id == uid, token);
            var phone = string.IsNullOrWhiteSpace(r.PhoneNumber) ? customer.PhoneNumber : r.PhoneNumber.Trim();
            if (!r.SaveAsDraft && (phone.Length < 9 || phone.Any(c => !char.IsDigit(c) && c != '+'))) throw new DomainException("Vui lòng nhập SĐT hợp lệ.");
            var order = ComboOrder.Create(uid, r.TemplateId, r.WeddingDate, r.GuestCount, r.Note,
                price.Lines.Select(l => (l.ListingId, l.VendorId, l.CategoryId, l.Title, l.Price)).ToList());
            order.SetPricing(price.DiscountPercent, r.Region, r.SaveAsDraft);
            if (source != null && !r.SaveAsDraft)
            {
                await CustomerOrderJourney.WithdrawLeadsAsync(_context, source.Items.Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList(), $"Combo được chỉnh sửa: {revisionReason}", token);
                source.Cancel();
                foreach (var line in source.Items) { line.SetStatus(ComboItemStatus.Cancelled); await ComboBudgetSynchronization.ReleaseAsync(_context, line, token); }
                (await CustomerOrderJourney.StateAsync(_context, uid, "combo", source.Id, token)).Supersede(order.Id);
                var affectedVendors = source.Items.Select(i => i.VendorId).Distinct().ToList();
                foreach (var recipient in await _context.Vendors.Where(v => affectedVendors.Contains(v.Id)).Select(v => v.UserId).ToListAsync(token))
                    _context.Notifications.Add(Notification.Create(recipient, "InApp", "", $"Khách đã chỉnh sửa combo {source.Code}, thay thế bằng {order.Code}. Yêu cầu/báo giá cũ đã dừng; vui lòng báo giá lại phiên bản mới. Lý do: {revisionReason}"));
            }
            if (previousDraft != null)
            {
                previousDraft.Cancel();
                (await CustomerOrderJourney.StateAsync(_context, uid, "combo", previousDraft.Id, token)).Supersede(order.Id);
            }
            // Flush releases inside the same transaction before finding reusable seed rows for the new revision.
            // The old draft/order remains historical; rollback restores it if any following step fails.
            if (source != null && !r.SaveAsDraft || previousDraft != null) await _context.SaveChangesAsync(token);
            _context.ComboOrders.Add(order);
            if (source != null) (await CustomerOrderJourney.StateAsync(_context, uid, "combo", order.Id, token)).RevisionOf(source.Id, revisionReason!);
            foreach (var item in order.Items)
            {
                var line = price.Lines.Single(l => l.CategoryId == item.CategoryId);
                var selection = selections.Single(s => s.ServiceItemId == line.ServiceItemId);
                item.SetSelection(line.ServiceItemId, line.Quantity, line.Unit, ComboBudgetSynchronization.Serialize(selection), line.CancellationPolicy);
                item.SetDiscount(line.DiscountShare);
                if (r.SaveAsDraft) continue;
                var lead = Lead.Create(uid, item.VendorId, item.ListingId, phone, r.WeddingDate, r.GuestCount, line.Total,
                    $"[Combo {order.Code}] {line.Quantity} {line.Unit}; giá tham khảo {line.Total:N0}đ. {r.Note}");
                lead.AttachSource("Combo", order.Id); _context.Leads.Add(lead);
                _context.Vouchers.Add(Voucher.Create(lead.Id, uid, Guid.NewGuid().ToString("N")[..8], discountValue: line.DiscountShare)); item.AttachLead(lead.Id);
                await ComboBudgetSynchronization.ReserveLineAsync(_context, scope, order, item, line, selection, token);
                var vendor = await _context.Vendors.Include(v => v.User).FirstAsync(v => v.Id == item.VendorId, token);
                await _notify.SendAsync("Email", vendor.User.Email, $"Bạn có yêu cầu combo {order.Code}: {item.Title}.", token);
            }
            _context.AuditLogs.Add(AuditLog.Create(uid, _user.Role, r.SaveAsDraft ? "Combo.DraftSaved" : source == null ? "Combo.Submitted" : "Combo.RevisionSubmitted", "ComboOrder", order.Id.ToString(), JsonSerializer.Serialize(new { revisionOfId = source?.Id, revisionReason })));
            await _context.SaveChangesAsync(token);
            return (await new GetMyComboOrdersQueryHandler(_context, _user).Load(uid, order.Id, token)).Single();
        }, ct);
    }
}

public record GetMyComboOrdersQuery : IRequest<List<ComboOrderDto>>;

public class GetMyComboOrdersQueryHandler : IRequestHandler<GetMyComboOrdersQuery, List<ComboOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetMyComboOrdersQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public Task<List<ComboOrderDto>> Handle(GetMyComboOrdersQuery r, CancellationToken ct)
        => Load(_user.UserId ?? throw new DomainException("Bạn cần đăng nhập."), null, ct);

    internal async Task<List<ComboOrderDto>> Load(Guid uid, Guid? id, CancellationToken ct)
    {
        var orders = await _context.ComboOrders.Include(o => o.Items).ThenInclude(i => i.Vendor)
            .Where(o => o.CustomerId == uid && (id == null || o.Id == id)).ToListAsync(ct);
        await ComboMap.SyncAsync(_context, orders, ct);
        await _context.SaveChangesAsync(ct);
        var leadIds = orders.SelectMany(o => o.Items).Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList();
        var vouchers = await _context.Vouchers.Where(v => leadIds.Contains(v.LeadId)).ToDictionaryAsync(v => v.LeadId, v => v.Code, ct);
        var orderIds = orders.Select(o => o.Id).ToList();
        var states = await _context.CustomerOrderStates.AsNoTracking().Where(s => s.CustomerId == uid && s.Kind == "combo" && orderIds.Contains(s.EntityId)).ToDictionaryAsync(s => s.EntityId, ct);
        return orders.OrderByDescending(o => o.CreatedAt).Select(o => new ComboOrderDto(o.Id, o.Code, o.Status.ToString(), o.CreatedAt,
            o.WeddingDate, o.GuestCount, o.Subtotal, o.DiscountPercent, o.DiscountAmount, o.Total,
            o.Items.Select(i => new ComboOrderItemDto(i.Id, i.ListingId, i.VendorId, i.Vendor?.BrandName ?? "", i.Title, i.Price, i.DiscountShare,
                i.LeadId, i.Status.ToString(), i.LeadId.HasValue && vouchers.TryGetValue(i.LeadId.Value, out var c) ? c : null,
                i.ServiceItemId, i.Quantity, i.Unit, i.CancellationPolicySnapshot, i.SelectionJson, i.CategoryId)).ToList(), o.PriceLockUntil, o.Region, o.ComboTemplateId,
                o.Note, states.GetValueOrDefault(o.Id)?.RevisionOfId, states.GetValueOrDefault(o.Id)?.RevisionReason)).ToList();
    }
}

public record CancelComboOrderCommand(Guid Id) : IRequest<bool>;

public class CancelComboOrderCommandHandler : IRequestHandler<CancelComboOrderCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public CancelComboOrderCommandHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<bool> Handle(CancelComboOrderCommand r, CancellationToken cancellationToken)
    {
        await new CustomerOrderActionCommandHandler(_context, _user).Handle(new("combo", r.Id, "withdraw", "Khách rút yêu cầu combo."), cancellationToken);
        return true;
    }
}

// ---------- Admin: combo mẫu ----------

public record GetAllComboTemplatesQuery(bool Mine = false) : IRequest<List<ComboTemplateDto>>;

public class GetAllComboTemplatesQueryHandler : IRequestHandler<GetAllComboTemplatesQuery, List<ComboTemplateDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetAllComboTemplatesQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<List<ComboTemplateDto>> Handle(GetAllComboTemplatesQuery r, CancellationToken ct)
    {
        var q = _context.ComboTemplates.AsNoTracking().Include(t => t.Items).ThenInclude(i => i.Listing).AsQueryable();
        if (r.Mine)
        {
            var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == _user.UserId, ct) ?? throw new DomainException("Không có hồ sơ NCC.");
            q = q.Where(t => t.OwnerVendorId == vendor.Id);
        }
        var templates = await q.OrderBy(t => t.SortOrder).ToListAsync(ct);
        var ownerIds = templates.Where(t => t.OwnerVendorId.HasValue).Select(t => t.OwnerVendorId!.Value).ToList();
        var owners = await _context.Vendors.Where(v => ownerIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.BrandName, ct);
        return templates.Select(t => ComboMap.ToDto(t) with { OwnerVendorName = t.OwnerVendorId.HasValue && owners.TryGetValue(t.OwnerVendorId.Value, out var name) ? name : null }).ToList();
    }
}

public record SaveComboTemplateCommand(Guid? Id, string Name, string Tier, string Style, string? Description, string? ImageUrl,
    bool IsActive, int SortOrder, List<ComboTemplateItemDto> Items, string? Region = null, decimal? SingleVendorDiscount = null) : IRequest<ComboTemplateDto>;

public class SaveComboTemplateCommandHandler : IRequestHandler<SaveComboTemplateCommand, ComboTemplateDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUserService _user;
    public SaveComboTemplateCommandHandler(IApplicationDbContext context, IAuditLogger audit, ICurrentUserService user) { _context = context; _audit = audit; _user = user; }

    public async Task<ComboTemplateDto> Handle(SaveComboTemplateCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new DomainException("Tên combo không được để trống.");
        if (r.Items == null || r.Items.Count == 0 || r.Items.Select(i => i.CategoryId).Distinct().Count() != r.Items.Count || r.Items.Any(i => !i.ServiceItemId.HasValue))
            throw new DomainException("Combo cần mỗi ngành một ServiceItem mặc định đã duyệt.");
        var requestedIds = r.Items.SelectMany(i => (i.AllowedItemIds ?? new()).Append(i.ServiceItemId!.Value)).Distinct().ToList();
        var allowedItems = await _context.ServiceItems.Include(i => i.Vendor).Include(i => i.Listing).Where(i => requestedIds.Contains(i.Id) && i.ModerationStatus == "Approved" && i.IsActive && i.Listing.Status == ListingStatus.Active).ToListAsync(ct);
        if (allowedItems.Count != requestedIds.Count) throw new DomainException("Có dịch vụ chưa được duyệt/không khả dụng.");
        var owner = ComboVendorPolicy.RequireOneVendor(allowedItems.Select(i => i.VendorId));
        if (_user.Role == "VendorOwner")
        {
            var currentVendor = (await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == _user.UserId, ct) ?? throw new DomainException("Không có NCC.")).Id;
            if (owner != currentVendor) throw new DomainException("NCC chỉ được tạo combo gồm dịch vụ của mình.");
        }
        else if (_user.Role is not ("SuperAdmin" or "Moderator")) throw new DomainException("Không có quyền quản lý combo.");
        if (allowedItems.Any(i => !i.Vendor.IsActive || !i.Vendor.AllowInCombos || !i.Vendor.IsVerified)) throw new DomainException("NCC phải đang hoạt động, đã xác minh và đồng ý tham gia combo.");
        if (r.SingleVendorDiscount.HasValue && r.SingleVendorDiscount > allowedItems[0].Vendor.MaxComboDiscount) throw new DomainException("Ưu đãi combo vượt mức giảm tối đa NCC đã đồng ý.");
        foreach (var slot in r.Items)
            if (allowedItems.Where(i => (slot.AllowedItemIds ?? new()).Append(slot.ServiceItemId!.Value).Contains(i.Id)).Any(i => i.CategoryId != slot.CategoryId))
                throw new DomainException("Lựa chọn thay thế phải cùng ngành với slot.");
        ComboTemplate t;
        if (r.Id.HasValue)
        {
            t = await _context.ComboTemplates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException("Combo không tồn tại.");
            if ((_user.Role == "VendorOwner" || t.OwnerVendorId.HasValue) && t.OwnerVendorId != owner) throw new DomainException("Không có quyền đổi combo sang nhà cung cấp khác.");
            t.Update(r.Name, r.Tier, r.Style, r.Description, r.ImageUrl, r.IsActive);
            foreach (var i in t.Items.ToList()) _context.ComboTemplateItems.Remove(i);
        }
        else
        {
            t = ComboTemplate.Create(r.Name, r.Tier, r.Style, r.Description, r.ImageUrl, r.SortOrder);
            _context.ComboTemplates.Add(t);
        }
        t.Update(r.Name, r.Tier, r.Style, r.Description, r.ImageUrl, r.IsActive, r.SortOrder);
        t.SetScope(owner, r.Region, r.SingleVendorDiscount, _user.Role != "VendorOwner");
        foreach (var i in r.Items)
        {
            var serviceItem = allowedItems.Single(x => x.Id == i.ServiceItemId);
            var slot = ComboTemplateItem.Create(t.Id, i.CategoryId, serviceItem.ListingId, i.IsOptional);
            slot.ConfigureItems(serviceItem.Id, JsonSerializer.Serialize(i.AllowedItemIds ?? new()));
            _context.ComboTemplateItems.Add(slot);
        }
        await _audit.LogAsync("ComboTemplate.Save", nameof(ComboTemplate), t.Id.ToString(), new { r.Name, items = r.Items?.Count });
        await _context.SaveChangesAsync(ct);
        return (await new GetAllComboTemplatesQueryHandler(_context, _user).Handle(new(_user.Role == "VendorOwner"), ct)).First(x => x.Id == t.Id);
    }
}

public record ReviewComboTemplateCommand(Guid Id, bool Approve) : IRequest<bool>;
public class ReviewComboTemplateCommandHandler(IApplicationDbContext context, IAuditLogger audit) : IRequestHandler<ReviewComboTemplateCommand, bool>
{
    public async Task<bool> Handle(ReviewComboTemplateCommand r, CancellationToken ct)
    {
        var template = await context.ComboTemplates.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == r.Id, ct) ?? throw new NotFoundException("Combo không tồn tại.");
        if (r.Approve && !await ComboVendorPolicy.IsOwnedTemplateAsync(context, template, ct)) throw new DomainException("Chỉ duyệt combo thuộc một NCC, gồm dịch vụ và lựa chọn của chính NCC đó.");
        template.Review(r.Approve); await audit.LogAsync("ComboTemplate.Review", nameof(ComboTemplate), template.Id.ToString(), r); await context.SaveChangesAsync(ct); return true;
    }
}

public record ArchiveComboTemplateCommand(Guid Id) : IRequest<bool>;
public class ArchiveComboTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService user, IAuditLogger audit) : IRequestHandler<ArchiveComboTemplateCommand, bool>
{
    public async Task<bool> Handle(ArchiveComboTemplateCommand r, CancellationToken ct)
    {
        var template = await context.ComboTemplates.FirstOrDefaultAsync(t => t.Id == r.Id, ct) ?? throw new NotFoundException("Combo không tồn tại.");
        if (user.Role == "VendorOwner")
        {
            var own = await context.Vendors.FirstOrDefaultAsync(v => v.UserId == user.UserId, ct) ?? throw new DomainException("Không có hồ sơ NCC.");
            if (template.OwnerVendorId != own.Id) throw new DomainException("Chỉ được ẩn combo của NCC mình.");
        }
        else if (user.Role is not ("SuperAdmin" or "Moderator")) throw new DomainException("Không có quyền quản lý combo.");
        template.Archive();
        await audit.LogAsync("ComboTemplate.Archive", nameof(ComboTemplate), template.Id.ToString()); await context.SaveChangesAsync(ct); return true;
    }
}
