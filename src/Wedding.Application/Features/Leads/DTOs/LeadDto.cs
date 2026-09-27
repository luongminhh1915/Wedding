namespace Wedding.Application.Features.Leads.DTOs;

public record VoucherSummaryDto(
    Guid Id,
    string Code,
    decimal? DiscountValue,
    decimal? DiscountPercent,
    string Status,
    DateTime ExpiresAt
);

public record LeadDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid VendorId,
    string VendorBrandName,
    Guid? ListingId,
    string? ListingTitle,
    string PhoneNumber, // BR-002: Bị mask '0987***123' với Vendor nếu IsPhoneUnlocked = false
    bool IsPhoneUnlocked,
    DateTime? WeddingDate,
    int? EstimatedGuests,
    decimal? EstimatedBudget,
    string? Notes,
    string Status,
    DateTime SlaDeadline,
    DateTime? AcceptedAt,
    DateTime CreatedAt,
    VoucherSummaryDto? Voucher
);

public record SendLeadResponseDto(
    Guid LeadId,
    string Status,
    DateTime SlaDeadline,
    VoucherSummaryDto Voucher,
    string Message
);
