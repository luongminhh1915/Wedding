using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class PaymentTransaction : BaseEntity
{
    public string TransactionId { get; private set; } = "";
    public string Gateway { get; private set; } = "";
    public string AccountNumber { get; private set; } = "";
    public decimal Amount { get; private set; }
    public string Content { get; private set; } = "";
    public string Status { get; private set; } = "Unmatched";
    public string? UnmatchedReason { get; private set; }
    public Guid? SettlementId { get; private set; }
    public Guid? PremiumOrderId { get; private set; }
    public Guid? MatchedBy { get; private set; }
    public string? MatchNote { get; private set; }
    private PaymentTransaction() { }
    public static PaymentTransaction Create(string id, string gateway, string account, decimal amount, string content)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 150 || string.IsNullOrWhiteSpace(content) || content.Length > 2000 || string.IsNullOrWhiteSpace(gateway) || string.IsNullOrWhiteSpace(account) || amount <= 0 || amount != Math.Truncate(amount)) throw new DomainException("Giao dịch cần mã duy nhất, nội dung, tài khoản và số tiền VND nguyên dương.");
        return new() { TransactionId = id.Trim(), Gateway = gateway, AccountNumber = account, Amount = amount, Content = content };
    }
    public void Unmatched(string reason) { UnmatchedReason = reason; SetUpdated(); }
    public void Match(Guid? settlementId, Guid? premiumId, Guid? actor, string? note)
    {
        if (Status == "Matched") throw new DomainException("Giao dịch đã được khớp, không thể sử dụng lại.");
        Status = "Matched"; SettlementId = settlementId; PremiumOrderId = premiumId; MatchedBy = actor; MatchNote = note; UnmatchedReason = null; SetUpdated();
    }
}

public class PremiumOrder : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public Guid InvitationId { get; private set; }
    public decimal Amount { get; private set; } = 130000m;
    public string PaymentReference { get; private set; } = "";
    public string Status { get; private set; } = "Pending";
    public DateTime ExpiresAt { get; private set; }
    public DateTime? PaidAt { get; private set; }
    private PremiumOrder() { }
    public static PremiumOrder Create(Guid customer, Guid invitation) => new() { CustomerId = customer, InvitationId = invitation, PaymentReference = $"WEDDY{Guid.NewGuid():N}".ToUpperInvariant(), ExpiresAt = DateTime.UtcNow.AddHours(24) };
    public void MarkPaid() { if (Status != "Pending") throw new DomainException("Đơn Premium đã được xử lý."); Status = "Paid"; PaidAt = DateTime.UtcNow; SetUpdated(); }
}

public class CommissionCreditNote : BaseEntity
{
    public Guid ContractId { get; private set; }
    public Guid VendorId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal AppliedAmount { get; private set; }
    public string Reason { get; private set; } = "";
    private CommissionCreditNote() { }
    public static CommissionCreditNote Create(Guid contract, Guid vendor, decimal amount, string reason)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(reason)) throw new DomainException("Credit note cần số tiền và lý do.");
        return new() { ContractId = contract, VendorId = vendor, Amount = amount, Reason = reason };
    }
    public void Apply(decimal amount) { if (amount <= 0 || AppliedAmount + amount > Amount) throw new DomainException("Credit note vượt giá trị còn lại."); AppliedAmount += amount; SetUpdated(); }
}

/// <summary>Only records actual externally issued invoices; never manufactures a fiscal invoice number.</summary>
public class EInvoiceRecord : BaseEntity
{
    public Guid SettlementId { get; private set; }
    public string Provider { get; private set; } = "";
    public string InvoiceNumber { get; private set; } = "";
    public string LookupUrl { get; private set; } = "";
    public decimal VatRate { get; private set; }
    public Guid RecordedBy { get; private set; }
    private EInvoiceRecord() { }
    public static EInvoiceRecord Record(Guid settlement, string provider, string number, string url, decimal vat, Guid actor)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(number) || !Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != "https" || vat < 0 || vat > 1)
            throw new DomainException("Cần nhà cung cấp, số hóa đơn thật, URL HTTPS tra cứu và thuế suất hợp lệ.");
        return new() { SettlementId = settlement, Provider = provider.Trim(), InvoiceNumber = number.Trim(), LookupUrl = url, VatRate = vat, RecordedBy = actor };
    }
}
