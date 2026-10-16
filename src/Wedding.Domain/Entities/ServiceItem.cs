using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class ServiceItem : BaseEntity
{
    public Guid VendorId { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal BasePrice { get; private set; }
    public string Unit { get; private set; } = "Package";
    public int MinGuests { get; private set; } = 1;
    public int MaxGuests { get; private set; } = 2000;
    public string Regions { get; private set; } = string.Empty;
    public string CancellationPolicy { get; private set; } = string.Empty;
    public string ModerationStatus { get; private set; } = "Draft";
    public string? ReviewNote { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int MaxBookingsPerDay { get; private set; } = 1;
    public Vendor Vendor { get; private set; } = null!;
    public Listing Listing { get; private set; } = null!;
    public ICollection<ItemVariant> Variants { get; private set; } = new List<ItemVariant>();
    public ICollection<ItemAddOn> AddOns { get; private set; } = new List<ItemAddOn>();
    private ServiceItem() { }
    public static ServiceItem Create(Guid vendorId, Guid listingId, Guid categoryId, string name, decimal price, string unit,
        int minGuests, int maxGuests, string regions, string cancellationPolicy)
    {
        var item = new ServiceItem { VendorId = vendorId, ListingId = listingId, CategoryId = categoryId };
        item.Update(name, price, unit, minGuests, maxGuests, regions, cancellationPolicy, true); return item;
    }
    public void Update(string name, decimal price, string unit, int minGuests, int maxGuests, string regions, string cancellationPolicy, bool active)
    {
        if (string.IsNullOrWhiteSpace(name) || price < 0 || price > 100000000000m || price != decimal.Truncate(price)) throw new DomainException("Tên/giá dịch vụ không hợp lệ (giá theo đồng nguyên).");
        if (unit is not ("Package" or "Table" or "Guest" or "Hour")) throw new DomainException("Đơn vị phải là Package/Table/Guest/Hour.");
        if (minGuests < 1 || maxGuests < minGuests || maxGuests > 10000) throw new DomainException("Sức chứa dịch vụ không hợp lệ.");
        if (string.IsNullOrWhiteSpace(regions) || string.IsNullOrWhiteSpace(cancellationPolicy)) throw new DomainException("Khu vực và chính sách hủy là bắt buộc.");
        Name = name.Trim(); BasePrice = price; Unit = unit; MinGuests = minGuests; MaxGuests = maxGuests; Regions = regions.Trim();
        CancellationPolicy = cancellationPolicy.Trim(); IsActive = active; ModerationStatus = "Draft"; SetUpdated();
    }
    public void Submit() { ModerationStatus = "Pending"; ReviewNote = null; SetUpdated(); }
    public void SetDailyCapacity(int capacity)
    {
        if (capacity is < 1 or > 100) throw new DomainException("Số đơn phục vụ mỗi ngày phải từ 1 đến 100.");
        MaxBookingsPerDay = capacity;
    }
    public void Review(bool approve, string? note)
    {
        if (ModerationStatus != "Pending") throw new DomainException("Dịch vụ chưa gửi duyệt.");
        if (!approve && string.IsNullOrWhiteSpace(note)) throw new DomainException("Vui lòng nhập lý do từ chối.");
        ModerationStatus = approve ? "Approved" : "Rejected"; ReviewNote = note; SetUpdated();
    }
    public bool Serves(string region) => Regions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(r => r == "*" || r.Equals(region.Trim(), StringComparison.OrdinalIgnoreCase));
    public static int QuantityFor(string unit, int guests, int requestedHours = 1) => unit switch
    {
        "Table" => (int)Math.Ceiling(guests / 10m), "Guest" => guests,
        "Hour" when requestedHours is >= 1 and <= 24 => requestedHours,
        "Package" => 1, _ => throw new DomainException("Số lượng/đơn vị dịch vụ không hợp lệ.")
    };
}
public class ItemVariant : BaseEntity
{
    public Guid ServiceItemId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal PriceDelta { get; private set; }
    private ItemVariant() { }
    public static ItemVariant Create(Guid itemId, string name, decimal delta)
    {
        if (string.IsNullOrWhiteSpace(name) || delta < -100000000000m || delta > 100000000000m || delta != decimal.Truncate(delta)) throw new DomainException("Tên biến thể và giá chênh theo đồng nguyên là bắt buộc.");
        return new() { ServiceItemId = itemId, Name = name.Trim(), PriceDelta = delta };
    }
}
public class ItemAddOn : BaseEntity
{
    public Guid ServiceItemId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool PerUnit { get; private set; }
    private ItemAddOn() { }
    public static ItemAddOn Create(Guid itemId, string name, decimal price, bool perUnit)
    {
        if (string.IsNullOrWhiteSpace(name) || price < 0 || price > 100000000000m || price != decimal.Truncate(price)) throw new DomainException("Tên/giá món kèm không hợp lệ (giá theo đồng nguyên).");
        return new() { ServiceItemId = itemId, Name = name.Trim(), Price = price, PerUnit = perUnit };
    }
}
public class BundleDiscountRule : BaseEntity
{
    public int MinItems { get; private set; }
    public decimal Percent { get; private set; }
    public bool IsActive { get; private set; } = true;
    private BundleDiscountRule() { }
    public static BundleDiscountRule Create(int minItems, decimal percent, bool active = true)
    {
        if (minItems is < 2 or > 12 || percent < 0 || percent > 0.30m) throw new DomainException("Quy tắc giảm giá không hợp lệ.");
        return new() { MinItems = minItems, Percent = percent, IsActive = active };
    }
}
