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
    private readonly IVerificationDocumentStore _documents;
    public SubmitVerificationCommandHandler(IApplicationDbContext context, ICurrentUserService user, IVerificationDocumentStore documents)
    { _context = context; _user = user; _documents = documents; }

    public async Task<VerificationDto> Handle(SubmitVerificationCommand r, CancellationToken ct)
    {
        var vendor = await VerificationDocumentAccess.RequireVendorAsync(_context, _user, ct);
        if (vendor.IsVerified) throw new DomainException("Nhà cung cấp đã được xác minh.");
        if (await _context.VendorVerifications.AnyAsync(v => v.VendorId == vendor.Id && v.Status == VerificationStatus.Submitted, ct))
            throw new DomainException("Hồ sơ đang chờ duyệt.");
        if (r.DocumentUrls == null || r.DocumentUrls.Count < 1 || r.DocumentUrls.Count > 10)
            throw new DomainException("Cần cung cấp từ 1 đến 10 tài liệu đính kèm (giấy phép, giấy tờ đại diện, ảnh cơ sở).");
        var documents = r.DocumentUrls.Select(url => VerificationDocumentAccess.NormalizeReference(url, vendor.Id, _documents)).ToList();
        if (documents.Distinct(StringComparer.Ordinal).Count() != documents.Count)
            throw new DomainException("Không đính kèm tài liệu trùng lặp.");
        var taxProfile = await _context.VendorTaxProfiles.FirstOrDefaultAsync(p => p.VendorId == vendor.Id, ct)
            ?? throw new DomainException("Vui lòng hoàn thiện hồ sơ pháp nhân, MST, ngân hàng và xác nhận thỏa thuận trước khi nộp giấy tờ.");
        taxProfile.Submit();
        var v = VendorVerification.Create(vendor.Id, r.BusinessLicenseNumber, JsonSerializer.Serialize(documents), r.Note);
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
    private readonly ICurrentUserService _user;
    public GetArticlesQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<List<ArticleDto>> Handle(GetArticlesQuery r, CancellationToken ct)
    {
        if (r.IncludeDrafts) await ArticleCmsAccess.RequireAsync(_context, _user, ct);
        var q = _context.Articles.AsQueryable();
        if (!r.IncludeDrafts) q = q.Where(a => a.IsPublished);
        if (!string.IsNullOrWhiteSpace(r.Slug)) q = q.Where(a => a.Slug == r.Slug);
        var list = await q.AsNoTracking().OrderByDescending(a => a.PublishedAt ?? a.CreatedAt).Take(100).ToListAsync(ct);
        var ids = list.Select(a => a.Id).ToList();
        var pins = await _context.ArticleListingPins.AsNoTracking().Where(p => ids.Contains(p.ArticleId) && p.Listing.Status == ListingStatus.Active && p.Listing.IsActive && p.Listing.Vendor.IsActive && p.Listing.Vendor.User.IsActive).ToListAsync(ct);
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
        await ArticleCmsAccess.RequireAsync(_context, _user, ct);
        var slug = ArticleCmsInput.Slug(r.Title, r.Slug, r.Excerpt, r.Content, r.Category);
        var thumbnail = ArticleImageReferences.Normalize(r.ThumbnailUrl);
        if (await _context.Articles.AnyAsync(a => a.Slug == slug, ct)) throw new DomainException("Slug bài viết đã tồn tại.");
        var a = Article.Create(_user.UserId!.Value, r.Title, slug, r.Excerpt ?? "", r.Content, thumbnail, r.Category.Trim(), null);
        a.Update(r.Title, slug, r.Excerpt ?? "", r.Content, thumbnail, r.Category.Trim());
        if (r.Publish) a.Publish();
        _context.Articles.Add(a);
        await _audit.LogAsync("Article.Create", nameof(Article), a.Id.ToString(), new { r.Title, r.Publish });
        await _context.SaveChangesAsync(ct);
        return new ArticleDto(a.Id, a.Title, a.Slug, a.Excerpt, a.Content, a.ThumbnailUrl, a.Category, a.IsPublished, a.PublishedAt, 0);
    }
}

// ---------- Audit + Dashboard ----------
public record AuditAccountInfoDto(
    Guid UserId,
    string FullName,
    string? Email,
    string? PhoneNumber,
    string Role,
    string? LoginName = null,
    string? BrandName = null,
    bool IsActive = true,
    DateTime? CreatedAt = null,
    DateTime? LastLoginAt = null,
    string? City = null,
    string? Address = null
);

public record AuditLogDto(
    Guid Id,
    DateTime CreatedAt,
    Guid? ActorId,
    string? ActorRole,
    string Action,
    string EntityType,
    string? EntityId,
    string? DataJson,
    string? EntityTitle = null,
    AuditAccountInfoDto? TargetAccount = null,
    AuditAccountInfoDto? ActorAccount = null
);
public record GetAuditLogsQuery(int Take) : IRequest<List<AuditLogDto>>;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, List<AuditLogDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAuditLogsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<AuditLogDto>> Handle(GetAuditLogsQuery r, CancellationToken ct)
    {
        var rawLogs = await _context.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Clamp(r.Take, 1, 500))
            .ToListAsync(ct);

        var actorIds = rawLogs.Where(l => l.ActorId.HasValue).Select(l => l.ActorId!.Value).Distinct().ToList();

        var userEntityIds = rawLogs.Where(l => l.EntityType == "User" && Guid.TryParse(l.EntityId, out _))
            .Select(l => Guid.Parse(l.EntityId!)).Distinct().ToList();

        var listingEntityIds = rawLogs.Where(l => l.EntityType == "Listing" && Guid.TryParse(l.EntityId, out _))
            .Select(l => Guid.Parse(l.EntityId!)).Distinct().ToList();

        var revisionEntityIds = rawLogs.Where(l => l.EntityType == "ListingRevision" && Guid.TryParse(l.EntityId, out _))
            .Select(l => Guid.Parse(l.EntityId!)).Distinct().ToList();

        var vendorEntityIds = rawLogs.Where(l => l.EntityType == "Vendor" && Guid.TryParse(l.EntityId, out _))
            .Select(l => Guid.Parse(l.EntityId!)).Distinct().ToList();

        var verificationEntityIds = rawLogs.Where(l => l.EntityType == "VendorVerification" && Guid.TryParse(l.EntityId, out _))
            .Select(l => Guid.Parse(l.EntityId!)).Distinct().ToList();

        var contractEntityIds = rawLogs.Where(l => (l.EntityType == "BookingContract" || l.EntityType == "Contract") && Guid.TryParse(l.EntityId, out _))
            .Select(l => Guid.Parse(l.EntityId!)).Distinct().ToList();

        var allUserIds = actorIds.Concat(userEntityIds).Distinct().ToList();
        var usersMap = allUserIds.Count > 0
            ? await _context.Users.AsNoTracking()
                .Include(u => u.Vendor)
                .Where(u => allUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, ct)
            : new Dictionary<Guid, User>();

        var listingsMap = listingEntityIds.Count > 0
            ? await _context.Listings.AsNoTracking()
                .Include(l => l.Vendor).ThenInclude(v => v.User)
                .Where(l => listingEntityIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, ct)
            : new Dictionary<Guid, Listing>();

        var revisionsMap = revisionEntityIds.Count > 0
            ? await _context.ListingRevisions.AsNoTracking()
                .Include(r => r.Listing).ThenInclude(l => l.Vendor).ThenInclude(v => v.User)
                .Where(r => revisionEntityIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, ct)
            : new Dictionary<Guid, ListingRevision>();

        var vendorsMap = vendorEntityIds.Count > 0
            ? await _context.Vendors.AsNoTracking()
                .Include(v => v.User)
                .Where(v => vendorEntityIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, ct)
            : new Dictionary<Guid, Vendor>();

        var verificationsMap = verificationEntityIds.Count > 0
            ? await _context.VendorVerifications.AsNoTracking()
                .Include(v => v.Vendor).ThenInclude(vnd => vnd.User)
                .Where(v => verificationEntityIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, ct)
            : new Dictionary<Guid, VendorVerification>();

        var contractsMap = contractEntityIds.Count > 0
            ? await _context.BookingContracts.AsNoTracking()
                .Include(c => c.Vendor).ThenInclude(v => v.User)
                .Include(c => c.Customer)
                .Where(c => contractEntityIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, ct)
            : new Dictionary<Guid, BookingContract>();

        AuditAccountInfoDto ToAccountInfo(User u, Vendor? vendor = null)
        {
            var v = vendor ?? u.Vendor;
            return new(
                u.Id,
                u.FullName,
                string.IsNullOrWhiteSpace(u.Email) ? null : u.Email,
                string.IsNullOrWhiteSpace(u.PhoneNumber) ? null : u.PhoneNumber,
                u.Role.ToString(),
                u.LoginName,
                v?.BrandName,
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt,
                v?.City,
                v?.Address
            );
        }

        return rawLogs.Select(log =>
        {
            AuditAccountInfoDto? actorAccount = null;
            if (log.ActorId.HasValue && usersMap.TryGetValue(log.ActorId.Value, out var actorUser))
            {
                actorAccount = ToAccountInfo(actorUser);
            }

            string? entityTitle = null;
            AuditAccountInfoDto? targetAccount = null;

            if (Guid.TryParse(log.EntityId, out var entityGuid))
            {
                if (log.EntityType == "User" && usersMap.TryGetValue(entityGuid, out var userTarget))
                {
                    entityTitle = userTarget.FullName;
                    targetAccount = ToAccountInfo(userTarget);
                }
                else if (log.EntityType == "Listing" && listingsMap.TryGetValue(entityGuid, out var listingTarget))
                {
                    entityTitle = listingTarget.Title;
                    if (listingTarget.Vendor?.User != null)
                    {
                        targetAccount = ToAccountInfo(listingTarget.Vendor.User, listingTarget.Vendor);
                    }
                    else if (listingTarget.Vendor != null)
                    {
                        targetAccount = new AuditAccountInfoDto(
                            listingTarget.Vendor.UserId,
                            string.IsNullOrWhiteSpace(listingTarget.Vendor.ContactPerson) ? listingTarget.Vendor.BrandName : listingTarget.Vendor.ContactPerson,
                            null,
                            listingTarget.Vendor.Hotline,
                            "VendorOwner",
                            null,
                            listingTarget.Vendor.BrandName,
                            listingTarget.Vendor.IsActive,
                            null,
                            null,
                            listingTarget.Vendor.City,
                            listingTarget.Vendor.Address
                        );
                    }
                }
                else if (log.EntityType == "ListingRevision" && revisionsMap.TryGetValue(entityGuid, out var revTarget))
                {
                    entityTitle = revTarget.Listing?.Title ?? $"Phiên bản #{revTarget.Sequence}";
                    if (revTarget.Listing?.Vendor?.User != null)
                    {
                        targetAccount = ToAccountInfo(revTarget.Listing.Vendor.User, revTarget.Listing.Vendor);
                    }
                }
                else if (log.EntityType == "Vendor" && vendorsMap.TryGetValue(entityGuid, out var vendorTarget))
                {
                    entityTitle = vendorTarget.BrandName;
                    if (vendorTarget.User != null)
                    {
                        targetAccount = ToAccountInfo(vendorTarget.User, vendorTarget);
                    }
                    else
                    {
                        targetAccount = new AuditAccountInfoDto(
                            vendorTarget.UserId,
                            string.IsNullOrWhiteSpace(vendorTarget.ContactPerson) ? vendorTarget.BrandName : vendorTarget.ContactPerson,
                            null,
                            vendorTarget.Hotline,
                            "VendorOwner",
                            null,
                            vendorTarget.BrandName,
                            vendorTarget.IsActive,
                            null,
                            null,
                            vendorTarget.City,
                            vendorTarget.Address
                        );
                    }
                }
                else if (log.EntityType == "VendorVerification" && verificationsMap.TryGetValue(entityGuid, out var verifTarget))
                {
                    entityTitle = $"Xác minh GPKD {verifTarget.BusinessLicenseNumber}";
                    if (verifTarget.Vendor?.User != null)
                    {
                        targetAccount = ToAccountInfo(verifTarget.Vendor.User, verifTarget.Vendor);
                    }
                    else if (verifTarget.Vendor != null)
                    {
                        targetAccount = new AuditAccountInfoDto(
                            verifTarget.Vendor.UserId,
                            string.IsNullOrWhiteSpace(verifTarget.Vendor.ContactPerson) ? verifTarget.Vendor.BrandName : verifTarget.Vendor.ContactPerson,
                            null,
                            verifTarget.Vendor.Hotline,
                            "VendorOwner",
                            null,
                            verifTarget.Vendor.BrandName,
                            verifTarget.Vendor.IsActive,
                            null,
                            null,
                            verifTarget.Vendor.City,
                            verifTarget.Vendor.Address
                        );
                    }
                }
                else if ((log.EntityType == "BookingContract" || log.EntityType == "Contract") && contractsMap.TryGetValue(entityGuid, out var contractTarget))
                {
                    entityTitle = $"Hợp đồng {contractTarget.ContractCode}";
                    if (contractTarget.Vendor?.User != null)
                    {
                        targetAccount = ToAccountInfo(contractTarget.Vendor.User, contractTarget.Vendor);
                    }
                    else if (contractTarget.Customer != null)
                    {
                        targetAccount = ToAccountInfo(contractTarget.Customer);
                    }
                }
            }

            if (targetAccount == null && actorAccount != null && (log.ActorRole is "VendorOwner" or "Customer"))
            {
                targetAccount = actorAccount;
            }

            return new AuditLogDto(
                log.Id,
                log.CreatedAt,
                log.ActorId,
                log.ActorRole,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.DataJson,
                entityTitle,
                targetAccount,
                actorAccount
            );
        }).ToList();
    }
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
