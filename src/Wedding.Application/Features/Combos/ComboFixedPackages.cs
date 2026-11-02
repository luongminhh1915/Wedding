using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

/// <summary>Public component content captured for one base service, without optional extras.</summary>
public record ComboComponentSnapshotDto(Guid ServiceItemId, Guid ListingId, Guid VendorId, Guid CategoryId,
    string Name, string ListingTitle, decimal BasePrice, string Unit, int Quantity, int MinGuests, int MaxGuests,
    string Regions, string CancellationPolicy, string Description, string? ImageUrl, List<string> GalleryUrls,
    ServiceDetailsDto? ServiceDetails, List<VariantDto> Variants, List<AddOnDto> AddOns, string SourceVersion);

public static class ComboFixedPackages
{
    public static bool IsFixed(ComboTemplate template) => template.PricingMode == "FixedPrice";
    public static ComboComponentSnapshotDto Capture(ServiceItem item)
    {
        var gallery = item.Listing.Media.Where(media => media.MediaType == "image")
            .OrderByDescending(media => media.IsFeatured).ThenBy(media => media.DisplayOrder).ThenBy(media => media.Id)
            .Select(media => media.MediaUrl).Where(url => !string.IsNullOrWhiteSpace(url)).Distinct().Take(12).ToList();
        var component = new ComboComponentSnapshotDto(item.Id, item.ListingId, item.VendorId, item.CategoryId,
            item.Name, item.Listing.Title, item.BasePrice, item.Unit, 1, item.MinGuests, item.MaxGuests, item.Regions,
            item.CancellationPolicy, item.Listing.Description, gallery.FirstOrDefault(), gallery,
            ListingServiceDetails.Read(item.Listing.ServiceDetailsJson),
            item.Variants.OrderBy(variant => variant.Id).Select(variant => new VariantDto(variant.Id, variant.Name, variant.PriceDelta)).ToList(),
            item.AddOns.OrderBy(addOn => addOn.Id).Select(addOn => new AddOnDto(addOn.Id, addOn.Name, addOn.Price, addOn.PerUnit)).ToList(), "");
        // SQL decimal scale and timestamp Kind must never manufacture a component revision.
        static string Money(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
        var canonical = JsonSerializer.Serialize(new { component.ServiceItemId, component.ListingId, component.VendorId,
            component.CategoryId, component.Name, component.ListingTitle, BasePrice = Money(component.BasePrice), component.Unit,
            component.Quantity, component.MinGuests, component.MaxGuests, component.Regions, component.CancellationPolicy,
            component.Description, component.GalleryUrls, component.ServiceDetails,
            Variants = component.Variants.Select(variant => new { variant.Id, variant.Name, PriceDelta = Money(variant.PriceDelta) }),
            AddOns = component.AddOns.Select(addOn => new { addOn.Id, addOn.Name, Price = Money(addOn.Price), addOn.PerUnit }) }, ComboTemplateContent.JsonOptions);
        return component with { SourceVersion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))) };
    }
    public static string Serialize(ComboComponentSnapshotDto component) => JsonSerializer.Serialize(component, ComboTemplateContent.JsonOptions);
    public static ComboComponentSnapshotDto? Read(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ComboComponentSnapshotDto>(json, ComboTemplateContent.JsonOptions); }
        catch (JsonException) { return null; }
    }
    public static List<ComboComponentSnapshotDto> Components(ComboTemplate template) => template.Items
        .Select(item => Read(item.ComponentSnapshotJson)).Where(component => component != null).Cast<ComboComponentSnapshotDto>()
        .OrderBy(component => component.ServiceItemId).ToList();
    public static ComboOptionDto Option(ComboComponentSnapshotDto component, string vendorName, bool verified, decimal rating) =>
        new(component.ListingId, component.VendorId, vendorName, verified, rating, component.Name, component.BasePrice,
            component.BasePrice, component.Regions, component.ImageUrl, component.ServiceItemId, component.Unit,
            component.MinGuests, component.MaxGuests, component.CancellationPolicy, [], [], component.GalleryUrls, component.Description);
    public static async Task<bool> MatchesApprovedSourcesAsync(IApplicationDbContext db, ComboTemplate template, CancellationToken ct)
    {
        var components = Components(template);
        if (components.Count != template.Items.Count || components.Count < 2 || template.ComboPrice == null ||
            template.OriginalPrice != components.Sum(component => component.BasePrice) ||
            components.Select(component => component.ServiceItemId).Distinct().Count() != components.Count ||
            components.Select(component => component.ListingId).Distinct().Count() != components.Count) return false;
        var ids = components.Select(component => component.ServiceItemId).ToList();
        var current = await ComboPublicCatalog.Eligible(db).Include(item => item.Listing).ThenInclude(listing => listing.Media)
            .Include(item => item.Variants).Include(item => item.AddOns).Where(item => ids.Contains(item.Id)).ToListAsync(ct);
        return current.Count == components.Count && current.All(item => item.VendorId == template.OwnerVendorId && item.Listing.PricingServiceItemId == item.Id &&
            Capture(item).SourceVersion == components.Single(component => component.ServiceItemId == item.Id).SourceVersion);
    }
    internal static void ValidatePrice(string mode, decimal? price)
    {
        if (mode is not ("Dynamic" or "FixedPrice")) throw new DomainException("Loại giá combo không hợp lệ.");
        if (mode == "FixedPrice" && (!price.HasValue || price <= 0 || price > 100000000000m || price != decimal.Truncate(price.Value)))
            throw new DomainException("Nhập giá combo theo đồng nguyên, lớn hơn 0 và tối đa 100 tỷ VNĐ.");
        if (mode == "Dynamic" && price.HasValue) throw new DomainException("Giá cố định cần chọn loại combo FixedPrice.");
    }
}
