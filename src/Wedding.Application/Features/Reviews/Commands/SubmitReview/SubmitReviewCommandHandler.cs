using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews.Commands.SubmitReview;

public class SubmitReviewCommandHandler : IRequestHandler<SubmitReviewCommand, ReviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubmitReviewCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ReviewDto> Handle(SubmitReviewCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để gửi đánh giá.");

        var customer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new DomainException("Tài khoản người dùng không tồn tại.");

        var listing = await _context.Listings
            .Include(l => l.Vendor)
            .FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken)
            ?? throw new DomainException("Dịch vụ cưới không tồn tại.");

        var isVerifiedBuyer = false;
        Guid? validContractId = null;
        string? contractCode = null;

        // BR-009: Tiêu chuẩn đánh giá xác thực (Verified Buyer Review)
        // Chỉ những khách hàng đã ký hợp đồng và hoàn tất dịch vụ cưới (Completed) mới được gắn nhãn [Verified Buyer]
        if (request.BookingContractId.HasValue && request.BookingContractId.Value != Guid.Empty)
        {
            var contract = await _context.BookingContracts
                .FirstOrDefaultAsync(c => c.Id == request.BookingContractId.Value && c.CustomerId == userId, cancellationToken);

            if (contract != null && contract.Status == ContractStatus.Completed)
            {
                isVerifiedBuyer = true;
                validContractId = contract.Id;
                contractCode = contract.ContractCode;
            }
        }
        else
        {
            // Kiểm tra xem khách có hợp đồng Completed nào với Vendor này không
            var existingContract = await _context.BookingContracts
                .Where(c => c.CustomerId == userId && c.VendorId == listing.VendorId && c.Status == ContractStatus.Completed)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingContract != null)
            {
                isVerifiedBuyer = true;
                validContractId = existingContract.Id;
                contractCode = existingContract.ContractCode;
            }
        }

        var review = Review.Create(
            contractId: validContractId,
            listingId: listing.Id,
            customerId: customer.Id,
            vendorId: listing.VendorId,
            rating: request.Rating,
            content: request.Content,
            photosJson: request.PhotosJson,
            isVerifiedBuyer: isVerifiedBuyer
        );

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);

        return new ReviewDto(
            Id: review.Id,
            BookingContractId: review.BookingContractId,
            ContractCode: contractCode,
            ListingId: listing.Id,
            ListingTitle: listing.Title,
            CustomerId: customer.Id,
            CustomerName: customer.FullName,
            VendorId: listing.VendorId,
            VendorBrandName: listing.Vendor.BrandName,
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
