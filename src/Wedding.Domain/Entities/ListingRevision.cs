using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>Append-only content snapshots. Review changes status, never rewrites the proposed content.</summary>
public class ListingRevision : BaseEntity
{
    public Guid ListingId { get; private set; }
    public int Sequence { get; private set; }
    public string ContentJson { get; private set; } = "";
    public string Status { get; private set; } = "Draft";
    public Guid CreatedById { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public Guid? ReviewedById { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNote { get; private set; }
    public Listing Listing { get; private set; } = null!;
    private ListingRevision() { }
    public static ListingRevision Create(Guid listingId, int sequence, string contentJson, Guid actor, bool submit = false)
    {
        if (listingId == Guid.Empty || actor == Guid.Empty || sequence < 1 || string.IsNullOrEmpty(contentJson) || contentJson.Length > 160000)
            throw new DomainException("Phiên bản dịch vụ không hợp lệ hoặc vượt giới hạn lưu trữ.");
        return new() { ListingId = listingId, Sequence = sequence, ContentJson = contentJson, CreatedById = actor,
            Status = submit ? "Pending" : "Draft", SubmittedAt = submit ? DateTime.UtcNow : null };
    }
    public void Supersede() { if (Status is "Draft" or "Pending") { Status = "Superseded"; SetUpdated(); } }
    public static ListingRevision CaptureBaseline(Guid listingId, string contentJson, Guid capturedBy)
    {
        var revision = Create(listingId, 1, contentJson, capturedBy);
        revision.Status = "Baseline"; // Captured now, not an invented historical approval/actor.
        return revision;
    }
    public void Review(bool approve, Guid actor, string? note)
    {
        if (Status != "Pending" || actor == Guid.Empty) throw new DomainException("Chỉ kiểm duyệt phiên bản đang chờ.");
        var reason = note?.Trim();
        if ((!approve && string.IsNullOrEmpty(reason)) || reason?.Length > 1000) throw new DomainException("Lý do từ chối cần từ 1 đến 1000 ký tự.");
        Status = approve ? "Approved" : "Rejected"; ReviewedById = actor; ReviewedAt = DateTime.UtcNow; ReviewNote = reason; SetUpdated();
    }
}
