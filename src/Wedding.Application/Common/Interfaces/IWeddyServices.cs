namespace Wedding.Application.Common.Interfaces;

/// <summary>P0 – Ghi nhật ký thao tác (AuditLog). Chỉ Add vào context; caller tự SaveChanges.</summary>
public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, string? entityId, object? data = null);
}

/// <summary>Persist in-app/email notifications; unsupported ZNS/SMS requests fall back to known-user email.</summary>
public interface INotificationSender
{
    Task SendAsync(string channel, string to, string message, CancellationToken ct = default);
    // Existing senders remain compatible; the durable sender stores typed resource context, never arbitrary URLs.
    Task SendContextAsync(string channel, string to, string message, string contextKind, Guid contextId, CancellationToken ct = default)
        => SendAsync(channel, to, message, ct);
}

/// <summary>Legacy interface name: generates references/QR only, never confirms a payment.</summary>
public interface IMockPaymentGateway
{
    string CreatePaymentRef(string prefix);
    string BuildVietQrUrl(decimal amount, string reference);
}
