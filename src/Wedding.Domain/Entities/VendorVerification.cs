using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>P6 – Hồ sơ xác minh NCC (GPKD, CCCD người đại diện, ảnh cơ sở).</summary>
public class VendorVerification : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string BusinessLicenseNumber { get; private set; } = string.Empty;
    public string DocumentUrlsJson { get; private set; } = "[]";
    public string? Note { get; private set; }
    public VerificationStatus Status { get; private set; } = VerificationStatus.Submitted;
    public Guid? ReviewedBy { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    public Vendor Vendor { get; private set; } = null!;

    private VendorVerification() { }

    public static VendorVerification Create(Guid vendorId, string licenseNumber, string documentUrlsJson, string? note)
    {
        if (string.IsNullOrWhiteSpace(licenseNumber)) throw new DomainException("Vui lòng nhập số giấy phép kinh doanh.");
        return new() { VendorId = vendorId, BusinessLicenseNumber = licenseNumber.Trim(), DocumentUrlsJson = documentUrlsJson, Note = note };
    }

    public void Review(Guid reviewerId, bool approved, string? note)
    {
        if (Status != VerificationStatus.Submitted) throw new DomainException("Hồ sơ đã được xử lý.");
        if (!approved && string.IsNullOrWhiteSpace(note)) throw new DomainException("Vui lòng nêu lý do yêu cầu bổ sung/từ chối.");
        Status = approved ? VerificationStatus.Approved : VerificationStatus.Rejected;
        ReviewedBy = reviewerId; ReviewNote = note; ReviewedAt = DateTime.UtcNow; SetUpdated();
    }
}

/// <summary>P6 – Tranh chấp hợp đồng do CSKH xử lý (SLA 72h).</summary>
public class Dispute : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public Guid ContractId { get; private set; }
    public Guid OpenedById { get; private set; }
    public string OpenedByRole { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public string? EvidenceUrl { get; private set; }
    public DisputeStatus Status { get; private set; } = DisputeStatus.Open;
    public Guid? AssigneeId { get; private set; }
    public string? Resolution { get; private set; }
    public DateTime SlaDeadline { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public BookingContract Contract { get; private set; } = null!;

    private Dispute() { }

    public static Dispute Create(Guid contractId, Guid openedById, string role, string reason, string? evidenceUrl)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10) throw new DomainException("Vui lòng mô tả lý do tranh chấp (tối thiểu 10 ký tự).");
        return new()
        {
            Code = $"TC-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}", ContractId = contractId, OpenedById = openedById,
            OpenedByRole = role, Reason = reason.Trim(), EvidenceUrl = evidenceUrl, SlaDeadline = DateTime.UtcNow.AddHours(72)
        };
    }

    public void Assign(Guid assigneeId) { if (Status == DisputeStatus.Open) Status = DisputeStatus.InReview; AssigneeId = assigneeId; SetUpdated(); }

    public void Resolve(bool inFavorOfCustomer, string resolution)
    {
        if (Status is DisputeStatus.ResolvedCustomer or DisputeStatus.ResolvedVendor) throw new DomainException("Tranh chấp đã được xử lý.");
        if (string.IsNullOrWhiteSpace(resolution)) throw new DomainException("Vui lòng nhập kết luận xử lý.");
        Status = inFavorOfCustomer ? DisputeStatus.ResolvedCustomer : DisputeStatus.ResolvedVendor;
        Resolution = resolution.Trim(); ResolvedAt = DateTime.UtcNow; SetUpdated();
    }
}
