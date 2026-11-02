using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Queries.GetVendorListings;

/// <summary>
/// Query lấy danh sách bài đăng của chính NCC đang đăng nhập.
/// </summary>
public record GetVendorListingsQuery : IRequest<List<ListingSummaryDto>>;

public class GetVendorListingsQueryHandler : IRequestHandler<GetVendorListingsQuery, List<ListingSummaryDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetVendorListingsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ListingSummaryDto>> Handle(GetVendorListingsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Không xác định được người dùng.");

        var vendor = await ListingPublication.RequireOwner(_context, _currentUserService, cancellationToken);

        var listings = await _context.Listings
            .Include(l => l.Category)
            .Include(l => l.Vendor)
            .Include(l => l.Media)
            .Where(l => l.VendorId == vendor.Id)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        var ids = listings.Select(listing => listing.Id).ToList();
        var pricing = await _context.ServiceItems.AsNoTracking().Where(item => ids.Contains(item.ListingId) &&
            item.Listing.PricingServiceItemId == item.Id && item.VendorId == vendor.Id && item.CategoryId == item.Listing.CategoryId)
            .ToDictionaryAsync(item => item.ListingId, cancellationToken);

        return listings.Select(l => new ListingSummaryDto(
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
            l.Media.FirstOrDefault(m => m.IsFeatured)?.MediaUrl ?? l.Media.FirstOrDefault()?.MediaUrl,
            l.UpdatedAt, l.IsActive, CategoryId: l.CategoryId, PricingServiceItemId: l.PricingServiceItemId,
            BasePrice: pricing.GetValueOrDefault(l.Id)?.BasePrice, PriceUnit: pricing.GetValueOrDefault(l.Id)?.Unit
        )).ToList();
    }
}
