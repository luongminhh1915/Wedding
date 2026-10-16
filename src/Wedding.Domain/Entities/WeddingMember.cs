using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class WeddingMember : BaseEntity
{
    public Guid WeddingProfileId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = "Viewer";
    public User User { get; private set; } = null!;
    private WeddingMember() { }
    public static WeddingMember Create(Guid weddingId, Guid userId, string role)
    { var m = new WeddingMember { WeddingProfileId = weddingId, UserId = userId }; m.SetRole(role); return m; }
    public void SetRole(string role)
    { if (role is not ("Editor" or "Viewer")) throw new DomainException("Quyền thành viên không hợp lệ."); Role = role; SetUpdated(); }
}

public class WeddingJoinInvitation : BaseEntity
{
    public Guid WeddingProfileId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Role { get; private set; } = "Editor";
    public string? Email { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public Guid? JoinedBy { get; private set; }
    private WeddingJoinInvitation() { }
    public static WeddingJoinInvitation Create(Guid weddingId, string role, string? email)
    {
        if (role is not ("Editor" or "Viewer")) throw new DomainException("Quyền lời mời không hợp lệ.");
        if (email?.Length > 256 || (!string.IsNullOrWhiteSpace(email) && !System.Net.Mail.MailAddress.TryCreate(email, out _))) throw new DomainException("Email người nhận không hợp lệ.");
        return new() { WeddingProfileId = weddingId, Role = role, Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            Code = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            ExpiresAt = DateTime.UtcNow.AddDays(7) };
    }
    public void Consume(Guid userId) { JoinedBy = userId; IsRevoked = true; SetUpdated(); }
    public void Revoke() { IsRevoked = true; SetUpdated(); }
}
