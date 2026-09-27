using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class Voucher : BaseEntity
{
    public string Code { get; private set; } = string.Empty; // BR-004: Mã ưu đãi độc nhất 8 ký tự
    public Guid LeadId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal? DiscountValue { get; private set; }
    public decimal? DiscountPercent { get; private set; }
    public VoucherStatus Status { get; private set; } = VoucherStatus.Active;
    public DateTime IssuedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; } // BR-004: Có hiệu lực 30 ngày
    public DateTime? RedeemedAt { get; private set; }

    // Navigation Properties
    public Lead Lead { get; private set; } = null!;
    public User Customer { get; private set; } = null!;
    public BookingContract? BookingContract { get; private set; }

    private Voucher() { } // Dành cho EF Core

    public static Voucher Create(Guid leadId, Guid customerId, string? customCode = null, 
        decimal? discountValue = 500000m, decimal? discountPercent = null)
    {
        var code = customCode ?? GenerateVoucherCode();
        var now = DateTime.UtcNow;

        return new Voucher
        {
            Id = Guid.NewGuid(),
            Code = code.ToUpperInvariant(),
            LeadId = leadId,
            CustomerId = customerId,
            DiscountValue = discountValue,
            DiscountPercent = discountPercent,
            Status = VoucherStatus.Active,
            IssuedAt = now,
            ExpiresAt = now.AddDays(30), // BR-004: 30 ngày hiệu lực
            CreatedAt = now
        };
    }

    public void Redeem()
    {
        if (Status != VoucherStatus.Active)
            throw new DomainException("Mã ưu đãi không khả dụng hoặc đã sử dụng.");

        if (DateTime.UtcNow > ExpiresAt)
        {
            Status = VoucherStatus.Expired;
            throw new DomainException("Mã ưu đãi đã hết hạn 30 ngày.");
        }

        Status = VoucherStatus.Redeemed;
        RedeemedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Expire()
    {
        Status = VoucherStatus.Expired;
        SetUpdated();
    }

    private static string GenerateVoucherCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var randomPart = new string(Enumerable.Repeat(chars, 4)
            .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
        return $"WVIP{randomPart}"; // Đảm bảo 8 ký tự độc nhất
    }
}
