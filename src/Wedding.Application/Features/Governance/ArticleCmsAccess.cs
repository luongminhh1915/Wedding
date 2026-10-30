using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Governance;

public static class ArticleCmsAccess
{
    public static async Task RequireAsync(IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (actor.UserId is not Guid id || actor.Role is not (nameof(UserRole.SuperAdmin) or nameof(UserRole.Moderator)))
            throw new DomainException("Chỉ quản trị viên hoặc kiểm duyệt viên đang hoạt động được quản lý cẩm nang.");
        var expectedRole = actor.Role == nameof(UserRole.SuperAdmin) ? UserRole.SuperAdmin : UserRole.Moderator;
        if (!await db.Users.AnyAsync(user => user.Id == id && user.IsActive && user.Role == expectedRole, ct))
            throw new DomainException("Chỉ quản trị viên hoặc kiểm duyệt viên đang hoạt động được quản lý cẩm nang.");
    }
}
