using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>Immutable submitted content. Only workflow metadata can transition; old snapshots remain stored.</summary>
public class ComboTemplateRevision : BaseEntity
{
    public Guid ComboTemplateId { get; private set; }
    public int Sequence { get; private set; }
    public string SnapshotJson { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Draft";
    public Guid CreatedById { get; private set; }
    public string CreatedByRole { get; private set; } = string.Empty;
    public DateTime? SubmittedAt { get; private set; }
    public Guid? ReviewedById { get; private set; }
    public string? ReviewedByRole { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTime? SupersededAt { get; private set; }
    public string? SupersededFromStatus { get; private set; }
    public ComboTemplate ComboTemplate { get; private set; } = null!;
    private ComboTemplateRevision() { }

    public static ComboTemplateRevision Create(Guid templateId, int sequence, string snapshotJson,
        Guid actorId, string actorRole, bool submit)
    {
        if (templateId == Guid.Empty || actorId == Guid.Empty || sequence < 1 ||
            string.IsNullOrWhiteSpace(snapshotJson) || snapshotJson.Length > 2000000 ||
            actorRole is not ("VendorOwner" or "Moderator" or "SuperAdmin"))
            throw new DomainException("Phiên bản combo không hợp lệ.");
        return new() { ComboTemplateId = templateId, Sequence = sequence, SnapshotJson = snapshotJson,
            CreatedById = actorId, CreatedByRole = actorRole, Status = submit ? "Pending" : "Draft",
            SubmittedAt = submit ? DateTime.UtcNow : null };
    }
    public void Supersede()
    {
        if (Status is not ("Draft" or "Pending" or "Rejected")) throw new DomainException("Không thể thay thế phiên bản đã duyệt.");
        SupersededFromStatus = Status; Status = "Superseded"; SupersededAt = DateTime.UtcNow; SetUpdated();
    }
    public void Review(bool approve, Guid actorId, string actorRole, string? note)
    {
        if (Status != "Pending" || actorId == Guid.Empty || actorRole is not ("Moderator" or "SuperAdmin"))
            throw new DomainException("Chỉ người kiểm duyệt được xử lý phiên bản đang chờ.");
        var cleanNote = note?.Trim();
        if ((!approve && string.IsNullOrWhiteSpace(cleanNote)) || cleanNote?.Length > 2000)
            throw new DomainException("Lý do từ chối cần từ 1 đến 2.000 ký tự; ghi chú duyệt tối đa 2.000 ký tự.");
        Status = approve ? "Approved" : "Rejected"; ReviewedById = actorId; ReviewedByRole = actorRole;
        ReviewedAt = DateTime.UtcNow; ReviewNote = cleanNote; SetUpdated();
    }
}
