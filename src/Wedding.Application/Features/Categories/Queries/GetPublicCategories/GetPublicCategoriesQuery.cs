using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;

namespace Wedding.Application.Features.Categories.Queries.GetPublicCategories;

public record CategoryDto(Guid Id, string Name, string Slug, int ListingCount, bool RequiresCommissionBasisApproval = false,
    Guid? BusinessGroupId = null, string? BusinessGroupName = null, decimal DefaultCommissionRate = 0, string? Icon = null);

public record GetPublicCategoriesQuery : IRequest<List<CategoryDto>>;

public class GetPublicCategoriesQueryHandler : IRequestHandler<GetPublicCategoriesQuery, List<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPublicCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CategoryDto>> Handle(GetPublicCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await _context.Categories.AsNoTracking()
            .Where(c => c.IsActive && (c.BusinessGroupId == null || c.BusinessGroup!.IsActive))
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Listings.Count(l => l.Status == Domain.Enums.ListingStatus.Active && l.IsActive && l.Vendor.IsActive && l.Vendor.User.IsActive && l.Vendor.User.Role == Domain.Enums.UserRole.VendorOwner),
                c.RequiresCommissionBasisApproval,
                c.BusinessGroupId,
                c.BusinessGroup != null ? c.BusinessGroup.Name : null,
                c.DefaultCommissionRate,
                c.Icon
            ))
            .ToListAsync(cancellationToken);
    }
}
