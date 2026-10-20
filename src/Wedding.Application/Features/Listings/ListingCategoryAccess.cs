using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings;

/// <summary>Business membership is a permitted service scope, not a commission-policy transition.
/// An unmigrated shop keeps existing records; only creating a new listing requires explicit scope.</summary>
public static class ListingCategoryAccess
{
    public const string LegacyGuidance = "Shop chưa được xác nhận nhóm kinh doanh. Vui lòng liên hệ Admin để bổ sung nhóm trước khi tạo dịch vụ mới; dịch vụ và hoa hồng cũ vẫn được giữ nguyên.";

    public static Task<Category> RequireForCreateAsync(IApplicationDbContext db, Guid vendorId, Guid categoryId, CancellationToken ct)
        => RequireAsync(db, vendorId, categoryId, allowUnmappedLegacy: false, ct);

    public static Task<Category> RequireForExistingAsync(IApplicationDbContext db, Guid vendorId, Guid categoryId, CancellationToken ct)
        => RequireAsync(db, vendorId, categoryId, allowUnmappedLegacy: true, ct);

    private static async Task<Category> RequireAsync(IApplicationDbContext db, Guid vendorId, Guid categoryId, bool allowUnmappedLegacy, CancellationToken ct)
    {
        var category = await db.Categories.AsNoTracking().Include(category => category.BusinessGroup)
            .SingleOrDefaultAsync(category => category.Id == categoryId && category.IsActive, ct)
            ?? throw new DomainException("Ngành hàng được chọn không tồn tại hoặc đã ngừng hoạt động.");
        if (category.BusinessGroupId.HasValue && category.BusinessGroup?.IsActive != true)
            throw new DomainException("Nhóm kinh doanh của ngành dịch vụ đã ngừng hoạt động. Liên hệ Admin để rà soát.");
        // Adding a scope must not withdraw a different immutable category on a legacy shop's existing listing.
        // This exception is read/review only: creation never calls this branch and no membership is inferred.
        if (allowUnmappedLegacy && await db.Vendors.AnyAsync(vendor => vendor.Id == vendorId && vendor.CommissionRate > 0, ct) &&
            await db.Listings.AnyAsync(listing => listing.VendorId == vendorId && listing.CategoryId == categoryId, ct)) return category;
        var memberships = db.VendorBusinessGroups.AsNoTracking().Where(membership => membership.VendorId == vendorId);
        if (!await memberships.AnyAsync(ct))
        {
            throw new DomainException(LegacyGuidance);
        }
        if (!category.BusinessGroupId.HasValue || category.BusinessGroup?.IsActive != true ||
            !await memberships.AnyAsync(membership => membership.BusinessGroupId == category.BusinessGroupId, ct))
            throw new DomainException("Ngành dịch vụ không thuộc nhóm kinh doanh đang hoạt động của shop. Liên hệ Admin nếu cần bổ sung nhóm; việc bổ sung không tự thay đổi hoa hồng hiện hành.");
        return category;
    }
}
