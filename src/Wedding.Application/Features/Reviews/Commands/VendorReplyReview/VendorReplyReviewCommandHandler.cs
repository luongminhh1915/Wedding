using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews.Commands.VendorReplyReview;

public class VendorReplyReviewCommandHandler : IRequestHandler<VendorReplyReviewCommand, ReviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public VendorReplyReviewCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ReviewDto> Handle(VendorReplyReviewCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để phản hồi đánh giá.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var review = await _context.Reviews
            .Include(r => r.Customer)
            .Include(r => r.Vendor)
            .Include(r => r.Listing)
            .Include(r => r.BookingContract)
            .FirstOrDefaultAsync(r => r.Id == request.ReviewId, cancellationToken)
            ?? throw new DomainException("Bài đánh giá không tồn tại.");

        if (review.VendorId != vendor.Id)
            throw new DomainException("Bạn chỉ có thể phản hồi đánh giá dành cho dịch vụ của chính mình.");

        review.AddVendorReply(request.Reply);
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
