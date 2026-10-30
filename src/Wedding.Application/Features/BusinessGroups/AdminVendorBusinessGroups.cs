using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.BusinessGroups;

public record GetVendorBusinessGroupsQuery(Guid VendorId) : IRequest<VendorBusinessGroupsDto>;
public record AssignVendorBusinessGroupsCommand(Guid VendorId, List<Guid>? BusinessGroupIds, string? Reason) : IRequest<VendorBusinessGroupsDto>;

internal static class AdminBusinessGroupAccess
{
    public static async Task<User> RequireAdmin(IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (actor.UserId is null || actor.Role != "SuperAdmin") throw new DomainException("Chỉ Admin được xác nhận nhóm kinh doanh của nhà cung cấp.");
        return await db.Users.SingleOrDefaultAsync(user => user.Id == actor.UserId && user.IsActive && user.Role == UserRole.SuperAdmin, ct)
            ?? throw new DomainException("Tài khoản Admin không còn hoạt động hoặc đã đổi quyền.");
    }
    public static async Task<VendorBusinessGroupsDto> Read(IApplicationDbContext db, Guid vendorId, CancellationToken ct)
    {
        var vendor = await db.Vendors.AsNoTracking().SingleOrDefaultAsync(vendor => vendor.Id == vendorId, ct)
            ?? throw new NotFoundException("Nhà cung cấp không tồn tại.");
        var groups = await db.VendorBusinessGroups.AsNoTracking().Where(membership => membership.VendorId == vendorId)
            .Select(membership => membership.BusinessGroup).OrderBy(group => group.DisplayOrder).ThenBy(group => group.Id).ToListAsync(ct);
        return new(groups.Select(BusinessGroupMap.ToDto).ToList(), vendor.CommissionRate != 0,
            vendor.CommissionRate != 0 ? vendor.CommissionRate : null, vendor.PartnershipPolicyAcceptedAt, vendor.PartnershipPolicyVersion);
    }
}
public class GetVendorBusinessGroupsQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetVendorBusinessGroupsQuery, VendorBusinessGroupsDto>
{
    public async Task<VendorBusinessGroupsDto> Handle(GetVendorBusinessGroupsQuery request, CancellationToken ct)
    {
        await AdminBusinessGroupAccess.RequireAdmin(db, actor, ct);
        return await AdminBusinessGroupAccess.Read(db, request.VendorId, ct);
    }
}
public class AssignVendorBusinessGroupsCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<AssignVendorBusinessGroupsCommand, VendorBusinessGroupsDto>
{
    public Task<VendorBusinessGroupsDto> Handle(AssignVendorBusinessGroupsCommand request, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var admin = await AdminBusinessGroupAccess.RequireAdmin(db, actor, token);
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length is < 1 or > 2000) throw new DomainException("Cần ghi lý do xác nhận nhóm kinh doanh, tối đa 2000 ký tự.");
        if (request.BusinessGroupIds is not { Count: >= 1 and <= 20 } || request.BusinessGroupIds.Any(id => id == Guid.Empty) ||
            request.BusinessGroupIds.Distinct().Count() != request.BusinessGroupIds.Count)
            throw new DomainException("Chọn ít nhất một nhóm kinh doanh hợp lệ, không trùng nhóm.");
        var vendor = await db.Vendors.Include(vendor => vendor.User).SingleOrDefaultAsync(vendor => vendor.Id == request.VendorId, token)
            ?? throw new NotFoundException("Nhà cung cấp không tồn tại.");
        if (!vendor.IsActive || !vendor.User.IsActive || vendor.User.Role != UserRole.VendorOwner)
            throw new DomainException("Chỉ xác nhận nhóm cho shop và chủ shop đang hoạt động.");
        var groups = await db.BusinessGroups.Where(group => request.BusinessGroupIds.Contains(group.Id) && group.IsActive).ToListAsync(token);
        if (groups.Count != request.BusinessGroupIds.Count) throw new DomainException("Có nhóm không tồn tại hoặc đã ngừng hoạt động.");
        var original = await db.VendorBusinessGroups.Where(membership => membership.VendorId == vendor.Id)
            .Select(membership => membership.BusinessGroupId).ToListAsync(token);
        var added = request.BusinessGroupIds.Except(original).OrderBy(id => id).ToList();
        // Add-only scope confirmation. This is NOT a financial-policy approval or the Vendor's own acknowledgement.
        foreach (var groupId in added) db.VendorBusinessGroups.Add(VendorBusinessGroup.Create(vendor.Id, groupId));
        if (added.Count > 0)
        {
            db.AuditLogs.Add(AuditLog.Create(admin.Id, "SuperAdmin", "Vendor.BusinessGroupsAssigned", nameof(Vendor), vendor.Id.ToString(),
                JsonSerializer.Serialize(new { Reason = reason, PreviousGroupIds = original.OrderBy(id => id), AddedGroupIds = added,
                    CommissionRateUnchanged = vendor.CommissionRate, vendor.PartnershipPolicyAcceptedAt, vendor.PartnershipPolicyVersion })));
            await db.SaveChangesAsync(token);
        }
        return await AdminBusinessGroupAccess.Read(db, vendor.Id, token);
    }, ct);
}
