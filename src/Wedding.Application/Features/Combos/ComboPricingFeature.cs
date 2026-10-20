using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Combos;

public record ComboSelection(Guid ServiceItemId, List<Guid>? VariantIds, List<Guid>? AddOnIds, int Hours = 1);
public record ComboPriceCommand(Guid? TemplateId, string Region, DateTime WeddingDate, int GuestCount, List<ComboSelection> Selections, Guid? RevisionOfId = null) : IRequest<ComboPriceDto>;
public record ComboPriceLineDto(Guid ServiceItemId, Guid ListingId, Guid VendorId, Guid CategoryId, string VendorName, string Title,
    string Unit, int Quantity, decimal Price, decimal DiscountShare, decimal Total, string CancellationPolicy, string Availability,
    ComboComponentSnapshotDto? Component = null);
public record ComboPriceDto(decimal Subtotal, decimal DiscountPercent, decimal DiscountAmount, decimal Total,
    List<ComboPriceLineDto> Lines, List<string> Warnings, decimal? PlannerRemaining = null,
    decimal? PlannerReservedBeforeCart = null, decimal? PlannerReplacedEstimate = null, decimal? PlannerBudgetTotal = null,
    string PricingMode = "Dynamic");
public class ComboPriceCommandHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<ComboPriceCommand, ComboPriceDto>
{
    public Task<ComboPriceDto> Handle(ComboPriceCommand r, CancellationToken ct) => ComboPricing.CalculateAsync(context, r, user.UserId, ct);
}
public static class ComboPricing
{
    public static async Task<ComboPriceDto> CalculateAsync(IApplicationDbContext context, ComboPriceCommand r, Guid? userId, CancellationToken ct)
    {
        if (r.Selections == null || r.Selections.Count is < 1 or > 12 || r.Selections.Select(s => s.ServiceItemId).Distinct().Count() != r.Selections.Count)
            throw new DomainException("Chọn từ 1 đến 12 dịch vụ khác nhau.");
        if (string.IsNullOrWhiteSpace(r.Region) || r.GuestCount is < 1 or > 10000 || r.WeddingDate.Date < DateTime.UtcNow.AddHours(7).Date)
            throw new DomainException("Vui lòng nhập khu vực, ngày cưới và số khách hợp lệ.");
        var template = r.TemplateId.HasValue ? await context.ComboTemplates.Include(t => t.Items).PubliclyAvailable().FirstOrDefaultAsync(t => t.Id == r.TemplateId, ct) ?? throw new DomainException("Combo mẫu không còn khả dụng.") : null;
        if (template != null && ComboFixedPackages.IsFixed(template)) return await FixedAsync(context, r, template, userId, ct);
        var ids = r.Selections.Select(s => s.ServiceItemId).ToList();
        var items = await ComboPublicCatalog.Eligible(context).Include(i => i.Vendor).ThenInclude(v => v.User).Include(i => i.Listing).Include(i => i.Variants).Include(i => i.AddOns)
            .Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        if (items.Count != ids.Count || items.Any(i => !i.IsActive || i.ModerationStatus != "Approved" || !i.Vendor.IsVerified || !i.Vendor.IsActive || !i.Vendor.User.IsActive || i.Listing.Status != ListingStatus.Active || !i.Listing.IsActive))
            throw new DomainException("Một dịch vụ chưa được duyệt/NCC chưa xác minh hoặc bài đăng không còn Active.");
        var ownerVendorId = ComboVendorPolicy.RequireOneVendor(items.Select(i => i.VendorId));
        if (items.Select(i => i.CategoryId).Distinct().Count() != items.Count) throw new DomainException("Mỗi ngành chỉ chọn một dịch vụ.");
        if (items.Any(i => !i.Serves(r.Region) || r.GuestCount < i.MinGuests || r.GuestCount > i.MaxGuests)) throw new DomainException("Dịch vụ không phục vụ khu vực hoặc không phù hợp số khách.");
        if (template != null)
        {
            if (!await ComboTemplateAvailability.ComponentsEligibleAsync(context, template, ct)) throw new DomainException("Combo cũ không thuộc riêng một NCC hoặc có dịch vụ không còn khả dụng. Vui lòng chọn combo NCC hợp lệ.");
            if (!string.IsNullOrWhiteSpace(template.Region) && !RegionRules.Serves(template.Region, r.Region)) throw new DomainException("Combo mẫu không phục vụ khu vực đã chọn.");
            // Customers may swap/add approved services, but only inside this vendor's catalog.
            if (template.OwnerVendorId != ownerVendorId) throw new DomainException(ComboVendorPolicy.SingleVendorMessage);
        }
        var vendors = items.Select(i => i.Vendor).DistinctBy(v => v.Id).ToList();
        if (vendors.Any(v => !v.AllowInCombos)) throw new DomainException("Có NCC chưa đồng ý tham gia combo.");
        var rules = await new GetBundleRulesQueryHandler(context).Handle(new(), ct);
        var percent = rules.Where(t => t.MinItems <= items.Count).OrderByDescending(t => t.MinItems).Select(t => t.Percent).FirstOrDefault();
        var hasRequiredPresetSlots = template == null || template.Items.Where(i => !i.IsOptional).All(slot => items.Any(item => item.CategoryId == slot.CategoryId));
        if (template?.OwnerVendorId != null && template.SingleVendorDiscount.HasValue && hasRequiredPresetSlots) percent = template.SingleVendorDiscount.Value;
        var requestedPercent = percent; percent = Math.Min(percent, vendors.Min(v => v.MaxComboDiscount));
        var warnings = new List<string> { "Giá tham khảo được khóa 7 ngày khi gửi. Giá chốt theo báo giá NCC; thay đổi vượt ±10% cần khách chấp nhận lại." };
        if (!hasRequiredPresetSlots) warnings.Add("Bạn đã bỏ hạng mục của combo mẫu; ưu đãi riêng của combo không còn áp dụng. Mức giảm được tính theo số hạng mục hiện có.");
        if (requestedPercent > percent) warnings.Add("Ưu đãi đã giảm xuống mức trần mà tất cả NCC đồng ý, sàn không bù chênh lệch.");
        var date = r.WeddingDate.Date; var nextDay = date.AddDays(1); var lines = new List<ComboPriceLineDto>();
        foreach (var item in items.OrderBy(i => i.Id))
        {
            var confirmed = await context.BookingContracts.CountAsync(c => c.VendorId == item.VendorId && c.Lead.ListingId == item.ListingId && c.WeddingDate >= date && c.WeddingDate < nextDay &&
                (c.Status == ContractStatus.Confirmed || c.Status == ContractStatus.Disputed || c.Status == ContractStatus.Completed), ct);
            if (confirmed >= item.MaxBookingsPerDay) throw new DomainException($"{item.Name} đã đủ lịch ngày cưới. Vui lòng đổi lựa chọn.");
            var selection = r.Selections.Single(s => s.ServiceItemId == item.Id);
            var variantIds = (selection.VariantIds ?? new()).Distinct().ToList(); var addonIds = (selection.AddOnIds ?? new()).Distinct().ToList();
            if (variantIds.Any(id => item.Variants.All(v => v.Id != id)) || addonIds.Any(id => item.AddOns.All(a => a.Id != id))) throw new DomainException("Biến thể/món kèm không thuộc dịch vụ.");
            var quantity = ServiceItem.QuantityFor(item.Unit, r.GuestCount, selection.Hours);
            var perUnit = item.BasePrice + item.Variants.Where(v => variantIds.Contains(v.Id)).Sum(v => v.PriceDelta);
            if (perUnit < 0) throw new DomainException("Tổng biến thể làm giá âm.");
            var price = perUnit * quantity + item.AddOns.Where(a => addonIds.Contains(a.Id)).Sum(a => a.Price * (a.PerUnit ? quantity : 1));
            lines.Add(new(item.Id, item.ListingId, item.VendorId, item.CategoryId, item.Vendor.BrandName, item.Name, item.Unit, quantity, price, 0, price, item.CancellationPolicy, "Còn năng lực dự kiến; chờ NCC xác nhận bằng báo giá"));
        }
        var subtotal = lines.Sum(l => l.Price); var discount = Math.Round(subtotal * percent, 0);
        var shares = ComboMoney.AllocateDiscount(lines.Select(line => line.Price).ToList(), discount);
        for (var i = 0; i < lines.Count; i++)
        {
            var share = shares[i]; lines[i] = lines[i] with { DiscountShare = share, Total = lines[i].Price - share };
        }
        if (r.RevisionOfId.HasValue)
        {
            if (!userId.HasValue) throw new DomainException("Bạn cần đăng nhập để tính giá phiên bản chỉnh sửa.");
            var source = await context.ComboOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == r.RevisionOfId && o.CustomerId == userId, ct)
                ?? throw new NotFoundException("Không tìm thấy combo gốc của bạn.");
            await Wedding.Application.Features.CustomerOrders.CustomerOrderJourney.EnsureCanReplaceComboAsync(context, source, ct);
            if (source.Items.First().VendorId != ownerVendorId) throw new DomainException("Phiên bản chỉnh sửa chỉ dùng dịch vụ cùng NCC của combo gốc.");
        }
        var projection = userId.HasValue ? await ComboBudgetSynchronization.ProjectAsync(context, userId.Value, lines.Select(line => line.CategoryId).ToList(), ct, r.RevisionOfId) : null;
        if (projection != null) warnings.AddRange(projection.Warnings);
        return new(subtotal, percent, discount, subtotal - discount, lines, warnings,
            projection?.Profile == null ? null : projection.Profile.TotalBudget - projection.ReservedBeforeCart - (subtotal - discount),
            projection?.Profile == null ? null : projection.ReservedBeforeCart,
            projection?.Profile == null ? null : projection.ReplacedSeedEstimate,
            projection?.Profile?.TotalBudget);
    }

    private static async Task<ComboPriceDto> FixedAsync(IApplicationDbContext context, ComboPriceCommand request,
        ComboTemplate template, Guid? userId, CancellationToken ct)
    {
        if (!await ComboTemplateAvailability.ComponentsEligibleAsync(context, template, ct))
            throw new DomainException("Combo đang tạm ngừng vì dịch vụ thành phần đã thay đổi hoặc không còn hợp lệ; cần NCC gửi duyệt lại combo.");
        var components = ComboFixedPackages.Components(template);
        if (components.Count != request.Selections.Count || request.Selections.Any(selection =>
            components.All(component => component.ServiceItemId != selection.ServiceItemId) ||
            (selection.VariantIds?.Count ?? 0) != 0 || (selection.AddOnIds?.Count ?? 0) != 0 || selection.Hours != 1))
            throw new DomainException("Giá combo áp dụng đúng các dịch vụ trong gói. Thay đổi thành phần cần thỏa thuận và báo giá riêng với NCC.");
        if (template.ComboPrice > template.OriginalPrice)
            throw new DomainException("Giá combo vượt tổng giá bán lẻ. Vui lòng trao đổi NCC để có báo giá riêng; chưa có chính sách phân bổ phần chênh lệch được phê duyệt.");
        if ((!string.IsNullOrWhiteSpace(template.Region) && !RegionRules.Serves(template.Region, request.Region)) ||
            components.Any(component => !RegionRules.Serves(component.Regions, request.Region) || request.GuestCount < component.MinGuests || request.GuestCount > component.MaxGuests))
            throw new DomainException("Combo không phục vụ khu vực hoặc không phù hợp số khách.");
        var ids = components.Select(component => component.ServiceItemId).ToList();
        var current = await context.ServiceItems.Include(item => item.Vendor).Where(item => ids.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        var date = request.WeddingDate.Date; var nextDay = date.AddDays(1);
        var subtotal = template.OriginalPrice!.Value; var total = template.ComboPrice!.Value;
        var discount = subtotal - total; var percent = subtotal == 0 ? 0 : discount / subtotal;
        var shares = ComboMoney.AllocateDiscount(components.Select(component => component.BasePrice).ToList(), discount);
        var lines = new List<ComboPriceLineDto>();
        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index]; var item = current[component.ServiceItemId];
            var confirmed = await context.BookingContracts.CountAsync(contract => contract.VendorId == item.VendorId && contract.Lead.ListingId == item.ListingId &&
                contract.WeddingDate >= date && contract.WeddingDate < nextDay &&
                (contract.Status == ContractStatus.Confirmed || contract.Status == ContractStatus.Disputed || contract.Status == ContractStatus.Completed), ct);
            if (confirmed >= item.MaxBookingsPerDay) throw new DomainException($"{component.Name} đã đủ lịch ngày cưới. Vui lòng trao đổi NCC.");
            lines.Add(new(component.ServiceItemId, component.ListingId, component.VendorId, component.CategoryId, item.Vendor.BrandName,
                component.Name, component.Unit, component.Quantity, component.BasePrice, shares[index], component.BasePrice - shares[index],
                component.CancellationPolicy, "Gói cố định đã duyệt; chờ NCC xác nhận lịch bằng báo giá", component));
        }
        if (request.RevisionOfId.HasValue)
        {
            if (!userId.HasValue) throw new DomainException("Bạn cần đăng nhập để tính giá phiên bản chỉnh sửa.");
            var source = await context.ComboOrders.Include(order => order.Items).FirstOrDefaultAsync(order => order.Id == request.RevisionOfId && order.CustomerId == userId, ct)
                ?? throw new NotFoundException("Không tìm thấy combo gốc của bạn.");
            await CustomerOrders.CustomerOrderJourney.EnsureCanReplaceComboAsync(context, source, ct);
            if (source.Items.First().VendorId != template.OwnerVendorId) throw new DomainException("Phiên bản chỉnh sửa chỉ dùng dịch vụ cùng NCC của combo gốc.");
        }
        var warnings = new List<string> { "Giá áp dụng nguyên gói đã duyệt. Khách và NCC có thể thỏa thuận thay đổi riêng trong báo giá/hợp đồng; combo công khai giữ nguyên." };
        var projection = userId.HasValue ? await ComboBudgetSynchronization.ProjectAsync(context, userId.Value, lines.Select(line => line.CategoryId).ToList(), ct, request.RevisionOfId) : null;
        if (projection != null) warnings.AddRange(projection.Warnings);
        return new(subtotal, percent, discount, total, lines, warnings,
            projection?.Profile == null ? null : projection.Profile.TotalBudget - projection.ReservedBeforeCart - total,
            projection?.Profile == null ? null : projection.ReservedBeforeCart,
            projection?.Profile == null ? null : projection.ReplacedSeedEstimate, projection?.Profile?.TotalBudget, "FixedPrice");
    }
}
