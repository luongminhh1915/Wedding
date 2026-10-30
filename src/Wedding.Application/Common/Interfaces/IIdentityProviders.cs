namespace Wedding.Application.Common.Interfaces;
public record GoogleIdentity(string Subject, string Email, string Name, string? Picture);
public interface IGoogleIdentityVerifier { Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct); }
public record FacebookIdentity(string Subject, string? Email, string Name, string? Picture);
public interface IFacebookIdentityVerifier
{
    bool IsConfigured { get; }
    string? AppId { get; }
    string? GraphApiVersion { get; }
    Task<FacebookIdentity> VerifyAsync(string accessToken, CancellationToken ct);
}
public interface IOtpDelivery
{
    bool EmailEnabled { get; }
    Task SendEmailAsync(string target, string code, CancellationToken ct);
}
public interface IPasswordResetDelivery
{
    bool PasswordRecoveryEnabled { get; }
    Task SendPasswordResetAsync(string target, string rawToken, CancellationToken ct);
}
