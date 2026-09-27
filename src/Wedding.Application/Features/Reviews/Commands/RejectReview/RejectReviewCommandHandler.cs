using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews.Commands.RejectReview;

public class RejectReviewCommandHandler : IRequestHandler<RejectReviewCommand, ReviewDto>
{
    private readonly IApplicationDbContext _context;

    public RejectReviewCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReviewDto> Handle(RejectReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews
            .Include(r => r.Customer)
            .Include(r => r.Vendor)
            .Include(r => r.Listing)
            .Include(r => r.BookingContract)
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken)
            ?? throw new DomainException("Bài đánh giá không tồn tại.");

        review.Reject();
        await _context.SaveChangesAsync(cancellationToken);

        return new ReviewDto(
            Id: review.Id,
            BookingContractId: review.BookingContractId,
            ContractCode: review.BookingContract?.ContractCode,
            ListingId: review.ListingId,
            ListingTitle: review.Listing.Title,
            CustomerId: review.CustomerId,
            CustomerName: review.Customer.FullName,
            VendorId: review.VendorId,
            VendorBrandName: review.Vendor.BrandName,
            Rating: review.Rating,
            Content: review.Content,
            PhotosJson: review.PhotosJson,
            IsVerifiedBuyer: review.IsVerifiedBuyer,
            Status: review.Status.ToString(),
            VendorReply: review.VendorReply,
            VendorRepliedAt: review.VendorRepliedAt,
            CreatedAt: review.CreatedAt
        );
    }
}
