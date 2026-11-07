using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.ApproveListing;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.ModerationQueue;

public record ModerationQueueRow(Guid Id, string Type, string Title, Guid? VendorId, string VendorName,
    string Status, Guid? RevisionId, int? RevisionSequence, DateTime ChangedAt, bool IsActive, bool IsVendorVerified);
public record ModerationQueuePage(List<ModerationQueueRow> Items, int Page, int PageSize, int TotalCount,
    int PendingCount, string Type, string Status, string Search);
public record GetModerationQueueQuery(string Type = "all", string Status = "Pending", string? Search = null,
    int Page = 1, int PageSize = 10) : IRequest<ModerationQueuePage>;

/// <summary>Only lightweight metadata is combined/paged in SQL. No albums, children or full catalogs are hydrated.
/// Status and content come from the newest proposal, not the still-published baseline.</summary>
public class GetModerationQueueQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetModerationQueueQuery, ModerationQueuePage>
{
    public async Task<ModerationQueuePage> Handle(GetModerationQueueQuery request, CancellationToken ct)
    {
        await ListingPublication.RequireStaff(db, actor, ct);
        var type = (request.Type ?? "all").Trim(); var status = (request.Status ?? "Pending").Trim();
        if (type is not ("all" or "listing" or "service-item" or "combo")) throw new DomainException("Loại nội dung kiểm duyệt không hợp lệ.");
        if (status is not ("all" or "Draft" or "Pending" or "Approved" or "Rejected")) throw new DomainException("Trạng thái kiểm duyệt không hợp lệ.");
        var search = (request.Search ?? "").Trim();
        if (search.Length > 150) throw new DomainException("Từ khóa tìm kiếm tối đa 150 ký tự.");
        var size = Math.Clamp(request.PageSize, 1, 20);
        var listingRevisions = db.ListingRevisions.AsNoTracking().Where(revision => !db.ListingRevisions.Any(newer => newer.ListingId == revision.ListingId && newer.Sequence > revision.Sequence));
        var listings = from listing in db.Listings.AsNoTracking()
            join revision in listingRevisions on listing.Id equals revision.ListingId into revisions
            from revision in revisions.DefaultIfEmpty()
            select new Candidate {
                Id = listing.Id, Type = "listing", Title = listing.Title, VendorId = listing.VendorId, VendorName = listing.Vendor.BrandName,
                Status = revision != null ? revision.Status : listing.Status == ListingStatus.PendingApproval ? "Pending" : listing.Status == ListingStatus.Draft ? "Draft" : listing.Status == ListingStatus.Rejected ? "Rejected" : "Approved",
                RevisionId = revision != null ? revision.Id : null, RevisionSequence = revision != null ? revision.Sequence : null,
                ChangedAt = revision != null ? revision.SubmittedAt ?? revision.CreatedAt : listing.UpdatedAt ?? listing.CreatedAt,
                IsActive = listing.IsActive, ProposalJson = revision != null ? revision.ContentJson : null,
                IsVendorVerified = listing.Vendor.IsVerified
            };
        var services = db.ServiceItems.AsNoTracking()
            .Where(item => !db.Listings.Any(listing => listing.Id == item.ListingId && listing.PricingServiceItemId == item.Id))
            .Select(item => new Candidate {
            Id = item.Id, Type = "service-item", Title = item.Name, VendorId = item.VendorId, VendorName = item.Vendor.BrandName,
            Status = item.ModerationStatus, RevisionId = null, RevisionSequence = null,
            ChangedAt = item.UpdatedAt ?? item.CreatedAt, IsActive = item.IsActive, ProposalJson = null,
            IsVendorVerified = item.Vendor.IsVerified
        });
        var comboRevisions = db.ComboTemplateRevisions.AsNoTracking().Where(revision => !db.ComboTemplateRevisions.Any(newer => newer.ComboTemplateId == revision.ComboTemplateId && newer.Sequence > revision.Sequence));
        var combos = from combo in db.ComboTemplates.AsNoTracking()
            join revision in comboRevisions on combo.Id equals revision.ComboTemplateId into revisions
            from revision in revisions.DefaultIfEmpty()
            select new Candidate {
                Id = combo.Id, Type = "combo", Title = combo.Name, VendorId = combo.OwnerVendorId,
                VendorName = db.Vendors.Where(vendor => vendor.Id == combo.OwnerVendorId).Select(vendor => vendor.BrandName).FirstOrDefault() ?? "Chưa gắn NCC",
                Status = revision != null ? revision.Status : combo.ModerationStatus,
                RevisionId = revision != null ? revision.Id : null, RevisionSequence = revision != null ? revision.Sequence : null,
                ChangedAt = revision != null ? revision.SubmittedAt ?? revision.CreatedAt : combo.UpdatedAt ?? combo.CreatedAt,
                IsActive = combo.IsActive, ProposalJson = revision != null ? revision.SnapshotJson : null,
                IsVendorVerified = db.Vendors.Where(vendor => vendor.Id == combo.OwnerVendorId).Select(vendor => vendor.IsVerified).FirstOrDefault()
            };
        var all = listings.Concat(services).Concat(combos);
        var pending = await all.CountAsync(row => row.Status == "Pending", ct);
        var query = type == "all" ? all : all.Where(row => row.Type == type);
        if (status != "all") query = query.Where(row => row.Status == status);
        if (search.Length > 0)
        {
            var keyword = search.ToLowerInvariant();
            // Search proposal text rather than a stale published name. JSON is not returned to clients.
            query = query.Where(row => row.VendorName.ToLower().Contains(keyword) ||
                (row.ProposalJson == null ? row.Title.ToLower().Contains(keyword) : row.ProposalJson.ToLower().Contains(keyword)));
        }
        var total = await query.CountAsync(ct);
        var page = Math.Clamp(request.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)size)));
        var rows = await query.OrderBy(row => row.ChangedAt).ThenBy(row => row.Type).ThenBy(row => row.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new(rows.Select(row => new ModerationQueueRow(row.Id, row.Type, ProposalTitle(row), row.VendorId,
            row.VendorName, row.Status, row.RevisionId, row.RevisionSequence, DateTime.SpecifyKind(row.ChangedAt, DateTimeKind.Utc), row.IsActive, row.IsVendorVerified)).ToList(),
            page, size, total, pending, type, status, search);
    }

    private static string ProposalTitle(Candidate row)
    {
        if (row.ProposalJson == null) return row.Title;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(row.ProposalJson);
            return json.RootElement.GetProperty(row.Type == "listing" ? "Title" : "Name").GetString() ?? "Nội dung phiên bản không hợp lệ";
        }
        catch (System.Text.Json.JsonException) { return "Nội dung phiên bản không hợp lệ"; }
        catch (KeyNotFoundException) { return "Nội dung phiên bản không hợp lệ"; }
        catch (InvalidOperationException) { return "Nội dung phiên bản không hợp lệ"; }
    }

    private sealed class Candidate
    {
        public Guid Id { get; set; } public string Type { get; set; } = ""; public string Title { get; set; } = "";
        public Guid? VendorId { get; set; } public string VendorName { get; set; } = ""; public string Status { get; set; } = "";
        public Guid? RevisionId { get; set; } public int? RevisionSequence { get; set; }
        public DateTime ChangedAt { get; set; } public bool IsActive { get; set; } public string? ProposalJson { get; set; }
        public bool IsVendorVerified { get; set; }
    }
}

public record ModerationQueueDetail(string Type, Guid Id, ListingDto? Listing = null,
    ServiceItemDto? ServiceItem = null, ComboTemplateDto? Combo = null, bool IsVendorVerified = false);
public record GetModerationQueueDetailQuery(string Type, Guid Id, Guid? RevisionId = null) : IRequest<ModerationQueueDetail>;
public class GetModerationQueueDetailQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetModerationQueueDetailQuery, ModerationQueueDetail>
{
    public async Task<ModerationQueueDetail> Handle(GetModerationQueueDetailQuery request, CancellationToken ct)
    {
        await ListingPublication.RequireStaff(db, actor, ct);
        if (request.Type == "listing")
        {
            var listing = await new GetListingEditorQueryHandler(db, actor).Handle(new(request.Id, request.RevisionId), ct);
            var isVendorVerified = await db.Vendors.Where(v => v.Id == listing.VendorId).Select(v => v.IsVerified).FirstOrDefaultAsync(ct);
            return new(request.Type, request.Id, Listing: listing, IsVendorVerified: isVendorVerified);
        }
        if (request.Type == "combo")
        {
            var combo = await new GetComboTemplatePreviewQueryHandler(db, actor).Handle(new(request.Id, request.RevisionId), ct);
            var isVendorVerified = await db.Vendors.Where(v => v.Id == combo.OwnerVendorId).Select(v => v.IsVerified).FirstOrDefaultAsync(ct);
            return new(request.Type, request.Id, Combo: combo, IsVendorVerified: isVendorVerified);
        }
        if (request.Type != "service-item") throw new DomainException("Loại nội dung kiểm duyệt không hợp lệ.");
        if (request.RevisionId.HasValue) throw new DomainException("Dịch vụ thành phần dùng mã phiên bản nội dung thay vì mã revision.");
        var item = await db.ServiceItems.AsNoTracking().Include(item => item.Vendor).Include(item => item.Variants).Include(item => item.AddOns)
            .SingleOrDefaultAsync(item => item.Id == request.Id, ct) ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        return new(request.Type, request.Id, ServiceItem: CatalogMap.ToDto(item), IsVendorVerified: item.Vendor?.IsVerified ?? false);
    }
}

public record AutoApproveModerationQueueResult(int ApprovedListings, int ApprovedServices, int ApprovedCombos, int TotalApproved,
    int Attempted = 0, int Skipped = 0, bool HasMore = false);
public record AutoApproveModerationQueueCommand(string Type = "all", int BatchSize = 20) : IRequest<AutoApproveModerationQueueResult>;

public class AutoApproveModerationQueueCommandHandler(
    IApplicationDbContext db,
    ICurrentUserService actor,
    IMediator mediator) : IRequestHandler<AutoApproveModerationQueueCommand, AutoApproveModerationQueueResult>
{
    public async Task<AutoApproveModerationQueueResult> Handle(AutoApproveModerationQueueCommand request, CancellationToken ct)
    {
        await ListingPublication.RequireStaff(db, actor, ct);
        var targetType = (request.Type ?? "all").Trim().ToLowerInvariant();
        if (targetType is not ("all" or "listing" or "service-item" or "combo"))
            throw new DomainException("Loại nội dung kiểm duyệt không hợp lệ.");
        if (request.BatchSize is < 1 or > 50)
            throw new DomainException("Mỗi lượt chỉ xử lý từ 1 đến 50 nội dung cho mỗi loại.");
        var size = request.BatchSize;
        int approvedListings = 0;
        int approvedServices = 0;
        int approvedCombos = 0;
        int attempted = 0;
        int skipped = 0;
        bool hasMore = false;

        // Every canonical command owns a short transaction, audit and concurrency checks.
        // Domain/stale conflicts may be skipped; cancellation or infrastructure errors must stop this request.
        async Task<bool> Review(Func<Task> action)
        {
            ct.ThrowIfCancellationRequested();
            attempted++;
            try { await action(); return true; }
            catch (DomainException) { skipped++; return false; }
            catch (NotFoundException) { skipped++; return false; }
            catch (DbUpdateConcurrencyException) { skipped++; return false; }
        }

        // 1. Listings
        if (targetType is "all" or "listing")
        {
            var pendingListingCandidates = await (
                from l in db.Listings.AsNoTracking()
                join r in db.ListingRevisions.AsNoTracking().Where(revision => !db.ListingRevisions.Any(newer => newer.ListingId == revision.ListingId && newer.Sequence > revision.Sequence)) on l.Id equals r.ListingId into revs
                from r in revs.DefaultIfEmpty()
                where (r != null && r.Status == "Pending") || (r == null && l.Status == ListingStatus.PendingApproval)
                select new { ListingId = l.Id, RevisionId = r != null ? (Guid?)r.Id : null }
            ).OrderBy(item => item.ListingId).Take(size + 1).ToListAsync(ct);
            hasMore |= pendingListingCandidates.Count > size;

            foreach (var item in pendingListingCandidates.Take(size))
            {
                if (await Review(() => mediator.Send(new ApproveListingCommand(item.ListingId, item.RevisionId), ct))) approvedListings++;
            }
        }

        // 2. Service items (non-canonical standalone)
        if (targetType is "all" or "service-item")
        {
            var pendingServiceItems = await db.ServiceItems.AsNoTracking()
                .Include(i => i.Variants)
                .Include(i => i.AddOns)
                .Where(i => i.ModerationStatus == "Pending" && !db.Listings.Any(l => l.Id == i.ListingId && l.PricingServiceItemId == i.Id))
                .OrderBy(i => i.Id).Take(size + 1).ToListAsync(ct);
            hasMore |= pendingServiceItems.Count > size;

            foreach (var item in pendingServiceItems.Take(size))
            {
                var version = CatalogMap.ReviewVersion(item);
                if (await Review(async () => { await mediator.Send(new ReviewServiceItemCommand(item.Id, true, "Phê duyệt qua lượt duyệt hàng loạt đã xác nhận", version), ct); })) approvedServices++;
            }
        }

        // 3. Combos
        if (targetType is "all" or "combo")
        {
            var pendingComboCandidates = await (
                from c in db.ComboTemplates.AsNoTracking()
                join r in db.ComboTemplateRevisions.AsNoTracking().Where(revision => !db.ComboTemplateRevisions.Any(newer => newer.ComboTemplateId == revision.ComboTemplateId && newer.Sequence > revision.Sequence)) on c.Id equals r.ComboTemplateId into revs
                from r in revs.DefaultIfEmpty()
                where (r != null && r.Status == "Pending") || (r == null && c.ModerationStatus == "Pending")
                select new { ComboId = c.Id, RevisionId = r != null ? (Guid?)r.Id : null }
            ).OrderBy(item => item.ComboId).Take(size + 1).ToListAsync(ct);
            hasMore |= pendingComboCandidates.Count > size;

            foreach (var item in pendingComboCandidates.Take(size))
            {
                if (await Review(async () => { await mediator.Send(new ReviewComboTemplateCommand(item.ComboId, true, "Phê duyệt qua lượt duyệt hàng loạt đã xác nhận", item.RevisionId), ct); })) approvedCombos++;
            }
        }

        return new AutoApproveModerationQueueResult(
            approvedListings,
            approvedServices,
            approvedCombos,
            approvedListings + approvedServices + approvedCombos, attempted, skipped, hasMore);
    }
}
