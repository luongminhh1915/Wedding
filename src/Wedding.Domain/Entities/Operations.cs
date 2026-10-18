using System.Text.RegularExpressions;
using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class VendorTaxProfile : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string LegalName { get; private set; } = "";
    public string TaxNumber { get; private set; } = "";
    public string BankCode { get; private set; } = "";
    public string BankAccountNumber { get; private set; } = "";
    public string BankAccountName { get; private set; } = "";
    public VerificationStatus Status { get; private set; } = VerificationStatus.Draft;
    public bool AgreementAccepted { get; private set; }
    public Vendor Vendor { get; private set; } = null!;
    private VendorTaxProfile() { }
    public static VendorTaxProfile Create(Guid vendorId) => new() { VendorId = vendorId };
    public void Update(string legalName, string taxNumber, string bankCode, string accountNumber, string accountName, bool agreementAccepted)
    {
        if (Status == VerificationStatus.Submitted) throw new DomainException("Hồ sơ đang duyệt, chưa thể thay đổi.");
        if (string.IsNullOrWhiteSpace(legalName) || !Regex.IsMatch(taxNumber ?? "", @"^\d{10}(-?\d{3})?$")) throw new DomainException("Tên pháp nhân và mã số thuế hợp lệ là bắt buộc.");
        if (!Regex.IsMatch(bankCode ?? "", @"^[A-Za-z0-9]{2,20}$") || !Regex.IsMatch(accountNumber ?? "", @"^\d{6,30}$") || string.IsNullOrWhiteSpace(accountName)) throw new DomainException("Thông tin ngân hàng không hợp lệ.");
        LegalName = legalName.Trim(); TaxNumber = taxNumber!.Trim(); BankCode = bankCode!; BankAccountNumber = accountNumber!; BankAccountName = accountName.Trim(); AgreementAccepted = agreementAccepted;
        Status = VerificationStatus.Draft; SetUpdated();
    }
    public void Submit()
    {
        if (string.IsNullOrWhiteSpace(TaxNumber) || !AgreementAccepted) throw new DomainException("Cần hoàn thiện MST, ngân hàng và đồng ý thỏa thuận đối tác.");
        Status = VerificationStatus.Submitted; SetUpdated();
    }
    public void Review(bool approved) { Status = approved ? VerificationStatus.Approved : VerificationStatus.Rejected; SetUpdated(); }
    public void Suspend() { Status = VerificationStatus.Suspended; SetUpdated(); }
}

public class Notification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = "InApp";
    public string Destination { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string? DeduplicationKey { get; private set; }
    public string? ContextKind { get; private set; }
    public Guid? ContextId { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; } = DateTime.UtcNow;
    public int Attempts { get; private set; }
    public string? DeliveryError { get; private set; }
    private Notification() { }
    public static Notification Create(Guid userId, string channel, string destination, string message, string? key = null,
        string? contextKind = null, Guid? contextId = null)
    {
        if ((contextKind == null) != (contextId == null) || contextId == Guid.Empty ||
            contextKind is not (null or "contract" or "quote" or "lead" or "appointment" or "combo" or "task" or "order-request"))
            throw new DomainException("Ngữ cảnh thông báo không hợp lệ.");
        return new() { UserId = userId, Channel = channel, Destination = destination, Message = message,
            DeduplicationKey = key, ContextKind = contextKind, ContextId = contextId };
    }
    public void Read() { ReadAt ??= DateTime.UtcNow; }
    public void Delivered() { SentAt = DateTime.UtcNow; DeliveryError = null; }
    public void Failed(string error) { Attempts++; DeliveryError = error; NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(360, Math.Pow(2, Attempts))); }
}

public class ArticleListingPin : BaseEntity
{
    public Guid ArticleId { get; private set; }
    public Guid ListingId { get; private set; }
    public Article Article { get; private set; } = null!;
    public Listing Listing { get; private set; } = null!;
    private ArticleListingPin() { }
    public static ArticleListingPin Create(Guid articleId, Guid listingId) => new() { ArticleId = articleId, ListingId = listingId };
}
public class EmergencyMatch : BaseEntity
{
    public Guid ContractId { get; private set; }
    public Guid ReplacementListingId { get; private set; }
    public Guid NewLeadId { get; private set; }
    public Guid AgentId { get; private set; }
    public string Reason { get; private set; } = "";
    private EmergencyMatch() { }
    public static EmergencyMatch Create(Guid contractId, Guid listingId, Guid leadId, Guid agentId, string reason)
        => new() { ContractId = contractId, ReplacementListingId = listingId, NewLeadId = leadId, AgentId = agentId, Reason = reason };
}
