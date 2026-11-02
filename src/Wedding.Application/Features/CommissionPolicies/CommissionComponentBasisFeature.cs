using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.CommissionPolicies;

public record CommissionComponentBasisDto(Guid Id, Guid ComboTemplateRevisionId, Guid ServiceItemId, int Sequence,
    decimal Rate, CommissionBasisKind BasisKind, string BasisDefinition, string ExcludedCostsJson, string Reason,
    DateTime EffectiveFrom, DateTime? EffectiveTo, CommissionPolicyStatus Status, Guid CreatedById, DateTime CreatedAt,
    DateTime? SubmittedAt, Guid? ApprovedById, DateTime? ApprovedAt, string? RejectionReason, Guid Version);

internal static class CommissionComponentBasisAccess
{
    public static CommissionComponentBasisDto Map(CommissionComponentBasisApproval basis) => new(
        basis.Id, basis.ComboTemplateRevisionId, basis.ServiceItemId, basis.Sequence, basis.Rate, basis.BasisKind,
        basis.BasisDefinition, basis.ExcludedCostsJson, basis.Reason, basis.EffectiveFrom, basis.EffectiveTo,
        basis.Status, basis.CreatedById, basis.CreatedAt, basis.SubmittedAt, basis.ApprovedById, basis.ApprovedAt,
        basis.RejectionReason, basis.Version);

    public static HashSet<Guid> RequiredServiceIds(string snapshotJson)
    {
        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryProperty(document.RootElement, "Items", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new DomainException("Snapshot phiên bản combo không có danh sách thành phần hợp lệ.");
            var ids = new HashSet<Guid>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !TryReadGuid(item, "ServiceItemId", out var serviceId))
                    throw new DomainException("Snapshot phiên bản combo thiếu ID dịch vụ thành phần hợp lệ.");
                ids.Add(serviceId);
                if (TryProperty(item, "AllowedItemIds", out var allowed))
                {
                    if (allowed.ValueKind != JsonValueKind.Array)
                        throw new DomainException("Snapshot phiên bản combo có danh sách lựa chọn thay thế không hợp lệ.");
                    foreach (var choice in allowed.EnumerateArray())
                    {
                        if (choice.ValueKind != JsonValueKind.String || !Guid.TryParse(choice.GetString(), out var id) || id == Guid.Empty)
                            throw new DomainException("Snapshot phiên bản combo có ID lựa chọn thay thế không hợp lệ.");
                        ids.Add(id);
                    }
                }
            }
            if (ids.Count == 0) throw new DomainException("Snapshot combo không có dịch vụ thành phần để duyệt.");
            return ids;
        }
        catch (JsonException)
        {
            throw new DomainException("Snapshot phiên bản combo không hợp lệ.");
        }
    }

    private static bool TryProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        value = default;
        return false;
    }

    private static bool TryReadGuid(JsonElement element, string name, out Guid id)
    {
        id = Guid.Empty;
        return TryProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String &&
            Guid.TryParse(value.GetString(), out id) && id != Guid.Empty;
    }

    public static async Task ValidateTarget(IApplicationDbContext db, Guid revisionId, Guid serviceItemId,
        CommissionBasisKind basisKind, CancellationToken ct)
    {
        var revision = await db.ComboTemplateRevisions.Include(x => x.ComboTemplate)
            .SingleOrDefaultAsync(x => x.Id == revisionId && x.Status == "Approved", ct);
        if (revision?.ComboTemplate is not { IsActive: true, ModerationStatus: "Approved" } combo)
            throw new DomainException("Basis chỉ được gắn với phiên bản combo đã duyệt, đang hoạt động.");
        if (!RequiredServiceIds(revision.SnapshotJson).Contains(serviceItemId))
            throw new DomainException("Dịch vụ không thuộc cấu hình hoặc lựa chọn của phiên bản combo này.");
        var service = await db.ServiceItems.Include(x => x.Vendor).ThenInclude(x => x.User)
            .Include(x => x.Listing).ThenInclude(x => x.Category).ThenInclude(x => x!.BusinessGroup)
            .SingleOrDefaultAsync(x => x.Id == serviceItemId, ct);
        if (service is not { IsActive: true, ModerationStatus: "Approved" } ||
            service.Listing is not { IsActive: true, Status: ListingStatus.Active } ||
            service.Listing.Category is not { IsActive: true } ||
            service.Listing.Category.BusinessGroup is { IsActive: false } ||
            service.VendorId != combo.OwnerVendorId ||
            service.Vendor is not { IsActive: true, IsVerified: true, AllowInCombos: true } ||
            service.Vendor.User is not { IsActive: true, Role: UserRole.VendorOwner })
            throw new DomainException("Dịch vụ thành phần phải đang hoạt động, đã duyệt và thuộc đúng nhà cung cấp của combo.");
        ValidateBasisKind(service.Listing.Category, basisKind);
    }

    public static void ValidateBasisKind(Category category, CommissionBasisKind basisKind)
    {
        var requiresManagementFee = category.RequiresCommissionBasisApproval ||
            category.BusinessGroup?.RequiresCommissionBasisApproval == true;
        var requiredKind = requiresManagementFee ? CommissionBasisKind.AzManagementFee : CommissionBasisKind.StandardService;
        if (basisKind != requiredKind)
            throw new DomainException(requiresManagementFee
                ? "Dịch vụ tổ chức cưới trọn gói chỉ được dùng cơ sở phí quản lý/tổ chức đã được Admin xác nhận."
                : "Dịch vụ thông thường phải dùng cơ sở tính hoa hồng dịch vụ, không phải phí quản lý gói A–Z.");
    }

    public static bool Covers(CommissionComponentBasisApproval basis, CommissionPolicyRule rule) =>
        basis.Status == CommissionPolicyStatus.Approved && basis.EffectiveFrom <= rule.EffectiveFrom &&
        (rule.EffectiveTo.HasValue
            ? !basis.EffectiveTo.HasValue || basis.EffectiveTo.Value >= rule.EffectiveTo.Value
            : !basis.EffectiveTo.HasValue);
}

public record GetCommissionComponentBasisQuery(Guid RevisionId) : IRequest<List<CommissionComponentBasisDto>>;

public class GetCommissionComponentBasisQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetCommissionComponentBasisQuery, List<CommissionComponentBasisDto>>
{
    public async Task<List<CommissionComponentBasisDto>> Handle(GetCommissionComponentBasisQuery request, CancellationToken ct)
    {
        await CommissionPolicyAccess.RequireReader(db, actor, ct);
        return (await db.CommissionComponentBasisApprovals.AsNoTracking()
                .Where(x => x.ComboTemplateRevisionId == request.RevisionId)
                .OrderBy(x => x.ServiceItemId).ThenByDescending(x => x.Sequence).ToListAsync(ct))
            .Select(CommissionComponentBasisAccess.Map).ToList();
    }
}

public record CreateCommissionComponentBasisCommand(Guid RevisionId, Guid ServiceItemId, decimal Rate,
    CommissionBasisKind BasisKind, string BasisDefinition, string ExcludedCostsJson, string Reason,
    DateTime EffectiveFrom, DateTime? EffectiveTo) : IRequest<CommissionComponentBasisDto>;

public class CreateCommissionComponentBasisCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<CreateCommissionComponentBasisCommand, CommissionComponentBasisDto>
{
    public Task<CommissionComponentBasisDto> Handle(CreateCommissionComponentBasisCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var admin = await CommissionPolicyAccess.RequireSuperAdmin(db, actor, token);
            await CommissionComponentBasisAccess.ValidateTarget(db, request.RevisionId, request.ServiceItemId, request.BasisKind, token);
            var sequence = (await db.CommissionComponentBasisApprovals
                .Where(x => x.ComboTemplateRevisionId == request.RevisionId && x.ServiceItemId == request.ServiceItemId)
                .Select(x => (int?)x.Sequence).MaxAsync(token) ?? 0) + 1;
            var basis = CommissionComponentBasisApproval.Create(request.RevisionId, request.ServiceItemId, sequence,
                request.Rate, request.BasisKind, request.BasisDefinition, request.ExcludedCostsJson, request.Reason,
                request.EffectiveFrom, request.EffectiveTo, admin.Id);
            db.CommissionComponentBasisApprovals.Add(basis);
            db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(), "CommissionComponentBasis.Created",
                nameof(CommissionComponentBasisApproval), basis.Id.ToString()));
            await db.SaveChangesAsync(token);
            return CommissionComponentBasisAccess.Map(basis);
        }, ct);
}

public record SubmitCommissionComponentBasisCommand(Guid Id, Guid ExpectedVersion) : IRequest<CommissionComponentBasisDto>;

public class SubmitCommissionComponentBasisCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<SubmitCommissionComponentBasisCommand, CommissionComponentBasisDto>
{
    public Task<CommissionComponentBasisDto> Handle(SubmitCommissionComponentBasisCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var admin = await CommissionPolicyAccess.RequireSuperAdmin(db, actor, token);
            var basis = await db.CommissionComponentBasisApprovals.SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Không tìm thấy basis thành phần.");
            if (basis.Version != request.ExpectedVersion) throw new DomainException("Basis đã thay đổi. Tải lại trước khi gửi duyệt.");
            await CommissionComponentBasisAccess.ValidateTarget(db, basis.ComboTemplateRevisionId, basis.ServiceItemId, basis.BasisKind, token);
            basis.Submit();
            db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(), "CommissionComponentBasis.Submitted",
                nameof(CommissionComponentBasisApproval), basis.Id.ToString()));
            await db.SaveChangesAsync(token);
            return CommissionComponentBasisAccess.Map(basis);
        }, ct);
}

public record ReviewCommissionComponentBasisCommand(Guid Id, Guid ExpectedVersion, bool Approve, string? Reason)
    : IRequest<CommissionComponentBasisDto>;

public class ReviewCommissionComponentBasisCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<ReviewCommissionComponentBasisCommand, CommissionComponentBasisDto>
{
    public Task<CommissionComponentBasisDto> Handle(ReviewCommissionComponentBasisCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var admin = await CommissionPolicyAccess.RequireSuperAdmin(db, actor, token);
            var basis = await db.CommissionComponentBasisApprovals.SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Không tìm thấy basis thành phần.");
            if (basis.Version != request.ExpectedVersion) throw new DomainException("Basis đã thay đổi. Tải lại trước khi duyệt.");
            if (request.Approve)
            {
                await CommissionComponentBasisAccess.ValidateTarget(db, basis.ComboTemplateRevisionId, basis.ServiceItemId, basis.BasisKind, token);
                var peers = await db.CommissionComponentBasisApprovals.Where(x =>
                    x.ComboTemplateRevisionId == basis.ComboTemplateRevisionId && x.ServiceItemId == basis.ServiceItemId &&
                    (x.Status == CommissionPolicyStatus.Submitted || x.Status == CommissionPolicyStatus.Approved)).ToListAsync(token);
                if (peers.Any(peer => peer.Id != basis.Id && WindowsOverlap(peer, basis)))
                    throw new DomainException("Đã có basis thành phần cùng thời gian hiệu lực chồng lấn.");
                basis.Approve(admin.Id);
            }
            else basis.Reject(admin.Id, request.Reason ?? "");
            db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(),
                request.Approve ? "CommissionComponentBasis.Approved" : "CommissionComponentBasis.Rejected",
                nameof(CommissionComponentBasisApproval), basis.Id.ToString(), request.Approve ? null : request.Reason?.Trim()));
            await db.SaveChangesAsync(token);
            return CommissionComponentBasisAccess.Map(basis);
        }, ct);

    private static bool WindowsOverlap(CommissionComponentBasisApproval left, CommissionComponentBasisApproval right) =>
        (!left.EffectiveTo.HasValue || right.EffectiveFrom <= left.EffectiveTo.Value) &&
        (!right.EffectiveTo.HasValue || left.EffectiveFrom <= right.EffectiveTo.Value);
}
