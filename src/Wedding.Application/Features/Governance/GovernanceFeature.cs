using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Contracts;

namespace Wedding.Application.Features.Governance;

// ---------- Xác minh NCC ----------
public record VerificationDto(Guid Id, Guid VendorId, string VendorName, string BusinessLicenseNumber, List<string> DocumentUrls, string? Note,
    string Status, string? ReviewNote, DateTime CreatedAt, DateTime? ReviewedAt);

internal static class GovMap
{
    public static VerificationDto ToDto(VendorVerification v) => new(v.Id, v.VendorId, v.Vendor?.BrandName ?? "", v.BusinessLicenseNumber,
        JsonSerializer.Deserialize<List<string>>(v.DocumentUrlsJson) ?? new(), v.Note, v.Status.ToString(), v.ReviewNote, v.CreatedAt, v.ReviewedAt);

    public static DisputeDto ToDto(Dispute d) => new(d.Id, d.Code, d.ContractId, d.Contract?.ContractCode ?? "", d.OpenedByRole, d.Reason,
        d.EvidenceUrl, d.Status.ToString(), d.Resolution, d.SlaDeadline, d.CreatedAt, d.ResolvedAt, DateTime.UtcNow > d.SlaDeadline && d.ResolvedAt == null);
}

public record SubmitVerificationCommand(string BusinessLicenseNumber, List<string> DocumentUrls, string? Note) : IRequest<VerificationDto>;

public class SubmitVerificationCommandHandler : IRequestHandler<SubmitVerificationCommand, VerificationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public SubmitVerificationCommandHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<VerificationDto> Handle(SubmitVerificationCommand r, CancellationToken ct)
    {
        var vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == _user.UserId, ct) ?? throw new DomainException("Tài khoản chưa gắn với nhà cung cấp.");
        if (vendor.IsVerified) throw new DomainException("Nhà cung cấp đã được xác minh.");
        if (await _context.VendorVerifications.AnyAsync(v => v.VendorId == vendor.Id && v.Status == VerificationStatus.Submitted, ct))
            throw new DomainException("Hồ sơ đang chờ duyệt.");
        if (r.DocumentUrls == null || r.DocumentUrls.Count < 3 || r.DocumentUrls.Count > 10 || r.DocumentUrls.Any(url => !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new DomainException("Cần 3–10 link HTTPS: giấy phép, giấy tờ đại diện và ảnh cơ sở.");
        var taxProfile = await _context.VendorTaxProfiles.FirstOrDefaultAsync(p => p.VendorId == vendor.Id, ct) ?? throw new DomainException("Vui lòng hoàn thiện hồ sơ pháp nhân, MST, ngân hàng trước.");
        taxProfile.Submit();
        var v = VendorVerification.Create(vendor.Id, r.BusinessLicenseNumber, JsonSerializer.Serialize(r.DocumentUrls), r.Note);
        _context.VendorVerifications.Add(v);
        await _context.SaveChangesAsync(ct);
        return GovMap.ToDto(v) with { VendorName = vendor.BrandName };
    }
}

public record GetVerificationsQuery(bool Mine) : IRequest<List<VerificationDto>>;

public class GetVerificationsQueryHandler : IRequestHandler<GetVerificationsQuery, List<VerificationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetVerificationsQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<List<VerificationDto>> Handle(GetVerificationsQuery r, CancellationToken ct)
    {
        var q = _context.VendorVerifications.AsNoTracking().Include(v => v.Vendor).AsQueryable();
        if (r.Mine) q = q.Where(v => v.Vendor.UserId == _user.UserId);
        return (await q.ToListAsync(ct)).OrderBy(v => v.Status).ThenByDescending(v => v.CreatedAt).Select(GovMap.ToDto).ToList();
    }
}

public record ReviewVerificationCommand(Guid Id, bool Approve, string? Note) : IRequest<VerificationDto>;

public class ReviewVerificationCommandHandler : IRequestHandler<ReviewVerificationCommand, VerificationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly IAuditLogger _audit;
    public ReviewVerificationCommandHandler(IApplicationDbContext context, ICurrentUserService user, IAuditLogger audit) { _context = context; _user = user; _audit = audit; }

    public async Task<VerificationDto> Handle(ReviewVerificationCommand r, CancellationToken ct)
    {
        var v = await _context.VendorVerifications.Include(x => x.Vendor).FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException("Hồ sơ không tồn tại.");
        v.Review(_user.UserId ?? Guid.Empty, r.Approve, r.Note);
        if (r.Approve) v.Vendor.SetVerified(true);
        var taxProfile = await _context.VendorTaxProfiles.FirstOrDefaultAsync(p => p.VendorId == v.VendorId, ct) ?? throw new DomainException("Thiếu hồ sơ MST/ngân hàng. NCC cần bổ sung trước khi duyệt.");
        taxProfile.Review(r.Approve);
        await _audit.LogAsync(r.Approve ? "Vendor.Verify" : "Vendor.VerifyReject", nameof(Vendor), v.VendorId.ToString(), new { r.Note });
        await _context.SaveChangesAsync(ct);
        return GovMap.ToDto(v);
    }
}

// ---------- Tranh chấp ----------
public record DisputeDto(Guid Id, string Code, Guid ContractId, string ContractCode, string OpenedByRole, string Reason, string? EvidenceUrl,
    string Status, string? Resolution, DateTime SlaDeadline, DateTime CreatedAt, DateTime? ResolvedAt, bool SlaBreached);

public record OpenDisputeCommand(Guid ContractId, string Reason, string? EvidenceUrl) : IRequest<DisputeDto>;

public class OpenDisputeCommandHandler : IRequestHandler<OpenDisputeCommand, DisputeDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public OpenDisputeCommandHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<DisputeDto> Handle(OpenDisputeCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var c = await _context.BookingContracts.Include(x => x.Vendor).FirstOrDefaultAsync(x => x.Id == r.ContractId, ct) ?? throw new NotFoundException("Hợp đồng không tồn tại.");
        if (c.CustomerId != uid && c.Vendor.UserId != uid) throw new DomainException("Bạn không thuộc hợp đồng này.");
        if (await _context.Disputes.AnyAsync(d => d.ContractId == c.Id && d.ResolvedAt == null, ct)) throw new DomainException("Hợp đồng đã có tranh chấp đang mở.");
        c.MarkDisputed();
        var commissions = await _context.Commissions.Where(x => x.ContractId == c.Id).ToListAsync(ct);
        foreach (var commission in commissions) commission.Freeze();
        var settlementIds = commissions.Where(x => x.SettlementId.HasValue).Select(x => x.SettlementId!.Value).ToList();
        var settlements = await _context.CommissionSettlements.Where(s => settlementIds.Contains(s.Id) && (s.Status == SettlementStatus.Closed || s.Status == SettlementStatus.Overdue)).ToListAsync(ct);
        foreach (var settlement in settlements) settlement.Dispute($"Tranh chấp HĐ {c.ContractCode}");
        var d = Dispute.Create(c.Id, uid, _user.Role ?? "", r.Reason, r.EvidenceUrl);
        _context.Disputes.Add(d);
        await _context.SaveChangesAsync(ct);
        return GovMap.ToDto(d) with { ContractCode = c.ContractCode };
    }
}

public record GetDisputesQuery(bool Mine) : IRequest<List<DisputeDto>>;

public class GetDisputesQueryHandler : IRequestHandler<GetDisputesQuery, List<DisputeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetDisputesQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<List<DisputeDto>> Handle(GetDisputesQuery r, CancellationToken ct)
    {
        var q = _context.Disputes.AsNoTracking().Include(d => d.Contract).ThenInclude(c => c.Vendor).AsQueryable();
        if (r.Mine) q = q.Where(d => d.Contract.CustomerId == _user.UserId || d.Contract.Vendor.UserId == _user.UserId);
        return (await q.ToListAsync(ct)).OrderBy(d => d.ResolvedAt.HasValue).ThenBy(d => d.SlaDeadline).Select(GovMap.ToDto).ToList();
    }
}

public record ResolveDisputeCommand(Guid Id, bool InFavorOfCustomer, bool CancelContract, string Resolution, decimal? RetainedDeposit = null) : IRequest<DisputeDto>;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, DisputeDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly IAuditLogger _audit;
    public ResolveDisputeCommandHandler(IApplicationDbContext context, ICurrentUserService user, IAuditLogger audit) { _context = context; _user = user; _audit = audit; }

    public async Task<DisputeDto> Handle(ResolveDisputeCommand r, CancellationToken ct)
    {
        var d = await _context.Disputes.Include(x => x.Contract).ThenInclude(c => c.Commissions).FirstOrDefaultAsync(x => x.Id == r.Id, ct)
            ?? throw new NotFoundException("Tranh chấp không tồn tại.");
        if (r.CancelContract)
        {
            if (_user.Role is not ("Finance" or "SuperAdmin")) throw new DomainException("Hủy tài chính cần Finance hoặc SuperAdmin xác minh số cọc thực giữ.");
            if (r.RetainedDeposit == null) throw new DomainException("Cần nhập số cọc NCC thực giữ đã được xác minh.");
            await FinancialContractService.CancelAsync(_context, d.Contract, r.RetainedDeposit.Value, r.Resolution, ct);
        }
        else
        {
            d.Contract.ResolveDispute(false, r.Resolution);
            foreach (var commission in d.Contract.Commissions) commission.Unfreeze(DateTime.UtcNow);
            var ids = d.Contract.Commissions.Where(c => c.SettlementId.HasValue).Select(c => c.SettlementId!.Value).ToList();
            var settlements = await _context.CommissionSettlements.Where(s => ids.Contains(s.Id) && s.Status == SettlementStatus.InDispute).ToListAsync(ct);
            foreach (var settlement in settlements)
                if (!await _context.Commissions.AnyAsync(c => c.SettlementId == settlement.Id && c.ContractId != d.ContractId && c.Status == CommissionStatus.Frozen, ct)) settlement.ResolveDispute();
        }
        d.Assign(_user.UserId ?? Guid.Empty);
        d.Resolve(r.InFavorOfCustomer, r.Resolution);
        await _audit.LogAsync("Dispute.Resolve", nameof(Dispute), d.Id.ToString(), new { r.InFavorOfCustomer, r.CancelContract, r.Resolution });
        await _context.SaveChangesAsync(ct);
        return GovMap.ToDto(d);
    }
}

// ---------- CMS bài viết ----------
public record ArticleDto(Guid Id, string Title, string Slug, string Excerpt, string Content, string? ThumbnailUrl, string Category,
    bool IsPublished, DateTime? PublishedAt, int ViewCount, List<Guid>? ListingIds = null);

public record GetArticlesQuery(bool IncludeDrafts, string? Slug) : IRequest<List<ArticleDto>>;

public class GetArticlesQueryHandler : IRequestHandler<GetArticlesQuery, List<ArticleDto>>
{
    private readonly IApplicationDbContext _context;
    public GetArticlesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ArticleDto>> Handle(GetArticlesQuery r, CancellationToken ct)
    {
        var q = _context.Articles.AsQueryable();
        if (!r.IncludeDrafts) q = q.Where(a => a.IsPublished);
        if (!string.IsNullOrWhiteSpace(r.Slug)) q = q.Where(a => a.Slug == r.Slug);
        var list = await q.AsNoTracking().OrderByDescending(a => a.PublishedAt ?? a.CreatedAt).Take(100).ToListAsync(ct);
        var ids = list.Select(a => a.Id).ToList();
        var pins = await _context.ArticleListingPins.AsNoTracking().Where(p => ids.Contains(p.ArticleId) && p.Listing.Status == ListingStatus.Active).ToListAsync(ct);
        return list.Select(a => new ArticleDto(a.Id, a.Title, a.Slug, a.Excerpt,
            r.Slug == null ? "" : a.Content, a.ThumbnailUrl, a.Category, a.IsPublished, a.PublishedAt, a.ViewCount, pins.Where(p => p.ArticleId == a.Id).Select(p => p.ListingId).ToList())).ToList();
    }
}

public record SaveArticleCommand(string Title, string Slug, string Excerpt, string Content, string? ThumbnailUrl, string Category, bool Publish) : IRequest<ArticleDto>;

public class SaveArticleCommandHandler : IRequestHandler<SaveArticleCommand, ArticleDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly IAuditLogger _audit;
    public SaveArticleCommandHandler(IApplicationDbContext context, ICurrentUserService user, IAuditLogger audit) { _context = context; _user = user; _audit = audit; }

    public async Task<ArticleDto> Handle(SaveArticleCommand r, CancellationToken ct)
    {
        if (await _context.Articles.AnyAsync(a => a.Slug == r.Slug.Trim().ToLower(), ct)) throw new DomainException("Slug bài viết đã tồn tại.");
        var a = Article.Create(_user.UserId ?? throw new DomainException("Bạn cần đăng nhập."), r.Title, r.Slug, r.Excerpt, r.Content, r.ThumbnailUrl, r.Category, null);
        a.Update(r.Title, r.Slug, r.Excerpt, r.Content, r.ThumbnailUrl, r.Category);
        if (r.Publish) a.Publish();
        _context.Articles.Add(a);
        await _audit.LogAsync("Article.Create", nameof(Article), a.Id.ToString(), new { r.Title, r.Publish });
        await _context.SaveChangesAsync(ct);
        return new ArticleDto(a.Id, a.Title, a.Slug, a.Excerpt, a.Content, a.ThumbnailUrl, a.Category, a.IsPublished, a.PublishedAt, 0);
    }
}

// ---------- Audit + Dashboard ----------
public record AuditLogDto(Guid Id, DateTime CreatedAt, Guid? ActorId, string? ActorRole, string Action, string EntityType, string? EntityId, string? DataJson);
public record GetAuditLogsQuery(int Take) : IRequest<List<AuditLogDto>>;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, List<AuditLogDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAuditLogsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<AuditLogDto>> Handle(GetAuditLogsQuery r, CancellationToken ct)
        => (await _context.AuditLogs.AsNoTracking().OrderByDescending(a => a.CreatedAt).Take(Math.Clamp(r.Take, 1, 500)).ToListAsync(ct))
            .Select(a => new AuditLogDto(a.Id, a.CreatedAt, a.ActorId, a.ActorRole, a.Action, a.EntityType, a.EntityId, a.DataJson)).ToList();
}

public record AdminDashboardDto(int Customers, int Vendors, int VerifiedVendors, int PendingVerifications, int LeadsThisMonth,
    int LeadsSlaBreached, int ContractsConfirmed, decimal Gmv, decimal CommissionPending, decimal CommissionPaid, int OpenDisputes,
    int ComboOrders, int UpcomingAppointments, decimal LeadToContractRate);
public record GetAdminDashboardQuery : IRequest<AdminDashboardDto>;

public class GetAdminDashboardQueryHandler : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    private readonly IApplicationDbContext _context;
    public GetAdminDashboardQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<AdminDashboardDto> Handle(GetAdminDashboardQuery r, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var contracts = await _context.BookingContracts.AsNoTracking().Where(c => c.Status == ContractStatus.Confirmed || c.Status == ContractStatus.Completed)
            .Select(c => c.ContractValue).ToListAsync(ct);
        var comm = await _context.Commissions.AsNoTracking().Select(c => new { c.Status, c.CommissionAmount }).ToListAsync(ct);
        var leads = await _context.Leads.CountAsync(ct);
        return new AdminDashboardDto(
            await _context.Users.CountAsync(u => u.Role == UserRole.Customer, ct),
            await _context.Vendors.CountAsync(ct), await _context.Vendors.CountAsync(v => v.IsVerified, ct),
            await _context.VendorVerifications.CountAsync(v => v.Status == VerificationStatus.Submitted, ct),
            await _context.Leads.CountAsync(l => l.CreatedAt >= monthStart, ct),
            await _context.Leads.CountAsync(l => l.Status == LeadStatus.New && l.SlaDeadline < now, ct),
            contracts.Count, contracts.Sum(),
            comm.Where(c => c.Status is CommissionStatus.Pending or CommissionStatus.Unsettled or CommissionStatus.InSettlement or CommissionStatus.Overdue).Sum(c => c.CommissionAmount),
            comm.Where(c => c.Status is CommissionStatus.Paid or CommissionStatus.Settled).Sum(c => c.CommissionAmount),
            await _context.Disputes.CountAsync(d => d.ResolvedAt == null, ct),
            await _context.ComboOrders.CountAsync(ct),
            await _context.Appointments.CountAsync(a => a.StartAt > now && a.Status == AppointmentStatus.Confirmed, ct),
            leads == 0 ? 0 : Math.Round((decimal)contracts.Count / leads * 100, 1));
    }
}
