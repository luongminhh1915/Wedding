namespace Wedding.Application.Common.Interfaces;

/// <summary>P0 – Ghi nhật ký thao tác (AuditLog). Chỉ Add vào context; caller tự SaveChanges.</summary>
public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, string? entityId, object? data = null);
}

/// <summary>P1/P7 – Gửi thông báo (SMS/Zalo/Email). Bản hiện tại là mock: chỉ ghi log.</summary>
public interface INotificationSender
{
    Task SendAsync(string channel, string to, string message, CancellationToken ct = default);
}

/// <summary>Mock cổng thanh toán (P2 Premium thiệp, P3 cọc lịch hẹn, P4 webhook ngân hàng).</summary>
public interface IMockPaymentGateway
{
    string CreatePaymentRef(string prefix);
    string BuildVietQrUrl(decimal amount, string reference);
}
