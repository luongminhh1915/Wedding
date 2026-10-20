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
    private readonly ICurrentUserService _user;

    public GetListingDetailQueryHandler(IApplicationDbContext context, ICurrentUserService user)
    {
        _context = context;
        _user = user;
    }

    public async Task<ListingDto> Handle(GetListingDetailQuery request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .AsNoTracking()
            .VisibleTo(_user)
            .Include(l => l.Category)
            .Include(l => l.Vendor)
            .Include(l => l.Media.OrderBy(m => m.DisplayOrder))
            .FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken)
            ?? throw new NotFoundException("Bài đăng không tồn tại hoặc chưa được công khai.");

        var dto = new ListingDto(
            listing.Id,
            listing.Title,
            listing.Slug,
            listing.MinPrice,
            listing.MaxPrice,
            listing.Description,
            listing.Location,
            listing.Status.ToString(),
            ListingAccess.CanReadModerationDetails(listing, _user) ? listing.RejectionReason : null,
            listing.CategoryId,
            listing.Category.Name,
            listing.VendorId,
            listing.Vendor.BrandName,
            listing.ViewCount,
            listing.CreatedAt,
            listing.ModeratedAt,
            listing.Media.Select(m => new ListingMediaDto(m.Id, m.MediaUrl, m.ThumbnailUrl, m.IsFeatured, m.DisplayOrder)).ToList(),
            ListingServiceDetails.Read(listing.ServiceDetailsJson), listing.CancellationPolicy,
            listing.Category.RequiresCommissionBasisApproval, listing.IsActive
        );
        if (ListingAccess.CanReadModerationDetails(listing, _user))
        {
            await ListingPublication.RequirePrivateRead(_context, _user, listing, cancellationToken);
            dto = await ListingServicePricing.Enrich(_context, listing, dto, cancellationToken);
        }
        else if (listing.PricingServiceItemId.HasValue)
        {
            var pricing = await _context.ServiceItems.AsNoTracking().Include(item => item.Variants).Include(item => item.AddOns)
                .SingleOrDefaultAsync(item => item.Id == listing.PricingServiceItemId && item.ListingId == listing.Id &&
                    item.VendorId == listing.VendorId && item.CategoryId == listing.CategoryId && item.ModerationStatus == "Approved", cancellationToken);
            if (pricing != null) dto = dto with { Pricing = ListingServicePricing.Capture(pricing) with { SourceVersion = null }, PricingServiceItemId = pricing.Id };
        }
        return dto;
    }
}
