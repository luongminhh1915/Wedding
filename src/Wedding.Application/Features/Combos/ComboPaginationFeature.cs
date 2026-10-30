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
    int Page, int PageSize, int TotalCount, List<ComboCategoryCount> CategoryCounts, ComboTemplateDto? SelectedTemplate,
    List<ComboTemplateEstimate>? Estimates = null, Guid? CategoryId = null);
public record GetComboCatalogPageQuery(Guid? CategoryId = null, Guid? VendorId = null, Guid? ListingId = null,
    string? Search = null, string Sort = "recommended", int Page = 1, int PageSize = 6,
    List<Guid>? SelectedItemIds = null, Guid? TemplateId = null, int Guests = 200,
    decimal? MinPrice = null, decimal? MaxPrice = null, string? City = null, string? Region = null,
    int? MinServices = null, int? MaxServices = null, decimal? MinSavings = null, decimal? MinRating = null,
    Guid? TemplateCategoryId = null, Guid? FilterVendorId = null) : IRequest<ComboCatalogPageDto>;

/// <summary>Page images/variants at the database, hydrate just current cart/preset options separately.
/// The old full builder contract remains available for saved direct detail links.</summary>
public class GetComboCatalogPageQueryHandler(IApplicationDbContext db) : IRequestHandler<GetComboCatalogPageQuery, ComboCatalogPageDto>
{
    public async Task<ComboCatalogPageDto> Handle(GetComboCatalogPageQuery r, CancellationToken ct)
    {
        ComboCatalogFilters.Validate(r);
        var size = Math.Clamp(r.PageSize, 1, 10);
        var guests = Math.Clamp(r.Guests, 1, 10000);
        var eligible = ComboPublicCatalog.Eligible(db);
        var filtered = r.VendorId.HasValue ? eligible.Where(i => i.VendorId == r.VendorId) : eligible;
        if (r.FilterVendorId.HasValue) filtered = filtered.Where(i => i.VendorId == r.FilterVendorId);
        var counts = await filtered.GroupBy(i => i.CategoryId).Select(g => new ComboCategoryCount(g.Key, g.Count())).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).ToListAsync(ct);
        var vendors = await db.Vendors.AsNoTracking().Where(v => eligible.Any(i => i.VendorId == v.Id)).OrderBy(v => v.BrandName)
            .Select(v => new ComboPublicVendorDto(v.Id, v.BrandName, v.Slug, v.LogoUrl, v.CoverImageUrl, v.City, v.Address, v.Bio,
                v.IsVerified, v.TotalReviews >= 3 ? v.RatingAvg : null, v.TotalReviews,
                OperatingAreaRules.Read(v.OperatingRegions, v.City).Regions, OperatingAreaRules.Read(v.OperatingRegions, v.City).Nationwide)).ToListAsync(ct);
        var search = (r.Search ?? "").Trim().ToLowerInvariant();
        if (search.Length > 150) throw new DomainException("Từ khóa tìm kiếm tối đa 150 ký tự.");
        var hydratedIds = (r.SelectedItemIds ?? []).Distinct().Take(40).ToHashSet();
        var templates = new List<ComboTemplate>();
        var estimates = new List<ComboTemplateEstimate>();
        var rules = await new GetBundleRulesQueryHandler(db).Handle(new(), ct);
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
            // Lightweight metadata is evaluated before paging so RegionRules has exactly the same
            // alias/wildcard semantics on SQLite and SQL Server. No albums/variants are loaded here.
            var metadata = await query.Select(i => new { i.Id, i.BasePrice, i.Unit, i.Regions, i.Vendor.City,
                i.Vendor.RatingAvg, i.Vendor.TotalReviews, i.CreatedAt }).ToListAsync(ct);
            var matches = metadata.Where(i => ComboCatalogFilters.Matches(r,
                new(i.Id, i.BasePrice * ServiceItem.QuantityFor(i.Unit, guests), 0,
                    i.BasePrice * ServiceItem.QuantityFor(i.Unit, guests), 0, 1, guests,
                    i.TotalReviews >= 3 ? i.RatingAvg : null), i.City, [i.Regions])).ToList();
            total = matches.Count;
            page = Math.Clamp(r.Page, 1, Math.Max(1, (total + size - 1) / size));
            var sorted = r.Sort switch {
                "price-desc" => matches.OrderByDescending(i => i.BasePrice * ServiceItem.QuantityFor(i.Unit, guests)).ThenBy(i => i.Id),
                "newest" => matches.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id),
                "rating" => matches.OrderByDescending(i => i.TotalReviews >= 3 ? i.RatingAvg : -1).ThenBy(i => i.Id),
                _ => matches.OrderBy(i => i.BasePrice * ServiceItem.QuantityFor(i.Unit, guests)).ThenBy(i => i.Id)
            };
            var pageIds = sorted.Skip((page - 1) * size).Take(size).Select(i => i.Id).ToList();
            var hydrated = await IncludeOptions(eligible.Where(i => pageIds.Contains(i.Id))).ToDictionaryAsync(i => i.Id, ct);
            pageItems = pageIds.Where(hydrated.ContainsKey).Select(id => hydrated[id]).ToList();
        }
        // Template JSON option ownership is a legacy invariant and cannot be translated by both
        // supported EF providers. Validate lightweight slot metadata BEFORE paging, not entire albums.
        var candidateQuery = db.ComboTemplates.AsNoTracking().Include(t => t.Items).PubliclyAvailable()
            .Where(t => (!categoryId.HasValue || t.Id == r.TemplateId) &&
                ((!r.VendorId.HasValue || t.OwnerVendorId == r.VendorId) &&
                 (!r.FilterVendorId.HasValue || t.OwnerVendorId == r.FilterVendorId) || t.Id == r.TemplateId));
        if (r.TemplateCategoryId.HasValue) candidateQuery = candidateQuery.Where(t => t.Id == r.TemplateId || t.Items.Any(s => s.CategoryId == r.TemplateCategoryId));
        if (search.Length > 0) candidateQuery = candidateQuery.Where(t => t.Id == r.TemplateId || t.Name.ToLower().Contains(search) ||
            db.Vendors.Any(v => v.Id == t.OwnerVendorId && v.BrandName.ToLower().Contains(search)));
        var valid = new List<(Guid Id, int SortOrder, DateTime CreatedAt, ComboTemplateEstimate Estimate)>();
        // Batch bounded metadata; retain only ID + numeric sort keys across batches. Complete
        // descriptions/gallery/options are returned solely for the requested page and saved cart.
        for (var offset = 0; ; offset += 100)
        {
            var batch = await candidateQuery.OrderBy(t => t.Id).Skip(offset).Take(100).ToListAsync(ct);
            if (batch.Count == 0) break;
            var ids = new HashSet<Guid>();
            foreach (var template in batch)
                foreach (var slot in template.Items)
                    if (slot.ServiceItemId.HasValue && ComboVendorPolicy.TryAllowedIds(slot, out var allowed)) { ids.Add(slot.ServiceItemId.Value); ids.UnionWith(allowed); }
            var itemMetadata = await eligible.Where(i => ids.Contains(i.Id)).Select(i => new { i.Id, i.VendorId, i.ListingId,
                i.CategoryId, i.BasePrice, i.Unit, i.Regions, i.MinGuests, i.MaxGuests, i.Vendor.City,
                i.Vendor.MaxComboDiscount, i.Vendor.RatingAvg, i.Vendor.TotalReviews }).ToDictionaryAsync(i => i.Id, ct);
            foreach (var template in batch)
            {
                if (ComboFixedPackages.IsFixed(template))
                {
                    if (!await ComboTemplateAvailability.ComponentsEligibleAsync(db, template, ct)) continue;
                    var components = ComboFixedPackages.Components(template);
                    if (template.Id == r.TemplateId) selectedTemplate = template;
                    if (categoryId.HasValue || r.VendorId.HasValue && template.OwnerVendorId != r.VendorId || r.FilterVendorId.HasValue && template.OwnerVendorId != r.FilterVendorId) continue;
                    if (components.Any(component => guests < component.MinGuests || guests > component.MaxGuests)) continue;
                    var packageVendor = vendors.FirstOrDefault(vendor => vendor.Id == template.OwnerVendorId);
                    if (packageVendor == null) continue;
                    var original = template.OriginalPrice!.Value; var packagePrice = template.ComboPrice!.Value;
                    var packageSavings = Math.Max(0, original - packagePrice);
                    var packageEstimate = new ComboTemplateEstimate(template.Id, original, packageSavings, packagePrice,
                        original == 0 ? 0 : packageSavings / original, components.Count, guests, packageVendor.RatingAvg);
                    if (!ComboCatalogFilters.Matches(r, packageEstimate, packageVendor.City, components.Select(component => component.Regions), template.Region)) continue;
                    if (search.Length > 0 && !template.Name.ToLowerInvariant().Contains(search) && !packageVendor.Name.ToLowerInvariant().Contains(search)) continue;
                    if (r.TemplateCategoryId.HasValue && !components.Any(component => component.CategoryId == r.TemplateCategoryId)) continue;
                    valid.Add((template.Id, template.SortOrder, template.CreatedAt, packageEstimate));
                    continue;
                }
                if (template.Items.Count == 0 || template.Items.Any(slot => !slot.ServiceItemId.HasValue ||
                    !ComboVendorPolicy.TryAllowedIds(slot, out var allowed) ||
                    allowed.Append(slot.ServiceItemId.Value).Any(id => !itemMetadata.TryGetValue(id, out var item) ||
                        item.VendorId != template.OwnerVendorId || item.CategoryId != slot.CategoryId) ||
                    !itemMetadata.TryGetValue(slot.ServiceItemId.Value, out var source) || source.ListingId != slot.ListingId)) continue;
                if (template.Id == r.TemplateId) selectedTemplate = template;
                if (categoryId.HasValue || r.VendorId.HasValue && template.OwnerVendorId != r.VendorId || r.FilterVendorId.HasValue && template.OwnerVendorId != r.FilterVendorId) continue;
                var required = template.Items.Where(s => !s.IsOptional).Select(s => itemMetadata[s.ServiceItemId!.Value]).ToList();
                if (required.Count == 0 || required.Any(i => guests < i.MinGuests || guests > i.MaxGuests)) continue;
                var vendor = required[0];
                var subtotal = required.Sum(i => i.BasePrice * ServiceItem.QuantityFor(i.Unit, guests));
                var requestedDiscount = template.SingleVendorDiscount ?? rules.Where(rule => rule.MinItems <= required.Count)
                    .OrderByDescending(rule => rule.MinItems).Select(rule => rule.Percent).FirstOrDefault();
                var percent = Math.Min(requestedDiscount, vendor.MaxComboDiscount);
                var savings = Math.Round(subtotal * percent, 0);
                var estimate = new ComboTemplateEstimate(template.Id, subtotal, savings, subtotal - savings, percent,
                    required.Count, guests, vendor.TotalReviews >= 3 ? vendor.RatingAvg : null);
                if (!ComboCatalogFilters.Matches(r, estimate, vendor.City, required.Select(i => i.Regions), template.Region)) continue;
                // The selected cart is hydrated independently, never used to bypass discovery filters.
                if (search.Length > 0 && !template.Name.ToLowerInvariant().Contains(search) && !vendors.Any(v => v.Id == template.OwnerVendorId && v.Name.ToLowerInvariant().Contains(search))) continue;
                if (r.TemplateCategoryId.HasValue && !template.Items.Any(s => s.CategoryId == r.TemplateCategoryId)) continue;
                valid.Add((template.Id, template.SortOrder, template.CreatedAt, estimate));
            }
            if (batch.Count < 100) break;
        }
        if (!categoryId.HasValue)
        {
            total = valid.Count;
            page = Math.Clamp(r.Page, 1, Math.Max(1, (total + size - 1) / size));
            var sorted = r.Sort switch {
                "price-asc" => valid.OrderBy(t => t.Estimate.Total).ThenBy(t => t.Id),
                "price-desc" => valid.OrderByDescending(t => t.Estimate.Total).ThenBy(t => t.Id),
                "rating" => valid.OrderByDescending(t => t.Estimate.ShopRating ?? -1).ThenBy(t => t.Id),
                "savings" => valid.OrderByDescending(t => t.Estimate.Savings).ThenBy(t => t.Id),
                "newest" => valid.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id),
                _ => valid.OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
            };
            var matches = sorted.Skip((page - 1) * size).Take(size).ToList();
            var pageIds = matches.Select(t => t.Id).ToList();
            var hydrated = await db.ComboTemplates.AsNoTracking().Include(t => t.Items).Where(t => pageIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
            templates = pageIds.Where(hydrated.ContainsKey).Select(id => hydrated[id]).ToList();
            estimates = matches.Select(t => t.Estimate).ToList();
        }
        foreach (var template in templates.Concat(selectedTemplate is null ? [] : new[] { selectedTemplate }))
            foreach (var slot in template.Items) hydratedIds.Add(slot.ServiceItemId!.Value);
        var extras = await IncludeOptions(eligible.Where(i => hydratedIds.Contains(i.Id))).ToListAsync(ct);
        var options = pageItems.Concat(extras).DistinctBy(i => i.Id).ToDictionary(i => i.Id);
        ComboTemplateDto MapTemplate(ComboTemplate t) => ComboMap.ToDto(t) with {
            OwnerVendorName = vendors.FirstOrDefault(v => v.Id == t.OwnerVendorId)?.Name,
            EstimatedTotal = ComboFixedPackages.IsFixed(t) ? t.OriginalPrice ?? 0 : t.Items.Where(s => !s.IsOptional).Sum(s => options.TryGetValue(s.ServiceItemId!.Value, out var item) ?
                item.BasePrice * ServiceItem.QuantityFor(item.Unit, guests) : 0)
        };
        var returnedOptions = extras.Select(MapOption).ToDictionary(option => option.ServiceItemId);
        if (selectedTemplate != null && ComboFixedPackages.IsFixed(selectedTemplate))
        {
            var vendor = vendors.First(vendor => vendor.Id == selectedTemplate.OwnerVendorId);
            foreach (var component in ComboFixedPackages.Components(selectedTemplate))
                returnedOptions[component.ServiceItemId] = ComboFixedPackages.Option(component, vendor.Name, vendor.IsVerified, vendor.RatingAvg ?? 0);
        }
        var builder = new ComboBuilderDto(categories.Select(c => new ComboCategoryDto(c.Id, c.Name, c.Slug, c.Icon,
            pageItems.Where(i => i.CategoryId == c.Id).Select(MapOption).ToList())).ToList(),
            templates.Select(MapTemplate).ToList(), rules, guests, vendors);
        return new(builder, returnedOptions.Values.ToList(), page, size, total, counts, selectedTemplate is null ? null : MapTemplate(selectedTemplate), estimates, categoryId);
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
