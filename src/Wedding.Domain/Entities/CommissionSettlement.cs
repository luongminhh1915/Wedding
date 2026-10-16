using Wedding.Domain.Common;
using Wedding.Domain.Enums;

namespace Wedding.Domain.Entities;

/// <summary>P4 – Bảng kê đối soát hoa hồng theo tháng (chốt ngày 25 – BR-007).</summary>
public class CommissionSettlement : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string Period { get; private set; } = string.Empty; // yyyy-MM
    public int CommissionCount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public SettlementStatus Status { get; private set; } = SettlementStatus.Open;
    public DateTime DueDate { get; private set; }
    public string PaymentReference { get; private set; } = string.Empty;
    public DateTime? PaidAt { get; private set; }
    public string? InvoiceNumber { get; private set; }

    public Vendor Vendor { get; private set; } = null!;

    private CommissionSettlement() { }

    public static CommissionSettlement Create(Guid vendorId, string period, int count, decimal total, DateTime dueDate)
        => new()
        {
            VendorId = vendorId, Period = period, CommissionCount = count, TotalAmount = total, DueDate = dueDate,
            PaymentReference = $"HH{vendorId.ToString("N")[..6].ToUpper()}{period.Replace("-", "")}"
        };

    /// <summary>Cập nhật theo các khoản chưa thanh toán; nếu bảng kê đã Paid mà phát sinh khoản mới → mở lại (Closed).</summary>
    public void Refresh(int unpaidCount, decimal unpaidTotal)
    {
        if (unpaidTotal <= 0) return;
        CommissionCount = unpaidCount; TotalAmount = PaidAmount + unpaidTotal;
        if (Status == SettlementStatus.Paid) { Status = SettlementStatus.Closed; PaidAt = null; }
        SetUpdated();
    }
    public void Close() { if (Status == SettlementStatus.Open) { Status = SettlementStatus.Closed; SetUpdated(); } }

    public void MarkPaid(decimal amount)
    {
        PaidAmount += amount;
        if (PaidAmount >= TotalAmount) { Status = SettlementStatus.Paid; PaidAt = DateTime.UtcNow; InvoiceNumber = $"MOCK-EINV-{Random.Shared.Next(100000, 999999)}"; }
        SetUpdated();
    }

    public void MarkOverdue() { if (Status == SettlementStatus.Closed && DateTime.UtcNow > DueDate) { Status = SettlementStatus.Overdue; SetUpdated(); } }
}
