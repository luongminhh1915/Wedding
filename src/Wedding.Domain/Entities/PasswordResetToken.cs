using System.Security.Cryptography;
using System.Text;
using Wedding.Domain.Common;

namespace Wedding.Domain.Entities;

/// <summary>Short-lived, single-use credential reset proof. The raw token is never persisted.</summary>
public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(Guid userId, out string rawToken)
    {
        rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new PasswordResetToken
        {
            UserId = userId,
            TokenHash = Hash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }

    public static string Hash(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken ?? string.Empty)));

    public bool TryConsume(DateTime nowUtc)
    {
        if (UsedAt.HasValue || ExpiresAt <= nowUtc) return false;
        UsedAt = nowUtc;
        SetUpdated();
        return true;
    }

    public void Revoke(DateTime nowUtc)
    {
        if (UsedAt.HasValue) return;
        UsedAt = nowUtc;
        SetUpdated();
    }
}
