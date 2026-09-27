using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class BookingContract : BaseEntity
{
    public string ContractCode { get; private set; } = string.Empty;
    public Guid LeadId { get; private set; }
    public Guid? VoucherId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal ContractValue { get; private set; }
    public decimal DepositAmount { get; private set; }
    public string? ContractImageUrl { get; private set; }
    public DateTime? WeddingDate { get; private set; }

    // BR-005: Xác thực giao dịch 2 chiều trong 72 giờ
    public ContractStatus Status { get; private set; } = ContractStatus.PendingVerification;
    public DateTime VerificationDeadline { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? CancellationReason { get; private set; }

    // Navigation Properties
    public Lead Lead { get; private set; } = null!;
    public Voucher? Voucher { get; private set; }
    public Vendor Vendor { get; private set; } = null!;
    public User Customer { get; private set; } = null!;
    public ICollection<Commission> Commissions { get; private set; } = new List<Commission>();
    public Review? Review { get; private set; }

    private BookingContract() { } // Dành cho EF Core

    public static BookingContract Create(Guid leadId, Guid? voucherId, Guid vendorId, Guid customerId, 
        decimal contractValue, decimal depositAmount, string? contractImageUrl, DateTime? weddingDate)
    {
        if (contractValue <= 0)
            throw new DomainException("Giá trị hợp đồng phải lớn hơn 0 VNĐ.");
        if (depositAmount < 0 || depositAmount > contractValue)
            throw new DomainException("Tiền đặt cọc không hợp lệ (0 <= Tiền cọc <= Giá trị HĐ).");

        var now = DateTime.UtcNow;
        return new BookingContract
        {
            Id = Guid.NewGuid(),
            ContractCode = $"HD-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            LeadId = leadId,
            VoucherId = voucherId,
            VendorId = vendorId,
            CustomerId = customerId,
            ContractValue = contractValue,
            DepositAmount = depositAmount,
            ContractImageUrl = contractImageUrl,
            WeddingDate = weddingDate,
            Status = ContractStatus.PendingVerification, // BR-005
            VerificationDeadline = now.AddHours(72),    // BR-005: 72 giờ duyệt
            CreatedAt = now
        };
    }

    public void ConfirmByCustomer()
    {
        if (Status != ContractStatus.PendingVerification)
            throw new DomainException("Hợp đồng không ở trạng thái chờ khách duyệt.");

        if (DateTime.UtcNow > VerificationDeadline)
        {
            Status = ContractStatus.Cancelled;
            CancellationReason = "Quá hạn 72h khách hàng không xác nhận hợp đồng (BR-005).";
            throw new DomainException(CancellationReason);
        }

        Status = ContractStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void RejectByCustomer(string reason)
    {
        Status = ContractStatus.Draft;
        CancellationReason = $"Khách hàng từ chối do sai lệch: {reason}";
        SetUpdated();
    }

    public void CompleteContract()
    {
        if (Status != ContractStatus.Confirmed)
            throw new DomainException("Chỉ hợp đồng đã xác thực mới có thể hoàn tất sau ngày cưới.");

        Status = ContractStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Cancel(string reason)
    {
        Status = ContractStatus.Cancelled;
        CancellationReason = reason;
        SetUpdated();
    }
}
