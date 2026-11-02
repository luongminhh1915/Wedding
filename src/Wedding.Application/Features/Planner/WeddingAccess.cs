using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Planner;

public record WeddingScope(Guid OwnerId, Guid? ProfileId, string Role)
{
    public bool CanEdit => Role is "Owner" or "Editor";
}

/// <summary>All shared planner resources are scoped through this authorization boundary, never a client-supplied owner ID.</summary>
public static class WeddingAccess
{
    public static async Task<WeddingScope> ResolveAsync(IApplicationDbContext context, ICurrentUserService user, CancellationToken ct, bool write = false, bool ownerOnly = false)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var membership = await context.WeddingMembers.AsNoTracking().Where(x => x.UserId == uid).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        WeddingProfile? profile = membership == null ? null : await context.WeddingProfiles.FirstOrDefaultAsync(x => x.Id == membership.WeddingProfileId, ct);
        profile ??= await context.WeddingProfiles.FirstOrDefaultAsync(x => x.CustomerId == uid, ct);
        var role = profile == null || profile.CustomerId == uid ? "Owner" : membership!.Role;
        if ((ownerOnly && role != "Owner") || (write && role == "Viewer")) throw new DomainException("Bạn không có quyền chỉnh sửa hồ sơ cưới này.");
        return new WeddingScope(profile?.CustomerId ?? uid, profile?.Id, role);
    }
}
