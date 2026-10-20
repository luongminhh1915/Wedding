using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public class SmtpDelivery(IConfiguration configuration) : IOtpDelivery
{
    public bool EmailEnabled => !string.IsNullOrWhiteSpace(configuration["Smtp:Host"]) && !string.IsNullOrWhiteSpace(configuration["Smtp:From"]);
    public Task SendEmailAsync(string target, string code, CancellationToken ct) => SendAsync(target, "Mã đăng nhập Wedding Match", $"Mã đăng nhập của bạn: {code}\nHiệu lực 5 phút. Không chia sẻ mã với bất kỳ ai.", ct);
    public async Task SendAsync(string target, string subject, string body, CancellationToken ct)
    {
        if (!EmailEnabled) throw new DomainException("Email SMTP chưa được cấu hình.");
        using var client = new SmtpClient(configuration["Smtp:Host"], configuration.GetValue("Smtp:Port", 587))
        {
            EnableSsl = true, Timeout = 10000,
            Credentials = new NetworkCredential(configuration["Smtp:Username"], configuration["Smtp:Password"])
        };
        using var mail = new MailMessage(configuration["Smtp:From"]!, target, subject, body);
        try { await client.SendMailAsync(mail, ct); }
        catch (SmtpException) { throw new DomainException("Không gửi được email. Vui lòng thử lại sau hoặc đăng nhập bằng mật khẩu."); }
    }
}
public class GoogleIdentityVerifier(IHttpClientFactory clients, IConfiguration configuration) : IGoogleIdentityVerifier
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static JsonWebKeySet? cachedKeys;
    private static DateTime expiresAt;
    public async Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct)
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId)) throw new DomainException("Google OAuth chưa được cấu hình. Vui lòng dùng mật khẩu.");
        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 16000) throw new DomainException("Google ID token không hợp lệ.");
        await Gate.WaitAsync(ct);
        try
        {
            if (cachedKeys == null || expiresAt <= DateTime.UtcNow)
            {
                using var response = await clients.CreateClient("identity").GetAsync("https://www.googleapis.com/oauth2/v3/certs", ct);
                response.EnsureSuccessStatusCode(); cachedKeys = new JsonWebKeySet(await response.Content.ReadAsStringAsync(ct));
                expiresAt = DateTime.UtcNow.AddSeconds(Math.Clamp(response.Headers.CacheControl?.MaxAge?.TotalSeconds ?? 300, 60, 3600));
            }
        }
        catch (HttpRequestException) { throw new DomainException("Chưa thể xác thực với Google. Vui lòng thử lại sau."); }
        finally { Gate.Release(); }
        try
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(idToken, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKeys = cachedKeys!.GetSigningKeys(), ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                ValidateIssuer = true, ValidIssuers = new[] { "accounts.google.com", "https://accounts.google.com" },
                ValidateAudience = true, ValidAudience = clientId, ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
            var subject = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email) || principal.FindFirst("email_verified")?.Value is not ("true" or "True")) throw new DomainException("Google chưa xác thực email này.");
            return new(subject, email, principal.FindFirst("name")?.Value ?? email, principal.FindFirst("picture")?.Value);
        }
        catch (SecurityTokenException) { throw new DomainException("Google ID token sai chữ ký, sai ứng dụng hoặc đã hết hạn."); }
        catch (ArgumentException) { throw new DomainException("Google ID token không hợp lệ."); }
    }
}
