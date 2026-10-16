using System.Text.Json;
using Microsoft.Extensions.Logging;
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

/// <summary>Mock: không gửi thật SMS/Zalo/Email – chỉ ghi log (tích hợp thật để P7).</summary>
public class LoggingNotificationSender : INotificationSender
{
    private readonly ILogger<LoggingNotificationSender> _logger;
    public LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) => _logger = logger;

    public Task SendAsync(string channel, string to, string message, CancellationToken ct = default)
    {
        _logger.LogInformation("[MOCK {Channel}] -> {To}: {Message}", channel, to, message);
        return Task.CompletedTask;
    }
}

public class MockPaymentGateway : IMockPaymentGateway
{
    public string CreatePaymentRef(string prefix) => $"{prefix}{DateTime.UtcNow:yyMMddHHmm}{Random.Shared.Next(100, 999)}";

    public string BuildVietQrUrl(decimal amount, string reference)
        => $"https://img.vietqr.io/image/970422-0338889999-compact2.png?amount={decimal.ToInt64(amount)}&addInfo={Uri.EscapeDataString(reference)}&accountName=WEDDING%20MATCH";
}
