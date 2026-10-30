using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;
public class IdentityTests
{
    [Fact] public void OtpIsHashedSingleUseAndLimited()
    {
        var otp = OtpCode.Create("test@example.com", out var delivered);
        Assert.NotEqual(delivered, otp.Code); Assert.Equal(64, otp.Code.Length);
        for (var i = 0; i < 5; i++) Assert.Throws<DomainException>(() => otp.Verify("000000"));
        Assert.Throws<DomainException>(() => otp.Verify(delivered));
        var valid = OtpCode.Create("test@example.com", out var code); valid.Verify(code);
        Assert.Throws<DomainException>(() => valid.Verify(code));
    }
    [Fact] public async Task GoogleDisabledFailsClosed()
    {
        var verifier = new GoogleIdentityVerifier(new Clients(), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<DomainException>(() => verifier.VerifyAsync("forged", default));
    }
    [Fact] public async Task FacebookDisabledFailsClosed()
    {
        var verifier = new FacebookIdentityVerifier(new Clients(), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<DomainException>(() => verifier.VerifyAsync("forged", default));
    }
    [Fact] public async Task FacebookVerifierChecksAppTokenAndMatchesGraphProfileToTokenSubject()
    {
        var appId = "test-facebook-app";
        var version = "v99.0";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var graphCalls = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            graphCalls.Add(request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath.EndsWith("/debug_token", StringComparison.Ordinal))
            {
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal(appId + "|test-app-secret", request.Headers.Authorization?.Parameter);
                var payload = System.Text.Json.JsonSerializer.Serialize(new { data = new
                {
                    app_id = appId, is_valid = true, user_id = "facebook-user-42",
                    expires_at = expiresAt, data_access_expires_at = expiresAt
                } });
                return JsonResponse(payload);
            }
            Assert.Equal("/" + version + "/me", request.RequestUri.AbsolutePath);
            Assert.Equal("?fields=id,name", request.RequestUri.Query);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("facebook-user-token", request.Headers.Authorization?.Parameter);
            return JsonResponse(System.Text.Json.JsonSerializer.Serialize(new
            {
                id = "facebook-user-42", name = "Facebook Customer"
            }));
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = appId,
            ["Facebook:AppSecret"] = "test-app-secret",
            ["Facebook:GraphApiVersion"] = version
        }).Build();

        var identity = await new FacebookIdentityVerifier(new HandlerClients(handler), configuration)
            .VerifyAsync("facebook-user-token", default);
        Assert.Equal("facebook-user-42", identity.Subject);
        Assert.Null(identity.Email);
        Assert.Equal("Facebook Customer", identity.Name);
        Assert.True(new FacebookIdentityVerifier(new HandlerClients(handler), configuration).IsConfigured);
        Assert.Equal(new[] { "/" + version + "/debug_token", "/" + version + "/me" }, graphCalls);
    }
    [Fact] public async Task FacebookVerifierRejectsTokenIssuedForDifferentAppBeforeReadingProfile()
    {
        var graphCalls = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            graphCalls.Add(request.RequestUri!.AbsolutePath);
            var payload = System.Text.Json.JsonSerializer.Serialize(new { data = new
            {
                app_id = "different-app", is_valid = true, user_id = "facebook-user-42",
                expires_at = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
            } });
            return JsonResponse(payload);
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Facebook:AppId"] = "expected-app", ["Facebook:AppSecret"] = "test-secret",
            ["Facebook:GraphApiVersion"] = "v99.0"
        }).Build();
        var verifier = new FacebookIdentityVerifier(new HandlerClients(handler), configuration);
        await Assert.ThrowsAsync<DomainException>(() => verifier.VerifyAsync("foreign-token", default));
        Assert.Single(graphCalls);
    }
    [Fact] public void MissingPaymentConfigurationNeverUsesDemoBank()
    {
        var gateway = new MockPaymentGateway(new ConfigurationBuilder().Build());
        Assert.Empty(gateway.BuildVietQrUrl(130000, "TEST"));
        Assert.NotEqual(gateway.CreatePaymentRef("WM"), gateway.CreatePaymentRef("WM"));
    }
    [Fact] public async Task AuditIsAppendOnly()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        var audit = AuditLog.Create(null, "System", "Test", "Test", "1"); db.AuditLogs.Add(audit); await db.SaveChangesAsync();
        db.AuditLogs.Remove(audit); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    private sealed class Clients : IHttpClientFactory { public HttpClient CreateClient(string name) => new(); }
    private static HttpResponseMessage JsonResponse(string json) => new(System.Net.HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };
    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
    private sealed class HandlerClients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
