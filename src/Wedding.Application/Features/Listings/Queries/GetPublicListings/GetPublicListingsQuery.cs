using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Queries.GetPublicListings;

/// <summary>
/// Query công khai lấy danh sách gói dịch vụ Active kèm bộ lọc.
/// Không yêu cầu xác thực — Customer không cần đăng nhập để xem.
/// </summary>
public record GetPublicListingsQuery(
    Guid? CategoryId,
    string? Location,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Keyword,
    string SortBy = "newest",  // newest | price_asc | price_desc | popular
    int Page = 1,
    int PageSize = 12,
    decimal? MinRating = null
) : IRequest<PublicListingsResult>;

public record PublicListingsResult(
    List<ListingSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public class GetPublicListingsQueryHandler : IRequestHandler<GetPublicListingsQuery, PublicListingsResult>
{
    private readonly IApplicationDbContext _context;

    public GetPublicListingsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PublicListingsResult> Handle(GetPublicListingsQuery request, CancellationToken cancellationToken)
    {
        if (request.MinPrice < 0 || request.MaxPrice < 0 ||
            request.MinPrice.HasValue && request.MaxPrice.HasValue && request.MinPrice > request.MaxPrice)
            throw new DomainException("Khoảng giá tìm kiếm không hợp lệ.");
        if (request.MinRating is < 0 or > 5)
            throw new DomainException("Điểm đánh giá cần nằm trong khoảng 0–5.");
        if (request.Keyword?.Length > 200 || request.Location?.Length > 200)
            throw new DomainException("Từ khóa và khu vực tìm kiếm tối đa 200 ký tự.");
        var query = _context.Listings
            .AsNoTracking()
            .Include(l => l.Category)
            .Include(l => l.Vendor)
            .Include(l => l.Media)
            .PubliclyVisible();

        // ── Lọc theo ngành hàng ──────────────────────────────────────────────
        if (request.CategoryId.HasValue)
            query = query.Where(l => l.CategoryId == request.CategoryId.Value);

        // ── Lọc theo khu vực ─────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(request.Location))
            query = query.Where(l => l.Location.Contains(request.Location.Trim()));

        // This is the shop's existing rating, not an invented service/combo score.
        if (request.MinRating.HasValue && request.MinRating > 0)
            query = query.Where(l => l.Vendor.TotalReviews >= 3 && l.Vendor.RatingAvg >= request.MinRating.Value);

        // ── Lọc theo khoảng giá ──────────────────────────────────────────────
        if (request.MinPrice.HasValue)
            query = query.Where(l => (_context.ServiceItems.Where(item => item.Id == l.PricingServiceItemId && item.ListingId == l.Id &&
                item.VendorId == l.VendorId && item.CategoryId == l.CategoryId && item.ModerationStatus == "Approved").Select(item => (decimal?)item.BasePrice).FirstOrDefault() ?? l.MaxPrice) >= request.MinPrice.Value);
        if (request.MaxPrice.HasValue)
            query = query.Where(l => (_context.ServiceItems.Where(item => item.Id == l.PricingServiceItemId && item.ListingId == l.Id &&
                item.VendorId == l.VendorId && item.CategoryId == l.CategoryId && item.ModerationStatus == "Approved").Select(item => (decimal?)item.BasePrice).FirstOrDefault() ?? l.MinPrice) <= request.MaxPrice.Value);

        // ── Tìm kiếm theo từ khóa ────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim().ToLower();
            query = query.Where(l =>
                l.Title.ToLower().Contains(kw) ||
                l.Vendor.BrandName.ToLower().Contains(kw) ||
                l.Location.ToLower().Contains(kw));
        }

        // ── Tổng số kết quả ──────────────────────────────────────────────────
        var totalCount = await query.CountAsync(cancellationToken);

        // ── Sắp xếp ──────────────────────────────────────────────────────────
        query = request.SortBy switch
        {
            "price_asc"  => query.OrderBy(l => _context.ServiceItems.Where(item => item.Id == l.PricingServiceItemId && item.ListingId == l.Id &&
                item.VendorId == l.VendorId && item.CategoryId == l.CategoryId && item.ModerationStatus == "Approved").Select(item => (decimal?)item.BasePrice).FirstOrDefault() ?? l.MinPrice).ThenBy(l => l.Id),
            "price_desc" => query.OrderByDescending(l => _context.ServiceItems.Where(item => item.Id == l.PricingServiceItemId && item.ListingId == l.Id &&
                item.VendorId == l.VendorId && item.CategoryId == l.CategoryId && item.ModerationStatus == "Approved").Select(item => (decimal?)item.BasePrice).FirstOrDefault() ?? l.MinPrice).ThenBy(l => l.Id),
            "popular"    => query.OrderByDescending(l => l.ViewCount).ThenBy(l => l.Id),
            _            => query.OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id) // newest
        };

        // ── Phân trang ───────────────────────────────────────────────────────
        var pageSize = Math.Clamp(request.PageSize, 1, 48);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var page = Math.Clamp(request.Page, 1, Math.Max(1, totalPages));
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = items.Select(listing => listing.Id).ToList();
        var pricing = await _context.ServiceItems.AsNoTracking().Where(item => ids.Contains(item.ListingId) &&
            item.Listing.PricingServiceItemId == item.Id && item.VendorId == item.Listing.VendorId && item.CategoryId == item.Listing.CategoryId &&
            item.ModerationStatus == "Approved").ToDictionaryAsync(item => item.ListingId, cancellationToken);

        var dtos = items.Select(l => new ListingSummaryDto(
            l.Id,
            l.Title,
            l.Slug,
            l.MinPrice,
            l.MaxPrice,
            l.Location,
            l.Status.ToString(),
            null, // Moderation notes are not public, even when an approved listing kept an old note.
            l.Category.Name,
            l.Vendor.BrandName,
            l.ViewCount,
            l.CreatedAt,
            l.Media.FirstOrDefault(m => m.IsFeatured)?.MediaUrl ?? l.Media.FirstOrDefault()?.MediaUrl,
            CategoryId: l.CategoryId, PricingServiceItemId: pricing.GetValueOrDefault(l.Id)?.Id,
            BasePrice: pricing.GetValueOrDefault(l.Id)?.BasePrice, PriceUnit: pricing.GetValueOrDefault(l.Id)?.Unit
        )).ToList();

        return new PublicListingsResult(
            dtos,
            totalCount,
            page,
            pageSize,
            totalPages
        );
    }
}
