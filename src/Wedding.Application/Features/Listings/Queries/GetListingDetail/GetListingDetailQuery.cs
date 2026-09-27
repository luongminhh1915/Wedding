using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Queries.GetListingDetail;

/// <summary>
/// Query lấy chi tiết 1 bài đăng theo ID (dùng cho cả Vendor xem lại và Moderator xem trước khi duyệt).
/// </summary>
public record GetListingDetailQuery(Guid ListingId) : IRequest<ListingDto>;

public class GetListingDetailQueryHandler : IRequestHandler<GetListingDetailQuery, ListingDto>
{
    private readonly IApplicationDbContext _context;

    public GetListingDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ListingDto> Handle(GetListingDetailQuery request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .Include(l => l.Category)
            .Include(l => l.Vendor)
            .Include(l => l.Media.OrderBy(m => m.DisplayOrder))
            .FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken)
            ?? throw new NotFoundException("Bài đăng", request.ListingId);

        return new ListingDto(
            listing.Id,
            listing.Title,
            listing.Slug,
            listing.MinPrice,
            listing.MaxPrice,
            listing.Description,
            listing.Location,
            listing.Status.ToString(),
            listing.RejectionReason,
            listing.CategoryId,
            listing.Category.Name,
            listing.VendorId,
            listing.Vendor.BrandName,
            listing.ViewCount,
            listing.CreatedAt,
            listing.ModeratedAt,
            listing.Media.Select(m => new ListingMediaDto(m.Id, m.MediaUrl, m.ThumbnailUrl, m.IsFeatured, m.DisplayOrder)).ToList()
        );
    }
}
