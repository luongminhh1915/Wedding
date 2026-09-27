using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Enums;

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
    int PageSize = 12
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
        var query = _context.Listings
            .Include(l => l.Category)
            .Include(l => l.Vendor)
            .Include(l => l.Media)
            .Where(l => l.Status == ListingStatus.Active);

        // ── Lọc theo ngành hàng ──────────────────────────────────────────────
        if (request.CategoryId.HasValue)
            query = query.Where(l => l.CategoryId == request.CategoryId.Value);

        // ── Lọc theo khu vực ─────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(request.Location))
            query = query.Where(l => l.Location.Contains(request.Location));

        // ── Lọc theo khoảng giá ──────────────────────────────────────────────
        if (request.MinPrice.HasValue)
            query = query.Where(l => l.MaxPrice >= request.MinPrice.Value);
        if (request.MaxPrice.HasValue)
            query = query.Where(l => l.MinPrice <= request.MaxPrice.Value);

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
            "price_asc"  => query.OrderBy(l => l.MinPrice),
            "price_desc" => query.OrderByDescending(l => l.MinPrice),
            "popular"    => query.OrderByDescending(l => l.ViewCount),
            _            => query.OrderByDescending(l => l.CreatedAt) // newest
        };

        // ── Phân trang ───────────────────────────────────────────────────────
        var pageSize = Math.Clamp(request.PageSize, 1, 48);
        var page = Math.Max(1, request.Page);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(l => new ListingSummaryDto(
            l.Id,
            l.Title,
            l.Slug,
            l.MinPrice,
            l.MaxPrice,
            l.Location,
            l.Status.ToString(),
            l.RejectionReason,
            l.Category.Name,
            l.Vendor.BrandName,
            l.ViewCount,
            l.CreatedAt,
            l.Media.FirstOrDefault(m => m.IsFeatured)?.MediaUrl ?? l.Media.FirstOrDefault()?.MediaUrl
        )).ToList();

        return new PublicListingsResult(
            dtos,
            totalCount,
            page,
            pageSize,
            (int)Math.Ceiling((double)totalCount / pageSize)
        );
    }
}
