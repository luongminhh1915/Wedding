using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Leads.Commands.SendLead;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

public record ComboOptionDto(Guid ListingId, Guid VendorId, string VendorName, bool VendorVerified, decimal RatingAvg,
    string Title, decimal Price, decimal MaxPrice, string Location, string? ImageUrl);
public record ComboCategoryDto(Guid CategoryId, string Name, string Slug, string? Icon, List<ComboOptionDto> Options);
public record ComboTemplateItemDto(Guid CategoryId, Guid ListingId, bool IsOptional);
public record ComboTemplateDto(Guid Id, string Name, string Tier, string Style, string? Description, string? ImageUrl,
    bool IsActive, decimal EstimatedTotal, List<ComboTemplateItemDto> Items);
public record ComboBuilderDto(List<ComboCategoryDto> Categories, List<ComboTemplateDto> Templates, List<DiscountTierDto> DiscountTiers);
public record DiscountTierDto(int MinItems, decimal Percent);

public record ComboOrderItemDto(Guid Id, Guid ListingId, Guid VendorId, string VendorName, string Title, decimal Price,
    decimal DiscountShare, Guid? LeadId, string Status, string? VoucherCode);
public record ComboOrderDto(Guid Id, string Code, string Status, DateTime CreatedAt, DateTime? WeddingDate, int? GuestCount,
    decimal Subtotal, decimal DiscountPercent, decimal DiscountAmount, decimal Total, List<ComboOrderItemDto> Items);

internal static class ComboMap
{
    public static ComboTemplateDto ToDto(ComboTemplate t) => new(t.Id, t.Name, t.Tier, t.Style, t.Description, t.ImageUrl, t.IsActive,
        t.Items.Where(i => !i.IsOptional).Sum(i => i.Listing?.MinPrice ?? 0),
        t.Items.Select(i => new ComboTemplateItemDto(i.CategoryId, i.ListingId, i.IsOptional)).ToList());

    /// <summary>Đồng bộ trạng thái từng dòng combo theo Lead/Hợp đồng thật.</summary>
    public static async Task SyncAsync(IApplicationDbContext ctx, List<ComboOrder> orders, CancellationToken ct)
    {
        var leadIds = orders.SelectMany(o => o.Items).Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList();
        var leads = await ctx.Leads.Where(l => leadIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var quoted = await ctx.Quotes.Where(q => leadIds.Contains(q.LeadId)).Select(q => q.LeadId).Distinct().ToListAsync(ct);
        foreach (var o in orders.Where(o => o.Status != ComboOrderStatus.Cancelled))
        {
            foreach (var i in o.Items.Where(i => i.LeadId.HasValue && leads.ContainsKey(i.LeadId.Value)))
            {
                var l = leads[i.LeadId!.Value];
                var s = l.Status switch
                {
                    LeadStatus.Contracted => ComboItemStatus.Contracted,
                    LeadStatus.Cancelled or LeadStatus.Expired => ComboItemStatus.Cancelled,
                    LeadStatus.Accepted or LeadStatus.Contacted => quoted.Contains(l.Id) ? ComboItemStatus.Quoted : ComboItemStatus.Accepted,
                    _ => ComboItemStatus.Sent
                };
                if (s != i.Status) i.SetStatus(s);
            }
            o.Recalculate();
        }
    }
}

public record GetComboBuilderQuery(string? City) : IRequest<ComboBuilderDto>;

public class GetComboBuilderQueryHandler : IRequestHandler<GetComboBuilderQuery, ComboBuilderDto>
{
    private readonly IApplicationDbContext _context;
    public GetComboBuilderQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<ComboBuilderDto> Handle(GetComboBuilderQuery r, CancellationToken ct)
    {
        var listings = await _context.Listings.AsNoTracking().Include(l => l.Vendor).Include(l => l.Category).Include(l => l.Media)
            .Where(l => l.Status == ListingStatus.Active && l.Vendor.IsActive).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(r.City))
            listings = listings.Where(l => l.Location.Contains(r.City, StringComparison.OrdinalIgnoreCase) || l.Vendor.City.Contains(r.City, StringComparison.OrdinalIgnoreCase)).ToList();
        var cats = listings.GroupBy(l => l.CategoryId).Select(g => (Cat: g.First().Category, Items: g)).OrderBy(g => g.Cat.DisplayOrder)
            .Select(g => new ComboCategoryDto(g.Cat.Id, g.Cat.Name, g.Cat.Slug, g.Cat.Icon, g.Items.OrderBy(l => l.MinPrice).Select(l =>
                new ComboOptionDto(l.Id, l.VendorId, l.Vendor.BrandName, l.Vendor.IsVerified, l.Vendor.RatingAvg, l.Title, l.MinPrice, l.MaxPrice,
                    l.Location, l.Media.OrderBy(m => m.DisplayOrder).Select(m => m.ThumbnailUrl ?? m.MediaUrl).FirstOrDefault())).ToList()))
            .ToList();
        var templates = await _context.ComboTemplates.AsNoTracking().Include(t => t.Items).ThenInclude(i => i.Listing)
            .Where(t => t.IsActive).OrderBy(t => t.SortOrder).ToListAsync(ct);
        return new ComboBuilderDto(cats, templates.Select(ComboMap.ToDto).ToList(),
            new() { new(3, 0.03m), new(5, 0.05m), new(7, 0.08m) });
    }
}

public record SubmitComboOrderCommand(Guid? TemplateId, List<Guid> ListingIds, DateTime? WeddingDate, int? GuestCount,
    string? PhoneNumber, string? Note) : IRequest<ComboOrderDto>;

/// <summary>P5 – Gửi combo: tạo ComboOrder + mỗi dịch vụ sinh 1 Lead (kèm voucher BR-004) tới NCC tương ứng.</summary>
public class SubmitComboOrderCommandHandler : IRequestHandler<SubmitComboOrderCommand, ComboOrderDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly ISender _mediator;

    public SubmitComboOrderCommandHandler(IApplicationDbContext context, ICurrentUserService user, ISender mediator)
    { _context = context; _user = user; _mediator = mediator; }

    public async Task<ComboOrderDto> Handle(SubmitComboOrderCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập để gửi combo.");
        var ids = (r.ListingIds ?? new()).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > 12) throw new DomainException("Combo cần từ 1 đến 12 dịch vụ.");
        var listings = await _context.Listings.Where(l => ids.Contains(l.Id) && l.Status == ListingStatus.Active).ToListAsync(ct);
        if (listings.Count != ids.Count) throw new DomainException("Một số dịch vụ không còn khả dụng, vui lòng tải lại.");

        var order = ComboOrder.Create(uid, r.TemplateId, r.WeddingDate, r.GuestCount, r.Note,
            listings.Select(l => (l.Id, l.VendorId, l.CategoryId, l.Title, l.MinPrice)).ToList());
        _context.ComboOrders.Add(order);
        await _context.SaveChangesAsync(ct);

        foreach (var item in order.Items)
        {
            var res = await _mediator.Send(new SendLeadCommand(item.ListingId, null, r.PhoneNumber, r.WeddingDate, r.GuestCount,
                item.Price - item.DiscountShare, $"[Combo {order.Code}] Giá combo đề xuất {item.Price - item.DiscountShare:N0}đ (ưu đãi {item.DiscountShare:N0}đ). {r.Note}".Trim()), ct);
            item.AttachLead(res.LeadId);
        }
        await _context.SaveChangesAsync(ct);
        return (await new GetMyComboOrdersQueryHandler(_context, _user).Load(uid, order.Id, ct)).Single();
    }
}

public record GetMyComboOrdersQuery : IRequest<List<ComboOrderDto>>;

public class GetMyComboOrdersQueryHandler : IRequestHandler<GetMyComboOrdersQuery, List<ComboOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetMyComboOrdersQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public Task<List<ComboOrderDto>> Handle(GetMyComboOrdersQuery r, CancellationToken ct)
        => Load(_user.UserId ?? throw new DomainException("Bạn cần đăng nhập."), null, ct);

    internal async Task<List<ComboOrderDto>> Load(Guid uid, Guid? id, CancellationToken ct)
    {
        var orders = await _context.ComboOrders.Include(o => o.Items).ThenInclude(i => i.Vendor)
            .Where(o => o.CustomerId == uid && (id == null || o.Id == id)).ToListAsync(ct);
        await ComboMap.SyncAsync(_context, orders, ct);
        await _context.SaveChangesAsync(ct);
        var leadIds = orders.SelectMany(o => o.Items).Where(i => i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList();
        var vouchers = await _context.Vouchers.Where(v => leadIds.Contains(v.LeadId)).ToDictionaryAsync(v => v.LeadId, v => v.Code, ct);
        return orders.OrderByDescending(o => o.CreatedAt).Select(o => new ComboOrderDto(o.Id, o.Code, o.Status.ToString(), o.CreatedAt,
            o.WeddingDate, o.GuestCount, o.Subtotal, o.DiscountPercent, o.DiscountAmount, o.Total,
            o.Items.Select(i => new ComboOrderItemDto(i.Id, i.ListingId, i.VendorId, i.Vendor?.BrandName ?? "", i.Title, i.Price, i.DiscountShare,
                i.LeadId, i.Status.ToString(), i.LeadId.HasValue && vouchers.TryGetValue(i.LeadId.Value, out var c) ? c : null)).ToList())).ToList();
    }
}

public record CancelComboOrderCommand(Guid Id) : IRequest<bool>;

public class CancelComboOrderCommandHandler : IRequestHandler<CancelComboOrderCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public CancelComboOrderCommandHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<bool> Handle(CancelComboOrderCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var o = await _context.ComboOrders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == r.Id && x.CustomerId == uid, ct)
            ?? throw new NotFoundException("Không tìm thấy combo.");
        var openLeadIds = o.Items.Where(i => i.Status == ComboItemStatus.Sent && i.LeadId.HasValue).Select(i => i.LeadId!.Value).ToList();
        o.Cancel();
        foreach (var lead in await _context.Leads.Where(l => openLeadIds.Contains(l.Id) && l.Status == LeadStatus.New).ToListAsync(ct))
            lead.Cancel("Khách hủy combo.");
        await _context.SaveChangesAsync(ct);
        return true;
    }
}

// ---------- Admin: combo mẫu ----------

public record GetAllComboTemplatesQuery : IRequest<List<ComboTemplateDto>>;

public class GetAllComboTemplatesQueryHandler : IRequestHandler<GetAllComboTemplatesQuery, List<ComboTemplateDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAllComboTemplatesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ComboTemplateDto>> Handle(GetAllComboTemplatesQuery r, CancellationToken ct)
        => (await _context.ComboTemplates.AsNoTracking().Include(t => t.Items).ThenInclude(i => i.Listing).OrderBy(t => t.SortOrder).ToListAsync(ct))
            .Select(ComboMap.ToDto).ToList();
}

public record SaveComboTemplateCommand(Guid? Id, string Name, string Tier, string Style, string? Description, string? ImageUrl,
    bool IsActive, int SortOrder, List<ComboTemplateItemDto> Items) : IRequest<ComboTemplateDto>;

public class SaveComboTemplateCommandHandler : IRequestHandler<SaveComboTemplateCommand, ComboTemplateDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditLogger _audit;
    public SaveComboTemplateCommandHandler(IApplicationDbContext context, IAuditLogger audit) { _context = context; _audit = audit; }

    public async Task<ComboTemplateDto> Handle(SaveComboTemplateCommand r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new DomainException("Tên combo không được để trống.");
        ComboTemplate t;
        if (r.Id.HasValue)
        {
            t = await _context.ComboTemplates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException("Combo không tồn tại.");
            t.Update(r.Name, r.Tier, r.Style, r.Description, r.ImageUrl, r.IsActive);
            foreach (var i in t.Items.ToList()) _context.ComboTemplateItems.Remove(i);
        }
        else
        {
            t = ComboTemplate.Create(r.Name, r.Tier, r.Style, r.Description, r.ImageUrl, r.SortOrder);
            _context.ComboTemplates.Add(t);
        }
        foreach (var i in r.Items ?? new()) _context.ComboTemplateItems.Add(ComboTemplateItem.Create(t.Id, i.CategoryId, i.ListingId, i.IsOptional));
        await _audit.LogAsync("ComboTemplate.Save", nameof(ComboTemplate), t.Id.ToString(), new { r.Name, items = r.Items?.Count });
        await _context.SaveChangesAsync(ct);
        return (await new GetAllComboTemplatesQueryHandler(_context).Handle(new(), ct)).First(x => x.Id == t.Id);
    }
}
