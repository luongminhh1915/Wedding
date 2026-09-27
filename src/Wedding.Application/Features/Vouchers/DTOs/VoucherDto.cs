namespace Wedding.Application.Features.Vouchers.DTOs;

public record VoucherDto(
    Guid Id,
    string Code,
    Guid LeadId,
    Guid CustomerId,
    string CustomerName,
    Guid VendorId,
    string VendorBrandName,
    decimal? DiscountValue,
    decimal? DiscountPercent,
    string Status,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    DateTime? RedeemedAt,
    bool IsExpired
);

public record ValidateVoucherResultDto(
    bool IsValid,
    string? Reason,
    VoucherDto? Voucher
);
