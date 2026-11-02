using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Services;

public class AuditLogger : IAuditLogger
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AuditLogger(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public Task LogAsync(string action, string entityType, string? entityId, object? data = null)
    {
        _context.AuditLogs.Add(AuditLog.Create(_currentUser.UserId, _currentUser.Role, action, entityType, entityId,
            data == null ? null : JsonSerializer.Serialize(data)));
        return Task.CompletedTask;
    }
}

/// <summary>Persist in-app messages and a durable email outbox; never log message bodies or OTPs.</summary>
public class DurableNotificationSender(IApplicationDbContext context) : INotificationSender
{
    public Task SendAsync(string channel, string to, string message, CancellationToken ct = default)
        => PersistAsync(channel, to, message, null, null, ct);

    public Task SendContextAsync(string channel, string to, string message, string contextKind, Guid contextId, CancellationToken ct = default)
        => PersistAsync(channel, to, message, contextKind, contextId, ct);

    private async Task PersistAsync(string channel, string to, string message, string? contextKind, Guid? contextId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(to)) return;
        var user = Guid.TryParse(to, out var id) ? await context.Users.FindAsync(new object[] { id }, ct)
            : await context.Users.FirstOrDefaultAsync(u => u.Email == to || u.PhoneNumber == to, ct);
        if (user == null) return; // Never send private data to a masked or unknown address.
        var notification = Notification.Create(user.Id, channel == "InApp" || string.IsNullOrWhiteSpace(user.Email) ? "InApp" : "Email", user.Email, message,
            contextKind: contextKind, contextId: contextId);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(ct);
    }
}

public class MockPaymentGateway(IConfiguration configuration) : IMockPaymentGateway
{
    public string CreatePaymentRef(string prefix) => $"{prefix}{Guid.NewGuid():N}".ToUpperInvariant();

    public string BuildVietQrUrl(decimal amount, string reference)
    {
        var bank = configuration["Payments:BankCode"]; var account = configuration["Payments:AccountNumber"]; var name = configuration["Payments:AccountName"];
        if (string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(name)) return "";
        return $"https://img.vietqr.io/image/{Uri.EscapeDataString(bank)}-{Uri.EscapeDataString(account)}-compact2.png?amount={decimal.ToInt64(amount)}&addInfo={Uri.EscapeDataString(reference)}&accountName={Uri.EscapeDataString(name)}";
    }
}
