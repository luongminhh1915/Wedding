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
}
