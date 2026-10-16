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

    public ICollection<ComboTemplateItem> Items { get; private set; } = new List<ComboTemplateItem>();

    private ComboTemplate() { }

    public static ComboTemplate Create(string name, string tier, string style, string? description, string? imageUrl, int sortOrder)
        => new() { Name = name.Trim(), Tier = tier, Style = style, Description = description, ImageUrl = imageUrl, SortOrder = sortOrder };

    public void Update(string name, string tier, string style, string? description, string? imageUrl, bool isActive)
    {
        Name = name.Trim(); Tier = tier; Style = style; Description = description; ImageUrl = imageUrl; IsActive = isActive; SetUpdated();
    }
}

public class ComboTemplateItem : BaseEntity
{
    public Guid ComboTemplateId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid ListingId { get; private set; }
    public bool IsOptional { get; private set; }

    public ComboTemplate ComboTemplate { get; private set; } = null!;
    public Category Category { get; private set; } = null!;
    public Listing Listing { get; private set; } = null!;

    private ComboTemplateItem() { }

    public static ComboTemplateItem Create(Guid templateId, Guid categoryId, Guid listingId, bool isOptional)
        => new() { ComboTemplateId = templateId, CategoryId = categoryId, ListingId = listingId, IsOptional = isOptional };
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
        if (lines.Select(l => l.CategoryId).Distinct().Count() != lines.Count)
            throw new DomainException("Mỗi hạng mục chỉ được chọn 1 dịch vụ.");
        var order = new ComboOrder
        {
            Code = $"CB-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}",
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
        if (items.All(i => i.Status == ComboItemStatus.Contracted)) Status = ComboOrderStatus.Completed;
        else if (items.Any(i => i.Status != ComboItemStatus.Sent)) Status = ComboOrderStatus.InProgress;
        SetUpdated();
    }

    public void Cancel()
    {
        if (Status == ComboOrderStatus.Completed) throw new DomainException("Combo đã hoàn tất, không thể hủy.");
        Status = ComboOrderStatus.Cancelled;
        foreach (var i in Items.Where(i => i.Status == ComboItemStatus.Sent)) i.SetStatus(ComboItemStatus.Cancelled);
        SetUpdated();
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
    public ComboItemStatus Status { get; private set; } = ComboItemStatus.Sent;

    public ComboOrder ComboOrder { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private ComboOrderItem() { }

    public static ComboOrderItem Create(Guid orderId, Guid listingId, Guid vendorId, Guid categoryId, string title, decimal price, decimal discountShare)
        => new() { ComboOrderId = orderId, ListingId = listingId, VendorId = vendorId, CategoryId = categoryId, Title = title, Price = price, DiscountShare = discountShare };

    public void AttachLead(Guid leadId) => LeadId = leadId;
    public void SetStatus(ComboItemStatus status) { Status = status; SetUpdated(); }
}
