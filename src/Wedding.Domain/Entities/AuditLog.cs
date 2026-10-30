using Wedding.Domain.Common;

namespace Wedding.Domain.Entities;

/// <summary>P0 – Nhật ký thao tác nhạy cảm (tài chính, duyệt, khóa tài khoản...).</summary>
public class AuditLog : BaseEntity
{
    public Guid? ActorId { get; private set; }
    public string? ActorRole { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public string? DataJson { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(Guid? actorId, string? actorRole, string action, string entityType, string? entityId, string? dataJson = null)
        => new() { ActorId = actorId, ActorRole = actorRole, Action = action, EntityType = entityType, EntityId = entityId, DataJson = dataJson };
}
