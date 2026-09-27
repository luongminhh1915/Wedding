using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class Lead : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid? ListingId { get; private set; }
    public DateTime? WeddingDate { get; private set; }
    public int? EstimatedGuests { get; private set; }
    public decimal? EstimatedBudget { get; private set; }
    public string? Notes { get; private set; }

    // BR-002: Smart Privacy - SĐT ban đầu bị mã hóa che đi
    public string RawPhoneNumber { get; private set; } = string.Empty;
    public string MaskedPhoneNumber { get; private set; } = string.Empty;
    public bool IsPhoneUnlocked { get; private set; } = false;

    // BR-003: SLA Tiếp Nhận 2 giờ
    public LeadStatus Status { get; private set; } = LeadStatus.New;
    public DateTime SlaDeadline { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public string? CancellationReason { get; private set; }

    // Navigation Properties
    public User Customer { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;
    public Listing? Listing { get; private set; }
    public Voucher? Voucher { get; private set; }
    public BookingContract? BookingContract { get; private set; }

    private Lead() { } // Dành cho EF Core

    public static Lead Create(Guid customerId, Guid vendorId, Guid? listingId, string rawPhone, 
        DateTime? weddingDate, int? estimatedGuests, decimal? estimatedBudget, string? notes)
    {
        if (string.IsNullOrWhiteSpace(rawPhone))
            throw new DomainException("Số điện thoại liên hệ là bắt buộc.");

        var cleanPhone = rawPhone.Trim();
        // BR-002: Che số điện thoại dạng 0987***123
        var masked = MaskPhone(cleanPhone);

        var now = DateTime.UtcNow;
        return new Lead
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            VendorId = vendorId,
            ListingId = listingId,
            RawPhoneNumber = cleanPhone,
            MaskedPhoneNumber = masked,
            IsPhoneUnlocked = false,
            WeddingDate = weddingDate,
            EstimatedGuests = estimatedGuests,
            EstimatedBudget = estimatedBudget,
            Notes = notes?.Trim(),
            Status = LeadStatus.New,
            SlaDeadline = now.AddHours(2), // BR-003: SLA 2h
            CreatedAt = now
        };
    }

    public void Accept()
    {
        if (Status != LeadStatus.New)
            throw new DomainException("Chỉ có thể tiếp nhận Lead ở trạng thái mới tạo.");

        Status = LeadStatus.Accepted;
        AcceptedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void UnlockPhone()
    {
        IsPhoneUnlocked = true;
        SetUpdated();
    }

    public void MarkContracted()
    {
        Status = LeadStatus.Contracted;
        SetUpdated();
    }

    public void Cancel(string reason)
    {
        Status = LeadStatus.Cancelled;
        CancellationReason = reason;
        SetUpdated();
    }

    public void Expire()
    {
        Status = LeadStatus.Expired;
        CancellationReason = "Quá hạn SLA 24h NCC không tiếp nhận (BR-003)";
        SetUpdated();
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length < 7) return phone;
        var start = phone.Substring(0, 4);
        var end = phone.Substring(phone.Length - 3);
        return $"{start}***{end}";
    }
}
