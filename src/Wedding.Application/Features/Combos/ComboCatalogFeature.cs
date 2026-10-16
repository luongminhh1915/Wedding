using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

public record VariantDto(Guid? Id, string Name, decimal PriceDelta);
public record AddOnDto(Guid? Id, string Name, decimal Price, bool PerUnit);
public record ServiceItemDto(Guid Id, Guid ListingId, Guid CategoryId, Guid VendorId, string VendorName, string Name,
    decimal BasePrice, string Unit, int MinGuests, int MaxGuests, string Regions, string CancellationPolicy,
    string ModerationStatus, string? ReviewNote, bool IsActive, int MaxBookingsPerDay, List<VariantDto> Variants, List<AddOnDto> AddOns);
public record ComboParticipationDto(bool AllowInCombos, decimal MaxComboDiscount);
internal static class CatalogMap
{
    public static ServiceItemDto ToDto(ServiceItem i) => new(i.Id, i.ListingId, i.CategoryId, i.VendorId, i.Vendor?.BrandName ?? "", i.Name,
        i.BasePrice, i.Unit, i.MinGuests, i.MaxGuests, i.Regions, i.CancellationPolicy, i.ModerationStatus, i.ReviewNote, i.IsActive,
        i.MaxBookingsPerDay, i.Variants.Select(v => new VariantDto(v.Id, v.Name, v.PriceDelta)).ToList(), i.AddOns.Select(a => new AddOnDto(a.Id, a.Name, a.Price, a.PerUnit)).ToList());
}
public record GetServiceItemsQuery(bool Moderation = false) : IRequest<List<ServiceItemDto>>;
public class GetServiceItemsQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetServiceItemsQuery, List<ServiceItemDto>>
{
    public async Task<List<ServiceItemDto>> Handle(GetServiceItemsQuery r, CancellationToken ct)
    {
        var q = context.ServiceItems.AsNoTracking().Include(i => i.Vendor).Include(i => i.Variants).Include(i => i.AddOns).AsQueryable();
        if (!r.Moderation) q = q.Where(i => i.Vendor.UserId == user.UserId);
        return (await q.OrderByDescending(i => i.CreatedAt).ToListAsync(ct)).Select(CatalogMap.ToDto).ToList();
    }
}
public record SaveServiceItemCommand(Guid? Id, Guid ListingId, string Name, decimal BasePrice, string Unit, int MinGuests, int MaxGuests,
    string Regions, string CancellationPolicy, bool IsActive, List<VariantDto> Variants, List<AddOnDto> AddOns,
    bool SubmitForApproval = false, int MaxBookingsPerDay = 1) : IRequest<ServiceItemDto>;
public class SaveServiceItemCommandHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<SaveServiceItemCommand, ServiceItemDto>
{
    public async Task<ServiceItemDto> Handle(SaveServiceItemCommand r, CancellationToken ct)
    {
        var vendor = await context.Vendors.FirstOrDefaultAsync(v => v.UserId == user.UserId, ct) ?? throw new DomainException("Không có hồ sơ NCC.");
        var listing = await context.Listings.FirstOrDefaultAsync(l => l.Id == r.ListingId && l.VendorId == vendor.Id && l.Status == ListingStatus.Active, ct) ?? throw new DomainException("Dịch vụ phải thuộc bài đăng Active của bạn.");
        if (!vendor.IsVerified) throw new DomainException("NCC phải được xác minh trước khi tạo dịch vụ combo.");
        var item = r.Id.HasValue ? await context.ServiceItems.Include(i => i.Variants).Include(i => i.AddOns).FirstOrDefaultAsync(i => i.Id == r.Id && i.VendorId == vendor.Id, ct) ?? throw new NotFoundException("Dịch vụ không tồn tại.")
            : ServiceItem.Create(vendor.Id, listing.Id, listing.CategoryId, r.Name, r.BasePrice, r.Unit, r.MinGuests, r.MaxGuests, r.Regions, r.CancellationPolicy);
        if (item.ListingId != r.ListingId) throw new DomainException("Không được đổi bài đăng của dịch vụ đã tạo.");
        item.Update(r.Name, r.BasePrice, r.Unit, r.MinGuests, r.MaxGuests, r.Regions, r.CancellationPolicy, r.IsActive); item.SetDailyCapacity(r.MaxBookingsPerDay);
        foreach (var old in item.Variants.ToList()) context.ItemVariants.Remove(old);
        foreach (var old in item.AddOns.ToList()) context.ItemAddOns.Remove(old);
        item.Variants.Clear(); item.AddOns.Clear();
        if ((r.Variants?.Count ?? 0) > 20 || (r.AddOns?.Count ?? 0) > 30) throw new DomainException("Tối đa 20 biến thể và 30 món kèm.");
        foreach (var v in r.Variants ?? new())
        {
            if (r.BasePrice + v.PriceDelta < 0) throw new DomainException("Giá sau biến thể không được âm.");
            item.Variants.Add(ItemVariant.Create(item.Id, v.Name, v.PriceDelta));
        }
        foreach (var a in r.AddOns ?? new()) item.AddOns.Add(ItemAddOn.Create(item.Id, a.Name, a.Price, a.PerUnit));
        if (r.SubmitForApproval) item.Submit(); if (!r.Id.HasValue) context.ServiceItems.Add(item);
        await context.SaveChangesAsync(ct); return CatalogMap.ToDto(item) with { VendorName = vendor.BrandName };
    }
}
public record ReviewServiceItemCommand(Guid Id, bool Approve, string? Note) : IRequest<bool>;
public class ReviewServiceItemCommandHandler(IApplicationDbContext context, IAuditLogger audit) : IRequestHandler<ReviewServiceItemCommand, bool>
{
    public async Task<bool> Handle(ReviewServiceItemCommand r, CancellationToken ct)
    {
        var item = await context.ServiceItems.FirstOrDefaultAsync(i => i.Id == r.Id, ct) ?? throw new NotFoundException("Dịch vụ không tồn tại.");
        item.Review(r.Approve, r.Note); await audit.LogAsync("ServiceItem.Review", nameof(ServiceItem), item.Id.ToString(), new { r.Approve, r.Note });
        await context.SaveChangesAsync(ct); return true;
    }
}
public record GetComboParticipationQuery : IRequest<ComboParticipationDto>;
public class GetComboParticipationQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetComboParticipationQuery, ComboParticipationDto>
{
    public async Task<ComboParticipationDto> Handle(GetComboParticipationQuery r, CancellationToken ct)
    {
        var v = await context.Vendors.FirstOrDefaultAsync(v => v.UserId == user.UserId, ct) ?? throw new NotFoundException("Không có NCC.");
        return new(v.AllowInCombos, v.MaxComboDiscount);
    }
}
public record SaveComboParticipationCommand(bool AllowInCombos, decimal MaxComboDiscount) : IRequest<ComboParticipationDto>;
public class SaveComboParticipationCommandHandler(IApplicationDbContext context, ICurrentUserService user, IAuditLogger audit) : IRequestHandler<SaveComboParticipationCommand, ComboParticipationDto>
{
    public async Task<ComboParticipationDto> Handle(SaveComboParticipationCommand r, CancellationToken ct)
    {
        var v = await context.Vendors.FirstOrDefaultAsync(v => v.UserId == user.UserId, ct) ?? throw new NotFoundException("Không có NCC.");
        v.ConfigureComboParticipation(r.AllowInCombos, r.MaxComboDiscount);
        await audit.LogAsync("Vendor.ComboConsent", nameof(Vendor), v.Id.ToString(), r); await context.SaveChangesAsync(ct); return new(v.AllowInCombos, v.MaxComboDiscount);
    }
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
