using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public class SmtpDelivery(IConfiguration configuration) : IOtpDelivery, IPasswordResetDelivery
{
    public bool EmailEnabled => !string.IsNullOrWhiteSpace(configuration["Smtp:Host"]) && !string.IsNullOrWhiteSpace(configuration["Smtp:From"]);
    public bool PasswordRecoveryEnabled => EmailEnabled && TryGetPasswordResetBaseUrl(out _);
    public Task SendEmailAsync(string target, string code, CancellationToken ct) => SendAsync(target, "Mã đăng nhập Wedding Match", $"Mã đăng nhập của bạn: {code}\nHiệu lực 5 phút. Không chia sẻ mã với bất kỳ ai.", ct);
    public Task SendPasswordResetAsync(string target, string rawToken, CancellationToken ct)
    {
        if (!TryGetPasswordResetBaseUrl(out var baseUrl))
            throw new DomainException("Khôi phục mật khẩu qua email chưa được cấu hình.");
        var link = new UriBuilder(new Uri(baseUrl!, "/reset-password"))
        {
            // The fragment is not sent to the web server or included in ordinary request logs.
            Fragment = "token=" + Uri.EscapeDataString(rawToken)
        }.Uri.AbsoluteUri;
        return SendAsync(target, "Đặt lại mật khẩu Wedding Match",
            $"Bạn vừa yêu cầu đặt lại mật khẩu Wedding Match. Mở liên kết sau trong 30 phút để tiếp tục:\n\n{link}\n\nNếu bạn không yêu cầu, hãy bỏ qua email này. Không chia sẻ liên kết với bất kỳ ai.", ct);
    }

    private bool TryGetPasswordResetBaseUrl(out Uri? baseUrl)
    {
        baseUrl = null;
        var configured = configuration["Auth:PasswordResetBaseUrl"];
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) ||
            (parsed.Scheme != Uri.UriSchemeHttps && !parsed.IsLoopback) ||
            !string.IsNullOrEmpty(parsed.UserInfo) || parsed.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment)) return false;
        baseUrl = parsed;
        return true;
    }

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

public class FacebookIdentityVerifier(IHttpClientFactory clients, IConfiguration configuration) : IFacebookIdentityVerifier
{
    private string? ConfiguredAppId => configuration["Facebook:AppId"]?.Trim();
    private string? AppSecret => configuration["Facebook:AppSecret"];
    private string? ConfiguredApiVersion => configuration["Facebook:GraphApiVersion"]?.Trim();
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ConfiguredAppId) &&
        !string.IsNullOrWhiteSpace(AppSecret) &&
        ConfiguredApiVersion is { } version && Regex.IsMatch(version, @"^v[0-9]{1,3}\.0$", RegexOptions.CultureInvariant);
    public string? AppId => IsConfigured ? ConfiguredAppId : null;
    public string? GraphApiVersion => IsConfigured ? ConfiguredApiVersion : null;

    public async Task<FacebookIdentity> VerifyAsync(string accessToken, CancellationToken ct)
    {
        if (!IsConfigured) throw new DomainException("Facebook OAuth chưa được cấu hình. Vui lòng dùng mật khẩu hoặc Google.");
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 8192)
            throw new DomainException("Facebook access token không hợp lệ.");

        try
        {
            var appId = ConfiguredAppId!;
            var apiRoot = $"https://graph.facebook.com/{ConfiguredApiVersion}";
            var appAccessToken = $"{appId}|{AppSecret}";
            using var debug = await GetGraphResponseAsync(
                $"{apiRoot}/debug_token?input_token={Uri.EscapeDataString(accessToken)}", appAccessToken, ct);
            var data = debug.RootElement.GetProperty("data");
            var tokenAppId = ReadString(data, "app_id");
            var userId = ReadString(data, "user_id");
            var valid = data.TryGetProperty("is_valid", out var validProperty) && validProperty.ValueKind == JsonValueKind.True;
            var expiresAt = ReadUnixTime(data, "expires_at");
            var dataExpiresAt = ReadUnixTime(data, "data_access_expires_at");
            if (!valid || tokenAppId != appId || string.IsNullOrWhiteSpace(userId) ||
                (expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow) ||
                (dataExpiresAt.HasValue && dataExpiresAt.Value <= DateTimeOffset.UtcNow))
                throw new DomainException("Facebook access token sai ứng dụng, hết hạn hoặc không còn hiệu lực.");

            using var profile = await GetGraphResponseAsync(
                $"{apiRoot}/me?fields=id,name", accessToken, ct);
            var profileId = ReadString(profile.RootElement, "id");
            var name = ReadString(profile.RootElement, "name");
            if (profileId != userId || userId.Length > 255)
                throw new DomainException("Danh tính Facebook không khớp với phiên đăng nhập.");
            return new(userId, null, string.IsNullOrWhiteSpace(name) ? "Khách hàng Facebook" : name, null);
        }
        catch (DomainException) { throw; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new DomainException("Facebook không phản hồi kịp thời. Vui lòng thử lại sau."); }
        catch (HttpRequestException) { throw new DomainException("Chưa thể xác thực với Facebook. Vui lòng thử lại sau."); }
        catch (JsonException) { throw new DomainException("Facebook trả về dữ liệu xác thực không hợp lệ."); }
        catch (KeyNotFoundException) { throw new DomainException("Facebook trả về thiếu thông tin xác thực."); }
        catch (InvalidOperationException) { throw new DomainException("Facebook trả về dữ liệu xác thực không hợp lệ."); }
        catch (ArgumentException) { throw new DomainException("Cấu hình Facebook OAuth chưa hợp lệ."); }
    }

    private async Task<JsonDocument> GetGraphResponseAsync(string url, string bearerToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var response = await clients.CreateClient("identity").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Facebook identity endpoint rejected the request.");
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(body, cancellationToken: ct);
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static DateTimeOffset? ReadUnixTime(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || !value.TryGetInt64(out var seconds) || seconds <= 0) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds); }
        catch (ArgumentOutOfRangeException) { return DateTimeOffset.MinValue; }
    }
}
