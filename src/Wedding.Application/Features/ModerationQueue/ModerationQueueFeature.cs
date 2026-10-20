using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.ModerationQueue;

public record ModerationQueueRow(Guid Id, string Type, string Title, Guid? VendorId, string VendorName,
    string Status, Guid? RevisionId, int? RevisionSequence, DateTime ChangedAt, bool IsActive);
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
                IsActive = listing.IsActive, ProposalJson = revision != null ? revision.ContentJson : null
            };
        var services = db.ServiceItems.AsNoTracking()
            .Where(item => !db.Listings.Any(listing => listing.Id == item.ListingId && listing.PricingServiceItemId == item.Id))
            .Select(item => new Candidate {
            Id = item.Id, Type = "service-item", Title = item.Name, VendorId = item.VendorId, VendorName = item.Vendor.BrandName,
            Status = item.ModerationStatus, RevisionId = null, RevisionSequence = null,
            ChangedAt = item.UpdatedAt ?? item.CreatedAt, IsActive = item.IsActive, ProposalJson = null
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
                IsActive = combo.IsActive, ProposalJson = revision != null ? revision.SnapshotJson : null
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
            row.VendorName, row.Status, row.RevisionId, row.RevisionSequence, DateTime.SpecifyKind(row.ChangedAt, DateTimeKind.Utc), row.IsActive)).ToList(),
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
    }
}

public record ModerationQueueDetail(string Type, Guid Id, ListingDto? Listing = null,
    ServiceItemDto? ServiceItem = null, ComboTemplateDto? Combo = null);
public record GetModerationQueueDetailQuery(string Type, Guid Id, Guid? RevisionId = null) : IRequest<ModerationQueueDetail>;
public class GetModerationQueueDetailQueryHandler(IApplicationDbContext db, ICurrentUserService actor)
    : IRequestHandler<GetModerationQueueDetailQuery, ModerationQueueDetail>
{
    public async Task<ModerationQueueDetail> Handle(GetModerationQueueDetailQuery request, CancellationToken ct)
    {
        await ListingPublication.RequireStaff(db, actor, ct);
        if (request.Type == "listing")
            return new(request.Type, request.Id, Listing: await new GetListingEditorQueryHandler(db, actor).Handle(new(request.Id, request.RevisionId), ct));
        if (request.Type == "combo")
            return new(request.Type, request.Id, Combo: await new GetComboTemplatePreviewQueryHandler(db, actor).Handle(new(request.Id, request.RevisionId), ct));
        if (request.Type != "service-item") throw new DomainException("Loại nội dung kiểm duyệt không hợp lệ.");
        if (request.RevisionId.HasValue) throw new DomainException("Dịch vụ thành phần dùng mã phiên bản nội dung thay vì mã revision.");
        var item = await db.ServiceItems.AsNoTracking().Include(item => item.Vendor).Include(item => item.Variants).Include(item => item.AddOns)
            .SingleOrDefaultAsync(item => item.Id == request.Id, ct) ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        return new(request.Type, request.Id, ServiceItem: CatalogMap.ToDto(item));
    }
}
