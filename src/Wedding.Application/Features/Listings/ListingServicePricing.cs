using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings;

/// <summary>Explicit transactional pricing, authored together with the source listing. Null option collections preserve existing children.</summary>
public record ServicePricingDto(decimal BasePrice, string Unit = "Package", int MinGuests = 1, int MaxGuests = 2000,
    string Regions = "", string? CancellationPolicy = null, int MaxBookingsPerDay = 1, Guid? ServiceItemId = null,
    List<VariantDto>? Variants = null, List<AddOnDto>? AddOns = null, string? SourceVersion = null);

public static class ListingServicePricing
{
    public static ServicePricingDto Capture(ServiceItem item) => new(item.BasePrice, item.Unit, item.MinGuests,
        item.MaxGuests, item.Regions, item.CancellationPolicy, item.MaxBookingsPerDay, item.Id,
        item.Variants.Select(x => new VariantDto(x.Id, x.Name, x.PriceDelta)).ToList(),
        item.AddOns.Select(x => new AddOnDto(x.Id, x.Name, x.Price, x.PerUnit)).ToList(), ContentVersion(item));

    private static string ContentVersion(ServiceItem item)
    {
        static string Money(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
        var json = JsonSerializer.Serialize(new { item.Id, item.ListingId, item.VendorId, item.CategoryId, item.Name,
            BasePrice = Money(item.BasePrice), item.Unit, item.MinGuests, item.MaxGuests, item.Regions, item.CancellationPolicy, item.MaxBookingsPerDay,
            Variants = item.Variants.OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, Delta = Money(x.PriceDelta) }),
            AddOns = item.AddOns.OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, Price = Money(x.Price), x.PerUnit }) });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    private static Task<List<ServiceItem>> Items(IApplicationDbContext db, Guid listingId, CancellationToken ct) =>
        db.ServiceItems.Include(x => x.Variants).Include(x => x.AddOns).Where(x => x.ListingId == listingId).ToListAsync(ct);

    public static async Task<ServicePricingDto?> Current(IApplicationDbContext db, Listing listing, CancellationToken ct)
    {
        if (!listing.PricingServiceItemId.HasValue) return null;
        var item = (await Items(db, listing.Id, ct)).SingleOrDefault(x => x.Id == listing.PricingServiceItemId);
        RequireSource(listing, item);
        return Capture(item!);
    }

    public static async Task<ServicePricingDto?> Prepare(IApplicationDbContext db, Listing listing, ServicePricingDto? request,
        CancellationToken ct, ServicePricingDto? previous = null)
    {
        if (request == null) return previous ?? await Current(db, listing, ct);
        var items = await Items(db, listing.Id, ct);
        var id = request.ServiceItemId ?? previous?.ServiceItemId ?? listing.PricingServiceItemId;
        if (listing.PricingServiceItemId.HasValue && id != listing.PricingServiceItemId)
            throw new DomainException("Không được đổi liên kết giá của dịch vụ đã thống nhất. Các dịch vụ cũ vẫn được bảo toàn.");
        var item = id.HasValue ? items.SingleOrDefault(x => x.Id == id) : null;
        if (item != null) RequireSource(listing, item);
        else if (id.HasValue && previous?.ServiceItemId != id)
            throw new DomainException("Dịch vụ giá không tồn tại hoặc không thuộc đúng dịch vụ/ngành của shop.");
        if (item == null && items.Count > 0)
            throw new DomainException("Dịch vụ này có dữ liệu giá cũ. Hãy chọn rõ dịch vụ giá cần liên kết; không có dữ liệu nào được hợp nhất hoặc xóa.");
        if (!id.HasValue) id = Guid.NewGuid();
        var expectedVersion = request.SourceVersion ?? previous?.SourceVersion;
        if (item != null && expectedVersion != null && expectedVersion != ContentVersion(item))
            throw new DomainException("Cấu hình giá gốc đã thay đổi. Hãy tải lại dịch vụ và gửi phiên bản mới để duyệt.");
        var variants = request.Variants ?? previous?.Variants ?? item?.Variants.Select(x => new VariantDto(x.Id, x.Name, x.PriceDelta)).ToList() ?? [];
        var addOns = request.AddOns ?? previous?.AddOns ?? item?.AddOns.Select(x => new AddOnDto(x.Id, x.Name, x.Price, x.PerUnit)).ToList() ?? [];
        var normalized = ServiceItemAccess.Normalize(new(id, listing.Id, listing.Title, request.BasePrice, request.Unit,
            request.MinGuests, request.MaxGuests, request.Regions, request.CancellationPolicy ?? previous?.CancellationPolicy ?? item?.CancellationPolicy ?? listing.CancellationPolicy,
            item?.IsActive ?? true, variants, addOns, MaxBookingsPerDay: request.MaxBookingsPerDay));
        ValidateChildIds(item, normalized.Variants, normalized.AddOns);
        return request with { ServiceItemId = id, Regions = normalized.Regions, CancellationPolicy = normalized.CancellationPolicy,
            Variants = NormalizeVariantIds(item, normalized.Variants), AddOns = NormalizeAddOnIds(item, normalized.AddOns),
            SourceVersion = item == null ? null : ContentVersion(item) };
    }

    public static async Task<ServiceItem> Apply(IApplicationDbContext db, Listing listing, ServicePricingDto pricing, CancellationToken ct)
    {
        // Only immutable server-authored snapshots may introduce a planned ID. All source and child ownership is rechecked here.
        var normalized = await Prepare(db, listing, pricing, ct, pricing) ?? throw new DomainException("Thiếu cấu hình giá.");
        var item = (await Items(db, listing.Id, ct)).SingleOrDefault(x => x.Id == normalized.ServiceItemId);
        if (item == null)
        {
            if (await db.ServiceItems.AnyAsync(x => x.Id == normalized.ServiceItemId, ct)) throw new DomainException("Mã dịch vụ giá đã được sử dụng.");
            item = ServiceItem.Create(listing.VendorId, listing.Id, listing.CategoryId, listing.Title, normalized.BasePrice,
                normalized.Unit, normalized.MinGuests, normalized.MaxGuests, normalized.Regions, normalized.CancellationPolicy!, normalized.ServiceItemId);
            db.ServiceItems.Add(item);
        }
        else if (!SameValues(item, listing.Title, normalized.BasePrice, normalized.Unit, normalized.MinGuests,
                     normalized.MaxGuests, normalized.Regions, normalized.CancellationPolicy!, item.IsActive, normalized.MaxBookingsPerDay))
            item.Update(listing.Title, normalized.BasePrice, normalized.Unit, normalized.MinGuests, normalized.MaxGuests,
                normalized.Regions, normalized.CancellationPolicy!, item.IsActive);
        item.SetDailyCapacity(normalized.MaxBookingsPerDay);
        if (UpdateChildren(db, item, normalized.Variants!, normalized.AddOns!)) item.SynchronizeListingModeration("Draft");
        listing.AttachPricingServiceItem(item.Id);
        if (listing.CancellationPolicy != normalized.CancellationPolicy) listing.SetCancellationPolicy(normalized.CancellationPolicy!);
        return item;
    }

    public static async Task Synchronize(IApplicationDbContext db, Listing listing, string status, CancellationToken ct, string? note = null)
    {
        if (!listing.PricingServiceItemId.HasValue) return;
        var item = db.ServiceItems.Local.SingleOrDefault(x => x.Id == listing.PricingServiceItemId)
            ?? await db.ServiceItems.SingleOrDefaultAsync(x => x.Id == listing.PricingServiceItemId, ct);
        RequireSource(listing, item);
        item!.SynchronizeListingModeration(status, note);
    }

    public static async Task<ListingDto> Enrich(IApplicationDbContext db, Listing listing, ListingDto dto, CancellationToken ct,
        ServicePricingDto? proposed = null)
    {
        var items = await db.ServiceItems.AsNoTracking().Include(x => x.Vendor).Include(x => x.Variants).Include(x => x.AddOns)
            .Where(x => x.ListingId == listing.Id && x.VendorId == listing.VendorId && x.CategoryId == listing.CategoryId).ToListAsync(ct);
        var item = items.SingleOrDefault(x => x.Id == listing.PricingServiceItemId);
        return dto with { Pricing = proposed ?? (item == null ? null : Capture(item)),
            PricingServiceItemId = listing.PricingServiceItemId,
            PricingOptions = items.Select(CatalogMap.ToDto).ToList(),
            PricingSelectionRequired = listing.PricingServiceItemId == null && items.Count > 0 };
    }

    private static void RequireSource(Listing listing, ServiceItem? item)
    {
        if (item == null || item.ListingId != listing.Id || item.VendorId != listing.VendorId || item.CategoryId != listing.CategoryId)
            throw new DomainException("Liên kết giá không thuộc đúng dịch vụ, shop hoặc ngành. Dữ liệu cũ được giữ để kiểm tra.");
    }

    internal static bool SameValues(ServiceItem item, string name, decimal price, string unit, int minGuests, int maxGuests,
        string regions, string cancellationPolicy, bool active, int capacity) => item.Name == name.Trim() && item.BasePrice == price && item.Unit == unit &&
        item.MinGuests == minGuests && item.MaxGuests == maxGuests && item.Regions == regions && item.CancellationPolicy == cancellationPolicy &&
        item.IsActive == active && item.MaxBookingsPerDay == capacity;

    internal static void ValidateChildIds(ServiceItem? item, List<VariantDto> variants, List<AddOnDto> addOns)
    {
        var variantIds = variants.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToList();
        var addOnIds = addOns.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToList();
        if (variantIds.Distinct().Count() != variantIds.Count || addOnIds.Distinct().Count() != addOnIds.Count ||
            variantIds.Any(id => item == null || item.Variants.All(x => x.Id != id)) ||
            addOnIds.Any(id => item == null || item.AddOns.All(x => x.Id != id)))
            throw new DomainException("Biến thể/món kèm bị trùng mã hoặc không thuộc dịch vụ đang sửa.");
    }

    private static List<VariantDto> NormalizeVariantIds(ServiceItem? item, List<VariantDto> variants)
    {
        var used = variants.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        return variants.Select(x => {
            if (x.Id.HasValue) return x;
            var existing = item?.Variants.FirstOrDefault(v => !used.Contains(v.Id) && v.Name == x.Name && v.PriceDelta == x.PriceDelta);
            if (existing == null) return x;
            used.Add(existing.Id); return x with { Id = existing.Id };
        }).ToList();
    }
    private static List<AddOnDto> NormalizeAddOnIds(ServiceItem? item, List<AddOnDto> addOns)
    {
        var used = addOns.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
        return addOns.Select(x => {
            if (x.Id.HasValue) return x;
            var existing = item?.AddOns.FirstOrDefault(a => !used.Contains(a.Id) && a.Name == x.Name && a.Price == x.Price && a.PerUnit == x.PerUnit);
            if (existing == null) return x;
            used.Add(existing.Id); return x with { Id = existing.Id };
        }).ToList();
    }

    internal static bool UpdateChildren(IApplicationDbContext db, ServiceItem item, List<VariantDto> variants, List<AddOnDto> addOns)
    {
        ValidateChildIds(item, variants, addOns);
        variants = NormalizeVariantIds(item, variants); addOns = NormalizeAddOnIds(item, addOns);
        var changed = false;
        foreach (var old in item.Variants.Where(x => variants.All(v => v.Id != x.Id)).ToList())
        { db.ItemVariants.Remove(old); item.Variants.Remove(old); changed = true; }
        foreach (var old in item.AddOns.Where(x => addOns.All(a => a.Id != x.Id)).ToList())
        { db.ItemAddOns.Remove(old); item.AddOns.Remove(old); changed = true; }
        foreach (var value in variants)
        {
            var existing = item.Variants.SingleOrDefault(x => x.Id == value.Id);
            if (existing == null) { var child = ItemVariant.Create(item.Id, value.Name, value.PriceDelta); item.Variants.Add(child); db.ItemVariants.Add(child); changed = true; }
            else if (existing.Name != value.Name || existing.PriceDelta != value.PriceDelta) { existing.Update(value.Name, value.PriceDelta); changed = true; }
        }
        foreach (var value in addOns)
        {
            var existing = item.AddOns.SingleOrDefault(x => x.Id == value.Id);
            if (existing == null) { var child = ItemAddOn.Create(item.Id, value.Name, value.Price, value.PerUnit); item.AddOns.Add(child); db.ItemAddOns.Add(child); changed = true; }
            else if (existing.Name != value.Name || existing.Price != value.Price || existing.PerUnit != value.PerUnit) { existing.Update(value.Name, value.Price, value.PerUnit); changed = true; }
        }
        return changed;
    }
}
