using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings;

public record ListingRevisionContent(string Title, decimal MinPrice, decimal MaxPrice, string Description,
    string Location, List<string> ImageUrls, string? ServiceDetailsJson, ServicePricingDto? Pricing = null);
public record ListingRevisionDto(Guid Id, int Sequence, string Status, DateTime CreatedAt, DateTime? SubmittedAt,
    DateTime? ReviewedAt, string? ReviewNote, Guid CreatedById, Guid? ReviewedById);

public static class ListingPublication
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static async Task<User> RequireStaff(IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (!actor.UserId.HasValue || actor.Role is not ("Moderator" or "SuperAdmin")) throw new DomainException("Không có quyền kiểm duyệt dịch vụ.");
        var role = actor.Role == "Moderator" ? UserRole.Moderator : UserRole.SuperAdmin;
        return await db.Users.SingleOrDefaultAsync(x => x.Id == actor.UserId && x.IsActive && x.Role == role, ct)
            ?? throw new DomainException("Tài khoản kiểm duyệt không còn hoạt động hoặc đã đổi quyền.");
    }
    public static async Task<Vendor> RequireOwner(IApplicationDbContext db, ICurrentUserService actor, CancellationToken ct)
    {
        if (!actor.UserId.HasValue || actor.Role != "VendorOwner") throw new DomainException("Chỉ nhà cung cấp được quản lý dịch vụ của shop mình.");
        return await db.Vendors.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == actor.UserId && x.IsActive && x.User.IsActive && x.User.Role == UserRole.VendorOwner, ct)
            ?? throw new DomainException("Nhà cung cấp không còn hoạt động.");
    }
    public static async Task RequirePrivateRead(IApplicationDbContext db, ICurrentUserService actor, Listing listing, CancellationToken ct)
    {
        if (actor.Role is "Moderator" or "SuperAdmin") { await RequireStaff(db, actor, ct); return; }
        var vendor = await RequireOwner(db, actor, ct);
        if (listing.VendorId != vendor.Id) throw new NotFoundException("Dịch vụ không tồn tại.");
    }
    public static ListingRevisionDto ToRevisionDto(ListingRevision revision) => new(revision.Id, revision.Sequence,
        revision.Status, DateTime.SpecifyKind(revision.CreatedAt, DateTimeKind.Utc),
        revision.SubmittedAt.HasValue ? DateTime.SpecifyKind(revision.SubmittedAt.Value, DateTimeKind.Utc) : null,
        revision.ReviewedAt.HasValue ? DateTime.SpecifyKind(revision.ReviewedAt.Value, DateTimeKind.Utc) : null,
        revision.ReviewNote, revision.CreatedById, revision.ReviewedById);
    public static ListingRevisionContent Read(ListingRevision revision)
    {
        try
        {
            var content = JsonSerializer.Deserialize<ListingRevisionContent>(revision.ContentJson, JsonOptions);
            if (content == null || content.ImageUrls == null || content.ImageUrls.Count > 10) throw new DomainException("Không đọc được phiên bản dịch vụ.");
            return content;
        }
        catch (JsonException) { throw new DomainException("Phiên bản dịch vụ không hợp lệ. Không có thay đổi nào được xuất bản."); }
    }
    public static ListingRevisionContent Capture(Listing listing, UpdateListingCommand? update = null, ListingRevisionContent? previous = null) => new(
        update?.Title.Trim() ?? listing.Title, update?.MinPrice ?? listing.MinPrice, update?.MaxPrice ?? listing.MaxPrice,
        update?.Description.Trim() ?? listing.Description, update?.Location.Trim() ?? listing.Location,
        update?.ImageUrls == null ? previous?.ImageUrls ?? listing.Media.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Id).Select(x => x.MediaUrl).ToList()
            : ListingImageReferences.NormalizeRequired(update.ImageUrls),
        update?.ServiceDetails == null ? previous != null ? previous.ServiceDetailsJson : listing.ServiceDetailsJson : ListingServiceDetails.NormalizeAndSerialize(update.ServiceDetails),
        previous?.Pricing);
    public static async Task<ListingRevision> Append(IApplicationDbContext db, Listing listing, Guid actor, ListingRevisionContent content, bool submit, CancellationToken ct)
    {
        var previous = await db.ListingRevisions.Where(x => x.ListingId == listing.Id).OrderByDescending(x => x.Sequence).ToListAsync(ct);
        if (previous.Count == 0 && listing.Status is ListingStatus.Active or ListingStatus.Hidden)
        {
            var baselineContent = Capture(listing) with { Pricing = await ListingServicePricing.Current(db, listing, ct) };
            var baseline = ListingRevision.CaptureBaseline(listing.Id, JsonSerializer.Serialize(baselineContent, JsonOptions), actor);
            db.ListingRevisions.Add(baseline); previous.Add(baseline);
        }
        foreach (var old in previous.Where(x => x.Status is "Draft" or "Pending")) old.Supersede();
        var revision = ListingRevision.Create(listing.Id, previous.Count == 0 ? 1 : previous[0].Sequence + 1,
            JsonSerializer.Serialize(content, JsonOptions), actor, submit);
        db.ListingRevisions.Add(revision);
        db.AuditLogs.Add(AuditLog.Create(actor, "VendorOwner", submit ? "ListingRevision.Submit" : "ListingRevision.SaveDraft", nameof(ListingRevision), revision.Id.ToString(),
            JsonSerializer.Serialize(new { listing.Id, revision.Sequence })));
        return revision;
    }
    public static async Task Apply(IApplicationDbContext db, Listing listing, ListingRevision revision, CancellationToken ct)
    {
        var content = Read(revision);
        var validate = new UpdateListingCommandValidator().Validate(new UpdateListingCommand(listing.Id, content.Title, content.MinPrice, content.MaxPrice, content.Description, content.Location,
            content.ImageUrls.Count == 0 ? null : content.ImageUrls));
        if (!validate.IsValid) throw new DomainException(validate.Errors[0].ErrorMessage);
        if (content.ServiceDetailsJson != null && ListingServiceDetails.Read(content.ServiceDetailsJson) == null)
            throw new DomainException("Nội dung dịch vụ bổ sung trong phiên bản không hợp lệ.");
        listing.Update(content.Title, content.MinPrice, content.MaxPrice, content.Description, content.Location);
        listing.SetServiceDetailsJson(content.ServiceDetailsJson);
        if (content.Pricing != null) await ListingServicePricing.Apply(db, listing, content.Pricing, ct);
        var old = await db.ListingMedias.Where(x => x.ListingId == listing.Id).OrderByDescending(x => x.IsFeatured).ThenBy(x => x.DisplayOrder).ThenBy(x => x.Id).ToListAsync(ct);
        if (!old.Select(x => x.MediaUrl).SequenceEqual(content.ImageUrls))
        {
            db.ListingMedias.RemoveRange(old);
            for (var i = 0; i < content.ImageUrls.Count; i++) db.ListingMedias.Add(ListingMedia.Create(listing.Id, content.ImageUrls[i], null, displayOrder: i, isFeatured: i == 0));
        }
    }
    public static ListingDto ToDto(Listing listing) => new(listing.Id, listing.Title, listing.Slug, listing.MinPrice, listing.MaxPrice,
        listing.Description, listing.Location, listing.Status.ToString(), listing.RejectionReason, listing.CategoryId, listing.Category.Name,
        listing.VendorId, listing.Vendor.BrandName, listing.ViewCount, listing.CreatedAt, listing.ModeratedAt,
        listing.Media.OrderBy(x => x.DisplayOrder).Select(x => new ListingMediaDto(x.Id, x.MediaUrl, x.ThumbnailUrl, x.IsFeatured, x.DisplayOrder)).ToList(),
        ListingServiceDetails.Read(listing.ServiceDetailsJson), listing.CancellationPolicy, listing.Category.RequiresCommissionBasisApproval, listing.IsActive);
    public static ListingDto Preview(Listing listing, ListingRevision revision)
    {
        var content = Read(revision);
        return ToDto(listing) with { Title = content.Title, MinPrice = content.MinPrice, MaxPrice = content.MaxPrice,
            Description = content.Description, Location = content.Location, ServiceDetails = ListingServiceDetails.Read(content.ServiceDetailsJson),
            Media = content.ImageUrls.Select((url, index) => new ListingMediaDto(PreviewMediaId(revision.Id, index), url, null, index == 0, index)).ToList(),
            LatestRevision = ToRevisionDto(revision), Pricing = content.Pricing };
    }
    private static Guid PreviewMediaId(Guid revisionId, int index)
    {
        var bytes = revisionId.ToByteArray(); bytes[15] ^= (byte)(index + 1); return new Guid(bytes);
    }
    public static async Task CheckSubmission(IApplicationDbContext db, Listing listing, CancellationToken ct)
    {
        await ListingCategoryAccess.RequireForExistingAsync(db, listing.VendorId, listing.CategoryId, ct);
        var vendor = await db.Vendors.SingleAsync(x => x.Id == listing.VendorId, ct);
        if (!vendor.IsActive || !await db.Users.AnyAsync(x => x.Id == vendor.UserId && x.IsActive && x.Role == UserRole.VendorOwner, ct) ||
            !vendor.IsVerified || !await db.VendorTaxProfiles.AnyAsync(x => x.VendorId == vendor.Id && x.Status == VerificationStatus.Approved, ct))
            throw new DomainException("Hoàn thiện hồ sơ pháp lý/ngân hàng và được xác minh trước khi nộp/duyệt bài đăng.");
        if (!await db.Categories.AnyAsync(x => x.Id == listing.CategoryId && x.IsActive, ct)) throw new DomainException("Ngành dịch vụ không còn hoạt động.");
    }
}

public record GetListingEditorQuery(Guid Id, Guid? RevisionId = null) : IRequest<ListingDto>;
public class GetListingEditorQueryHandler(IApplicationDbContext db, ICurrentUserService actor) : IRequestHandler<GetListingEditorQuery, ListingDto>
{
    public async Task<ListingDto> Handle(GetListingEditorQuery request, CancellationToken ct)
    {
        var listing = await db.Listings.AsNoTracking().Include(x => x.Category).Include(x => x.Vendor).Include(x => x.Media)
            .SingleOrDefaultAsync(x => x.Id == request.Id, ct) ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        await ListingPublication.RequirePrivateRead(db, actor, listing, ct);
        var history = await db.ListingRevisions.AsNoTracking().Where(x => x.ListingId == listing.Id).OrderByDescending(x => x.Sequence).ToListAsync(ct);
        var latest = history.FirstOrDefault();
        var revision = request.RevisionId.HasValue ? history.FirstOrDefault(x => x.Id == request.RevisionId) ?? throw new NotFoundException("Phiên bản không tồn tại.")
            : (listing.Status is ListingStatus.Active or ListingStatus.Hidden) && (latest?.Status is "Draft" or "Pending" or "Rejected") ? latest : null;
        var dto = await ListingServicePricing.Enrich(db, listing,
            revision == null ? ListingPublication.ToDto(listing) : ListingPublication.Preview(listing, revision), ct);
        if (revision != null) dto = dto with { Pricing = ListingPublication.Read(revision).Pricing };
        return dto with
            { LatestRevision = revision == null ? latest == null ? null : ListingPublication.ToRevisionDto(latest) : ListingPublication.ToRevisionDto(revision),
                RevisionHistory = history.Select(ListingPublication.ToRevisionDto).ToList() };
    }
}
public record SetListingActivityCommand(Guid Id, bool IsActive) : IRequest;
public class SetListingActivityCommandHandler(IApplicationDbContext db, ICurrentUserService actor) : IRequestHandler<SetListingActivityCommand>
{
    public Task Handle(SetListingActivityCommand request, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var vendor = await ListingPublication.RequireOwner(db, actor, token);
        var listing = await db.Listings.SingleOrDefaultAsync(x => x.Id == request.Id && x.VendorId == vendor.Id, token)
            ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        listing.SetActivity(request.IsActive);
        db.AuditLogs.Add(AuditLog.Create(actor.UserId, actor.Role, "Listing.Activity", nameof(Listing), listing.Id.ToString(), JsonSerializer.Serialize(new { request.IsActive })));
        await db.SaveChangesAsync(token); return Unit.Value;
    }, ct);
}
