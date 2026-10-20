using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.BusinessGroups;

public record BusinessGroupDto(Guid Id, string Code, string Name, string Description,
    decimal DefaultCommissionRate, bool RequiresCommissionBasisApproval, string PolicyVersion);

public record VendorBusinessGroupsDto(List<BusinessGroupDto> Groups, bool UsesLegacyCommissionRate,
    decimal? LegacyCommissionRate, DateTime? PolicyAcceptedAt, string? PolicyVersion);

internal static class BusinessGroupMap
{
    public static BusinessGroupDto ToDto(BusinessGroup group) => new(group.Id, group.Code, group.Name,
        group.Description, group.DefaultCommissionRate, group.RequiresCommissionBasisApproval, group.PolicyVersion);
}

public record GetBusinessGroupsQuery : IRequest<List<BusinessGroupDto>>;

public class GetBusinessGroupsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetBusinessGroupsQuery, List<BusinessGroupDto>>
{
    public async Task<List<BusinessGroupDto>> Handle(GetBusinessGroupsQuery request, CancellationToken ct)
        => (await context.BusinessGroups.AsNoTracking().Where(group => group.IsActive)
            .OrderBy(group => group.DisplayOrder).ThenBy(group => group.Id).ToListAsync(ct))
            .Select(BusinessGroupMap.ToDto).ToList();
}

public record GetMyBusinessGroupsQuery : IRequest<VendorBusinessGroupsDto>;

public class GetMyBusinessGroupsQueryHandler(IApplicationDbContext context, ICurrentUserService user)
    : IRequestHandler<GetMyBusinessGroupsQuery, VendorBusinessGroupsDto>
{
    public async Task<VendorBusinessGroupsDto> Handle(GetMyBusinessGroupsQuery request, CancellationToken ct)
    {
        if (!user.UserId.HasValue || user.UserId == Guid.Empty || user.Role != nameof(UserRole.VendorOwner))
            throw new DomainException("Chỉ nhà cung cấp được xem chính sách của shop mình.");
        var vendor = await context.Vendors.AsNoTracking()
            .Include(vendor => vendor.BusinessGroups).ThenInclude(membership => membership.BusinessGroup)
            .FirstOrDefaultAsync(vendor => vendor.UserId == user.UserId && vendor.IsActive &&
                vendor.User.IsActive && vendor.User.Role == UserRole.VendorOwner, ct)
            ?? throw new NotFoundException("Hồ sơ nhà cung cấp đang hoạt động không tồn tại.");
        var groups = vendor.BusinessGroups.Select(membership => membership.BusinessGroup)
            .OrderBy(group => group.DisplayOrder).ThenBy(group => group.Id).Select(BusinessGroupMap.ToDto).ToList();
        var usesLegacyRate = vendor.CommissionRate != 0m;
        return new(groups, usesLegacyRate, usesLegacyRate ? vendor.CommissionRate : null,
            vendor.PartnershipPolicyAcceptedAt, vendor.PartnershipPolicyVersion);
    }
}
