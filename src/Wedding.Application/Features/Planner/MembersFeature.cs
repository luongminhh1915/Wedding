using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Planner;

public record WeddingMemberDto(Guid Id, Guid UserId, string Name, string Email, string Role);
public record WeddingMembersDto(string MyRole, List<WeddingMemberDto> Members);
public record GetWeddingMembersQuery : IRequest<WeddingMembersDto>;
public class GetWeddingMembersHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<GetWeddingMembersQuery, WeddingMembersDto>
{
    public async Task<WeddingMembersDto> Handle(GetWeddingMembersQuery r, CancellationToken ct)
    {
        var scope = await WeddingAccess.ResolveAsync(db, user, ct);
        var members = await db.WeddingMembers.AsNoTracking().Where(x => x.WeddingProfileId == scope.ProfileId)
            .Select(x => new WeddingMemberDto(x.Id, x.UserId, x.User.FullName, x.User.Email, x.Role)).ToListAsync(ct);
        var owner = await db.Users.AsNoTracking().FirstAsync(x => x.Id == scope.OwnerId, ct);
        members.Insert(0, new WeddingMemberDto(Guid.Empty, owner.Id, owner.FullName, owner.Email, "Owner"));
        return new(scope.Role, members);
    }
}
public record CreateWeddingJoinCommand(string Role = "Editor", string? Email = null) : IRequest<WeddingJoinDto>;
public record WeddingJoinDto(Guid Id, string Code, string Role, DateTime ExpiresAt);
public class CreateWeddingJoinHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<CreateWeddingJoinCommand, WeddingJoinDto>
{
    public async Task<WeddingJoinDto> Handle(CreateWeddingJoinCommand r, CancellationToken ct)
    {
        var scope = await WeddingAccess.ResolveAsync(db, user, ct, ownerOnly: true);
        if (scope.ProfileId == null) throw new DomainException("Vui lòng hoàn thành hồ sơ cưới trước khi mời.");
        var invitation = WeddingJoinInvitation.Create(scope.ProfileId.Value, r.Role, r.Email);
        db.WeddingJoinInvitations.Add(invitation); await db.SaveChangesAsync(ct);
        return new(invitation.Id, invitation.Code, invitation.Role, invitation.ExpiresAt);
    }
}
public record JoinWeddingCommand(string Code) : IRequest<WeddingProfileDto>;
public class JoinWeddingHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<JoinWeddingCommand, WeddingProfileDto>
{
    public Task<WeddingProfileDto> Handle(JoinWeddingCommand r, CancellationToken ct) => db.ExecuteSerializableAsync(inner => Join(r, inner), ct);
    private async Task<WeddingProfileDto> Join(JoinWeddingCommand r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var invite = await db.WeddingJoinInvitations.SingleOrDefaultAsync(x => x.Code == r.Code, ct)
            ?? throw new DomainException("Lời mời không hợp lệ.");
        if (invite.IsRevoked || invite.ExpiresAt <= DateTime.UtcNow) throw new DomainException("Lời mời đã được sử dụng hoặc hết hạn.");
        var profile = await db.WeddingProfiles.SingleAsync(x => x.Id == invite.WeddingProfileId, ct);
        var joining = await db.Users.SingleAsync(x => x.Id == uid, ct);
        if (invite.Email != null && invite.Email != joining.Email.ToLowerInvariant()) throw new DomainException("Lời mời dành cho email khác.");
        if (profile.CustomerId == uid) throw new DomainException("Bạn đã là chủ hồ sơ cưới này.");
        if (await db.WeddingMembers.AnyAsync(x => x.UserId == uid, ct)) throw new DomainException("Bạn đang tham gia một hồ sơ cưới. Hãy rời hồ sơ hiện tại trước.");
        db.WeddingMembers.Add(WeddingMember.Create(profile.Id, uid, invite.Role)); invite.Consume(uid);
        await db.SaveChangesAsync(ct); return PlannerMap.ToDto(profile);
    }
}
public record UpdateWeddingMemberCommand(Guid Id, string? Role) : IRequest<Unit>;
public class UpdateWeddingMemberHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<UpdateWeddingMemberCommand, Unit>
{
    public async Task<Unit> Handle(UpdateWeddingMemberCommand r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var scope = await WeddingAccess.ResolveAsync(db, user, ct);
        var member = await db.WeddingMembers.FirstOrDefaultAsync(x => x.Id == r.Id && x.WeddingProfileId == scope.ProfileId, ct)
            ?? throw new DomainException("Không tìm thấy thành viên.");
        if (scope.Role != "Owner" && !(member.UserId == uid && r.Role == null)) throw new DomainException("Chỉ chủ hồ sơ có quyền quản lý thành viên.");
        if (r.Role == null) db.WeddingMembers.Remove(member); else member.SetRole(r.Role);
        await db.SaveChangesAsync(ct); return Unit.Value;
    }
}
