using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>P1 – Mã OTP đăng nhập qua SĐT/email (gửi SMS/Zalo được mock).</summary>
public class OtpCode : BaseEntity
{
    public string Target { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public int Attempts { get; private set; }
    public bool IsUsed { get; private set; }

    private OtpCode() { }

    public static OtpCode Create(string target)
        => new()
        {
            Target = target.Trim().ToLowerInvariant(),
            Code = Random.Shared.Next(100000, 999999).ToString(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

    public void Verify(string code)
    {
        if (IsUsed || DateTime.UtcNow > ExpiresAt) throw new DomainException("Mã OTP đã hết hạn, vui lòng gửi lại.");
        if (Attempts >= 5) throw new DomainException("Bạn đã nhập sai quá 5 lần, vui lòng gửi lại mã.");
        if (Code != code.Trim()) { Attempts++; throw new DomainException("Mã OTP không chính xác."); }
        IsUsed = true;
    }
}
