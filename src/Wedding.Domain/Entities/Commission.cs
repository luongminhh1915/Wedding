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
    public decimal PaidAmount { get; private set; }
    public Guid? SettlementId { get; private set; }
    public DateTime? ReleaseAt { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();

    // Navigation Properties
    public BookingContract Contract { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private Commission() { } // Dành cho EF Core

    public static Commission Create(Guid contractId, Guid vendorId, CommissionPeriod period, 
        decimal commissionRate, decimal commissionAmount, DateTime dueDate)
    {
        if (commissionAmount <= 0)
            throw new DomainException("Số tiền hoa hồng phải lớn hơn 0 VNĐ.");
        if (commissionRate < .03m || commissionRate > .12m) throw new DomainException("Tỷ lệ hoa hồng phải trong khung 3–12%.");

        return new Commission
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            VendorId = vendorId,
            Period = period,
            CommissionRate = commissionRate,
            CommissionAmount = commissionAmount,
            DueDate = dueDate,
            Status = CommissionStatus.Unsettled,
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
        ApplyPayment(Math.Max(0, CommissionAmount - EffectivePaidAmount), referenceCode);
    }

    public decimal EffectivePaidAmount => Status == CommissionStatus.Paid ? CommissionAmount : PaidAmount;
    public decimal OutstandingAmount => Math.Max(0, CommissionAmount - EffectivePaidAmount);

    public void ApplyPayment(decimal amount, string referenceCode)
    {
        if (Status is not (CommissionStatus.Pending or CommissionStatus.Unsettled or CommissionStatus.InSettlement or CommissionStatus.Overdue))
            throw new DomainException("Khoản hoa hồng đang bị khóa, đóng băng hoặc đã xử lý.");
        if (amount <= 0 || amount > OutstandingAmount) throw new DomainException("Số tiền thanh toán vượt dư nợ.");
        PaidAmount += amount;
        if (PaidAmount == CommissionAmount) Status = CommissionStatus.Settled;
        PaymentReferenceCode = referenceCode;
        if (Status == CommissionStatus.Settled) PaidAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public void LockUntil(DateTime releaseAt) { Status = CommissionStatus.EscrowLocked; ReleaseAt = releaseAt; Version = Guid.NewGuid(); SetUpdated(); }
    public void Release(DateTime now)
    {
        if (Status != CommissionStatus.EscrowLocked || ReleaseAt == null || now < ReleaseAt) return;
        Status = CommissionStatus.Unsettled; DueDate = SettlementCalendar.DueDate(now); Version = Guid.NewGuid(); SetUpdated();
    }
    public void AssignSettlement(Guid id) { if (Status is not (CommissionStatus.Unsettled or CommissionStatus.Pending or CommissionStatus.Overdue)) throw new DomainException("Khoản hoa hồng chưa thể đối soát."); SettlementId = id; Status = CommissionStatus.InSettlement; Version = Guid.NewGuid(); SetUpdated(); }
    public void Freeze() { if (Status is CommissionStatus.Unsettled or CommissionStatus.Pending or CommissionStatus.InSettlement or CommissionStatus.Overdue or CommissionStatus.EscrowLocked) { Status = CommissionStatus.Frozen; Version = Guid.NewGuid(); SetUpdated(); } }
    public void Unfreeze(DateTime now) { if (Status != CommissionStatus.Frozen) return; Status = ReleaseAt > now ? CommissionStatus.EscrowLocked : SettlementId.HasValue ? CommissionStatus.InSettlement : CommissionStatus.Unsettled; Version = Guid.NewGuid(); SetUpdated(); }
    public void SetCancellationLiability(decimal amount)
    {
        if (amount < 0) throw new DomainException("Nghĩa vụ hoa hồng không được âm.");
        var paid = EffectivePaidAmount;
        CommissionAmount = amount; PaidAmount = paid;
        Status = amount == 0 ? CommissionStatus.Cancelled : paid >= amount ? CommissionStatus.Settled : CommissionStatus.Unsettled;
        Version = Guid.NewGuid(); SetUpdated();
    }

    public void AdjustAmount(decimal newAmount)
    {
        if (newAmount <= 0)
            throw new DomainException("Số tiền hoa hồng phải lớn hơn 0 VNĐ.");

        CommissionAmount = newAmount;
        SetUpdated();
    }

    public void MarkOverdue()
    {
        if ((Status is CommissionStatus.Pending or CommissionStatus.Unsettled or CommissionStatus.InSettlement) && DateTime.UtcNow > DueDate)
        {
            Status = CommissionStatus.Overdue;
            SetUpdated();
        }
    }

    public void Cancel()
    {
        PaidAmount = EffectivePaidAmount;
        Status = CommissionStatus.Cancelled;
        Version = Guid.NewGuid();
        SetUpdated();
    }
}

/// <summary>Vietnam billing cycle: close on day 25, due on day 30 (last day in February).</summary>
public static class SettlementCalendar
{
    public static DateTime DueDate(DateTime eligibleAt)
    {
        var vn = eligibleAt.ToUniversalTime().AddHours(7);
        if (vn.Day >= 25) vn = vn.AddMonths(1);
        return new DateTime(vn.Year, vn.Month, Math.Min(30, DateTime.DaysInMonth(vn.Year, vn.Month)), 23, 59, 59, DateTimeKind.Utc).AddHours(-7);
    }
}
