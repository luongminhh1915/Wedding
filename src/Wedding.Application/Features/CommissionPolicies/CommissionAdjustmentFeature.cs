using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.CommissionPolicies;

public record CommissionAdjustmentDto(Guid Id, Guid ContractId, Guid CommissionId, Guid VendorId, Guid RequestedById,
    Guid CommissionVersionSnapshot, decimal OriginalRateSnapshot, decimal OriginalAmountSnapshot,
    decimal OutstandingAmountSnapshot, decimal ProposedRate, string Reason, CommissionAdjustmentStatus Status,
    Guid? DecidedById, DateTime? DecidedAt, string? DecisionReason, DateTime CreatedAt, Guid Version);

internal static class CommissionAdjustmentAccess
{
    public static CommissionAdjustmentDto Map(CommissionAdjustmentRequest request) => new(
        request.Id, request.ContractId, request.CommissionId, request.VendorId, request.RequestedById,
        request.CommissionVersionSnapshot, request.OriginalRateSnapshot, request.OriginalAmountSnapshot,
        request.OutstandingAmountSnapshot, request.ProposedRate, request.Reason, request.Status,
        request.DecidedById, request.DecidedAt, request.DecisionReason, request.CreatedAt, request.Version);

    public static async Task<(BookingContract Contract, Commission Commission)> LoadEligibleObligation(
        IApplicationDbContext db, Guid contractId, Guid commissionId, CancellationToken ct)
    {
        var contract = await db.BookingContracts.SingleOrDefaultAsync(x => x.Id == contractId, ct)
            ?? throw new NotFoundException("Không tìm thấy hợp đồng.");
        if (contract.Status == ContractStatus.Disputed)
            throw new DomainException("Hợp đồng đang tranh chấp; không thể xử lý điều chỉnh.");
        if (contract.Status is not (ContractStatus.Confirmed or ContractStatus.Completed))
            throw new DomainException("Chỉ hợp đồng đã xác nhận hoặc hoàn tất mới đủ điều kiện đề xuất điều chỉnh.");

        var commission = await db.Commissions.SingleOrDefaultAsync(x => x.Id == commissionId && x.ContractId == contractId, ct)
            ?? throw new DomainException("Khoản nghĩa vụ không thuộc hợp đồng đã chọn.");
        if (commission.OutstandingAmount <= 0 ||
            commission.Status is CommissionStatus.Frozen or CommissionStatus.EscrowLocked ||
            commission.Status is not (CommissionStatus.Pending or CommissionStatus.Unsettled or CommissionStatus.InSettlement or CommissionStatus.Overdue))
            throw new DomainException("Khoản nghĩa vụ không có dư nợ đủ điều kiện hoặc đang bị khóa.");
        if (commission.SettlementId.HasValue)
        {
            var settlement = await db.CommissionSettlements.SingleOrDefaultAsync(x => x.Id == commission.SettlementId.Value, ct);
            if (settlement == null || settlement.VendorId != commission.VendorId)
                throw new DomainException("Không xác minh được bảng kê gắn với khoản hoa hồng.");
            if (settlement.Status == SettlementStatus.InDispute)
                throw new DomainException("Bảng kê đang tranh chấp; không thể xử lý điều chỉnh.");
        }
        return (contract, commission);
    }
}

public record CreateCommissionAdjustmentCommand(Guid ContractId, Guid CommissionId, decimal ProposedRate, string Reason)
    : IRequest<CommissionAdjustmentDto>;

public class CreateCommissionAdjustmentCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<CreateCommissionAdjustmentCommand, CommissionAdjustmentDto>
{
    public Task<CommissionAdjustmentDto> Handle(CreateCommissionAdjustmentCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            if (actor.UserId is null || actor.Role != nameof(UserRole.VendorOwner))
                throw new DomainException("Chỉ chủ nhà cung cấp được đề xuất điều chỉnh hoa hồng.");
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == actor.UserId && x.IsActive && x.Role == UserRole.VendorOwner, token)
                ?? throw new DomainException("Tài khoản nhà cung cấp không còn hoạt động hoặc đã đổi quyền.");
            var vendor = await db.Vendors.SingleOrDefaultAsync(x => x.UserId == user.Id && x.IsActive, token)
                ?? throw new DomainException("Không tìm thấy hồ sơ nhà cung cấp đang hoạt động.");
            if (!await db.BookingContracts.AnyAsync(x => x.Id == request.ContractId && x.VendorId == vendor.Id, token))
                throw new DomainException("Bạn không có quyền đề xuất điều chỉnh cho hợp đồng này.");
            var (contract, commission) = await CommissionAdjustmentAccess.LoadEligibleObligation(db, request.ContractId, request.CommissionId, token);
            if (await db.CommissionAdjustmentRequests.AnyAsync(x =>
                    x.CommissionId == commission.Id && x.Status == CommissionAdjustmentStatus.Submitted, token))
                throw new DomainException("Khoản hoa hồng này đã có yêu cầu điều chỉnh đang chờ.");
            var adjustment = CommissionAdjustmentRequest.Create(contract, commission, user.Id, request.ProposedRate, request.Reason);
            db.CommissionAdjustmentRequests.Add(adjustment);
            db.AuditLogs.Add(AuditLog.Create(user.Id, user.Role.ToString(), "CommissionAdjustment.Submitted",
                nameof(CommissionAdjustmentRequest), adjustment.Id.ToString()));
            await db.SaveChangesAsync(token);
            return CommissionAdjustmentAccess.Map(adjustment);
        }, ct);
}

public record GetCommissionAdjustmentsQuery(CommissionAdjustmentStatus? Status = null) : IRequest<List<CommissionAdjustmentDto>>;

public class GetCommissionAdjustmentsQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetCommissionAdjustmentsQuery, List<CommissionAdjustmentDto>>
{
    public async Task<List<CommissionAdjustmentDto>> Handle(GetCommissionAdjustmentsQuery request, CancellationToken ct)
    {
        await CommissionPolicyAccess.RequireSuperAdmin(db, actor, ct);
        var query = db.CommissionAdjustmentRequests.AsNoTracking();
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        return (await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct))
            .Select(CommissionAdjustmentAccess.Map).ToList();
    }
}

public record DecideCommissionAdjustmentCommand(Guid Id, Guid ExpectedVersion, bool Approve, string Reason)
    : IRequest<CommissionAdjustmentDto>;

public class DecideCommissionAdjustmentCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<DecideCommissionAdjustmentCommand, CommissionAdjustmentDto>
{
    public Task<CommissionAdjustmentDto> Handle(DecideCommissionAdjustmentCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var admin = await CommissionPolicyAccess.RequireSuperAdmin(db, actor, token);
            var adjustment = await db.CommissionAdjustmentRequests.SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Không tìm thấy yêu cầu điều chỉnh hoa hồng.");
            if (adjustment.Version != request.ExpectedVersion)
                throw new DomainException("Yêu cầu đã thay đổi. Tải lại trước khi quyết định.");
            if (request.Approve)
            {
                var (_, commission) = await CommissionAdjustmentAccess.LoadEligibleObligation(db,
                    adjustment.ContractId, adjustment.CommissionId, token);
                if (commission.Version != adjustment.CommissionVersionSnapshot)
                    throw new DomainException("Khoản hoa hồng đã thay đổi sau khi gửi; yêu cầu cũ không thể được duyệt.");
                adjustment.Approve(admin.Id, request.Reason);
            }
            // Rejecting closes an obsolete request without authorizing any financial change.
            // A later payment, dispute or hold must not permanently block that final decision.
            else adjustment.Reject(admin.Id, request.Reason);
            db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(),
                request.Approve ? "CommissionAdjustment.Approved" : "CommissionAdjustment.Rejected",
                nameof(CommissionAdjustmentRequest), adjustment.Id.ToString(), request.Reason.Trim()));
            await db.SaveChangesAsync(token);
            return CommissionAdjustmentAccess.Map(adjustment);
        }, ct);
}
