using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Application.Features.Listings;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews.Queries.GetListingReviews;

public record GetListingReviewsQuery(
    Guid? ListingId = null,
    Guid? VendorId = null
) : IRequest<List<PublicReviewDto>>;

public class GetListingReviewsQueryHandler : IRequestHandler<GetListingReviewsQuery, List<PublicReviewDto>>
{
    private readonly IApplicationDbContext _context;

    public GetListingReviewsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PublicReviewDto>> Handle(GetListingReviewsQuery request, CancellationToken cancellationToken)
    {
        var visibleListings = _context.Listings.PubliclyVisible();
        if (request.ListingId is { } listingId && listingId != Guid.Empty)
        {
            if (!await visibleListings.AnyAsync(listing => listing.Id == listingId, cancellationToken))
                throw new NotFoundException("Dịch vụ không tồn tại hoặc chưa được công khai.");
        }
        else if (request.VendorId is { } vendorId && vendorId != Guid.Empty)
        {
            if (!await _context.Vendors.AnyAsync(vendor => vendor.Id == vendorId && vendor.IsActive && vendor.User.IsActive, cancellationToken))
                throw new NotFoundException("Nhà cung cấp không tồn tại hoặc chưa được công khai.");
        }
        else throw new NotFoundException("Không tìm thấy dịch vụ hoặc nhà cung cấp.");

        var query = _context.Reviews
            .Include(r => r.Customer)
            .Include(r => r.Vendor)
            .Include(r => r.Listing)
            .Where(r => r.Status == ReviewStatus.Approved && visibleListings.Select(listing => listing.Id).Contains(r.ListingId));

        if (request.ListingId.HasValue && request.ListingId.Value != Guid.Empty)
        {
            query = query.Where(r => r.ListingId == request.ListingId.Value);
        }
        else if (request.VendorId.HasValue && request.VendorId.Value != Guid.Empty)
        {
            query = query.Where(r => r.VendorId == request.VendorId.Value);
        }

        var reviews = await query
            .OrderByDescending(r => r.IsVerifiedBuyer) // Ưu tiên hiển thị Verified Buyer lên trước
            .ThenByDescending(r => r.CreatedAt)
            .Take(100)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reviews.Select(r => new PublicReviewDto(
            Id: r.Id,
            ListingId: r.ListingId,
            ListingTitle: r.Listing.Title,
            CustomerName: r.Customer.FullName,
            VendorId: r.VendorId,
            VendorBrandName: r.Vendor.BrandName,
            Rating: r.Rating,
            Content: r.Content,
            PhotosJson: r.PhotosJson,
            IsVerifiedBuyer: r.IsVerifiedBuyer,
            Status: r.Status.ToString(),
            VendorReply: r.VendorReply,
            VendorRepliedAt: r.VendorRepliedAt,
            CreatedAt: r.CreatedAt,
            QualityRating: r.QualityRating, ServiceRating: r.ServiceRating, PunctualityRating: r.PunctualityRating, ValueRating: r.ValueRating, AverageRating: r.AverageRating
        )).ToList();
    }
}
