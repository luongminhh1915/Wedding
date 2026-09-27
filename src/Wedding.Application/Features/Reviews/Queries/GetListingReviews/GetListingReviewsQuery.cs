using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Reviews.Queries.GetListingReviews;

public record GetListingReviewsQuery(
    Guid? ListingId = null,
    Guid? VendorId = null
) : IRequest<List<ReviewDto>>;

public class GetListingReviewsQueryHandler : IRequestHandler<GetListingReviewsQuery, List<ReviewDto>>
{
    private readonly IApplicationDbContext _context;

    public GetListingReviewsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReviewDto>> Handle(GetListingReviewsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Reviews
            .Include(r => r.Customer)
            .Include(r => r.Vendor)
            .Include(r => r.Listing)
            .Include(r => r.BookingContract)
            .Where(r => r.Status == ReviewStatus.Approved);

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
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reviews.Select(r => new ReviewDto(
            Id: r.Id,
            BookingContractId: r.BookingContractId,
            ContractCode: r.BookingContract?.ContractCode,
            ListingId: r.ListingId,
            ListingTitle: r.Listing.Title,
            CustomerId: r.CustomerId,
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
            CreatedAt: r.CreatedAt
        )).ToList();
    }
}
