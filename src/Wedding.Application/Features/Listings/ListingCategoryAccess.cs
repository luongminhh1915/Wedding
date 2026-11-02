using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings;

/// <summary>Services choose an active category independently of historical shop memberships.
/// A category selection never changes the shop's rate or a confirmed financial snapshot.</summary>
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
        // New services need a canonical active group so their category policy can be displayed.
        // Keep historical unmapped listings readable/reviewable for positive-rate legacy shops,
        // but never bypass an inactive group or infer a membership/rate from an old listing.
        if (!category.BusinessGroupId.HasValue && !(allowUnmappedLegacy &&
            await db.Vendors.AnyAsync(vendor => vendor.Id == vendorId && vendor.CommissionRate > 0, ct) &&
            await db.Listings.AnyAsync(listing => listing.VendorId == vendorId && listing.CategoryId == categoryId, ct)))
            throw new DomainException("Danh mục dịch vụ chưa có nhóm hoa hồng hợp lệ. Liên hệ Admin để rà soát danh mục; hệ thống không tự chọn nhóm hoặc tỷ lệ thay bạn.");
        return category;
    }
}
