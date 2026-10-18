using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

public record ComboPage<T>(List<T> Items, int Page, int PageSize, int TotalCount, string? Filter = null, bool FocusFound = false);
public record ComboCategoryCount(Guid CategoryId, int Count);
public record ComboCatalogPageDto(ComboBuilderDto Builder, List<ComboOptionDto> SelectedOptions,
    int Page, int PageSize, int TotalCount, List<ComboCategoryCount> CategoryCounts, ComboTemplateDto? SelectedTemplate);
public record GetComboCatalogPageQuery(Guid? CategoryId = null, Guid? VendorId = null, Guid? ListingId = null,
    string? Search = null, string Sort = "recommended", int Page = 1, int PageSize = 6,
    List<Guid>? SelectedItemIds = null, Guid? TemplateId = null, int Guests = 200) : IRequest<ComboCatalogPageDto>;

/// <summary>Page images/variants at the database, hydrate just current cart/preset options separately.
/// The old full builder contract remains available for saved direct detail links.</summary>
public class GetComboCatalogPageQueryHandler(IApplicationDbContext db) : IRequestHandler<GetComboCatalogPageQuery, ComboCatalogPageDto>
{
    public async Task<ComboCatalogPageDto> Handle(GetComboCatalogPageQuery r, CancellationToken ct)
    {
        var size = Math.Clamp(r.PageSize, 1, 10);
        var eligible = db.ServiceItems.AsNoTracking().Where(i => i.IsActive && i.ModerationStatus == "Approved" &&
            i.Listing.Status == ListingStatus.Active && i.Vendor.IsActive && i.Vendor.User.IsActive && i.Vendor.IsVerified && i.Vendor.AllowInCombos);
        var filtered = r.VendorId.HasValue ? eligible.Where(i => i.VendorId == r.VendorId) : eligible;
        var counts = await filtered.GroupBy(i => i.CategoryId).Select(g => new ComboCategoryCount(g.Key, g.Count())).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync(ct);
        var vendors = await db.Vendors.AsNoTracking().Where(v => eligible.Any(i => i.VendorId == v.Id)).OrderBy(v => v.BrandName)
            .Select(v => new ComboPublicVendorDto(v.Id, v.BrandName, v.Slug, v.LogoUrl, v.CoverImageUrl, v.City, v.Address, v.Bio,
                v.IsVerified, v.TotalReviews >= 3 ? v.RatingAvg : null, v.TotalReviews)).ToListAsync(ct);
        var search = (r.Search ?? "").Trim().ToLowerInvariant();
        if (search.Length > 150) throw new DomainException("Từ khóa tìm kiếm tối đa 150 ký tự.");
        var hydratedIds = (r.SelectedItemIds ?? []).Distinct().Take(40).ToHashSet();
        var templates = new List<ComboTemplate>();
        ComboTemplate? selectedTemplate = null;
        var pageItems = new List<ServiceItem>();
        var page = 1;
        var total = 0;
        var categoryId = r.CategoryId ?? (r.ListingId.HasValue ? await eligible.Where(i => i.ListingId == r.ListingId).Select(i => (Guid?)i.CategoryId).FirstOrDefaultAsync(ct) : null);
        if (categoryId.HasValue)
        {
            var query = filtered.Where(i => i.CategoryId == categoryId);
            if (r.ListingId.HasValue) query = query.Where(i => i.ListingId == r.ListingId);
            if (search.Length > 0) query = query.Where(i => i.Name.ToLower().Contains(search) || i.Vendor.BrandName.ToLower().Contains(search));
            total = await query.CountAsync(ct);
            page = Math.Clamp(r.Page, 1, Math.Max(1, (total + size - 1) / size));
            var sorted = r.Sort switch {
                "price-desc" => query.OrderByDescending(i => i.BasePrice).ThenBy(i => i.Id),
                "newest" => query.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id),
                _ => query.OrderBy(i => i.BasePrice).ThenBy(i => i.Id)
            };
            pageItems = await IncludeOptions(sorted.Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        }
        // Template JSON option ownership is a legacy invariant and cannot be translated by both
        // supported EF providers. Validate lightweight slot metadata BEFORE paging, not entire albums.
        var candidates = await db.ComboTemplates.AsNoTracking().Include(t => t.Items)
            .Where(t => t.IsActive && t.ModerationStatus == "Approved" && t.OwnerVendorId != null &&
                (!categoryId.HasValue || t.Id == r.TemplateId) &&
                (!r.VendorId.HasValue || t.OwnerVendorId == r.VendorId || t.Id == r.TemplateId))
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Id).ToListAsync(ct);
        var eligibleIds = await eligible.Select(i => i.Id).ToListAsync(ct);
        var eligibleSet = eligibleIds.ToHashSet();
        var valid = new List<ComboTemplate>();
        foreach (var template in candidates)
        {
            if (template.Items.Count == 0 || template.Items.Any(s => !s.ServiceItemId.HasValue || !eligibleSet.Contains(s.ServiceItemId.Value)) ||
                !await ComboVendorPolicy.IsOwnedTemplateAsync(db, template, ct)) continue;
            if (template.Id == r.TemplateId) selectedTemplate = template;
            if ((!r.VendorId.HasValue || template.OwnerVendorId == r.VendorId) &&
                (search.Length == 0 || template.Name.ToLowerInvariant().Contains(search) ||
                vendors.Any(v => v.Id == template.OwnerVendorId && v.Name.ToLowerInvariant().Contains(search)))) valid.Add(template);
        }
        if (!categoryId.HasValue)
        {
            total = valid.Count;
            page = Math.Clamp(r.Page, 1, Math.Max(1, (total + size - 1) / size));
            var sorted = r.Sort == "newest" ? valid.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id).ToList() : valid;
            templates = sorted.Skip((page - 1) * size).Take(size).ToList();
        }
        foreach (var template in templates.Concat(selectedTemplate is null ? [] : new[] { selectedTemplate }))
            foreach (var slot in template.Items) hydratedIds.Add(slot.ServiceItemId!.Value);
        var extras = await IncludeOptions(eligible.Where(i => hydratedIds.Contains(i.Id))).ToListAsync(ct);
        var options = pageItems.Concat(extras).DistinctBy(i => i.Id).ToDictionary(i => i.Id);
        var guests = Math.Clamp(r.Guests, 1, 10000);
        ComboTemplateDto MapTemplate(ComboTemplate t) => ComboMap.ToDto(t) with {
            OwnerVendorName = vendors.FirstOrDefault(v => v.Id == t.OwnerVendorId)?.Name,
            EstimatedTotal = t.Items.Where(s => !s.IsOptional).Sum(s => options.TryGetValue(s.ServiceItemId!.Value, out var item) ?
                item.BasePrice * ServiceItem.QuantityFor(item.Unit, guests) : 0)
        };
        var builder = new ComboBuilderDto(categories.Select(c => new ComboCategoryDto(c.Id, c.Name, c.Slug, c.Icon,
            pageItems.Where(i => i.CategoryId == c.Id).Select(MapOption).ToList())).ToList(),
            templates.Select(MapTemplate).ToList(), await new GetBundleRulesQueryHandler(db).Handle(new(), ct), guests, vendors);
        return new(builder, extras.Select(MapOption).ToList(), page, size, total, counts, selectedTemplate is null ? null : MapTemplate(selectedTemplate));
    }

    private static IQueryable<ServiceItem> IncludeOptions(IQueryable<ServiceItem> q) => q.Include(i => i.Vendor)
        .Include(i => i.Listing).ThenInclude(l => l.Media).Include(i => i.Variants).Include(i => i.AddOns);
    private static ComboOptionDto MapOption(ServiceItem i) => new(i.ListingId, i.VendorId, i.Vendor.BrandName, i.Vendor.IsVerified,
        i.Vendor.TotalReviews >= 3 ? i.Vendor.RatingAvg : 0, i.Name, i.BasePrice, i.BasePrice, i.Regions,
        i.Listing.Media.Where(m => m.MediaType == "image").OrderByDescending(m => m.IsFeatured).ThenBy(m => m.DisplayOrder)
            .Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefault(), i.Id, i.Unit, i.MinGuests, i.MaxGuests, i.CancellationPolicy,
        CatalogMap.ToDto(i).Variants, CatalogMap.ToDto(i).AddOns,
        i.Listing.Media.Where(m => m.MediaType == "image").OrderByDescending(m => m.IsFeatured).ThenBy(m => m.DisplayOrder)
            .Select(m => m.MediaUrl).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().Take(12).ToList(), i.Listing.Description);
}

public record GetMyComboOrdersPageQuery(int Page = 1, int PageSize = 6, string Filter = "all", Guid? FocusId = null) : IRequest<ComboPage<ComboOrderDto>>;
public class GetMyComboOrdersPageQueryHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetMyComboOrdersPageQuery, ComboPage<ComboOrderDto>>
{
    public async Task<ComboPage<ComboOrderDto>> Handle(GetMyComboOrdersPageQuery r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var size = Math.Clamp(r.PageSize, 1, 10);
        var states = db.CustomerOrderStates.Where(s => s.CustomerId == uid && s.Kind == "combo");
        var q = db.ComboOrders.Where(o => o.CustomerId == uid);
        var focusFound = r.FocusId.HasValue && await q.AnyAsync(o => o.Id == r.FocusId, ct);
        var filter = focusFound ? await states.AnyAsync(s => s.EntityId == r.FocusId && s.ArchivedAt != null, ct) ? "archived" : "all" : r.Filter;
        if (filter == "archived") q = q.Where(o => states.Any(s => s.EntityId == o.Id && s.ArchivedAt != null));
        else {
            q = q.Where(o => !states.Any(s => s.EntityId == o.Id && s.ArchivedAt != null));
            if (filter == "draft") q = q.Where(o => o.Status == ComboOrderStatus.Draft);
            else if (filter == "closed") q = q.Where(o => o.Status == ComboOrderStatus.Completed || o.Status == ComboOrderStatus.Cancelled || o.Status == ComboOrderStatus.Expired || states.Any(s => s.EntityId == o.Id && s.SupersededById != null));
            else if (filter == "active") q = q.Where(o => o.Status != ComboOrderStatus.Draft && o.Status != ComboOrderStatus.Completed && o.Status != ComboOrderStatus.Cancelled && o.Status != ComboOrderStatus.Expired && !states.Any(s => s.EntityId == o.Id && s.SupersededById != null));
        }
        var total = await q.CountAsync(ct);
        var page = Math.Clamp(r.Page, 1, Math.Max(1, (total + size - 1) / size));
        if (focusFound) {
            var orderedIds = await q.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).Select(o => o.Id).ToListAsync(ct);
            page = orderedIds.IndexOf(r.FocusId!.Value) / size + 1;
        }
        var ids = await q.OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id).Skip((page - 1) * size).Take(size).Select(o => o.Id).ToListAsync(ct);
        var orders = await new GetMyComboOrdersQueryHandler(db, user).LoadIds(uid, ids, ct);
        return new(orders, page, size, total, filter, focusFound);
    }
}
