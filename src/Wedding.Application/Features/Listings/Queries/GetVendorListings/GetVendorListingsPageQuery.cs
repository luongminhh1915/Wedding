using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Queries.GetVendorListings;

public record VendorListingsPageDto(List<ListingSummaryDto> Items, int TotalCount, int Page,
    int PageSize, int TotalPages);

public record GetVendorListingsPageQuery(string? Keyword = null, ListingStatus? Status = null,
    int Page = 1, int PageSize = 10, Guid? CategoryId = null) : IRequest<VendorListingsPageDto>;

/// <summary>Page a shop's own listings at the database, independently from the legacy array endpoint.</summary>
public class GetVendorListingsPageQueryHandler(IApplicationDbContext context, ICurrentUserService user)
    : IRequestHandler<GetVendorListingsPageQuery, VendorListingsPageDto>
{
    public async Task<VendorListingsPageDto> Handle(GetVendorListingsPageQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 50)
            throw new DomainException("Trang phải từ 1 và số dịch vụ mỗi trang từ 1 đến 50.");
        var offset = (long)(request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue)
            throw new DomainException("Trang yêu cầu vượt quá giới hạn phân trang. Hãy chọn lại trang.");
        if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
            throw new DomainException("Trạng thái dịch vụ không hợp lệ.");
        var keyword = (request.Keyword ?? string.Empty).Trim();
        if (keyword.Length > 200)
            throw new DomainException("Từ khóa tìm dịch vụ tối đa 200 ký tự.");
        if (!user.UserId.HasValue || user.UserId == Guid.Empty || user.Role != nameof(UserRole.VendorOwner))
            throw new DomainException("Chỉ nhà cung cấp được xem danh sách dịch vụ của shop mình.");

        // Do not trust a vendor ID, claimed role alone, or an inactive actor/shop.
        var vendorId = await context.Vendors.AsNoTracking()
            .Where(vendor => vendor.UserId == user.UserId && vendor.IsActive && vendor.User.IsActive &&
                vendor.User.Role == UserRole.VendorOwner)
            .Select(vendor => (Guid?)vendor.Id).SingleOrDefaultAsync(ct)
            ?? throw new DomainException("Hồ sơ nhà cung cấp đang hoạt động không tồn tại.");

        var query = context.Listings.AsNoTracking().Where(listing => listing.VendorId == vendorId);
        if (request.CategoryId.HasValue) query = query.Where(listing => listing.CategoryId == request.CategoryId);
        if (request.Status.HasValue)
            query = query.Where(listing => listing.Status == request.Status.Value);
        if (keyword.Length > 0)
        {
            var normalizedKeyword = keyword.ToLowerInvariant();
            query = query.Where(listing => listing.Title.ToLower().Contains(normalizedKeyword) ||
                listing.Category.Name.ToLower().Contains(normalizedKeyword) ||
                listing.Location.ToLower().Contains(normalizedKeyword));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(listing => context.ListingRevisions.Where(revision => revision.ListingId == listing.Id)
                .OrderByDescending(revision => revision.Sequence).Select(revision => (DateTime?)(revision.UpdatedAt ?? revision.CreatedAt)).FirstOrDefault()
                ?? listing.UpdatedAt ?? listing.CreatedAt)
            .ThenBy(listing => listing.Id).Skip((int)offset).Take(request.PageSize)
            .Select(listing => new ListingSummaryDto(listing.Id, listing.Title, listing.Slug,
                listing.MinPrice, listing.MaxPrice, listing.Location, listing.Status.ToString(),
                listing.RejectionReason, listing.Category.Name, listing.Vendor.BrandName,
                listing.ViewCount, listing.CreatedAt,
                listing.Media.OrderByDescending(media => media.IsFeatured).ThenBy(media => media.DisplayOrder)
                    .ThenBy(media => media.Id).Select(media => media.MediaUrl).FirstOrDefault(),
                context.ListingRevisions.Where(revision => revision.ListingId == listing.Id).OrderByDescending(revision => revision.Sequence)
                    .Select(revision => (DateTime?)(revision.UpdatedAt ?? revision.CreatedAt)).FirstOrDefault() ?? listing.UpdatedAt, listing.IsActive,
                context.ListingRevisions.Where(revision => revision.ListingId == listing.Id).OrderByDescending(revision => revision.Sequence).Select(revision => revision.Status).FirstOrDefault(),
                context.ListingRevisions.Where(revision => revision.ListingId == listing.Id).OrderByDescending(revision => revision.Sequence).Select(revision => (Guid?)revision.Id).FirstOrDefault(),
                context.ListingRevisions.Where(revision => revision.ListingId == listing.Id).OrderByDescending(revision => revision.Sequence).Select(revision => revision.ReviewNote).FirstOrDefault(),
                listing.CategoryId, listing.PricingServiceItemId,
                context.ServiceItems.Where(item => item.Id == listing.PricingServiceItemId && item.ListingId == listing.Id && item.VendorId == listing.VendorId && item.CategoryId == listing.CategoryId).Select(item => (decimal?)item.BasePrice).FirstOrDefault(),
                context.ServiceItems.Where(item => item.Id == listing.PricingServiceItemId && item.ListingId == listing.Id && item.VendorId == listing.VendorId && item.CategoryId == listing.CategoryId).Select(item => item.Unit).FirstOrDefault()))
            .ToListAsync(ct);
        return new(items, total, request.Page, request.PageSize,
            Math.Max(1, (int)Math.Ceiling(total / (double)request.PageSize)));
    }
}
