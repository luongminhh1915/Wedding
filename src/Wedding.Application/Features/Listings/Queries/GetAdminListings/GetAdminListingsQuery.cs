using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Listings.Queries.GetAdminListings;

public record GetAdminListingsQuery(ListingStatus? Status = null) : IRequest<List<ListingDto>>;

public class GetAdminListingsQueryHandler : IRequestHandler<GetAdminListingsQuery, List<ListingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAdminListingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ListingDto>> Handle(GetAdminListingsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Listings
            .Include(l => l.Category)
            .Include(l => l.Vendor)
            .Include(l => l.Media)
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(l => l.Status == request.Status.Value);
        }

        var listings = await query
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        return listings.Select(l => new ListingDto(
            l.Id,
            l.Title,
            l.Slug,
            l.MinPrice,
            l.MaxPrice,
            l.Description,
            l.Location,
            l.Status.ToString(),
            l.RejectionReason,
            l.CategoryId,
            l.Category.Name,
            l.VendorId,
            l.Vendor.BrandName,
            l.ViewCount,
            l.CreatedAt,
            l.ModeratedAt,
            l.Media.Select(m => new ListingMediaDto(m.Id, m.MediaUrl, m.ThumbnailUrl, m.IsFeatured, m.DisplayOrder)).ToList()
        )).ToList();
    }
}
