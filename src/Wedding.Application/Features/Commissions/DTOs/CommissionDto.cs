using Wedding.Application.Common.Interfaces;

namespace Wedding.Application.Features.Commissions.DTOs;

public record CommissionItemDto(
    Guid Id,
    Guid ContractId,
    string ContractCode,
    string CustomerName,
    decimal ContractValue,
    string Period,
    decimal CommissionRate,
    decimal CommissionAmount,
    DateTime DueDate,
    string Status,
    DateTime? PaidAt,
    string? PaymentReferenceCode,
    string? QrImageUrl
);

public record MonthlySettlementStatementDto(
    Guid VendorId,
    string VendorBrandName,
    int Month,
    int Year,
    DateTime SettlementDate, // Ngày 25
    DateTime DueDate,        // Ngày cuối tháng
    decimal TotalContractValue,
    decimal TotalCommissionAmount,
    decimal TotalPendingAmount,
    decimal TotalPaidAmount,
    decimal TotalOverdueAmount,
    string PaymentStatus, // Pending, Paid, PartiallyPaid, Overdue
    string TransferContent, // HH <VendorId> T<Month>
    VietQrInfo? VietQr,
    List<CommissionItemDto> Items
);
