using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;

namespace Wedding.Application.Features.Planner;
public record OnboardingStatusDto(bool Completed, Guid? WeddingProfileId, string Role, DateTime? CompletedAt);
public record OnboardingStatusQuery : IRequest<OnboardingStatusDto>;
public class OnboardingStatusHandler(IApplicationDbContext db, ICurrentUserService user) : IRequestHandler<OnboardingStatusQuery, OnboardingStatusDto>
{
    public async Task<OnboardingStatusDto> Handle(OnboardingStatusQuery r, CancellationToken ct)
    {
        var scope = await WeddingAccess.ResolveAsync(db, user, ct);
        var completedAt = scope.ProfileId.HasValue ? await db.WeddingProfiles.AsNoTracking().Where(p => p.Id == scope.ProfileId).Select(p => p.OnboardingCompletedAt).SingleAsync(ct) : null;
        // An accepted, database-backed shared-wedding membership uses that existing wedding rather than creating a second private wizard.
        var sharedWedding = scope.ProfileId.HasValue && scope.Role is "Editor" or "Viewer";
        return new(completedAt.HasValue || sharedWedding, scope.ProfileId, scope.Role, completedAt);
    }
}
