using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

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
    public decimal CreditAmount { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public string? DisputeReason { get; private set; }

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
        if (Status != SettlementStatus.Open) throw new DomainException("Bảng kê đã chốt không được thêm dòng; chuyển khoản phát sinh sang kỳ tiếp theo.");
        if (unpaidTotal <= 0) return;
        CommissionCount = unpaidCount; TotalAmount = PaidAmount + unpaidTotal;
        if (Status == SettlementStatus.Paid) { Status = SettlementStatus.Closed; PaidAt = null; }
        SetUpdated();
    }
    public void Close() { if (Status == SettlementStatus.Open) { Status = SettlementStatus.Closed; SetUpdated(); } }

    public void MarkPaid(decimal amount)
    {
        if (Status is not (SettlementStatus.Closed or SettlementStatus.Overdue)) throw new DomainException("Bảng kê chưa chốt hoặc đang tranh chấp.");
        if (amount <= 0 || amount > TotalAmount - PaidAmount) throw new DomainException("Số tiền vượt dư nợ bảng kê.");
        PaidAmount += amount;
        if (PaidAmount == TotalAmount) { Status = SettlementStatus.Paid; PaidAt = DateTime.UtcNow; }
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public void MarkOverdue() { if (Status == SettlementStatus.Closed && DateTime.UtcNow > DueDate) { Status = SettlementStatus.Overdue; SetUpdated(); } }
    public void ApplyCredit(decimal amount) { if (Status != SettlementStatus.Open || amount < 0 || amount > TotalAmount) throw new DomainException("Credit chỉ áp dụng trước khi chốt."); TotalAmount -= amount; CreditAmount += amount; if (TotalAmount == 0) { Status = SettlementStatus.Paid; PaidAt = DateTime.UtcNow; } Version = Guid.NewGuid(); SetUpdated(); }
    public void Dispute(string reason) { if (Status is SettlementStatus.Paid or SettlementStatus.Open || string.IsNullOrWhiteSpace(reason)) throw new DomainException("Bảng kê không thể tranh chấp hoặc thiếu lý do."); Status = SettlementStatus.InDispute; DisputeReason = reason.Trim(); Version = Guid.NewGuid(); SetUpdated(); }
    public void ResolveDispute() { if (Status != SettlementStatus.InDispute) throw new DomainException("Bảng kê không có tranh chấp."); Status = SettlementStatus.Closed; DisputeReason = null; Version = Guid.NewGuid(); SetUpdated(); }
    public void AttachInvoice(string number) { if (Status != SettlementStatus.Paid) throw new DomainException("Chỉ ghi hóa đơn sau khi bảng kê đã trả đủ."); InvoiceNumber = number; SetUpdated(); }
}
