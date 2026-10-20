using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews;

public static class ReviewRules
{
    // New submissions require actual completion, not a date-derived assumption of service use.
    // Historical consultation reviews retain their stored flag and existing weighting.
    public static bool IsEligible(BookingContract c, DateTime now) => c.Status == ContractStatus.Completed;
    public static async Task<User> RequireActiveCustomerAsync(IApplicationDbContext db, ICurrentUserService current, CancellationToken ct)
    {
        if (!current.UserId.HasValue || current.UserId == Guid.Empty || current.Role != nameof(UserRole.Customer))
            throw new DomainException("Chỉ khách hàng đang đăng nhập được gửi đánh giá hoặc xem hợp đồng đủ điều kiện.");
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == current.UserId && user.IsActive && user.Role == UserRole.Customer, ct)
            ?? throw new DomainException("Tài khoản khách hàng không hoạt động hoặc quyền đã thay đổi. Vui lòng đăng nhập lại.");
    }
    public static (decimal Rating, int Count) Calculate(IEnumerable<Review> reviews)
    {
        var approved = reviews.Where(r => r.Status == ReviewStatus.Approved).ToList();
        var weight = approved.Sum(r => r.IsVerifiedBuyer ? 1m : .5m);
        return (weight == 0 ? 0 : Math.Round(approved.Sum(r => r.AverageRating * (r.IsVerifiedBuyer ? 1m : .5m)) / weight, 1, MidpointRounding.AwayFromZero), approved.Count);
    }
    public static async Task RefreshVendorAsync(IApplicationDbContext db, Guid vendorId, CancellationToken ct)
    {
        var reviews = await db.Reviews.AsNoTracking().Where(r => r.VendorId == vendorId && r.Status == ReviewStatus.Approved).ToListAsync(ct);
        var result = Calculate(reviews); var vendor = await db.Vendors.FirstOrDefaultAsync(v => v.Id == vendorId, ct);
        if (vendor != null) vendor.UpdateRating(result.Rating, result.Count);
    }
    public static ReviewDto Dto(Review r) => new(r.Id, r.BookingContractId, r.BookingContract?.ContractCode, r.ListingId, r.Listing?.Title ?? "", r.CustomerId, r.Customer?.FullName ?? "", r.VendorId, r.Vendor?.BrandName ?? "", r.Rating, r.Content, r.PhotosJson, r.IsVerifiedBuyer, r.Status.ToString(), r.VendorReply, r.VendorRepliedAt, r.CreatedAt,
        r.QualityRating, r.ServiceRating, r.PunctualityRating, r.ValueRating, r.AverageRating, r.ModerationFlag, r.CreatedAt.AddHours(24), r.RejectionReason);
}
