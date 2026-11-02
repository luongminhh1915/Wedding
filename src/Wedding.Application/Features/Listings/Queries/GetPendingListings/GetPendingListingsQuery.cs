using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Listings.Queries.GetPendingListings;

public record GetPendingListingsQuery : IRequest<List<ListingDto>>;
public class GetPendingListingsQueryHandler(IApplicationDbContext db, ICurrentUserService actor) : IRequestHandler<GetPendingListingsQuery, List<ListingDto>>
{
    public async Task<List<ListingDto>> Handle(GetPendingListingsQuery request, CancellationToken ct)
    {
        await ListingPublication.RequireStaff(db, actor, ct);
        var pending = await db.ListingRevisions.AsNoTracking().Where(x => x.Status == "Pending")
            .OrderBy(x => x.SubmittedAt).ToListAsync(ct);
        var ids = pending.Select(x => x.ListingId).ToList();
        var listings = await db.Listings.AsNoTracking().Include(x => x.Category).Include(x => x.Vendor).Include(x => x.Media)
            .Where(x => x.Status == ListingStatus.PendingApproval || ids.Contains(x.Id)).OrderBy(x => x.UpdatedAt).ToListAsync(ct);
        return listings.Select(listing =>
        {
            var revision = pending.Where(x => x.ListingId == listing.Id).OrderByDescending(x => x.Sequence).FirstOrDefault();
            return revision == null ? ListingPublication.ToDto(listing) : ListingPublication.Preview(listing, revision);
        }).ToList();
    }
}
