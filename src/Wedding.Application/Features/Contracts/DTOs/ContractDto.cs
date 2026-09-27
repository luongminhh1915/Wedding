namespace Wedding.Application.Features.Contracts.DTOs;

public record ContractCommissionDto(
    Guid Id,
    string Period,
    decimal CommissionRate,
    decimal CommissionAmount,
    DateTime DueDate,
    string Status
);

public record ContractDto(
    Guid Id,
    string ContractCode,
    Guid LeadId,
    Guid? VoucherId,
    string? VoucherCode,
    decimal? VoucherDiscount,
    Guid VendorId,
    string VendorBrandName,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    decimal ContractValue,
    decimal DepositAmount,
    string? ContractImageUrl,
    DateTime? WeddingDate,
    string Status,
    DateTime VerificationDeadline,
    DateTime? ConfirmedAt,
    DateTime? CompletedAt,
    string? CancellationReason,
    DateTime CreatedAt,
    List<ContractCommissionDto> Commissions
);
