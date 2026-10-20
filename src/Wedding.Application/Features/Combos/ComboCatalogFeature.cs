using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Application.Features.Listings.Commands.SubmitForApproval;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

public record VariantDto(Guid? Id, string Name, decimal PriceDelta);
public record AddOnDto(Guid? Id, string Name, decimal Price, bool PerUnit);
public record ServiceItemDto(Guid Id, Guid ListingId, Guid CategoryId, Guid VendorId, string VendorName, string Name,
    decimal BasePrice, string Unit, int MinGuests, int MaxGuests, string Regions, string CancellationPolicy,
    string ModerationStatus, string? ReviewNote, bool IsActive, int MaxBookingsPerDay, List<VariantDto> Variants, List<AddOnDto> AddOns,
    string ReviewVersion = "", string? ImageUrl = null, string? CategoryName = null,
    string? ListingStatus = null, bool ListingIsActive = true, bool IsCanonical = false);
public record ComboParticipationDto(bool AllowInCombos, decimal MaxComboDiscount);
internal static class CatalogMap
{
    public static ServiceItemDto ToDto(ServiceItem i) => new(i.Id, i.ListingId, i.CategoryId, i.VendorId, i.Vendor?.BrandName ?? "", i.Name,
        i.BasePrice, i.Unit, i.MinGuests, i.MaxGuests, i.Regions, i.CancellationPolicy, i.ModerationStatus, i.ReviewNote, i.IsActive,
        i.MaxBookingsPerDay, i.Variants.Select(v => new VariantDto(v.Id, v.Name, v.PriceDelta)).ToList(), i.AddOns.Select(a => new AddOnDto(a.Id, a.Name, a.Price, a.PerUnit)).ToList(),
        ReviewVersion(i), i.Listing?.Media.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.DisplayOrder).Select(x => x.ThumbnailUrl ?? x.MediaUrl).FirstOrDefault(),
        i.Listing?.Category?.Name, i.Listing?.Status.ToString(), i.Listing?.IsActive ?? false, i.Listing?.PricingServiceItemId == i.Id);

    internal static string ReviewVersion(ServiceItem item)
    {
        // An opaque content version, not a client-authored approval claim. Ticks are unchanged by SQL datetime2's unspecified Kind.
        // Money is already whole VND; canonical text avoids decimal JSON scale changing after a decimal(18,0) SQL round-trip.
        static string Money(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);
        var snapshot = JsonSerializer.Serialize(new
        {
            item.Id, item.ListingId, item.CategoryId, item.VendorId, item.Name, BasePrice = Money(item.BasePrice), item.Unit,
            item.MinGuests, item.MaxGuests, item.Regions, item.CancellationPolicy, item.ModerationStatus, item.ReviewNote,
            item.IsActive, item.MaxBookingsPerDay, CreatedAtTicks = item.CreatedAt.Ticks, UpdatedAtTicks = item.UpdatedAt?.Ticks,
            Variants = item.Variants.OrderBy(variant => variant.Id).Select(variant => new { variant.Id, variant.Name, PriceDelta = Money(variant.PriceDelta) }),
            AddOns = item.AddOns.OrderBy(addOn => addOn.Id).Select(addOn => new { addOn.Id, addOn.Name, Price = Money(addOn.Price), addOn.PerUnit })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    }
}
internal static class ServiceItemAccess
{
    internal static async Task<ComboTemplateActor> OwnerAsync(IApplicationDbContext db, ICurrentUserService user, CancellationToken ct)
    {
        var actor = await ComboTemplateLifecycle.ActorAsync(db, user, false, ct);
        if (!actor.VendorId.HasValue) throw new DomainException("Chỉ nhà cung cấp được quản lý dịch vụ và chính sách combo của shop mình.");
        return actor;
    }

    internal static SaveServiceItemCommand Normalize(SaveServiceItemCommand request)
    {
        static string Text(string? value, int limit, string label)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > limit)
                throw new DomainException($"{label} là bắt buộc và tối đa {limit} ký tự.");
            return value.Trim();
        }
        if ((request.Variants?.Count ?? 0) > 20 || (request.AddOns?.Count ?? 0) > 30)
            throw new DomainException("Tối đa 20 biến thể và 30 món kèm.");
        if (request.Variants?.Any(variant => variant == null) == true || request.AddOns?.Any(addOn => addOn == null) == true)
            throw new DomainException("Biến thể và món kèm không được chứa phần tử rỗng.");
        var normalized = request with
        {
            Name = Text(request.Name, 250, "Tên dịch vụ"),
            Regions = Text(request.Regions, 1000, "Khu vực phục vụ"),
            CancellationPolicy = Text(request.CancellationPolicy, 4000, "Chính sách hủy"),
            Variants = (request.Variants ?? []).Select(variant => variant with { Name = Text(variant.Name, 200, "Tên biến thể") }).ToList(),
            AddOns = (request.AddOns ?? []).Select(addOn => addOn with { Name = Text(addOn.Name, 200, "Tên món kèm") }).ToList()
        };
        // Validate every value using the existing domain rules before changing a tracked item or its children.
        var candidate = ServiceItem.Create(Guid.Empty, Guid.Empty, Guid.Empty, normalized.Name, normalized.BasePrice,
            normalized.Unit, normalized.MinGuests, normalized.MaxGuests, normalized.Regions, normalized.CancellationPolicy);
        candidate.SetDailyCapacity(normalized.MaxBookingsPerDay);
        foreach (var variant in normalized.Variants)
        {
            _ = ItemVariant.Create(Guid.Empty, variant.Name, variant.PriceDelta);
            if (normalized.BasePrice + variant.PriceDelta < 0) throw new DomainException("Giá sau biến thể không được âm.");
        }
        foreach (var addOn in normalized.AddOns) _ = ItemAddOn.Create(Guid.Empty, addOn.Name, addOn.Price, addOn.PerUnit);
        return normalized;
    }

    internal static async Task CheckApprovalAsync(IApplicationDbContext db, ServiceItem item, CancellationToken ct)
    {
        var listing = await db.Listings.SingleOrDefaultAsync(listing => listing.Id == item.ListingId, ct);
        if (listing == null || listing.VendorId != item.VendorId || listing.CategoryId != item.CategoryId ||
            listing.Status != ListingStatus.Active || !listing.IsActive)
            throw new DomainException("Dịch vụ phải thuộc đúng bài đăng đã duyệt, đang hoạt động và đúng ngành của shop.");
        // Participation consent affects public combo availability, not approval of a standalone catalog component.
        await ListingPublication.CheckSubmission(db, listing, ct);
    }
}
public record GetServiceItemsQuery(bool Moderation = false) : IRequest<List<ServiceItemDto>>;
public class GetServiceItemsQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetServiceItemsQuery, List<ServiceItemDto>>
{
    public async Task<List<ServiceItemDto>> Handle(GetServiceItemsQuery r, CancellationToken ct)
    {
        var actor = r.Moderation
            ? await ComboTemplateLifecycle.ActorAsync(context, user, true, ct)
            : await ServiceItemAccess.OwnerAsync(context, user, ct);
        var q = context.ServiceItems.AsNoTracking().Include(i => i.Vendor).Include(i => i.Variants).Include(i => i.AddOns)
            .Include(i => i.Listing).ThenInclude(l => l.Category).Include(i => i.Listing).ThenInclude(l => l.Media).AsQueryable();
        if (!r.Moderation) q = q.Where(i => i.VendorId == actor.VendorId);
        else q = q.Where(i => i.Listing.PricingServiceItemId != i.Id);
        return (await q.OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id).ToListAsync(ct)).Select(CatalogMap.ToDto).ToList();
    }
}
public record SaveServiceItemCommand(Guid? Id, Guid ListingId, string Name, decimal BasePrice, string Unit, int MinGuests, int MaxGuests,
    string Regions, string CancellationPolicy, bool IsActive, List<VariantDto> Variants, List<AddOnDto> AddOns,
    bool SubmitForApproval = false, int MaxBookingsPerDay = 1) : IRequest<ServiceItemDto>;
public class SaveServiceItemCommandHandler(IApplicationDbContext context, ICurrentUserService user, IAuditLogger audit) : IRequestHandler<SaveServiceItemCommand, ServiceItemDto>
{
    public Task<ServiceItemDto> Handle(SaveServiceItemCommand request, CancellationToken ct) => context.ExecuteSerializableAsync(async token =>
    {
        var actor = await ServiceItemAccess.OwnerAsync(context, user, token);
        var r = ServiceItemAccess.Normalize(request);
        var vendor = await context.Vendors.SingleAsync(v => v.Id == actor.VendorId, token);
        var listing = await context.Listings.FirstOrDefaultAsync(l => l.Id == r.ListingId && l.VendorId == vendor.Id, token) ?? throw new DomainException("Dịch vụ phải thuộc bài đăng của bạn.");
        var existing = await context.ServiceItems.Include(i => i.Variants).Include(i => i.AddOns).Where(i => i.ListingId == listing.Id && i.VendorId == vendor.Id).ToListAsync(token);
        if (!r.Id.HasValue && existing.Count > 1) throw new DomainException("Có nhiều dữ liệu giá cũ. Hãy chọn rõ dịch vụ để chỉnh sửa trong Dịch vụ của tôi.");
        var item = r.Id.HasValue ? await context.ServiceItems.Include(i => i.Variants).Include(i => i.AddOns).FirstOrDefaultAsync(i => i.Id == r.Id && i.VendorId == vendor.Id, token) ?? throw new NotFoundException("Dịch vụ không tồn tại.")
            : existing.SingleOrDefault() ?? ServiceItem.Create(vendor.Id, listing.Id, listing.CategoryId, r.Name, r.BasePrice, r.Unit, r.MinGuests, r.MaxGuests, r.Regions, r.CancellationPolicy);
        if (item.ListingId != r.ListingId) throw new DomainException("Không được đổi bài đăng của dịch vụ đã tạo.");
        if (item.CategoryId != listing.CategoryId) throw new DomainException("Ngành dịch vụ không khớp với bài đăng của shop.");
        if (listing.PricingServiceItemId == item.Id)
        {
            // Compatible old endpoint follows the one source review workflow; it cannot rewrite a published facet directly.
            var pricing = new ServicePricingDto(r.BasePrice, r.Unit, r.MinGuests, r.MaxGuests, r.Regions,
                r.CancellationPolicy, r.MaxBookingsPerDay, item.Id, request.Variants, request.AddOns);
            await new UpdateListingCommandHandler(context, user).Handle(new(listing.Id, r.Name, listing.MinPrice,
                listing.MaxPrice, listing.Description, listing.Location, Pricing: pricing), token);
            if (r.SubmitForApproval) await new SubmitForApprovalCommandHandler(context, user).Handle(new(listing.Id), token);
            return CatalogMap.ToDto(item) with { VendorName = vendor.BrandName };
        }
        if (listing.Status != ListingStatus.Active || !listing.IsActive) throw new DomainException("Dịch vụ cũ phải thuộc bài đăng Active của bạn.");
        if (!vendor.IsVerified) throw new DomainException("NCC phải được xác minh trước khi quản lý dịch vụ.");
        ListingServicePricing.ValidateChildIds(existing.SingleOrDefault(x => x.Id == item.Id), r.Variants, r.AddOns);
        var changed = !ListingServicePricing.SameValues(item, r.Name, r.BasePrice, r.Unit, r.MinGuests, r.MaxGuests, r.Regions, r.CancellationPolicy, r.IsActive, r.MaxBookingsPerDay);
        if (changed) item.Update(r.Name, r.BasePrice, r.Unit, r.MinGuests, r.MaxGuests, r.Regions, r.CancellationPolicy, r.IsActive);
        item.SetDailyCapacity(r.MaxBookingsPerDay);
        if (ListingServicePricing.UpdateChildren(context, item, r.Variants, r.AddOns)) changed = true;
        if (changed) item.SynchronizeListingModeration("Draft");
        if (r.SubmitForApproval) item.Submit(); if (!existing.Any(x => x.Id == item.Id)) context.ServiceItems.Add(item);
        await audit.LogAsync("ServiceItem.Save", nameof(ServiceItem), item.Id.ToString(),
            new { r.SubmitForApproval, vendorId = vendor.Id, actorId = actor.Id, actorRole = actor.Role });
        await context.SaveChangesAsync(token); return CatalogMap.ToDto(item) with { VendorName = vendor.BrandName };
    }, ct);
}
public record ReviewServiceItemCommand(Guid Id, bool Approve, string? Note, string? ReviewVersion = null) : IRequest<bool>;
public class ReviewServiceItemCommandHandler(IApplicationDbContext context, IAuditLogger audit, ICurrentUserService user) : IRequestHandler<ReviewServiceItemCommand, bool>
{
    public Task<bool> Handle(ReviewServiceItemCommand r, CancellationToken ct) => context.ExecuteSerializableAsync(async token =>
    {
        var actor = await ComboTemplateLifecycle.ActorAsync(context, user, true, token);
        var note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
        if ((!r.Approve && note == null) || note?.Length > 2000)
            throw new DomainException("Lý do từ chối cần từ 1 đến 2.000 ký tự; ghi chú tối đa 2.000 ký tự.");
        var item = await context.ServiceItems.Include(item => item.Variants).Include(item => item.AddOns)
            .FirstOrDefaultAsync(i => i.Id == r.Id, token) ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        if (await context.Listings.AnyAsync(listing => listing.Id == item.ListingId && listing.PricingServiceItemId == item.Id, token))
            throw new DomainException("Dịch vụ này được kiểm duyệt cùng nguồn trong Dịch vụ của tôi. Hãy duyệt phiên bản dịch vụ tương ứng.");
        var version = CatalogMap.ReviewVersion(item);
        if (string.IsNullOrWhiteSpace(r.ReviewVersion) || !string.Equals(r.ReviewVersion, version, StringComparison.Ordinal))
            throw new DomainException("Nội dung dịch vụ đã thay đổi hoặc thiếu phiên bản kiểm duyệt. Hãy tải lại hàng chờ trước khi duyệt.");
        if (r.Approve) await ServiceItemAccess.CheckApprovalAsync(context, item, token);
        item.Review(r.Approve, note);
        var ownerUserId = await context.Vendors.Where(vendor => vendor.Id == item.VendorId).Select(vendor => vendor.UserId).SingleAsync(token);
        // There is no service-item context in the notification allowlist; never masquerade as a lead or financial resource.
        context.Notifications.Add(Notification.Create(ownerUserId, "InApp", "",
            $"Dịch vụ combo {item.Name}: {(r.Approve ? "đã được duyệt" : "bị từ chối")}.{(note == null ? "" : " " + note)}",
            $"service-item-review:{item.Id:D}:{version}:{(r.Approve ? "approved" : "rejected")}"));
        await audit.LogAsync("ServiceItem.Review", nameof(ServiceItem), item.Id.ToString(),
            new { r.Approve, note, reviewVersion = version, actorId = actor.Id, actorRole = actor.Role });
        await context.SaveChangesAsync(token); return true;
    }, ct);
}
public record GetComboParticipationQuery : IRequest<ComboParticipationDto>;
public class GetComboParticipationQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetComboParticipationQuery, ComboParticipationDto>
{
    public async Task<ComboParticipationDto> Handle(GetComboParticipationQuery r, CancellationToken ct)
    {
        var actor = await ServiceItemAccess.OwnerAsync(context, user, ct);
        var v = await context.Vendors.AsNoTracking().SingleAsync(v => v.Id == actor.VendorId, ct);
        return new(v.AllowInCombos, v.MaxComboDiscount);
    }
}
public record SaveComboParticipationCommand(bool AllowInCombos, decimal MaxComboDiscount) : IRequest<ComboParticipationDto>;
public class SaveComboParticipationCommandHandler(IApplicationDbContext context, ICurrentUserService user, IAuditLogger audit) : IRequestHandler<SaveComboParticipationCommand, ComboParticipationDto>
{
    public Task<ComboParticipationDto> Handle(SaveComboParticipationCommand r, CancellationToken ct) => context.ExecuteSerializableAsync(async token =>
    {
        var actor = await ServiceItemAccess.OwnerAsync(context, user, token);
        var v = await context.Vendors.SingleAsync(v => v.Id == actor.VendorId, token);
        v.ConfigureComboParticipation(r.AllowInCombos, r.MaxComboDiscount);
        await audit.LogAsync("Vendor.ComboConsent", nameof(Vendor), v.Id.ToString(),
            new { r.AllowInCombos, r.MaxComboDiscount, actorId = actor.Id, actorRole = actor.Role });
        await context.SaveChangesAsync(token); return new ComboParticipationDto(v.AllowInCombos, v.MaxComboDiscount);
    }, ct);
}
public record GetBundleRulesQuery : IRequest<List<DiscountTierDto>>;
public class GetBundleRulesQueryHandler(IApplicationDbContext context) : IRequestHandler<GetBundleRulesQuery, List<DiscountTierDto>>
{
    public async Task<List<DiscountTierDto>> Handle(GetBundleRulesQuery r, CancellationToken ct)
    {
        var rules = await context.BundleDiscountRules.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.MinItems).Select(r => new DiscountTierDto(r.MinItems, r.Percent)).ToListAsync(ct);
        return rules.Count == 0 ? new() { new(3, .03m), new(5, .05m), new(7, .08m) } : rules;
    }
}
public record SaveBundleRulesCommand(List<DiscountTierDto> Rules) : IRequest<List<DiscountTierDto>>;
public class SaveBundleRulesCommandHandler(IApplicationDbContext context, IAuditLogger audit) : IRequestHandler<SaveBundleRulesCommand, List<DiscountTierDto>>
{
    public async Task<List<DiscountTierDto>> Handle(SaveBundleRulesCommand r, CancellationToken ct)
    {
        if (r.Rules.Count == 0 || r.Rules.Select(x => x.MinItems).Distinct().Count() != r.Rules.Count) throw new DomainException("Cần quy tắc không trùng số lượng.");
        var sorted = r.Rules.OrderBy(x => x.MinItems).ToList();
        if (sorted.Zip(sorted.Skip(1)).Any(p => p.First.Percent > p.Second.Percent)) throw new DomainException("Mức giảm phải tăng theo số hạng mục.");
        context.BundleDiscountRules.RemoveRange(await context.BundleDiscountRules.ToListAsync(ct));
        foreach (var tier in sorted) context.BundleDiscountRules.Add(BundleDiscountRule.Create(tier.MinItems, tier.Percent));
        await audit.LogAsync("BundleRules.Save", nameof(BundleDiscountRule), null, r);
        await context.SaveChangesAsync(ct); return sorted;
    }
}
