using Wedding.Domain.Entities;

namespace Wedding.Application.Features.Contracts.DTOs;

public record ContractCategorySnapshotDto(Guid CategoryId, string CategoryName, Guid? BusinessGroupId,
    string? BusinessGroupName, string RateSource, string? PolicyVersion);

public record ContractCommissionDto(
    Guid Id,
    string Period,
    decimal CommissionRate,
    decimal CommissionAmount,
    DateTime DueDate,
    string Status
);

public static class ContractCategorySnapshotMap
{
    public static ContractDto WithCategorySnapshot(this ContractDto dto, BookingContract contract) => dto with
    {
        CategorySnapshot = contract.CommissionCategoryIdSnapshot.HasValue ? new(contract.CommissionCategoryIdSnapshot.Value,
            contract.CommissionCategoryNameSnapshot!, contract.CommissionBusinessGroupIdSnapshot,
            contract.CommissionBusinessGroupNameSnapshot, contract.CommissionRateSourceSnapshot!, contract.CommissionPolicyVersionSnapshot) : null
    };
}

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
    List<ContractCommissionDto> Commissions,
    string? CancellationPolicySnapshot = null,
    decimal? CommissionRateSnapshot = null,
    string? RejectReason = null,
    int RevisionCount = 0,
    decimal AppointmentDepositCredit = 0
)
{
    public ContractCategorySnapshotDto? CategorySnapshot { get; init; }
}
