using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Planner;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Invitations.Commands;

public record ManageInvitationCommand(Guid Id, bool? Published, bool Delete = false) : IRequest<Unit>;
public class ManageInvitationHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<ManageInvitationCommand, Unit>
{
    public async Task<Unit> Handle(ManageInvitationCommand r, CancellationToken ct)
    {
        var scope = await WeddingAccess.ResolveAsync(db, user, ct, write: true, ownerOnly: r.Delete);
        var inv = await db.WeddingInvitations.FirstOrDefaultAsync(x => x.Id == r.Id && x.CustomerId == scope.OwnerId, ct) ?? throw new DomainException("Không tìm thấy thiệp.");
        if (r.Delete) db.WeddingInvitations.Remove(inv); else inv.SetPublished(r.Published ?? false);
        await db.SaveChangesAsync(ct); return Unit.Value;
    }
}
