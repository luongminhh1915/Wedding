using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class Listing : BaseEntity
{
    public Guid VendorId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public decimal MinPrice { get; private set; }
    public decimal MaxPrice { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? ServiceDetailsJson { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public ListingStatus Status { get; private set; } = ListingStatus.Draft;
    public bool IsActive { get; private set; } = true;
    // The price/options facet of this source service. Legacy sibling rows are retained separately.
    public Guid? PricingServiceItemId { get; private set; }
    public void AttachPricingServiceItem(Guid id)
    {
        if (id == Guid.Empty || PricingServiceItemId.HasValue && PricingServiceItemId != id)
            throw new DomainException("Không thể thay thế dịch vụ giá đã liên kết. Các dịch vụ cũ và giao dịch của chúng được giữ nguyên.");
        PricingServiceItemId = id;
    }
    public string? RejectionReason { get; private set; }
    public int ViewCount { get; private set; } = 0;
    public int FavoriteCount { get; private set; } = 0;
    public DateTime? ModeratedAt { get; private set; }
    public string CancellationPolicy { get; private set; } = "Hủy trước 90 ngày: hoàn 100% cọc; trước 30–89 ngày: hoàn 50% cọc; dưới 30 ngày: không hoàn cọc. NCC hủy: hoàn 100% cọc.";
    public void SetCancellationPolicy(string policy) { if (string.IsNullOrWhiteSpace(policy) || policy.Length > 4000) throw new DomainException("Chính sách hủy cần từ 1 đến 4000 ký tự."); CancellationPolicy = policy.Trim(); SetUpdated(); }
    public void SetServiceDetailsJson(string? json)
    {
        if (json?.Length > 60000) throw new DomainException("Tổng nội dung dịch vụ tối đa 60.000 ký tự lưu trữ.");
        ServiceDetailsJson = json;
        SetUpdated();
    }

    // Navigation Properties
    public Vendor Vendor { get; private set; } = null!;
    public Category Category { get; private set; } = null!;
    public ICollection<ListingMedia> Media { get; private set; } = new List<ListingMedia>();
    public ICollection<Lead> Leads { get; private set; } = new List<Lead>();
    public ICollection<Review> Reviews { get; private set; } = new List<Review>();

    private Listing() { } // Dành cho EF Core

    public static Listing Create(Guid vendorId, Guid categoryId, string title, string slug, 
        decimal minPrice, decimal maxPrice, string description, string location)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Tiêu đề bài đăng không được để trống.");
        if (minPrice < 0 || maxPrice < minPrice)
            throw new DomainException("Khoảng giá không hợp lệ (Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu).");

        return new Listing
        {
            Id = Guid.NewGuid(),
            VendorId = vendorId,
            CategoryId = categoryId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Description = description.Trim(),
            Location = location.Trim(),
            Status = ListingStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string title, decimal minPrice, decimal maxPrice, string description, string location)
    {
        Title = title.Trim();
        MinPrice = minPrice;
        MaxPrice = maxPrice;
        Description = description.Trim();
        Location = location.Trim();
        SetUpdated();
    }

    public void SubmitForApproval()
    {
        Status = ListingStatus.PendingApproval;
        RejectionReason = null;
        SetUpdated();
    }

    public void Approve()
    {
        Status = ListingStatus.Active;
        ModeratedAt = DateTime.UtcNow;
        RejectionReason = null;
        SetUpdated();
    }

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Cần cung cấp lý do từ chối bài đăng.");

        Status = ListingStatus.Rejected;
        ModeratedAt = DateTime.UtcNow;
        RejectionReason = reason.Trim();
        SetUpdated();
    }

    public void SetStatus(ListingStatus status)
    {
        Status = status;
        SetUpdated();
    }

    public void SetActivity(bool active) { IsActive = active; SetUpdated(); }

    public void IncrementViewCount()
    {
        ViewCount++;
    }
}
