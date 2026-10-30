using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

/// <summary>Public runtime eligibility, separate from the stricter submission/legal review workflow.
/// Does not manufacture a legal profile for legacy shops or reinterpret their tax approval.</summary>
public static class ComboPublicCatalog
{
    public static IQueryable<ServiceItem> Eligible(IApplicationDbContext db) => db.ServiceItems.AsNoTracking().Where(i =>
        i.IsActive && i.ModerationStatus == "Approved" && i.Listing.Status == ListingStatus.Active && i.Listing.IsActive &&
        i.Listing.VendorId == i.VendorId && i.Listing.CategoryId == i.CategoryId &&
        db.Categories.Any(c => c.Id == i.CategoryId && c.IsActive &&
            (!c.BusinessGroupId.HasValue || c.BusinessGroup!.IsActive)) &&
        i.Vendor.IsActive && i.Vendor.User.IsActive && i.Vendor.User.Role == UserRole.VendorOwner &&
        i.Vendor.IsVerified && i.Vendor.AllowInCombos);
}

public record ComboTemplateEstimate(Guid TemplateId, decimal Subtotal, decimal Savings, decimal Total,
    decimal DiscountPercent, int RequiredServiceCount, int Guests, decimal? ShopRating);

internal static class ComboCatalogFilters
{
    public static void Validate(GetComboCatalogPageQuery r)
    {
        if ((r.Search?.Trim().Length ?? 0) > 150 || (r.City?.Trim().Length ?? 0) > 100 || (r.Region?.Trim().Length ?? 0) > 100)
            throw new DomainException("Từ khóa tối đa 150 ký tự; địa điểm/khu vực tối đa 100 ký tự.");
        if (r.MinPrice < 0 || r.MaxPrice < 0 || r.MinSavings < 0 || r.MinPrice > 1000000000000000m ||
            r.MaxPrice > 1000000000000000m || r.MinSavings > 1000000000000000m ||
            r.MinPrice.HasValue && r.MaxPrice.HasValue && r.MinPrice > r.MaxPrice ||
            r.MinPrice.HasValue && r.MinPrice != decimal.Truncate(r.MinPrice.Value) ||
            r.MaxPrice.HasValue && r.MaxPrice != decimal.Truncate(r.MaxPrice.Value) ||
            r.MinSavings.HasValue && r.MinSavings != decimal.Truncate(r.MinSavings.Value))
            throw new DomainException("Khoảng giá/mức tiết kiệm phải theo đồng nguyên, không âm và giá tối đa không nhỏ hơn tối thiểu.");
        if (r.MinServices is < 1 or > 12 || r.MaxServices is < 1 or > 12 || r.MinServices.HasValue && r.MaxServices.HasValue && r.MinServices > r.MaxServices || r.MinRating is < 0 or > 5)
            throw new DomainException("Số dịch vụ từ 1–12; đánh giá shop từ 0–5 sao.");
        if (r.Sort is not ("recommended" or "price-asc" or "price-desc" or "newest" or "rating" or "savings"))
            throw new DomainException("Cách sắp xếp không hợp lệ.");
    }

    public static bool Matches(GetComboCatalogPageQuery r, ComboTemplateEstimate estimate, string city, IEnumerable<string> regions, string? templateRegion = null)
        => (!r.MinPrice.HasValue || estimate.Total >= r.MinPrice) && (!r.MaxPrice.HasValue || estimate.Total <= r.MaxPrice) &&
           (!r.MinSavings.HasValue || estimate.Savings >= r.MinSavings) &&
           (!r.MinServices.HasValue || estimate.RequiredServiceCount >= r.MinServices) &&
           (!r.MaxServices.HasValue || estimate.RequiredServiceCount <= r.MaxServices) &&
           (!r.MinRating.HasValue || estimate.ShopRating.HasValue && estimate.ShopRating >= r.MinRating) &&
           (string.IsNullOrWhiteSpace(r.City) || RegionRules.Canonical(city) == RegionRules.Canonical(r.City)) &&
           (string.IsNullOrWhiteSpace(r.Region) ||
               (string.IsNullOrWhiteSpace(templateRegion) || RegionRules.Serves(templateRegion, r.Region)) && regions.All(area => RegionRules.Serves(area, r.Region)));
}
