using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class Commission : BaseEntity
{
    public Guid ContractId { get; private set; }
    public Guid VendorId { get; private set; }
    public CommissionPeriod Period { get; private set; } // BR-006: 50% lúc cọc (K1), 50% sau cưới (K2)
    public decimal CommissionRate { get; private set; } // FM-001 (3% - 12%)
    public decimal CommissionAmount { get; private set; } // FM-002..FM-004
    public DateTime DueDate { get; private set; } // BR-007: Đối soát ngày 25
    public CommissionStatus Status { get; private set; } = CommissionStatus.Pending;
    public DateTime? PaidAt { get; private set; }
    public string? PaymentReferenceCode { get; private set; }
    public string? VietQrPayload { get; private set; }

    // Navigation Properties
    public BookingContract Contract { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private Commission() { } // Dành cho EF Core

    public static Commission Create(Guid contractId, Guid vendorId, CommissionPeriod period, 
        decimal commissionRate, decimal commissionAmount, DateTime dueDate)
    {
        if (commissionAmount <= 0)
            throw new DomainException("Số tiền hoa hồng phải lớn hơn 0 VNĐ.");

        return new Commission
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            VendorId = vendorId,
            Period = period,
            CommissionRate = commissionRate,
            CommissionAmount = commissionAmount,
            DueDate = dueDate,
            Status = CommissionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetQrPayload(string payload, string referenceCode)
    {
        VietQrPayload = payload;
        PaymentReferenceCode = referenceCode;
        SetUpdated();
    }

    public void MarkPaid(string referenceCode)
    {
        Status = CommissionStatus.Paid;
        PaymentReferenceCode = referenceCode;
        PaidAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void MarkOverdue()
    {
        if (Status == CommissionStatus.Pending && DateTime.UtcNow > DueDate)
        {
            Status = CommissionStatus.Overdue;
            SetUpdated();
        }
    }

    public void Cancel()
    {
        Status = CommissionStatus.Cancelled;
        SetUpdated();
    }
}
