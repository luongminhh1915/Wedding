using Wedding.Domain.Common;

namespace Wedding.Domain.Entities;

/// <summary>P0 – Thông báo tạm ứng hoa hồng (trước đây lưu trong bộ nhớ/JSON).</summary>
public class AdvancePayment : BaseEntity
{
    public Guid ContractId { get; private set; }
    public string ContractCode { get; private set; } = string.Empty;
    public Guid VendorId { get; private set; }
    public string VendorBrandName { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? BankName { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? BankAccountName { get; private set; }
    public string? Note { get; private set; }
    public string Status { get; private set; } = "PendingApproval";
    public DateTime? ProcessedAt { get; private set; }
    public string? AdminNote { get; private set; }

    private AdvancePayment() { }

    public static AdvancePayment Create(Guid contractId, string contractCode, Guid vendorId, string vendorBrandName,
        decimal amount, string reason, string? bankName, string? bankAccountNumber, string? bankAccountName, string? note)
        => new()
        {
            ContractId = contractId, ContractCode = contractCode, VendorId = vendorId, VendorBrandName = vendorBrandName,
            Amount = amount, Reason = reason, BankName = bankName, BankAccountNumber = bankAccountNumber,
            BankAccountName = bankAccountName, Note = note
        };

    public void Process(bool approved, string? adminNote)
    {
        Status = approved ? "Approved" : "Rejected";
        ProcessedAt = DateTime.UtcNow;
        AdminNote = adminNote;
        SetUpdated();
    }
}
