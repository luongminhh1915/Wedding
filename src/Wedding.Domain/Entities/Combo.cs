using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>P5 – Combo mẫu do admin tạo (Tiết kiệm / Phổ biến / Sang trọng).</summary>
public class ComboTemplate : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Tier { get; private set; } = "Popular"; // Saving | Popular | Luxury
    public string Style { get; private set; } = "Classic";
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }
    public Guid? OwnerVendorId { get; private set; }
    public decimal? SingleVendorDiscount { get; private set; }
    public string ModerationStatus { get; private set; } = "Approved";
    public string? Region { get; private set; }

    public ICollection<ComboTemplateItem> Items { get; private set; } = new List<ComboTemplateItem>();

    private ComboTemplate() { }

    public static ComboTemplate Create(string name, string tier, string style, string? description, string? imageUrl, int sortOrder)
        => new() { Name = name.Trim(), Tier = tier, Style = style, Description = description, ImageUrl = imageUrl, SortOrder = sortOrder };

    public void Update(string name, string tier, string style, string? description, string? imageUrl, bool isActive, int? sortOrder = null)
    {
        Name = name.Trim(); Tier = tier; Style = style; Description = description; ImageUrl = imageUrl; IsActive = isActive;
        if (sortOrder.HasValue) SortOrder = sortOrder.Value;
        SetUpdated();
    }
    public void SetScope(Guid? ownerVendorId, string? region, decimal? singleVendorDiscount, bool approved)
    {
        if (singleVendorDiscount < 0 || singleVendorDiscount > 0.30m) throw new DomainException("Giảm giá combo tối đa 30%.");
        OwnerVendorId = ownerVendorId; Region = region; SingleVendorDiscount = singleVendorDiscount;
        ModerationStatus = approved ? "Approved" : "Pending"; IsActive = approved && IsActive; SetUpdated();
    }
    public void Review(bool approve)
    {
        if (ModerationStatus != "Pending") throw new DomainException("Chỉ duyệt combo đang chờ. Combo đã ẩn cần được NCC chỉnh sửa và gửi lại.");
        ModerationStatus = approve ? "Approved" : "Rejected"; IsActive = approve; SetUpdated();
    }
    public void Archive() { IsActive = false; ModerationStatus = "Archived"; SetUpdated(); }
}

public class ComboTemplateItem : BaseEntity
{
    public Guid ComboTemplateId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid ListingId { get; private set; }
    public bool IsOptional { get; private set; }
    public Guid? ServiceItemId { get; private set; }
    public string AllowedItemIdsJson { get; private set; } = "[]";

    public ComboTemplate ComboTemplate { get; private set; } = null!;
    public Category Category { get; private set; } = null!;
    public Listing Listing { get; private set; } = null!;

    private ComboTemplateItem() { }

    public static ComboTemplateItem Create(Guid templateId, Guid categoryId, Guid listingId, bool isOptional)
        => new() { ComboTemplateId = templateId, CategoryId = categoryId, ListingId = listingId, IsOptional = isOptional };
    public void ConfigureItems(Guid serviceItemId, string allowedItemIdsJson) { ServiceItemId = serviceItemId; AllowedItemIdsJson = allowedItemIdsJson; }
}

/// <summary>P5 – Đơn combo khách gửi; mỗi dòng sinh 1 Lead tới NCC tương ứng.</summary>
public class ComboOrder : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public Guid? ComboTemplateId { get; private set; }
    public DateTime? WeddingDate { get; private set; }
    public int? GuestCount { get; private set; }
    public string? Note { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal Total { get; private set; }
    public string Region { get; private set; } = string.Empty;
    public DateTime? PriceLockUntil { get; private set; }
    public ComboOrderStatus Status { get; private set; } = ComboOrderStatus.Submitted;

    public User Customer { get; private set; } = null!;
    public ICollection<ComboOrderItem> Items { get; private set; } = new List<ComboOrderItem>();

    private ComboOrder() { }

    /// <summary>Giảm giá combo theo số hạng mục: ≥3 → 3%, ≥5 → 5%, ≥7 → 8% (NCC chịu theo tỷ lệ giá trị).</summary>
    public static decimal DiscountFor(int itemCount) => itemCount >= 7 ? 0.08m : itemCount >= 5 ? 0.05m : itemCount >= 3 ? 0.03m : 0m;

    public static ComboOrder Create(Guid customerId, Guid? templateId, DateTime? weddingDate, int? guestCount, string? note,
        IReadOnlyList<(Guid ListingId, Guid VendorId, Guid CategoryId, string Title, decimal Price)> lines)
    {
        if (lines.Count == 0) throw new DomainException("Combo phải có ít nhất 1 hạng mục.");
        if (lines.Select(l => l.VendorId).Distinct().Count() != 1)
            throw new DomainException("Mỗi combo chỉ được chứa dịch vụ của một nhà cung cấp.");
        if (lines.Select(l => l.CategoryId).Distinct().Count() != lines.Count)
            throw new DomainException("Mỗi hạng mục chỉ được chọn 1 dịch vụ.");
        var order = new ComboOrder
        {
            Code = $"CB-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
            CustomerId = customerId, ComboTemplateId = templateId, WeddingDate = weddingDate, GuestCount = guestCount, Note = note
        };
        order.Subtotal = lines.Sum(l => l.Price);
        order.DiscountPercent = DiscountFor(lines.Count);
        order.DiscountAmount = Math.Round(order.Subtotal * order.DiscountPercent, 0);
        order.Total = order.Subtotal - order.DiscountAmount;
        foreach (var l in lines)
        {
            var share = order.Subtotal == 0 ? 0 : Math.Round(order.DiscountAmount * l.Price / order.Subtotal, 0);
            order.Items.Add(ComboOrderItem.Create(order.Id, l.ListingId, l.VendorId, l.CategoryId, l.Title, l.Price, share));
        }
        return order;
    }

    public void Recalculate()
    {
        var items = Items.Where(i => i.Status != ComboItemStatus.Cancelled).ToList();
        if (items.Count == 0) { Status = ComboOrderStatus.Cancelled; SetUpdated(); return; }
        if (Status is ComboOrderStatus.Draft or ComboOrderStatus.Expired) return;
        if (items.All(i => i.Status == ComboItemStatus.Contracted)) Status = ComboOrderStatus.Completed;
        else if (items.Any(i => i.Status == ComboItemStatus.Contracted)) Status = ComboOrderStatus.PartiallyContracted;
        else if (items.All(i => i.Status == ComboItemStatus.Quoted)) Status = ComboOrderStatus.Quoted;
        else if (items.Any(i => i.Status == ComboItemStatus.Quoted)) Status = ComboOrderStatus.PartiallyQuoted;
        else if (items.Any(i => i.Status != ComboItemStatus.Sent)) Status = ComboOrderStatus.InProgress;
        SetUpdated();
    }
    public void SetPricing(decimal discountPercent, string region, bool draft)
    {
        if (Items.Select(i => i.VendorId).Distinct().Count() != 1) throw new DomainException("Mỗi combo chỉ được chứa dịch vụ của một nhà cung cấp.");
        if (discountPercent < 0 || discountPercent > 0.30m) throw new DomainException("Mức giảm giá không hợp lệ.");
        Region = region; DiscountPercent = discountPercent; DiscountAmount = Math.Round(Subtotal * discountPercent, 0); Total = Subtotal - DiscountAmount;
        var sorted = Items.OrderBy(i => i.Id).ToList();
        var shares = ComboMoney.AllocateDiscount(sorted.Select(i => i.Price).ToList(), DiscountAmount);
        for (var index = 0; index < sorted.Count; index++) sorted[index].SetDiscount(shares[index]);
        Status = draft ? ComboOrderStatus.Draft : ComboOrderStatus.Submitted; PriceLockUntil = draft ? null : DateTime.UtcNow.AddDays(7); SetUpdated();
    }
    public void SubmitDraft()
    {
        if (Status != ComboOrderStatus.Draft) throw new DomainException("Đơn không ở trạng thái nháp.");
        if (Items.Select(i => i.VendorId).Distinct().Count() != 1) throw new DomainException("Không thể gửi lại bản nháp cũ chứa nhiều nhà cung cấp.");
        Status = ComboOrderStatus.Submitted; PriceLockUntil = DateTime.UtcNow.AddDays(7); SetUpdated();
    }
    public void ExpirePriceLock() { if (Status is not (ComboOrderStatus.Completed or ComboOrderStatus.Cancelled or ComboOrderStatus.Draft)) { Status = ComboOrderStatus.Expired; SetUpdated(); } }

    public void Cancel()
    {
        if (Status == ComboOrderStatus.Completed) throw new DomainException("Combo đã hoàn tất, không thể hủy.");
        Status = ComboOrderStatus.Cancelled;
        foreach (var i in Items.Where(i => i.Status == ComboItemStatus.Sent)) i.SetStatus(ComboItemStatus.Cancelled);
        SetUpdated();
    }
}

/// <summary>Largest-remainder allocation preserves every VND without assigning rounding debt to a free line.</summary>
public static class ComboMoney
{
    public static IReadOnlyList<decimal> AllocateDiscount(IReadOnlyList<decimal> prices, decimal amount)
    {
        var subtotal = prices.Sum();
        if (prices.Any(p => p < 0) || amount < 0 || amount > subtotal || amount != decimal.Truncate(amount)) throw new DomainException("Phân bổ ưu đãi không hợp lệ.");
        if (subtotal == 0) return prices.Select(_ => 0m).ToList();
        var exact = prices.Select(p => amount * (p / subtotal)).ToList();
        var shares = exact.Select(decimal.Floor).ToList();
        var remainder = (int)(amount - shares.Sum());
        var priority = exact.Select((value, index) => new { Index = index, Fraction = value - shares[index] })
            .OrderByDescending(x => x.Fraction).ThenBy(x => x.Index).ToList();
        for (var i = 0; i < remainder; i++) shares[priority[i].Index]++;
        return shares;
    }
}

public class ComboOrderItem : BaseEntity
{
    public Guid ComboOrderId { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public decimal DiscountShare { get; private set; }
    public Guid? LeadId { get; private set; }
    public Guid? ServiceItemId { get; private set; }
    public Guid? BudgetItemId { get; private set; }
    public string SelectionJson { get; private set; } = "{}";
    public string CancellationPolicySnapshot { get; private set; } = string.Empty;
    public int Quantity { get; private set; } = 1;
    public string Unit { get; private set; } = "Package";
    public ComboItemStatus Status { get; private set; } = ComboItemStatus.Sent;

    public ComboOrder ComboOrder { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private ComboOrderItem() { }

    public static ComboOrderItem Create(Guid orderId, Guid listingId, Guid vendorId, Guid categoryId, string title, decimal price, decimal discountShare)
        => new() { ComboOrderId = orderId, ListingId = listingId, VendorId = vendorId, CategoryId = categoryId, Title = title, Price = price, DiscountShare = discountShare };

    public void AttachLead(Guid leadId) => LeadId = leadId;
    public void SetDiscount(decimal discount) => DiscountShare = discount;
    public void SetSelection(Guid serviceItemId, int quantity, string unit, string selectionJson, string cancellationPolicy)
    { ServiceItemId = serviceItemId; Quantity = quantity; Unit = unit; SelectionJson = selectionJson; CancellationPolicySnapshot = cancellationPolicy; }
    public void AttachBudget(Guid budgetItemId) => BudgetItemId = budgetItemId;
    public void SetStatus(ComboItemStatus status) { Status = status; SetUpdated(); }
}
