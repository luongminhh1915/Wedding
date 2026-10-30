using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.CommissionPolicies;

public record CommissionPolicyRuleDto(
    Guid Id,
    CommissionRuleScope Scope,
    Guid? CategoryId,
    Guid? VendorId,
    Guid? ServiceItemId,
    Guid? ComboTemplateId,
    Guid? ComboTemplateRevisionId,
    decimal? Rate,
    CommissionBasisKind BasisKind,
    string PolicyVersion,
    string Reason,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    CommissionPolicyStatus Status,
    Guid CreatedById,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    Guid? ApprovedById,
    DateTime? ApprovedAt,
    string? RejectionReason,
    Guid Version);

internal static class CommissionPolicyAccess
{
    public static async Task<User> RequireSuperAdmin(IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (actor.UserId is null || actor.Role != nameof(UserRole.SuperAdmin))
            throw new DomainException("Chỉ SuperAdmin được quản lý chính sách hoa hồng.");
        return await db.Users.SingleOrDefaultAsync(user => user.Id == actor.UserId && user.IsActive && user.Role == UserRole.SuperAdmin, ct)
            ?? throw new DomainException("Tài khoản Admin không còn hoạt động hoặc đã đổi quyền.");
    }

    public static async Task RequireReader(IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (actor.UserId is null || actor.Role is not (nameof(UserRole.SuperAdmin) or nameof(UserRole.Finance)))
            throw new DomainException("Bạn không có quyền xem chính sách hoa hồng.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor.UserId && x.IsActive, ct);
        if (user == null || user.Role.ToString() != actor.Role || user.Role is not (UserRole.SuperAdmin or UserRole.Finance))
            throw new DomainException("Tài khoản không còn hoạt động hoặc đã đổi quyền.");
    }

    public static CommissionPolicyRuleDto Map(CommissionPolicyRule rule) => new(
        rule.Id, rule.Scope, rule.CategoryId, rule.VendorId, rule.ServiceItemId, rule.ComboTemplateId, rule.ComboTemplateRevisionId,
        rule.Rate, rule.BasisKind, rule.PolicyVersion, rule.Reason, rule.EffectiveFrom, rule.EffectiveTo,
        rule.Status, rule.CreatedById, rule.CreatedAt, rule.SubmittedAt, rule.ApprovedById, rule.ApprovedAt,
        rule.RejectionReason, rule.Version);

    public static async Task ValidateTarget(IApplicationDbContext db, CommissionPolicyRule rule, CancellationToken ct)
    {
        if (rule.CategoryId.HasValue)
        {
            var category = await db.Categories.Include(x => x.BusinessGroup)
                .SingleOrDefaultAsync(x => x.Id == rule.CategoryId.Value, ct);
            if (category is not { IsActive: true } || category.BusinessGroup is { IsActive: false })
                throw new DomainException("Danh mục đích không tồn tại hoặc không còn hoạt động.");
            CommissionComponentBasisAccess.ValidateBasisKind(category, rule.BasisKind);
        }
        if (rule.VendorId.HasValue)
        {
            var vendor = await db.Vendors.Include(x => x.User)
                .SingleOrDefaultAsync(x => x.Id == rule.VendorId.Value, ct);
            if (vendor is not { IsActive: true } || vendor.User is not { IsActive: true, Role: UserRole.VendorOwner })
                throw new DomainException("Nhà cung cấp đích không tồn tại hoặc không còn hoạt động.");
        }
        if (rule.ServiceItemId.HasValue)
        {
            var service = await db.ServiceItems.Include(x => x.Vendor).ThenInclude(x => x.User)
                .Include(x => x.Listing).ThenInclude(x => x.Category).ThenInclude(x => x!.BusinessGroup)
                .SingleOrDefaultAsync(x => x.Id == rule.ServiceItemId.Value, ct);
            if (service is not { IsActive: true } || service.ModerationStatus != "Approved" ||
                service.Listing is not { IsActive: true, Status: ListingStatus.Active } ||
                service.Listing.Category is not { IsActive: true } ||
                service.Listing.Category.BusinessGroup is { IsActive: false } ||
                service.Vendor is not { IsActive: true } ||
                service.Vendor.User is not { IsActive: true, Role: UserRole.VendorOwner })
                throw new DomainException("Dịch vụ đích phải còn hoạt động và đã được duyệt.");
            CommissionComponentBasisAccess.ValidateBasisKind(service.Listing.Category, rule.BasisKind);
        }
        if (rule.ComboTemplateId.HasValue)
        {
            var combo = await db.ComboTemplates.SingleOrDefaultAsync(x => x.Id == rule.ComboTemplateId.Value, ct);
            if (combo is not { IsActive: true, ModerationStatus: "Approved", PricingMode: "FixedPrice" })
                throw new DomainException("Combo đích phải còn hoạt động, đã duyệt và dùng giá cố định.");
            var revision = await db.ComboTemplateRevisions.SingleOrDefaultAsync(x =>
                x.Id == rule.ComboTemplateRevisionId && x.ComboTemplateId == combo.Id && x.Status == "Approved", ct);
            if (revision == null)
                throw new DomainException("Policy Combo phải gắn với đúng phiên bản ComboTemplate đã được duyệt.");
        }
    }
}

public record GetCommissionPolicyRulesQuery(CommissionPolicyStatus? Status = null) : IRequest<List<CommissionPolicyRuleDto>>;

public class GetCommissionPolicyRulesQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetCommissionPolicyRulesQuery, List<CommissionPolicyRuleDto>>
{
    public async Task<List<CommissionPolicyRuleDto>> Handle(GetCommissionPolicyRulesQuery request, CancellationToken ct)
    {
        await CommissionPolicyAccess.RequireReader(db, actor, ct);
        var query = db.CommissionPolicyRules.AsNoTracking();
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        return (await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct))
            .Select(CommissionPolicyAccess.Map).ToList();
    }
}

public record CreateCommissionPolicyRuleCommand(
    CommissionRuleScope Scope,
    Guid? CategoryId,
    Guid? VendorId,
    Guid? ServiceItemId,
    Guid? ComboTemplateId,
    Guid? ComboTemplateRevisionId,
    decimal? Rate,
    CommissionBasisKind BasisKind,
    string PolicyVersion,
    string Reason,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo) : IRequest<CommissionPolicyRuleDto>;

public class CreateCommissionPolicyRuleCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<CreateCommissionPolicyRuleCommand, CommissionPolicyRuleDto>
{
    public Task<CommissionPolicyRuleDto> Handle(CreateCommissionPolicyRuleCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var admin = await CommissionPolicyAccess.RequireSuperAdmin(db, actor, token);
            var rule = CommissionPolicyRule.Create(request.Scope, request.CategoryId, request.VendorId, request.ServiceItemId,
                request.ComboTemplateId, request.ComboTemplateRevisionId, request.Rate, request.BasisKind, request.PolicyVersion, request.Reason,
                request.EffectiveFrom, request.EffectiveTo, admin.Id);
            await CommissionPolicyAccess.ValidateTarget(db, rule, token);
            db.CommissionPolicyRules.Add(rule);
            db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(), "CommissionPolicy.Created",
                nameof(CommissionPolicyRule), rule.Id.ToString(), System.Text.Json.JsonSerializer.Serialize(new
                {
                    rule.Scope, rule.CategoryId, rule.VendorId, rule.ServiceItemId, rule.ComboTemplateId, rule.ComboTemplateRevisionId,
                    rule.Rate, rule.BasisKind, rule.PolicyVersion, rule.EffectiveFrom, rule.EffectiveTo
                })));
            await db.SaveChangesAsync(token);
            return CommissionPolicyAccess.Map(rule);
        }, ct);
}

public record SubmitCommissionPolicyRuleCommand(Guid Id, Guid ExpectedVersion) : IRequest<CommissionPolicyRuleDto>;

public class SubmitCommissionPolicyRuleCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<SubmitCommissionPolicyRuleCommand, CommissionPolicyRuleDto>
{
    public Task<CommissionPolicyRuleDto> Handle(SubmitCommissionPolicyRuleCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var admin = await CommissionPolicyAccess.RequireSuperAdmin(db, actor, token);
            var rule = await db.CommissionPolicyRules.SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Không tìm thấy chính sách hoa hồng.");
            if (rule.Version != request.ExpectedVersion) throw new DomainException("Chính sách đã thay đổi. Tải lại trước khi gửi duyệt.");
            await CommissionPolicyAccess.ValidateTarget(db, rule, token);
            rule.Submit();
            db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(), "CommissionPolicy.Submitted",
                nameof(CommissionPolicyRule), rule.Id.ToString()));
            await db.SaveChangesAsync(token);
            return CommissionPolicyAccess.Map(rule);
        }, ct);
}

public record ReviewCommissionPolicyRuleCommand(Guid Id, Guid ExpectedVersion, bool Approve, string? Reason)
    : IRequest<CommissionPolicyRuleDto>;

public class ReviewCommissionPolicyRuleCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<ReviewCommissionPolicyRuleCommand, CommissionPolicyRuleDto>
{
    public Task<CommissionPolicyRuleDto> Handle(ReviewCommissionPolicyRuleCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var admin = await CommissionPolicyAccess.RequireSuperAdmin(db, actor, token);
            var rule = await db.CommissionPolicyRules.SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Không tìm thấy chính sách hoa hồng.");
            if (rule.Version != request.ExpectedVersion) throw new DomainException("Chính sách đã thay đổi. Tải lại trước khi duyệt.");
            if (request.Approve)
            {
                await CommissionPolicyAccess.ValidateTarget(db, rule, token);
                if (rule.Scope == CommissionRuleScope.Combo)
                    await EnsureComboComponentsApproved(db, rule, token);
                await EnsureNoOverlappingPolicy(db, rule, token);
                rule.Approve(admin.Id);
            }
            else rule.Reject(admin.Id, request.Reason ?? "");
            db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(),
                request.Approve ? "CommissionPolicy.Approved" : "CommissionPolicy.Rejected",
                nameof(CommissionPolicyRule), rule.Id.ToString(), request.Approve ? null : request.Reason?.Trim()));
            await db.SaveChangesAsync(token);
            return CommissionPolicyAccess.Map(rule);
        }, ct);

    private static async Task EnsureNoOverlappingPolicy(IApplicationDbContext db, CommissionPolicyRule candidate, CancellationToken ct)
    {
        var peers = await db.CommissionPolicyRules.Where(x =>
            x.Status == CommissionPolicyStatus.Submitted || x.Status == CommissionPolicyStatus.Approved).ToListAsync(ct);
        if (peers.Any(peer => peer.Id != candidate.Id && peer.Overlaps(candidate)))
            throw new DomainException("Không thể duyệt: đã có chính sách cùng phạm vi/đối tượng với thời gian hiệu lực chồng lấn.");
    }

    private static async Task EnsureComboComponentsApproved(IApplicationDbContext db, CommissionPolicyRule rule, CancellationToken ct)
    {
        var revision = await db.ComboTemplateRevisions.AsNoTracking()
            .SingleAsync(x => x.Id == rule.ComboTemplateRevisionId, ct);
        var requiredServiceIds = CommissionComponentBasisAccess.RequiredServiceIds(revision.SnapshotJson);
        var approvals = await db.CommissionComponentBasisApprovals.AsNoTracking()
            .Where(x => x.ComboTemplateRevisionId == revision.Id && x.Status == CommissionPolicyStatus.Approved)
            .ToListAsync(ct);
        var missing = requiredServiceIds.Where(serviceId =>
            !approvals.Any(approval => approval.ServiceItemId == serviceId && CommissionComponentBasisAccess.Covers(approval, rule)))
            .ToList();
        if (missing.Count > 0)
            throw new DomainException($"Chưa thể duyệt policy Combo: thiếu basis/rate đã duyệt cho {missing.Count} thành phần hoặc lựa chọn trong đúng phiên bản.");
        foreach (var serviceId in requiredServiceIds)
        {
            var basis = approvals.First(approval => approval.ServiceItemId == serviceId &&
                CommissionComponentBasisAccess.Covers(approval, rule));
            await CommissionComponentBasisAccess.ValidateTarget(db, revision.Id, serviceId, basis.BasisKind, ct);
        }
    }
}
