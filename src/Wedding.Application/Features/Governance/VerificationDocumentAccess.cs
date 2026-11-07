using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Governance;

public static class VerificationDocumentAccess
{
    public static async Task<Vendor> RequireVendorAsync(IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (actor.UserId is not Guid id || actor.Role != nameof(UserRole.VendorOwner))
            throw new DomainException("Chỉ nhà cung cấp đang hoạt động được nộp giấy tờ xác minh.");
        return await db.Vendors.Include(v => v.User).FirstOrDefaultAsync(v => v.UserId == id && v.IsActive &&
            v.User.IsActive && v.User.Role == UserRole.VendorOwner, ct)
            ?? throw new DomainException("Nhà cung cấp không còn hoạt động hoặc phiên đăng nhập đã thay đổi.");
    }

    public static async Task<bool> CanReadAsync(IApplicationDbContext db, ICurrentUserService actor, Guid vendorId, CancellationToken ct)
    {
        if (actor.UserId is not Guid id) return false;
        if (actor.Role is nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin))
        {
            var role = actor.Role == nameof(UserRole.SuperAdmin) ? UserRole.SuperAdmin : UserRole.Moderator;
            return await db.Users.AnyAsync(u => u.Id == id && u.IsActive && u.Role == role, ct);
        }
        return actor.Role == nameof(UserRole.VendorOwner) && await db.Vendors.AnyAsync(v => v.Id == vendorId &&
            v.UserId == id && v.IsActive && v.User.IsActive && v.User.Role == UserRole.VendorOwner, ct);
    }

    public static string NormalizeReference(string reference, Guid vendorId, IVerificationDocumentStore store)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference.Length > 2048 || reference != reference.Trim())
            throw new DomainException("Đường dẫn tài liệu không hợp lệ.");
        if (reference.StartsWith("/uploads/documents/", StringComparison.Ordinal))
        {
            if (!store.IsOwnedExistingReference(vendorId, reference))
                throw new DomainException("Tài liệu chưa được lưu hoặc không thuộc nhà cung cấp này. Vui lòng tải lại.");
            return reference;
        }
        // Existing historical HTTPS references remain readable on their existing records.
        // New submissions cannot relabel somebody else's absolute upload URL as legacy evidence.
        throw new DomainException("Vui lòng tải tài liệu trực tiếp lên hồ sơ của bạn. Liên kết cũ được giữ trên hồ sơ đã nộp.");
    }
}
