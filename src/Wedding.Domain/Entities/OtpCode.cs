using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;
using System.Security.Cryptography;
using System.Text;

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

    public static OtpCode Create(string target, out string deliveryCode)
    {
        deliveryCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otp = new OtpCode { Target = target.Trim().ToLowerInvariant(), ExpiresAt = DateTime.UtcNow.AddMinutes(5) };
        otp.Code = otp.Digest(deliveryCode);
        return otp;
    }

    private string Digest(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{Id:N}:{code}")));
    public void InvalidateDelivery() { IsUsed = true; SetUpdated(); }

    public void Verify(string code)
    {
        if (IsUsed || DateTime.UtcNow > ExpiresAt) throw new DomainException("Mã OTP đã hết hạn, vui lòng gửi lại.");
        if (Attempts >= 5) throw new DomainException("Bạn đã nhập sai quá 5 lần, vui lòng gửi lại mã.");
        if (code == null || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Code), Encoding.UTF8.GetBytes(Digest(code.Trim()))))
        { Attempts++; throw new DomainException("Mã OTP không chính xác."); }
        IsUsed = true;
    }
}
