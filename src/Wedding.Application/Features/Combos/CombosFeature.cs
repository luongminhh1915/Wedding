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
    bool IsActive, decimal EstimatedTotal, List<ComboTemplateItemDto> Items, Guid? OwnerVendorId = null, string? Region = null, string ModerationStatus = "Approved", decimal? SingleVendorDiscount = null, string? OwnerVendorName = null,
    ComboTemplateContentDto? Content = null, ComboTemplateRevisionDto? LatestRevision = null,
    List<ComboTemplateRevisionDto>? RevisionHistory = null, List<ComboOptionDto>? PreviewOptions = null, int SortOrder = 0,
    string PricingMode = "Dynamic", decimal? ComboPrice = null, decimal? OriginalPrice = null,
    decimal? SavingsAmount = null, decimal? SavingsPercentage = null, List<ComboComponentSnapshotDto>? Components = null,
    bool RequiresComponentReview = false);
public record ComboBuilderDto(List<ComboCategoryDto> Categories, List<ComboTemplateDto> Templates, List<DiscountTierDto> DiscountTiers,
    int PreviewGuestCount = 200, List<ComboPublicVendorDto>? Vendors = null);
public record DiscountTierDto(int MinItems, decimal Percent);

public record ComboOrderItemDto(Guid Id, Guid ListingId, Guid VendorId, string VendorName, string Title, decimal Price,
    decimal DiscountShare, Guid? LeadId, string Status, string? VoucherCode, Guid? ServiceItemId = null, int Quantity = 1, string Unit = "Package", string? CancellationPolicy = null, string? SelectionJson = null, Guid? CategoryId = null,
    ComboComponentSnapshotDto? Component = null);
public record ComboOrderDto(Guid Id, string Code, string Status, DateTime CreatedAt, DateTime? WeddingDate, int? GuestCount,
    decimal Subtotal, decimal DiscountPercent, decimal DiscountAmount, decimal Total, List<ComboOrderItemDto> Items, DateTime? PriceLockUntil = null, string Region = "", Guid? ComboTemplateId = null,
    string? Note = null, Guid? RevisionOfId = null, string? RevisionReason = null);

internal static class ComboMap
{
    public static ComboTemplateDto ToDto(ComboTemplate t) => new(t.Id, t.Name, t.Tier, t.Style, t.Description, t.ImageUrl, t.IsActive,
        ComboFixedPackages.IsFixed(t) ? t.OriginalPrice ?? 0 : t.Items.Where(i => !i.IsOptional).Sum(i => i.Listing?.MinPrice ?? 0),
        t.Items.Select(i => new ComboTemplateItemDto(i.CategoryId, i.ListingId, i.IsOptional, i.ServiceItemId,
            ComboVendorPolicy.TryAllowedIds(i, out var ids) ? ids : [])).ToList(), t.OwnerVendorId, t.Region, t.ModerationStatus, t.SingleVendorDiscount,
        Content: ComboTemplateContent.Read(t), SortOrder: t.SortOrder, PricingMode: t.PricingMode, ComboPrice: t.ComboPrice,
        OriginalPrice: t.OriginalPrice, SavingsAmount: t.OriginalPrice > t.ComboPrice ? t.OriginalPrice - t.ComboPrice : null,
        SavingsPercentage: t.OriginalPrice > 0 && t.OriginalPrice > t.ComboPrice ? (t.OriginalPrice - t.ComboPrice) / t.OriginalPrice * 100 : null,
        Components: ComboFixedPackages.IsFixed(t) ? ComboFixedPackages.Components(t) : null);

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
        var items = await ComboPublicCatalog.Eligible(_context).Include(i => i.Vendor).Include(i => i.Listing).ThenInclude(l => l.Category)
            .Include(i => i.Listing).ThenInclude(l => l.Media).Include(i => i.Variants).Include(i => i.AddOns)
            .ToListAsync(ct);
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
            .PubliclyAvailable().OrderBy(t => t.SortOrder).ToListAsync(ct);
        // Legacy listing-only presets cannot initialize a real service cart. Leave them available
        // to admins for repair, but never present a nonfunctional or artificially priced preset.
        var catalog = items.ToDictionary(i => i.Id);
        templates = templates.Where(t => t.Items.Count > 0 && t.Items.All(slot => slot.ServiceItemId.HasValue && catalog.ContainsKey(slot.ServiceItemId.Value)))
            .Where(t => string.IsNullOrWhiteSpace(r.Style) || t.Style.Equals(r.Style, StringComparison.OrdinalIgnoreCase)).ToList();
        var ownedTemplates = new List<ComboTemplate>();
        foreach (var template in templates)
            if (await ComboTemplateAvailability.ComponentsEligibleAsync(_context, template, ct)) ownedTemplates.Add(template);
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
            EstimatedTotal = ComboFixedPackages.IsFixed(t) ? t.OriginalPrice ?? 0 : t.Items.Where(slot => !slot.IsOptional).Sum(slot => catalog[slot.ServiceItemId!.Value].BasePrice * ServiceItem.QuantityFor(catalog[slot.ServiceItemId.Value].Unit, guests))
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
                price.Lines.Select(l => (l.ListingId, l.VendorId, l.CategoryId, l.Title, l.Price)).ToList(), price.PricingMode == "FixedPrice");
            if (price.PricingMode == "FixedPrice") order.SetFixedPricing(price.Total, r.Region, r.SaveAsDraft);
            else order.SetPricing(price.DiscountPercent, r.Region, r.SaveAsDraft);
            if (source != null && !r.SaveAsDraft)
            {
                await CustomerOrderJourney.WithdrawLeadsAsync(_context, source.Items.Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList(), $"Combo được chỉnh sửa: {revisionReason}", token);
                source.Cancel();
                foreach (var line in source.Items) { line.SetStatus(ComboItemStatus.Cancelled); await ComboBudgetSynchronization.ReleaseAsync(_context, line, token); }
                (await CustomerOrderJourney.StateAsync(_context, uid, "combo", source.Id, token)).Supersede(order.Id);
                var affectedVendors = source.Items.Select(i => i.VendorId).Distinct().ToList();
                foreach (var recipient in await _context.Vendors.Where(v => affectedVendors.Contains(v.Id)).Select(v => v.UserId).ToListAsync(token))
                    _context.Notifications.Add(Notification.Create(recipient, "InApp", "", $"Khách đã chỉnh sửa combo {source.Code}, thay thế bằng {order.Code}. Yêu cầu/báo giá cũ đã dừng; vui lòng báo giá lại phiên bản mới. Lý do: {revisionReason}", contextKind: "combo", contextId: order.Id));
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
                var line = price.Lines.Single(l => l.ListingId == item.ListingId);
                var selection = selections.Single(s => s.ServiceItemId == line.ServiceItemId);
                item.SetSelection(line.ServiceItemId, line.Quantity, line.Unit, ComboBudgetSynchronization.Serialize(selection), line.CancellationPolicy);
                item.SetDiscount(line.DiscountShare);
                if (line.Component != null) item.FreezeComponent(ComboFixedPackages.Serialize(line.Component));
                if (r.SaveAsDraft) continue;
                var lead = Lead.Create(uid, item.VendorId, item.ListingId, phone, r.WeddingDate, r.GuestCount, line.Total,
                    $"[Combo {order.Code}] {line.Quantity} {line.Unit}; giá tham khảo {line.Total:N0}đ. {r.Note}");
                lead.AttachSource("Combo", order.Id); _context.Leads.Add(lead);
                _context.Vouchers.Add(Voucher.Create(lead.Id, uid, Guid.NewGuid().ToString("N")[..8], discountValue: line.DiscountShare)); item.AttachLead(lead.Id);
                await ComboBudgetSynchronization.ReserveLineAsync(_context, scope, order, item, line, selection, token);
                var vendor = await _context.Vendors.Include(v => v.User).FirstAsync(v => v.Id == item.VendorId, token);
                await _notify.SendContextAsync("Email", vendor.User.Email, $"Bạn có yêu cầu combo {order.Code}: {item.Title}.", "lead", lead.Id, token);
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
        => await LoadIds(uid, id.HasValue ? new List<Guid> { id.Value } : null, ct);

    internal async Task<List<ComboOrderDto>> LoadIds(Guid uid, List<Guid>? ids, CancellationToken ct)
    {
        var orders = await _context.ComboOrders.Include(o => o.Items).ThenInclude(i => i.Vendor)
            .Where(o => o.CustomerId == uid && (ids == null || ids.Contains(o.Id))).ToListAsync(ct);
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
                i.ServiceItemId, i.Quantity, i.Unit, i.CancellationPolicySnapshot, i.SelectionJson, i.CategoryId, ComboFixedPackages.Read(i.ComponentSnapshotJson))).ToList(), o.PriceLockUntil, o.Region, o.ComboTemplateId,
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
public class GetAllComboTemplatesQueryHandler(IApplicationDbContext context, ICurrentUserService user)
    : IRequestHandler<GetAllComboTemplatesQuery, List<ComboTemplateDto>>
{
    public async Task<List<ComboTemplateDto>> Handle(GetAllComboTemplatesQuery request, CancellationToken ct)
    {
        var actor = await ComboTemplateLifecycle.ActorAsync(context, user, !request.Mine, ct);
        if (request.Mine && !actor.VendorId.HasValue) throw new DomainException("Chỉ NCC được xem danh sách combo của shop mình.");
        var query = context.ComboTemplates.AsNoTracking().Include(template => template.Items).ThenInclude(item => item.Listing).AsQueryable();
        if (request.Mine) query = query.Where(template => template.OwnerVendorId == actor.VendorId);
        var templates = await query.OrderBy(template => template.SortOrder).ThenBy(template => template.Id).ToListAsync(ct);
        var results = new List<ComboTemplateDto>();
        foreach (var template in templates) results.Add(await ComboTemplateLifecycle.PrivateDtoAsync(context, template, ct));
        return results;
    }
}

public record SaveComboTemplateCommand(Guid? Id, string Name, string Tier, string Style, string? Description, string? ImageUrl,
    bool IsActive, int SortOrder, List<ComboTemplateItemDto> Items, string? Region = null, decimal? SingleVendorDiscount = null,
    bool SubmitForApproval = true, ComboTemplateContentDto? Content = null, string PricingMode = "Dynamic", decimal? ComboPrice = null) : IRequest<ComboTemplateDto>;

public class SaveComboTemplateCommandHandler(IApplicationDbContext context, IAuditLogger audit, ICurrentUserService user)
    : IRequestHandler<SaveComboTemplateCommand, ComboTemplateDto>
{
    public Task<ComboTemplateDto> Handle(SaveComboTemplateCommand request, CancellationToken ct) => context.ExecuteSerializableAsync(async token =>
    {
        var actor = await ComboTemplateLifecycle.ActorAsync(context, user, false, token);
        var name = (request.Name ?? "").Trim(); var style = (request.Style ?? "").Trim();
        if (name.Length is < 1 or > 150 || style.Length is < 1 or > 50 || request.Tier is not ("Saving" or "Popular" or "Luxury") ||
            request.Description?.Length > 4000 || request.Region?.Length > 1000 || request.ImageUrl?.Length > 1000)
            throw new DomainException("Tên, phong cách, phân khúc hoặc nội dung combo không hợp lệ.");
        var template = request.Id.HasValue ? await context.ComboTemplates.Include(template => template.Items).ThenInclude(item => item.Listing)
            .FirstOrDefaultAsync(template => template.Id == request.Id, token) ?? throw new NotFoundException("Combo không tồn tại.") : null;
        if (template != null) ComboTemplateLifecycle.Own(template, actor);
        ComboFixedPackages.ValidatePrice(request.PricingMode, request.ComboPrice);
        if (template == null && actor.VendorId.HasValue && request.PricingMode != "FixedPrice")
            throw new DomainException("Combo mới của shop cần chọn ít nhất hai dịch vụ và nhập giá combo cố định.");
        if (template != null && template.PricingMode == "FixedPrice" && request.PricingMode != "FixedPrice")
            throw new DomainException("Không được đổi combo cố định về combo linh hoạt cũ.");
        if (request.Items is not { Count: >= 1 and <= 12 } || request.Items.Any(slot => slot == null || !slot.ServiceItemId.HasValue))
            throw new DomainException("Combo cần từ 1 đến 12 dịch vụ mặc định đã duyệt.");
        var requestedIds = request.Items.Select(slot => slot.ServiceItemId!.Value).Distinct().ToList();
        var owner = actor.VendorId ?? ComboVendorPolicy.RequireOneVendor(await context.ServiceItems
            .Where(item => requestedIds.Contains(item.Id)).Select(item => item.VendorId).ToListAsync(token));
        if (template?.OwnerVendorId.HasValue == true && template.OwnerVendorId != owner)
            throw new DomainException("Không được đổi combo sang nhà cung cấp khác.");
        var latest = template == null ? null : await context.ComboTemplateRevisions.AsNoTracking()
            .Where(revision => revision.ComboTemplateId == template.Id).OrderByDescending(revision => revision.Sequence).FirstOrDefaultAsync(token);
        var inheritedContent = latest == null ? template == null ? null : ComboTemplateContent.Read(template) : ComboTemplateLifecycle.ReadProposal(latest).Content;
        var content = ComboTemplateContent.Normalize(request.Content ?? inheritedContent);
        if (!string.IsNullOrWhiteSpace(request.ImageUrl)) ComboTemplateContent.Normalize(new([request.ImageUrl]));
        var proposal = new ComboTemplateProposalDto(name, request.Tier, style, request.Description?.Trim(), request.ImageUrl?.Trim(), request.IsActive,
            request.SortOrder, request.Items, owner, request.Region?.Trim(), request.SingleVendorDiscount, content, request.PricingMode, request.ComboPrice);
        var items = await ComboTemplateLifecycle.ValidateAsync(context, proposal, token);
        proposal = proposal with { Items = request.Items.Select(slot => slot with {
            ListingId = items.Single(item => item.Id == slot.ServiceItemId).ListingId,
            AllowedItemIds = (slot.AllowedItemIds ?? []).Distinct().ToList() }).ToList(),
            SingleVendorDiscount = request.PricingMode == "FixedPrice" ? null : request.SingleVendorDiscount,
            Components = request.PricingMode == "FixedPrice" ? items.OrderBy(item => item.Id).Select(ComboFixedPackages.Capture).ToList() : null };
        if (template == null)
        {
            template = ComboTemplate.Create(name, request.Tier, style, request.Description?.Trim(), request.ImageUrl?.Trim(), request.SortOrder);
            template.StageUnpublished(owner, request.SubmitForApproval ? "Pending" : "Draft");
            context.ComboTemplates.Add(template);
        }
        var revision = await ComboTemplateLifecycle.AddRevisionAsync(context, template, proposal, actor, request.SubmitForApproval, token);
        await audit.LogAsync("ComboTemplate.ProposalSaved", nameof(ComboTemplate), template.Id.ToString(),
            new { revisionId = revision.Id, revision.Sequence, request.SubmitForApproval });
        await context.SaveChangesAsync(token);
        return await ComboTemplateLifecycle.PrivateDtoAsync(context, template, token);
    }, ct);
}

public record ReviewComboTemplateCommand(Guid Id, bool Approve, string? Note = null, Guid? RevisionId = null) : IRequest<bool>;
public class ReviewComboTemplateCommandHandler(IApplicationDbContext context, IAuditLogger audit, ICurrentUserService user)
    : IRequestHandler<ReviewComboTemplateCommand, bool>
{
    public Task<bool> Handle(ReviewComboTemplateCommand request, CancellationToken ct) => context.ExecuteSerializableAsync(async token =>
    {
        var actor = await ComboTemplateLifecycle.ActorAsync(context, user, true, token);
        var note = request.Note?.Trim();
        if ((!request.Approve && string.IsNullOrWhiteSpace(note)) || note?.Length > 2000)
            throw new DomainException("Lý do từ chối cần từ 1 đến 2.000 ký tự; ghi chú tối đa 2.000 ký tự.");
        var template = await context.ComboTemplates.Include(template => template.Items).FirstOrDefaultAsync(template => template.Id == request.Id, token)
            ?? throw new NotFoundException("Combo không tồn tại.");
        var latest = await context.ComboTemplateRevisions.Where(revision => revision.ComboTemplateId == template.Id)
            .OrderByDescending(revision => revision.Sequence).FirstOrDefaultAsync(token);
        if (latest == null)
        {
            if (request.RevisionId.HasValue || template.ModerationStatus != "Pending")
                throw new DomainException("Combo không có phiên bản chờ duyệt tương ứng.");
            if (request.Approve && !await ComboTemplateAvailability.ComponentsEligibleAsync(context, template, token))
                throw new DomainException("Combo cũ có dịch vụ không còn đủ điều kiện. Cần lưu bản đề xuất hợp lệ trước khi duyệt.");
            template.RecordLegacyReview(request.Approve, note);
        }
        else
        {
            if (latest.Status != "Pending" || request.RevisionId.HasValue && latest.Id != request.RevisionId)
                throw new DomainException("Phiên bản chờ duyệt đã thay đổi hoặc đã được xử lý. Hãy tải lại trước khi duyệt.");
            if (latest.CreatedById == actor.Id) throw new DomainException("Người tạo phiên bản không được tự kiểm duyệt combo của mình.");
            if (request.Approve) await ComboTemplateLifecycle.ApplyAsync(context, template, ComboTemplateLifecycle.ReadProposal(latest), note, token);
            latest.Review(request.Approve, actor.Id, actor.Role, note);
            if (!request.Approve && template.ModerationStatus != "Approved")
                template.StageUnpublished(template.OwnerVendorId!.Value, "Rejected", note);
        }
        var ownerUserId = await context.Vendors.Where(vendor => vendor.Id == template.OwnerVendorId).Select(vendor => (Guid?)vendor.UserId).FirstOrDefaultAsync(token);
        if (ownerUserId.HasValue) context.Notifications.Add(Notification.Create(ownerUserId.Value, "InApp", "",
            $"Combo {template.Name}: {(request.Approve ? "đã được duyệt" : "bị từ chối")}.{(string.IsNullOrWhiteSpace(note) ? "" : " " + note)}"));
        await audit.LogAsync("ComboTemplate.Review", nameof(ComboTemplate), template.Id.ToString(),
            new { request.Approve, note, revisionId = latest?.Id });
        await context.SaveChangesAsync(token); return true;
    }, ct);
}

public record ArchiveComboTemplateCommand(Guid Id) : IRequest<bool>;
public class ArchiveComboTemplateCommandHandler(IApplicationDbContext context, ICurrentUserService user, IAuditLogger audit)
    : IRequestHandler<ArchiveComboTemplateCommand, bool>
{
    public Task<bool> Handle(ArchiveComboTemplateCommand request, CancellationToken ct)
        => new SetComboTemplateActivityCommandHandler(context, user, audit).Handle(new(request.Id, false), ct);
}
