using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>P4 – Báo giá có cấu trúc NCC gửi cho Lead; khách chấp nhận → điền sẵn hợp đồng.</summary>
public class Quote : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public Guid LeadId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string ItemsJson { get; private set; } = "[]";
    public decimal TotalAmount { get; private set; }
    public decimal DepositAmount { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal VatRate { get; private set; }
    public decimal VatAmount { get; private set; }
    public string? Terms { get; private set; }
    public DateTime ValidUntil { get; private set; }
    public QuoteStatus Status { get; private set; } = QuoteStatus.Sent;
    public string? CustomerNote { get; private set; }
    public int Version { get; private set; } = 1;

    public Lead Lead { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private Quote() { }

    public static Quote Create(Guid leadId, Guid vendorId, Guid customerId, string itemsJson, decimal total, decimal deposit,
        string? terms, int validDays, int version)
    {
        if (total <= 0) throw new DomainException("Tổng giá trị báo giá phải lớn hơn 0.");
        if (deposit < 0 || deposit > total) throw new DomainException("Tiền cọc không hợp lệ.");
        return new Quote
        {
            Code = $"BG-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}", LeadId = leadId, VendorId = vendorId,
            CustomerId = customerId, ItemsJson = itemsJson, TotalAmount = total, DepositAmount = deposit, Terms = terms,
            ValidUntil = DateTime.UtcNow.AddDays(Math.Clamp(validDays, 1, 60)), Version = version
        };
    }

    public void Accept()
    {
        if (Status != QuoteStatus.Sent) throw new DomainException("Báo giá không còn hiệu lực.");
        if (DateTime.UtcNow > ValidUntil) { Status = QuoteStatus.Expired; throw new DomainException("Báo giá đã hết hạn."); }
        Status = QuoteStatus.Accepted; SetUpdated();
    }
    public void SetVat(decimal subtotal, decimal vatRate)
    {
        if (vatRate < 0 || vatRate > 1) throw new DomainException("Thuế suất phải từ 0 đến 100%.");
        Subtotal = subtotal; VatRate = vatRate; VatAmount = Math.Round(subtotal * vatRate, 0, MidpointRounding.AwayFromZero); TotalAmount = subtotal + VatAmount; SetUpdated();
    }

    public void Reject(string? note) { if (Status != QuoteStatus.Sent) throw new DomainException("Báo giá không còn hiệu lực."); Status = QuoteStatus.Rejected; CustomerNote = note; SetUpdated(); }
    public void Supersede() { if (Status == QuoteStatus.Sent) { Status = QuoteStatus.Superseded; SetUpdated(); } }
    public void Withdraw() { if (Status is QuoteStatus.Sent or QuoteStatus.Accepted) { Status = QuoteStatus.Superseded; SetUpdated(); } }
}
