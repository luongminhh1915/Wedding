using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings;

/// <summary>Publication and preview rules shared by browse, details and public reviews.</summary>
public static class ListingAccess
{
    public static IQueryable<Listing> PubliclyVisible(this IQueryable<Listing> query) => query.Where(listing =>
        listing.Status == ListingStatus.Active && listing.Vendor.IsActive && listing.Vendor.User.IsActive);

    public static IQueryable<Listing> VisibleTo(this IQueryable<Listing> query, ICurrentUserService user)
    {
        var canModerate = user.UserId.HasValue && user.Role is nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin);
        var canOwn = user.UserId.HasValue && user.Role == nameof(UserRole.VendorOwner);
        return query.Where(listing => canModerate || canOwn && listing.Vendor.UserId == user.UserId ||
            listing.Status == ListingStatus.Active && listing.Vendor.IsActive && listing.Vendor.User.IsActive);
    }

    public static bool CanReadModerationDetails(Listing listing, ICurrentUserService user) => user.UserId.HasValue &&
        (user.Role is nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin) ||
            user.Role == nameof(UserRole.VendorOwner) && listing.Vendor.UserId == user.UserId);

    public static async Task RequireImageUploadAccessAsync(IApplicationDbContext db, ICurrentUserService user, CancellationToken ct)
    {
        if (user.Role != nameof(UserRole.VendorOwner) || !user.UserId.HasValue ||
            !await db.Vendors.AnyAsync(vendor => vendor.UserId == user.UserId && vendor.IsActive && vendor.User.IsActive, ct))
            throw new DomainException("Chỉ nhà cung cấp đang hoạt động được tải ảnh dịch vụ.");
    }
}
