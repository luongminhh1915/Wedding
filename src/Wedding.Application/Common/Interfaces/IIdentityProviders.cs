namespace Wedding.Application.Common.Interfaces;
public record GoogleIdentity(string Subject, string Email, string Name, string? Picture);
public interface IGoogleIdentityVerifier { Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct); }
public interface IOtpDelivery
{
    bool EmailEnabled { get; }
    Task SendEmailAsync(string target, string code, CancellationToken ct);
}
