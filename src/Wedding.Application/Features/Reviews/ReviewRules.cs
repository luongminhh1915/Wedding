using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Reviews;

public static class ReviewRules
{
    public static bool IsEligible(BookingContract c, DateTime now) => c.Status == ContractStatus.Completed || c.Status == ContractStatus.Confirmed && c.WeddingDate.HasValue && c.WeddingDate.Value.Date.AddDays(3).AddHours(-7) <= now;
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
