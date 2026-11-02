using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Combos;

namespace Wedding.Application.Features.Listings.DTOs;

public record ListingDto(
    Guid Id,
    string Title,
    string Slug,
    decimal MinPrice,
    decimal MaxPrice,
    string Description,
    string Location,
    string Status,
    string? RejectionReason,
    Guid CategoryId,
    string CategoryName,
    Guid VendorId,
    string VendorBrandName,
    int ViewCount,
    DateTime CreatedAt,
    DateTime? ModeratedAt,
    List<ListingMediaDto> Media,
    ServiceDetailsDto? ServiceDetails = null,
    string? CancellationPolicy = null,
    bool CategoryRequiresCommissionBasisApproval = false,
    bool IsActive = true,
    ListingRevisionDto? LatestRevision = null,
    List<ListingRevisionDto>? RevisionHistory = null,
    ServicePricingDto? Pricing = null,
    Guid? PricingServiceItemId = null,
    List<ServiceItemDto>? PricingOptions = null,
    bool PricingSelectionRequired = false
);

public record ListingMediaDto(
    Guid Id,
    string MediaUrl,
    string? ThumbnailUrl,
    bool IsFeatured,
    int DisplayOrder
);

public record ListingSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    decimal MinPrice,
    decimal MaxPrice,
    string Location,
    string Status,
    string? RejectionReason,
    string CategoryName,
    string VendorBrandName,
    int ViewCount,
    DateTime CreatedAt,
    string? PrimaryImageUrl,
    DateTime? UpdatedAt = null,
    bool IsActive = true,
    string? RevisionStatus = null,
    Guid? LatestRevisionId = null,
    string? RevisionReviewNote = null,
    Guid CategoryId = default,
    Guid? PricingServiceItemId = null,
    decimal? BasePrice = null,
    string? PriceUnit = null
);
