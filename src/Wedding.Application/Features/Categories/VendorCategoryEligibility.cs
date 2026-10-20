using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Categories.Queries.GetPublicCategories;
using Wedding.Application.Features.Listings;

namespace Wedding.Application.Features.Categories;

public record VendorCategoryEligibilityDto(List<CategoryDto> Categories, List<CategoryDto> ExistingCategories,
    bool RequiresMembershipReview, string? Guidance, List<Guid> BusinessGroupIds);
public record GetMyCategoriesQuery : IRequest<VendorCategoryEligibilityDto>;
public class GetMyCategoriesQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetMyCategoriesQuery, VendorCategoryEligibilityDto>
{
    public async Task<VendorCategoryEligibilityDto> Handle(GetMyCategoriesQuery request, CancellationToken ct)
    {
        var vendor = await ListingPublication.RequireOwner(db, actor, ct);
        var membershipIds = await db.VendorBusinessGroups.AsNoTracking().Where(membership => membership.VendorId == vendor.Id)
            .Select(membership => membership.BusinessGroupId).OrderBy(id => id).ToListAsync(ct);
        var activeIds = await db.BusinessGroups.AsNoTracking().Where(group => group.IsActive && membershipIds.Contains(group.Id))
            .Select(group => group.Id).ToListAsync(ct);
        var allowed = await db.Categories.AsNoTracking().Where(category => category.IsActive && category.BusinessGroupId.HasValue && activeIds.Contains(category.BusinessGroupId.Value))
            .OrderBy(category => category.DisplayOrder).ThenBy(category => category.Id)
            .Select(category => new CategoryDto(category.Id, category.Name, category.Slug, 0, category.RequiresCommissionBasisApproval,
                category.BusinessGroupId, category.BusinessGroup!.Name, category.DefaultCommissionRate, category.Icon)).ToListAsync(ct);
        var existing = await db.Categories.AsNoTracking().Where(category => db.Listings.Any(listing => listing.VendorId == vendor.Id && listing.CategoryId == category.Id))
            .OrderBy(category => category.DisplayOrder).ThenBy(category => category.Id)
            .Select(category => new CategoryDto(category.Id, category.Name, category.Slug, 0, category.RequiresCommissionBasisApproval,
                category.BusinessGroupId, category.BusinessGroup != null ? category.BusinessGroup.Name : null, category.DefaultCommissionRate, category.Icon)).ToListAsync(ct);
        var requiresReview = activeIds.Count == 0;
        return new(allowed, existing, requiresReview, membershipIds.Count == 0 ? ListingCategoryAccess.LegacyGuidance :
            requiresReview ? "Nhóm kinh doanh của shop đã ngừng hoạt động. Liên hệ Admin để rà soát; dịch vụ và giao dịch cũ không bị xóa." : null, membershipIds);
    }
}
