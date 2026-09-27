using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews.Commands.ApproveReview;

public class ApproveReviewCommandHandler : IRequestHandler<ApproveReviewCommand, ReviewDto>
{
    private readonly IApplicationDbContext _context;

    public ApproveReviewCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReviewDto> Handle(ApproveReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _context.Reviews
            .Include(r => r.Customer)
            .Include(r => r.Vendor)
            .Include(r => r.Listing)
            .Include(r => r.BookingContract)
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken)
            ?? throw new DomainException("Bài đánh giá không tồn tại.");

        // Chuyển trạng thái sang Approved
        review.Approve();

        // FM-005: Tính lại điểm uy tín trung bình của Nhà cung cấp (Vendor Rating Average)
        // Rating_Avg = Sum(Rating_i * W_i) / Sum(W_i)
        // Trong đó W_i = 1.0 cho Verified Buyer, W_i = 0.5 cho đánh giá thường
        var vendorReviews = await _context.Reviews
            .Where(r => r.VendorId == review.VendorId && (r.Status == ReviewStatus.Approved || r.Id == review.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        decimal totalWeightedScore = 0;
        decimal totalWeight = 0;

        foreach (var r in vendorReviews)
        {
            var weight = r.IsVerifiedBuyer ? 1.0m : 0.5m;
            totalWeightedScore += r.Rating * weight;
            totalWeight += weight;
        }

        var newRatingAvg = 5.0m;
        if (totalWeight > 0)
        {
            var calculated = totalWeightedScore / totalWeight;
            // Làm tròn tới 1 chữ số thập phân theo FM-005 (VD: 4.8 / 5.0)
            newRatingAvg = Math.Round(calculated, 1, MidpointRounding.AwayFromZero);
        }

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.Id == review.VendorId, cancellationToken);

        if (vendor != null)
        {
            vendor.UpdateRating(newRatingAvg, vendorReviews.Count);
        }

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
