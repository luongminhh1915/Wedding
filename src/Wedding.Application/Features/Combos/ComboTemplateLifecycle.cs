using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

public record ComboTemplateContentDto(List<string>? GalleryUrls = null, string? Terms = null,
    DateTime? ValidFromUtc = null, DateTime? ValidUntilUtc = null, string? DetailedDescription = null);
public record ComboTemplateProposalDto(string Name, string Tier, string Style, string? Description,
    string? ImageUrl, bool IsActive, int SortOrder, List<ComboTemplateItemDto> Items, Guid OwnerVendorId,
    string? Region, decimal? SingleVendorDiscount, ComboTemplateContentDto? Content,
    string PricingMode = "Dynamic", decimal? ComboPrice = null, List<ComboComponentSnapshotDto>? Components = null);
public record ComboTemplateRevisionDto(Guid Id, int Sequence, string Status, Guid CreatedById, string CreatedByRole,
    DateTime CreatedAt, DateTime? SubmittedAt, Guid? ReviewedById, string? ReviewedByRole, DateTime? ReviewedAt,
    string? ReviewNote, ComboTemplateProposalDto? Proposal = null, DateTime? SupersededAt = null, string? SupersededFromStatus = null);

public static class ComboTemplateContent
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNameCaseInsensitive = true };

    public static ComboTemplateContentDto Normalize(ComboTemplateContentDto? content)
    {
        content ??= new();
        var terms = (content.Terms ?? "").Trim();
        if (terms.Length > 4000 || (content.DetailedDescription?.Length ?? 0) > 4000 || (content.GalleryUrls?.Count ?? 0) > 10)
            throw new DomainException("Điều khoản combo tối đa 4.000 ký tự, gallery tối đa 10 ảnh.");
        var gallery = (content.GalleryUrls ?? []).Select(reference =>
        {
            var value = (reference ?? "").Trim();
            if (value.StartsWith("/uploads/listings/", StringComparison.Ordinal) && ListingImageReferences.IsValid(value)) return value;
            var validated = ListingServiceDetails.Read(ListingServiceDetails.NormalizeAndSerialize(new(PortfolioUrls: [value])));
            return validated!.PortfolioUrls![0];
        }).Distinct().ToList();
        var from = AsUtc(content.ValidFromUtc); var until = AsUtc(content.ValidUntilUtc);
        if (from.HasValue && until.HasValue && until <= from)
            throw new DomainException("Thời hạn kết thúc combo phải sau thời điểm bắt đầu.");
        return new(gallery, terms, from, until, content.DetailedDescription?.Trim());
    }
    internal static DateTime? AsUtc(DateTime? value) => value.HasValue ? value.Value.Kind switch
    { DateTimeKind.Local => value.Value.ToUniversalTime(), _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) } : null;
    public static string Serialize(ComboTemplateContentDto content)
    {
        var json = JsonSerializer.Serialize(Normalize(content), JsonOptions);
        if (json.Length > 20000) throw new DomainException("Nội dung bổ sung combo tối đa 20.000 ký tự lưu trữ.");
        return json;
    }
    public static ComboTemplateContentDto Read(ComboTemplate template)
    {
        try
        {
            ComboTemplateContentDto? content = string.IsNullOrWhiteSpace(template.ContentJson) ? new() : JsonSerializer.Deserialize<ComboTemplateContentDto>(template.ContentJson, JsonOptions);
            return Normalize(content) with { ValidFromUtc = AsUtc(template.ValidFromUtc), ValidUntilUtc = AsUtc(template.ValidUntilUtc) };
        }
        catch (JsonException) { return new([], null, AsUtc(template.ValidFromUtc), AsUtc(template.ValidUntilUtc)); }
        catch (DomainException) { return new([], null, AsUtc(template.ValidFromUtc), AsUtc(template.ValidUntilUtc)); }
    }
}

public static class ComboTemplateAvailability
{
    public static IQueryable<ComboTemplate> PubliclyAvailable(this IQueryable<ComboTemplate> query)
    {
        var now = DateTime.UtcNow;
        return query.Where(template => template.IsActive && template.ModerationStatus == "Approved" && template.OwnerVendorId != null &&
            (!template.ValidFromUtc.HasValue || template.ValidFromUtc <= now) &&
            (!template.ValidUntilUtc.HasValue || template.ValidUntilUtc > now));
    }
    public static async Task<bool> ComponentsEligibleAsync(IApplicationDbContext db, ComboTemplate template, CancellationToken ct)
    {
        if (ComboFixedPackages.IsFixed(template)) return await ComboFixedPackages.MatchesApprovedSourcesAsync(db, template, ct);
        if (!await ComboVendorPolicy.IsOwnedTemplateAsync(db, template, ct)) return false;
        var ids = new HashSet<Guid>();
        foreach (var slot in template.Items)
        {
            if (!ComboVendorPolicy.TryAllowedIds(slot, out var allowed)) return false;
            ids.Add(slot.ServiceItemId!.Value); ids.UnionWith(allowed);
        }
        return await ComboPublicCatalog.Eligible(db).CountAsync(item => ids.Contains(item.Id), ct) == ids.Count;
    }
}

internal record ComboTemplateActor(Guid Id, string Role, Guid? VendorId);
internal static class ComboTemplateLifecycle
{
    internal static async Task<ComboTemplateActor> ActorAsync(IApplicationDbContext db, ICurrentUserService user, bool staffOnly, CancellationToken ct)
    {
        if (!user.UserId.HasValue || user.UserId == Guid.Empty) throw new DomainException("Bạn cần đăng nhập để quản lý combo.");
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(actor => actor.Id == user.UserId && actor.IsActive, ct);
        if (actor == null || actor.Role.ToString() != user.Role || actor.Role is not (UserRole.VendorOwner or UserRole.Moderator or UserRole.SuperAdmin) ||
            staffOnly && actor.Role == UserRole.VendorOwner) throw new DomainException("Bạn không có quyền quản lý hoặc kiểm duyệt combo.");
        Guid? vendorId = null;
        if (actor.Role == UserRole.VendorOwner)
            vendorId = await db.Vendors.Where(vendor => vendor.UserId == actor.Id && vendor.IsActive)
                .Select(vendor => (Guid?)vendor.Id).SingleOrDefaultAsync(ct) ?? throw new DomainException("Shop đang hoạt động không tồn tại.");
        return new(actor.Id, actor.Role.ToString(), vendorId);
    }
    internal static void Own(ComboTemplate template, ComboTemplateActor actor)
    {
        if (actor.VendorId.HasValue && template.OwnerVendorId != actor.VendorId)
            throw new NotFoundException("Không tìm thấy combo thuộc shop của bạn.");
    }
    internal static ComboTemplateProposalDto ReadProposal(ComboTemplateRevision revision)
    {
        try { return JsonSerializer.Deserialize<ComboTemplateProposalDto>(revision.SnapshotJson, ComboTemplateContent.JsonOptions)
            ?? throw new DomainException("Nội dung phiên bản combo không hợp lệ."); }
        catch (JsonException) { throw new DomainException("Không đọc được nội dung phiên bản combo."); }
    }
    internal static ComboTemplateRevisionDto RevisionDto(ComboTemplateRevision revision, bool includeProposal = false) => new(
        revision.Id, revision.Sequence, revision.Status, revision.CreatedById, revision.CreatedByRole, ComboTemplateContent.AsUtc(revision.CreatedAt)!.Value,
        ComboTemplateContent.AsUtc(revision.SubmittedAt), revision.ReviewedById, revision.ReviewedByRole, ComboTemplateContent.AsUtc(revision.ReviewedAt), revision.ReviewNote,
        includeProposal ? ReadProposal(revision) : null, ComboTemplateContent.AsUtc(revision.SupersededAt), revision.SupersededFromStatus);
    internal static async Task<List<ServiceItem>> ValidateAsync(IApplicationDbContext db, ComboTemplateProposalDto proposal, CancellationToken ct)
    {
        ComboFixedPackages.ValidatePrice(proposal.PricingMode, proposal.ComboPrice);
        var fixedPackage = proposal.PricingMode == "FixedPrice";
        if (proposal.Items is not { Count: >= 1 and <= 12 } ||
            proposal.Items.Any(slot => slot == null || !slot.ServiceItemId.HasValue || slot.ServiceItemId == Guid.Empty || (slot.AllowedItemIds?.Count ?? 0) > 100) ||
            !fixedPackage && proposal.Items.Select(slot => slot.CategoryId).Distinct().Count() != proposal.Items.Count)
            throw new DomainException("Combo cần từ 1 đến 12 ngành, mỗi ngành có một dịch vụ mặc định đã duyệt và tối đa 100 lựa chọn.");
        if (fixedPackage && (proposal.Items.Count < 2 || proposal.Items.Select(slot => slot.ServiceItemId).Distinct().Count() != proposal.Items.Count ||
            proposal.Items.Any(slot => slot.IsOptional || (slot.AllowedItemIds?.Count ?? 0) != 0)))
            throw new DomainException("Combo cố định cần ít nhất hai dịch vụ độc lập khác nhau; không có dịch vụ tùy chọn hoặc thay thế.");
        if (!fixedPackage && proposal.SingleVendorDiscount is < 0 or > .30m) throw new DomainException("Giảm giá combo phải từ 0 đến 30%.");
        var ids = proposal.Items.SelectMany(slot => (slot.AllowedItemIds ?? []).Append(slot.ServiceItemId!.Value)).Distinct().ToList();
        var items = await db.ServiceItems.Include(item => item.Vendor).ThenInclude(vendor => vendor.User).Include(item => item.Listing)
            .ThenInclude(listing => listing.Media).Include(item => item.Variants).Include(item => item.AddOns)
            .Where(item => ids.Contains(item.Id) && item.IsActive && item.ModerationStatus == "Approved" && item.Listing.Status == ListingStatus.Active && item.Listing.IsActive).ToListAsync(ct);
        if (items.Count != ids.Count || items.Any(item => item.VendorId != proposal.OwnerVendorId || !item.Vendor.IsActive ||
            !item.Vendor.User.IsActive || !item.Vendor.IsVerified || !item.Vendor.AllowInCombos))
            throw new DomainException("Mọi dịch vụ/lựa chọn phải đã duyệt, đang hoạt động và thuộc cùng NCC đã xác minh, đồng ý tham gia combo.");
        if (fixedPackage && items.Select(item => item.ListingId).Distinct().Count() != items.Count)
            throw new DomainException("Hai cấu hình của cùng một dịch vụ gốc không được tính là hai dịch vụ độc lập.");
        if (fixedPackage && items.Any(item => item.Listing.PricingServiceItemId != item.Id))
            throw new DomainException("Chọn cấu hình giá đã được duyệt gắn với dịch vụ gốc trong Dịch vụ của tôi.");
        if (fixedPackage && items.Any(item => item.Unit != "Package"))
            throw new DomainException("Combo cố định hiện chỉ ghép dịch vụ có đơn vị trọn gói (Package). Dịch vụ theo bàn/khách/giờ cần thỏa thuận số lượng và báo giá riêng.");
        if (!fixedPackage && proposal.SingleVendorDiscount > items[0].Vendor.MaxComboDiscount)
            throw new DomainException("Ưu đãi combo vượt mức giảm tối đa NCC đã đồng ý.");
        foreach (var slot in proposal.Items)
            if (items.Where(item => (slot.AllowedItemIds ?? []).Append(slot.ServiceItemId!.Value).Contains(item.Id)).Any(item => item.CategoryId != slot.CategoryId))
                throw new DomainException("Lựa chọn thay thế phải cùng ngành với dịch vụ mặc định.");
        if (fixedPackage && proposal.Components != null && (proposal.Components.Count != items.Count ||
            items.Any(item => proposal.Components.SingleOrDefault(component => component.ServiceItemId == item.Id)?.SourceVersion != ComboFixedPackages.Capture(item).SourceVersion)))
            throw new DomainException("Dịch vụ thành phần đã thay đổi. Hãy lưu và gửi lại combo để duyệt nội dung mới.");
        return items;
    }
    internal static async Task<ComboTemplateDto> PrivateDtoAsync(IApplicationDbContext db, ComboTemplate template, CancellationToken ct)
    {
        var revisions = await db.ComboTemplateRevisions.AsNoTracking().Where(revision => revision.ComboTemplateId == template.Id)
            .OrderByDescending(revision => revision.Sequence).ToListAsync(ct);
        var ownerName = await db.Vendors.Where(vendor => vendor.Id == template.OwnerVendorId).Select(vendor => vendor.BrandName).FirstOrDefaultAsync(ct);
        var dto = ComboMap.ToDto(template) with { OwnerVendorName = ownerName,
            LatestRevision = revisions.Count == 0 ? null : RevisionDto(revisions[0], true),
            RevisionHistory = revisions.Select(revision => RevisionDto(revision)).ToList() };
        if (template.ModerationStatus != "Approved" && revisions.Count > 0)
        {
            var proposal = ReadProposal(revisions[0]);
            dto = ProposalDto(dto, proposal);
        }
        return dto with { RequiresComponentReview = template.ModerationStatus == "Approved" && ComboFixedPackages.IsFixed(template) &&
            !await ComboTemplateAvailability.ComponentsEligibleAsync(db, template, ct) };
    }
    internal static ComboTemplateDto ProposalDto(ComboTemplateDto dto, ComboTemplateProposalDto proposal)
    {
        var original = proposal.Components?.Sum(component => component.BasePrice);
        return dto with { Name = proposal.Name, Tier = proposal.Tier, Style = proposal.Style, Description = proposal.Description,
            ImageUrl = proposal.ImageUrl, Region = proposal.Region, SingleVendorDiscount = proposal.SingleVendorDiscount,
            Items = proposal.Items, OwnerVendorId = proposal.OwnerVendorId, Content = proposal.Content, SortOrder = proposal.SortOrder,
            PricingMode = proposal.PricingMode, ComboPrice = proposal.ComboPrice, Components = proposal.Components, OriginalPrice = original,
            EstimatedTotal = original ?? dto.EstimatedTotal, SavingsAmount = original > proposal.ComboPrice ? original - proposal.ComboPrice : null,
            SavingsPercentage = original > 0 && original > proposal.ComboPrice ? (original - proposal.ComboPrice) / original * 100 : null };
    }
    internal static async Task<ComboTemplateRevision> AddRevisionAsync(IApplicationDbContext db, ComboTemplate template,
        ComboTemplateProposalDto proposal, ComboTemplateActor actor, bool submit, CancellationToken ct)
    {
        var latest = await db.ComboTemplateRevisions.Where(revision => revision.ComboTemplateId == template.Id)
            .OrderByDescending(revision => revision.Sequence).FirstOrDefaultAsync(ct);
        if (latest?.Status is "Draft" or "Pending" or "Rejected") latest.Supersede();
        var json = JsonSerializer.Serialize(proposal, ComboTemplateContent.JsonOptions);
        var revision = ComboTemplateRevision.Create(template.Id, (latest?.Sequence ?? 0) + 1, json, actor.Id, actor.Role, submit);
        db.ComboTemplateRevisions.Add(revision);
        if (template.ModerationStatus != "Approved") template.StageUnpublished(proposal.OwnerVendorId, submit ? "Pending" : "Draft");
        return revision;
    }
    internal static async Task ApplyAsync(IApplicationDbContext db, ComboTemplate template, ComboTemplateProposalDto proposal, string? note, CancellationToken ct)
    {
        var items = await ValidateAsync(db, proposal, ct);
        var active = template.ModerationStatus == "Approved" ? template.IsActive : proposal.IsActive;
        template.Update(proposal.Name, proposal.Tier, proposal.Style, proposal.Description, proposal.ImageUrl, active, proposal.SortOrder);
        var content = ComboTemplateContent.Normalize(proposal.Content);
        template.Publish(proposal.OwnerVendorId, proposal.Region, proposal.SingleVendorDiscount,
            ComboTemplateContent.Serialize(content), content.ValidFromUtc, content.ValidUntilUtc, active, note);
        template.SetPackagePricing(proposal.PricingMode, proposal.ComboPrice,
            proposal.PricingMode == "FixedPrice" ? items.Sum(item => item.BasePrice) : null);
        foreach (var old in template.Items.ToList()) db.ComboTemplateItems.Remove(old);
        template.Items.Clear();
        foreach (var slot in proposal.Items)
        {
            var service = items.Single(item => item.Id == slot.ServiceItemId);
            var item = ComboTemplateItem.Create(template.Id, slot.CategoryId, service.ListingId, slot.IsOptional);
            item.ConfigureItems(service.Id, JsonSerializer.Serialize(slot.AllowedItemIds ?? []));
            if (proposal.PricingMode == "FixedPrice") item.FreezeComponent(ComboFixedPackages.Serialize(ComboFixedPackages.Capture(service)));
            template.Items.Add(item);
            db.ComboTemplateItems.Add(item);
        }
    }
}

public record SubmitComboTemplateCommand(Guid Id) : IRequest<ComboTemplateDto>;
public class SubmitComboTemplateCommandHandler(IApplicationDbContext db, ICurrentUserService user, IAuditLogger audit)
    : IRequestHandler<SubmitComboTemplateCommand, ComboTemplateDto>
{
    public Task<ComboTemplateDto> Handle(SubmitComboTemplateCommand request, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var actor = await ComboTemplateLifecycle.ActorAsync(db, user, false, token);
        var template = await db.ComboTemplates.Include(template => template.Items).ThenInclude(item => item.Listing)
            .FirstOrDefaultAsync(template => template.Id == request.Id, token) ?? throw new NotFoundException("Combo không tồn tại.");
        ComboTemplateLifecycle.Own(template, actor);
        var latest = await db.ComboTemplateRevisions.Where(revision => revision.ComboTemplateId == template.Id)
            .OrderByDescending(revision => revision.Sequence).FirstOrDefaultAsync(token);
        if (latest == null || latest.Status is not ("Draft" or "Rejected")) throw new DomainException("Chỉ gửi bản nháp hoặc bản đã bị từ chối; cần lưu thay đổi trước.");
        var proposal = ComboTemplateLifecycle.ReadProposal(latest);
        await ComboTemplateLifecycle.ValidateAsync(db, proposal, token);
        var revision = await ComboTemplateLifecycle.AddRevisionAsync(db, template, proposal, actor, true, token);
        await audit.LogAsync("ComboTemplate.Submitted", nameof(ComboTemplate), template.Id.ToString(), new { revisionId = revision.Id });
        await db.SaveChangesAsync(token);
        return await ComboTemplateLifecycle.PrivateDtoAsync(db, template, token);
    }, ct);
}

public record SetComboTemplateActivityCommand(Guid Id, bool IsActive) : IRequest<bool>;
public class SetComboTemplateActivityCommandHandler(IApplicationDbContext db, ICurrentUserService user, IAuditLogger audit)
    : IRequestHandler<SetComboTemplateActivityCommand, bool>
{
    public Task<bool> Handle(SetComboTemplateActivityCommand request, CancellationToken ct) => db.ExecuteSerializableAsync(async token =>
    {
        var actor = await ComboTemplateLifecycle.ActorAsync(db, user, false, token);
        var template = await db.ComboTemplates.Include(template => template.Items).FirstOrDefaultAsync(template => template.Id == request.Id, token)
            ?? throw new NotFoundException("Combo không tồn tại.");
        ComboTemplateLifecycle.Own(template, actor);
        if (request.IsActive && (template.ModerationStatus != "Approved" || template.ValidUntilUtc <= DateTime.UtcNow ||
            !await ComboTemplateAvailability.ComponentsEligibleAsync(db, template, token)))
            throw new DomainException("Chỉ kích hoạt combo đã duyệt, chưa hết hạn và còn đầy đủ dịch vụ hợp lệ.");
        template.SetActivity(request.IsActive);
        await audit.LogAsync("ComboTemplate.ActivityChanged", nameof(ComboTemplate), template.Id.ToString(), new { request.IsActive });
        await db.SaveChangesAsync(token); return true;
    }, ct);
}

public record GetComboTemplatePreviewQuery(Guid Id, Guid? RevisionId = null) : IRequest<ComboTemplateDto>;
public class GetComboTemplatePreviewQueryHandler(IApplicationDbContext db, ICurrentUserService user)
    : IRequestHandler<GetComboTemplatePreviewQuery, ComboTemplateDto>
{
    public async Task<ComboTemplateDto> Handle(GetComboTemplatePreviewQuery request, CancellationToken ct)
    {
        var actor = await ComboTemplateLifecycle.ActorAsync(db, user, false, ct);
        var template = await db.ComboTemplates.AsNoTracking().Include(template => template.Items).ThenInclude(item => item.Listing)
            .FirstOrDefaultAsync(template => template.Id == request.Id, ct) ?? throw new NotFoundException("Combo không tồn tại.");
        ComboTemplateLifecycle.Own(template, actor);
        var revisionQuery = db.ComboTemplateRevisions.AsNoTracking().Where(revision => revision.ComboTemplateId == template.Id);
        var revision = request.RevisionId.HasValue ? await revisionQuery.FirstOrDefaultAsync(revision => revision.Id == request.RevisionId, ct)
            ?? throw new NotFoundException("Phiên bản combo không tồn tại.") : await revisionQuery.OrderByDescending(revision => revision.Sequence).FirstOrDefaultAsync(ct);
        var dto = await ComboTemplateLifecycle.PrivateDtoAsync(db, template, ct);
        if (revision != null)
        {
            var proposal = ComboTemplateLifecycle.ReadProposal(revision);
            dto = ComboTemplateLifecycle.ProposalDto(dto, proposal) with { ModerationStatus = revision.Status, LatestRevision = ComboTemplateLifecycle.RevisionDto(revision, true) };
        }
        if (dto.PricingMode == "FixedPrice" && dto.Components != null)
            return dto with { PreviewOptions = dto.Components.Select(component => ComboFixedPackages.Option(component, dto.OwnerVendorName ?? "", true, 0)).ToList() };
        var ids = dto.Items.Where(slot => slot.ServiceItemId.HasValue).SelectMany(slot => (slot.AllowedItemIds ?? []).Append(slot.ServiceItemId!.Value)).Distinct().ToList();
        var options = await db.ServiceItems.AsNoTracking().Include(item => item.Vendor).Include(item => item.Listing).ThenInclude(listing => listing.Media)
            .Include(item => item.Variants).Include(item => item.AddOns).Where(item => ids.Contains(item.Id) && item.VendorId == dto.OwnerVendorId).ToListAsync(ct);
        return dto with { EstimatedTotal = dto.Items.Where(slot => !slot.IsOptional).Sum(slot =>
            options.Where(item => item.Id == slot.ServiceItemId).Select(item => item.BasePrice * ServiceItem.QuantityFor(item.Unit, 200)).FirstOrDefault()),
            PreviewOptions = options.Select(item => new ComboOptionDto(item.ListingId, item.VendorId, item.Vendor.BrandName,
            item.Vendor.IsVerified, item.Vendor.RatingAvg, item.Name, item.BasePrice, item.BasePrice, item.Regions,
            item.Listing.Media.OrderByDescending(media => media.IsFeatured).ThenBy(media => media.DisplayOrder).Select(media => media.MediaUrl).FirstOrDefault(),
            item.Id, item.Unit, item.MinGuests, item.MaxGuests, item.CancellationPolicy, CatalogMap.ToDto(item).Variants, CatalogMap.ToDto(item).AddOns,
            item.Listing.Media.Select(media => media.MediaUrl).Take(10).ToList(), item.Listing.Description)).ToList() };
    }
}
