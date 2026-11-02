using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Planner;
public record UpdateWeddingBudgetCommand(decimal TotalBudget) : IRequest<decimal>;
public class UpdateWeddingBudgetHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<UpdateWeddingBudgetCommand, decimal>
{
    public async Task<decimal> Handle(UpdateWeddingBudgetCommand r, CancellationToken ct)
    {
        var scope = await WeddingAccess.ResolveAsync(db, user, ct, write: true);
        if (r.TotalBudget < 0 || r.TotalBudget > 100_000_000_000m) throw new DomainException("Ngân sách phải từ 0 đến 100 tỷ đồng.");
        var profile = await db.WeddingProfiles.FirstOrDefaultAsync(p => p.Id == scope.ProfileId, ct) ?? throw new DomainException("Hãy hoàn thành hồ sơ cưới để lưu tổng ngân sách.");
        profile.Update(profile.GroomName, profile.BrideName, profile.WeddingDate, profile.City, r.TotalBudget, profile.GuestCount, profile.Style, profile.PartnerEmail);
        await db.SaveChangesAsync(ct); return profile.TotalBudget;
    }
}
