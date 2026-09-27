using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Listings.Queries.GetPendingListings;

/// <summary>
/// Query lấy danh sách bài đăng đang chờ kiểm duyệt — chỉ dành cho Moderator (BR-008).
/// </summary>
public record GetPendingListingsQuery : IRequest<List<ListingDto>>;

public class GetPendingListingsQueryHandler : IRequestHandler<GetPendingListingsQuery, List<ListingDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPendingListingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ListingDto>> Handle(GetPendingListingsQuery request, CancellationToken cancellationToken)
    {
        var listings = await _context.Listings
            .Include(l => l.Category)
            .Include(l => l.Vendor)
            .Include(l => l.Media)
            .Where(l => l.Status == ListingStatus.PendingApproval)
            .OrderBy(l => l.UpdatedAt) // SLA 24h: ưu tiên bài nộp sớm nhất
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
