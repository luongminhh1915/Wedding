namespace Wedding.Application.Common.Interfaces;

public record AdvancePaymentNoticeDto(
    Guid Id,
    Guid ContractId,
    string ContractCode,
    Guid VendorId,
    string VendorBrandName,
    decimal Amount,
    string Reason,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName,
    string? Note,
    string Status, // "PendingApproval" | "Approved" | "Rejected"
    DateTime RequestedAt,
    DateTime? ProcessedAt = null,
    string? AdminNote = null
);

public interface IAdvancePaymentService
{
    Task<AdvancePaymentNoticeDto> CreateRequestAsync(
        Guid contractId,
        string contractCode,
        Guid vendorId,
        string vendorBrandName,
        decimal amount,
        string reason,
        string? bankName = null,
        string? bankAccountNumber = null,
        string? bankAccountName = null,
        string? note = null
    );

    Task<List<AdvancePaymentNoticeDto>> GetAllAsync();
    Task<List<AdvancePaymentNoticeDto>> GetByContractIdAsync(Guid contractId);
    Task<AdvancePaymentNoticeDto?> GetPendingByContractIdAsync(Guid contractId);
    Task<AdvancePaymentNoticeDto?> GetByIdAsync(Guid id);
    Task<AdvancePaymentNoticeDto?> ApproveAsync(Guid id, string? adminNote = null);
    Task<AdvancePaymentNoticeDto?> RejectAsync(Guid id, string? reason = null);
}
