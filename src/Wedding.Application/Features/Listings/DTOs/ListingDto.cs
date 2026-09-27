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
    List<ListingMediaDto> Media
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
    string? PrimaryImageUrl
);
