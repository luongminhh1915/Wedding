using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.CommissionNegotiations;

public record CommissionNegotiationMessageDto(
    Guid Id,
    Guid NegotiationRequestId,
    Guid SenderId,
    string SenderName,
    string SenderRole,
    string Message,
    decimal? ProposedRateSnapshot,
    string? AttachmentUrl,
    DateTime CreatedAt);

public record CommissionNegotiationDto(
    Guid Id,
    Guid VendorId,
    string VendorName,
    CommissionRuleScope Scope,
    Guid? CategoryId,
    string? CategoryName,
    Guid? ServiceItemId,
    string? ServiceItemName,
    decimal CurrentRate,
    decimal ProposedRate,
    decimal? CounterRate,
    decimal? ApprovedRate,
    NegotiationReasonCategory ReasonCategory,
    string ReasonCategoryName,
    string ReasonDetail,
    string? CooperationPlan,
    NegotiationStatus Status,
    Guid CreatedById,
    string CreatedByName,
    Guid? AssignedModeratorId,
    string? AssignedModeratorName,
    Guid? DecidedById,
    string? DecidedByName,
    DateTime? DecidedAt,
    string? DecisionReason,
    Guid? ResultingPolicyRuleId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid Version,
    List<CommissionNegotiationMessageDto> Messages);

internal static class CommissionNegotiationAccess
{
    public static string DisplayReasonCategory(NegotiationReasonCategory category) => category switch
    {
        NegotiationReasonCategory.NewPartner => "Đối tác mới gia nhập nền tảng",
        NegotiationReasonCategory.HighValueOrder => "Đơn hàng / Gói dịch vụ giá trị cao",
        NegotiationReasonCategory.VolumeGrowth => "Cam kết doanh số hoặc số lượng hợp đồng lớn",
        NegotiationReasonCategory.MarginCost => "Biên lợi nhuận thấp / Chi phí đầu vào cao",
        NegotiationReasonCategory.StrategicLongTerm => "Hợp tác chiến lược dài hạn",
        NegotiationReasonCategory.Other => "Lý do kinh doanh đặc thù khác",
        _ => category.ToString()
    };

    public static CommissionNegotiationDto Map(CommissionNegotiationRequest req) => new(
        req.Id,
        req.VendorId,
        req.Vendor?.BrandName ?? string.Empty,
        req.Scope,
        req.CategoryId,
        req.Category?.Name,
        req.ServiceItemId,
        req.ServiceItem?.Name,
        req.CurrentRate,
        req.ProposedRate,
        req.CounterRate,
        req.ApprovedRate,
        req.ReasonCategory,
        DisplayReasonCategory(req.ReasonCategory),
        req.ReasonDetail,
        req.CooperationPlan,
        req.Status,
        req.CreatedById,
        req.CreatedBy?.FullName ?? req.CreatedBy?.Email ?? string.Empty,
        req.AssignedModeratorId,
        req.AssignedModerator?.FullName ?? req.AssignedModerator?.Email,
        req.DecidedById,
        req.DecidedBy?.FullName ?? req.DecidedBy?.Email,
        req.DecidedAt,
        req.DecisionReason,
        req.ResultingPolicyRuleId,
        req.CreatedAt,
        req.UpdatedAt,
        req.Version,
        req.Messages.OrderBy(m => m.CreatedAt).Select(m => new CommissionNegotiationMessageDto(
            m.Id,
            m.NegotiationRequestId,
            m.SenderId,
            m.Sender?.FullName ?? m.Sender?.Email ?? m.SenderRole,
            m.SenderRole,
            m.Message,
            m.ProposedRateSnapshot,
            m.AttachmentUrl,
            m.CreatedAt)).ToList()
    );

    public static async Task<(User User, Vendor Vendor)> RequireVendorOwner(
        IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (actor.UserId is null || actor.Role != nameof(UserRole.VendorOwner))
            throw new DomainException("Chỉ chủ nhà cung cấp (VendorOwner) mới có quyền thực hiện thao tác này.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == actor.UserId && x.IsActive && x.Role == UserRole.VendorOwner, ct)
            ?? throw new DomainException("Tài khoản nhà cung cấp không còn hoạt động.");
        var vendor = await db.Vendors.SingleOrDefaultAsync(x => x.UserId == user.Id && x.IsActive, ct)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại hoặc đã ngừng hoạt động.");
        return (user, vendor);
    }

    public static async Task<User> RequireStaff(
        IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (actor.UserId is null || actor.Role is not (nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin) or nameof(UserRole.Finance)))
            throw new DomainException("Chỉ nhân viên Ban quản trị (Moderator / SuperAdmin / Finance) mới có quyền thực hiện.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == actor.UserId && x.IsActive &&
            (x.Role == UserRole.Moderator || x.Role == UserRole.SuperAdmin || x.Role == UserRole.Finance), ct)
            ?? throw new DomainException("Tài khoản quản trị không tồn tại hoặc đã ngừng hoạt động.");
        if (actor.Role != user.Role.ToString()) throw new DomainException("Vai trò quản trị đã thay đổi. Vui lòng đăng nhập lại.");
        return user;
    }

    public static async Task<User> RequireSuperAdmin(
        IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (actor.UserId is null || actor.Role != nameof(UserRole.SuperAdmin))
            throw new DomainException("Chỉ SuperAdmin mới có quyền ra quyết định cuối cùng cho đàm phán hoa hồng.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == actor.UserId && x.IsActive && x.Role == UserRole.SuperAdmin, ct)
            ?? throw new DomainException("Tài khoản SuperAdmin không tồn tại hoặc đã ngừng hoạt động.");
        return user;
    }

    public static IQueryable<CommissionNegotiationRequest> IncludeDetails(IQueryable<CommissionNegotiationRequest> query) =>
        query.Include(x => x.Vendor)
             .Include(x => x.Category)
             .Include(x => x.ServiceItem)
             .Include(x => x.CreatedBy)
             .Include(x => x.AssignedModerator)
             .Include(x => x.DecidedBy)
             .Include(x => x.Messages).ThenInclude(m => m.Sender);
}

// ── 1. Create Negotiation Request ─────────────────────────────────────────────
public record CreateCommissionNegotiationCommand(
    CommissionRuleScope Scope,
    Guid? CategoryId,
    Guid? ServiceItemId,
    decimal ProposedRate,
    NegotiationReasonCategory ReasonCategory,
    string ReasonDetail,
    string? CooperationPlan) : IRequest<CommissionNegotiationDto>;

public class CreateCommissionNegotiationCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<CreateCommissionNegotiationCommand, CommissionNegotiationDto>
{
    public Task<CommissionNegotiationDto> Handle(CreateCommissionNegotiationCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var (user, vendor) = await CommissionNegotiationAccess.RequireVendorOwner(db, actor, token);

            if (request.Scope == CommissionRuleScope.VendorCategory)
            {
                if (!request.CategoryId.HasValue) throw new DomainException("Cần chọn ngành hàng cho đàm phán cấp ngành hàng.");
                if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId.Value && c.IsActive, token))
                    throw new DomainException("Ngành hàng không tồn tại hoặc đã ngừng hoạt động.");
            }
            else if (request.Scope == CommissionRuleScope.Service)
            {
                if (!request.ServiceItemId.HasValue) throw new DomainException("Cần chọn dịch vụ cho đàm phán cấp dịch vụ.");
                var serviceItem = await db.ServiceItems.SingleOrDefaultAsync(s => s.Id == request.ServiceItemId.Value && s.VendorId == vendor.Id && s.IsActive, token)
                    ?? throw new DomainException("Dịch vụ không tồn tại hoặc không thuộc quyền sở hữu của shop.");
                request = request with { CategoryId = serviceItem.CategoryId };
            }

            // Check duplicate active negotiation for same target
            var duplicate = await db.CommissionNegotiationRequests.AnyAsync(x =>
                x.VendorId == vendor.Id &&
                x.Scope == request.Scope &&
                x.CategoryId == request.CategoryId &&
                x.ServiceItemId == request.ServiceItemId &&
                (x.Status == NegotiationStatus.Submitted || x.Status == NegotiationStatus.InDiscussion || x.Status == NegotiationStatus.CounterOffered),
                token);
            if (duplicate)
                throw new DomainException("Shop đã có một yêu cầu đàm phán đang chờ xử lý cho mục tiêu này.");

            // Resolve current rate snapshot
            decimal currentRate = vendor.CommissionRate;
            if (currentRate <= 0m && request.CategoryId.HasValue)
            {
                var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == request.CategoryId.Value, token);
                if (category != null && category.DefaultCommissionRate > 0)
                    currentRate = category.DefaultCommissionRate;
            }
            if (currentRate <= 0m)
                throw new DomainException("Shop đang kế thừa tỷ lệ theo danh mục. Cần chọn một danh mục hoặc dịch vụ có mức hiện hành xác định; không tự giả định tỷ lệ chung cho shop.");

            var negotiation = CommissionNegotiationRequest.Create(
                vendor.Id,
                request.Scope,
                request.CategoryId,
                request.ServiceItemId,
                currentRate,
                request.ProposedRate,
                request.ReasonCategory,
                request.ReasonDetail,
                request.CooperationPlan,
                user.Id);

            db.CommissionNegotiationRequests.Add(negotiation);
            db.AuditLogs.Add(AuditLog.Create(user.Id, user.Role.ToString(), "CommissionNegotiation.Created",
                nameof(CommissionNegotiationRequest), negotiation.Id.ToString()));

            await db.SaveChangesAsync(token);

            var created = await CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests)
                .SingleAsync(x => x.Id == negotiation.Id, token);
            return CommissionNegotiationAccess.Map(created);
        }, ct);
}

// ── 2. Send Message / Discussion ──────────────────────────────────────────────
public record SendNegotiationMessageCommand(
    Guid Id,
    string Message,
    string? AttachmentUrl = null) : IRequest<CommissionNegotiationDto>;

public class SendNegotiationMessageCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<SendNegotiationMessageCommand, CommissionNegotiationDto>
{
    public Task<CommissionNegotiationDto> Handle(SendNegotiationMessageCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            if (actor.UserId is null) throw new DomainException("Chưa đăng nhập.");
            var user = await db.Users.SingleOrDefaultAsync(x => x.Id == actor.UserId && x.IsActive, token)
                ?? throw new DomainException("Tài khoản người dùng không hợp lệ.");
            if (actor.Role != user.Role.ToString()) throw new DomainException("Vai trò tài khoản đã thay đổi. Vui lòng đăng nhập lại.");

            var negotiation = await CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests)
                .SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Yêu cầu đàm phán không tồn tại.");

            if (user.Role == UserRole.VendorOwner)
            {
                var vendor = await db.Vendors.SingleOrDefaultAsync(x => x.UserId == user.Id && x.IsActive, token);
                if (vendor == null || negotiation.VendorId != vendor.Id)
                    throw new DomainException("Bạn không có quyền gửi tin nhắn trong đàm phán này.");
            }
            else if (user.Role is not (UserRole.Moderator or UserRole.SuperAdmin or UserRole.Finance))
            {
                throw new DomainException("Bạn không có quyền tham gia đàm phán này.");
            }

            var msg = negotiation.AddMessage(request.Message, user.Id, user.Role.ToString(), request.AttachmentUrl);
            db.CommissionNegotiationMessages.Add(msg);
            await db.SaveChangesAsync(token);

            return CommissionNegotiationAccess.Map(negotiation);
        }, ct);
}

// ── 3. Staff Counter-Offer ───────────────────────────────────────────────────
public record CounterOfferNegotiationCommand(
    Guid Id,
    Guid ExpectedVersion,
    decimal CounterRate,
    string Message) : IRequest<CommissionNegotiationDto>;

public class CounterOfferNegotiationCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<CounterOfferNegotiationCommand, CommissionNegotiationDto>
{
    public Task<CommissionNegotiationDto> Handle(CounterOfferNegotiationCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var user = await CommissionNegotiationAccess.RequireStaff(db, actor, token);
            if (user.Role == UserRole.Finance)
                throw new DomainException("Chỉ Moderator hoặc SuperAdmin mới được gửi mức đề xuất lại.");
            var negotiation = await CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests)
                .SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Yêu cầu đàm phán không tồn tại.");

            if (negotiation.Version != request.ExpectedVersion)
                throw new DomainException("Yêu cầu đàm phán đã được thay đổi bởi người khác. Vui lòng tải lại.");

            var msg = negotiation.AddCounterOffer(request.CounterRate, request.Message, user.Id, user.Role.ToString());
            db.CommissionNegotiationMessages.Add(msg);
            if (user.Role == UserRole.Moderator && !negotiation.AssignedModeratorId.HasValue)
                negotiation.AssignModerator(user.Id);

            db.AuditLogs.Add(AuditLog.Create(user.Id, user.Role.ToString(), "CommissionNegotiation.CounterOffered",
                nameof(CommissionNegotiationRequest), negotiation.Id.ToString()));

            await db.SaveChangesAsync(token);
            return CommissionNegotiationAccess.Map(negotiation);
        }, ct);
}

// ── 4. Vendor Respond to Counter-Offer ────────────────────────────────────────
public record RespondToCounterOfferCommand(
    Guid Id,
    Guid ExpectedVersion,
    bool Accept,
    string Message,
    decimal? NewProposedRate = null) : IRequest<CommissionNegotiationDto>;

public class RespondToCounterOfferCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<RespondToCounterOfferCommand, CommissionNegotiationDto>
{
    public Task<CommissionNegotiationDto> Handle(RespondToCounterOfferCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var (user, vendor) = await CommissionNegotiationAccess.RequireVendorOwner(db, actor, token);
            var negotiation = await CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests)
                .SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Yêu cầu đàm phán không tồn tại.");

            if (negotiation.VendorId != vendor.Id)
                throw new DomainException("Bạn không có quyền phản hồi đàm phán này.");
            if (negotiation.Version != request.ExpectedVersion)
                throw new DomainException("Yêu cầu đàm phán đã được cập nhật bởi quản trị viên. Vui lòng tải lại.");

            var msg = negotiation.RespondToCounter(request.Accept, request.Message, user.Id, request.NewProposedRate);
            db.CommissionNegotiationMessages.Add(msg);

            db.AuditLogs.Add(AuditLog.Create(user.Id, user.Role.ToString(), "CommissionNegotiation.Responded",
                nameof(CommissionNegotiationRequest), negotiation.Id.ToString()));

            await db.SaveChangesAsync(token);
            return CommissionNegotiationAccess.Map(negotiation);
        }, ct);
}

// ── 5. SuperAdmin Final Decision (Approve / Reject) ──────────────────────────
public record DecideNegotiationCommand(
    Guid Id,
    Guid ExpectedVersion,
    bool Approve,
    decimal? ApprovedRate,
    string Reason,
    bool CreatePolicyRule = false) : IRequest<CommissionNegotiationDto>;

public class DecideNegotiationCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<DecideNegotiationCommand, CommissionNegotiationDto>
{
    public Task<CommissionNegotiationDto> Handle(DecideNegotiationCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var admin = await CommissionNegotiationAccess.RequireSuperAdmin(db, actor, token);
            var negotiation = await CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests)
                .SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Yêu cầu đàm phán không tồn tại.");

            if (negotiation.Version != request.ExpectedVersion)
                throw new DomainException("Yêu cầu đàm phán đã có thay đổi. Vui lòng tải lại trang.");

            if (request.Approve)
            {
                if (request.CreatePolicyRule)
                    throw new DomainException("Chấp thuận đàm phán không tự tạo hoặc kích hoạt chính sách. Cần quy trình cơ sở tính, hợp đồng/phụ lục đã ký và ngày hiệu lực được xác nhận riêng.");
                decimal rate = request.ApprovedRate ?? negotiation.CounterRate ?? negotiation.ProposedRate;
                var msg = negotiation.Approve(admin.Id, rate, request.Reason, policyRuleId: null);
                db.CommissionNegotiationMessages.Add(msg);
                db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(), "CommissionNegotiation.Approved",
                    nameof(CommissionNegotiationRequest), negotiation.Id.ToString()));
            }
            else
            {
                var msg = negotiation.Reject(admin.Id, request.Reason);
                db.CommissionNegotiationMessages.Add(msg);
                db.AuditLogs.Add(AuditLog.Create(admin.Id, admin.Role.ToString(), "CommissionNegotiation.Rejected",
                    nameof(CommissionNegotiationRequest), negotiation.Id.ToString()));
            }

            await db.SaveChangesAsync(token);
            return CommissionNegotiationAccess.Map(negotiation);
        }, ct);
}

// ── 6. Vendor Cancel Negotiation ─────────────────────────────────────────────
public record CancelNegotiationCommand(Guid Id, string Reason) : IRequest<CommissionNegotiationDto>;

public class CancelNegotiationCommandHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<CancelNegotiationCommand, CommissionNegotiationDto>
{
    public Task<CommissionNegotiationDto> Handle(CancelNegotiationCommand request, CancellationToken ct) =>
        db.ExecuteSerializableAsync(async token =>
        {
            var (user, vendor) = await CommissionNegotiationAccess.RequireVendorOwner(db, actor, token);
            var negotiation = await CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests)
                .SingleOrDefaultAsync(x => x.Id == request.Id, token)
                ?? throw new NotFoundException("Yêu cầu đàm phán không tồn tại.");

            if (negotiation.VendorId != vendor.Id)
                throw new DomainException("Bạn không có quyền hủy đàm phán này.");

            var msg = negotiation.Cancel(user.Id, request.Reason);
            db.CommissionNegotiationMessages.Add(msg);
            db.AuditLogs.Add(AuditLog.Create(user.Id, user.Role.ToString(), "CommissionNegotiation.Cancelled",
                nameof(CommissionNegotiationRequest), negotiation.Id.ToString()));

            await db.SaveChangesAsync(token);
            return CommissionNegotiationAccess.Map(negotiation);
        }, ct);
}

// ── 7. Get My Negotiations (Vendor) ───────────────────────────────────────────
public record GetMyNegotiationsQuery(NegotiationStatus? Status = null) : IRequest<List<CommissionNegotiationDto>>;

public class GetMyNegotiationsQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetMyNegotiationsQuery, List<CommissionNegotiationDto>>
{
    public async Task<List<CommissionNegotiationDto>> Handle(GetMyNegotiationsQuery request, CancellationToken ct)
    {
        var (_, vendor) = await CommissionNegotiationAccess.RequireVendorOwner(db, actor, ct);
        var query = CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests.AsNoTracking())
            .Where(x => x.VendorId == vendor.Id);

        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status.Value);

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(CommissionNegotiationAccess.Map).ToList();
    }
}

// ── 8. Get Negotiation Detail ────────────────────────────────────────────────
public record GetNegotiationDetailQuery(Guid Id) : IRequest<CommissionNegotiationDto>;

public class GetNegotiationDetailQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetNegotiationDetailQuery, CommissionNegotiationDto>
{
    public async Task<CommissionNegotiationDto> Handle(GetNegotiationDetailQuery request, CancellationToken ct)
    {
        if (actor.UserId is null) throw new DomainException("Chưa đăng nhập.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor.UserId && x.IsActive, ct)
            ?? throw new DomainException("Tài khoản không còn hoạt động.");
        if (actor.Role != user.Role.ToString()) throw new DomainException("Vai trò tài khoản đã thay đổi. Vui lòng đăng nhập lại.");
        var negotiation = await CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException("Yêu cầu đàm phán không tồn tại.");

        if (actor.Role == nameof(UserRole.VendorOwner))
        {
            var vendor = await db.Vendors.SingleOrDefaultAsync(x => x.UserId == actor.UserId && x.IsActive, ct);
            if (vendor == null || negotiation.VendorId != vendor.Id)
                throw new DomainException("Bạn không có quyền xem yêu cầu đàm phán này.");
        }
        else if (actor.Role is not (nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin) or nameof(UserRole.Finance)))
        {
            throw new DomainException("Bạn không có quyền xem yêu cầu đàm phán này.");
        }

        return CommissionNegotiationAccess.Map(negotiation);
    }
}

// ── 9. Get Admin Negotiations (Staff Queue) ───────────────────────────────────
public record GetAdminNegotiationsQuery(
    NegotiationStatus? Status = null,
    Guid? VendorId = null,
    CommissionRuleScope? Scope = null) : IRequest<List<CommissionNegotiationDto>>;

public class GetAdminNegotiationsQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetAdminNegotiationsQuery, List<CommissionNegotiationDto>>
{
    public async Task<List<CommissionNegotiationDto>> Handle(GetAdminNegotiationsQuery request, CancellationToken ct)
    {
        await CommissionNegotiationAccess.RequireStaff(db, actor, ct);
        var query = CommissionNegotiationAccess.IncludeDetails(db.CommissionNegotiationRequests.AsNoTracking());

        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        if (request.VendorId.HasValue) query = query.Where(x => x.VendorId == request.VendorId.Value);
        if (request.Scope.HasValue) query = query.Where(x => x.Scope == request.Scope.Value);

        var list = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(CommissionNegotiationAccess.Map).ToList();
    }
}
