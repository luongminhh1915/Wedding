using System.Text.Json;
using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class CommissionComponentBasisApproval : BaseEntity
{
    public Guid ComboTemplateRevisionId { get; private set; }
    public Guid ServiceItemId { get; private set; }
    public int Sequence { get; private set; }
    public decimal Rate { get; private set; }
    public CommissionBasisKind BasisKind { get; private set; }
    public string BasisDefinition { get; private set; } = string.Empty;
    public string ExcludedCostsJson { get; private set; } = "[]";
    public string Reason { get; private set; } = string.Empty;
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public CommissionPolicyStatus Status { get; private set; } = CommissionPolicyStatus.Draft;
    public Guid CreatedById { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public Guid? ApprovedById { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();

    public ComboTemplateRevision ComboTemplateRevision { get; private set; } = null!;
    public ServiceItem ServiceItem { get; private set; } = null!;
    public User CreatedBy { get; private set; } = null!;
    public User? ApprovedBy { get; private set; }

    private CommissionComponentBasisApproval() { }

    public static CommissionComponentBasisApproval Create(Guid revisionId, Guid serviceItemId, int sequence, decimal rate,
        CommissionBasisKind basisKind, string basisDefinition, string excludedCostsJson, string reason,
        DateTime effectiveFrom, DateTime? effectiveTo, Guid createdById)
    {
        if (revisionId == Guid.Empty || serviceItemId == Guid.Empty || sequence < 1 || createdById == Guid.Empty)
            throw new DomainException("Đối tượng phê duyệt basis không hợp lệ.");
        if (rate is < .03m or > .12m) throw new DomainException("Tỷ lệ thành phần phải trong khung 3–12%.");
        if (rate != decimal.Round(rate, 4))
            throw new DomainException("Tỷ lệ thành phần chỉ hỗ trợ tối đa 4 chữ số thập phân; không tự làm tròn mức đã yêu cầu.");
        if (basisKind is not (CommissionBasisKind.StandardService or CommissionBasisKind.AzManagementFee))
            throw new DomainException("Basis thành phần không hợp lệ.");
        if (string.IsNullOrWhiteSpace(basisDefinition) || basisDefinition.Trim().Length > 2000)
            throw new DomainException("Cần mô tả cách xác định cơ sở tính, tối đa 2.000 ký tự.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("Cần ghi lý do phê duyệt basis, tối đa 2.000 ký tự.");
        if (effectiveFrom.Kind != DateTimeKind.Utc || effectiveTo is { Kind: not DateTimeKind.Utc } ||
            effectiveTo.HasValue && effectiveTo <= effectiveFrom)
            throw new DomainException("Thời gian hiệu lực phải là UTC và ngày kết thúc phải sau ngày bắt đầu.");
        var exclusionsJson = JsonSerializer.Serialize(ParseExclusions(excludedCostsJson));
        // Match the mapped application storage limit after serialization; escaping
        // can expand otherwise valid Vietnamese/special-character labels considerably.
        if (exclusionsJson.Length > 12000)
            throw new DomainException("Danh sách khoản loại trừ sau chuẩn hóa vượt quá 12.000 ký tự JSON.");
        return new CommissionComponentBasisApproval
        {
            ComboTemplateRevisionId = revisionId,
            ServiceItemId = serviceItemId,
            Sequence = sequence,
            Rate = rate,
            BasisKind = basisKind,
            BasisDefinition = basisDefinition.Trim(),
            ExcludedCostsJson = exclusionsJson,
            Reason = reason.Trim(),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Submit()
    {
        if (Status != CommissionPolicyStatus.Draft) throw new DomainException("Chỉ basis nháp mới được gửi duyệt.");
        Status = CommissionPolicyStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public void Approve(Guid adminId)
    {
        if (Status != CommissionPolicyStatus.Submitted || adminId == Guid.Empty)
            throw new DomainException("Chỉ basis đã gửi mới được Admin duyệt.");
        Status = CommissionPolicyStatus.Approved;
        ApprovedById = adminId;
        ApprovedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public void Reject(Guid adminId, string reason)
    {
        if (Status != CommissionPolicyStatus.Submitted || adminId == Guid.Empty ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("Basis đã gửi cần Admin và lý do từ chối hợp lệ.");
        Status = CommissionPolicyStatus.Rejected;
        ApprovedById = adminId;
        ApprovedAt = DateTime.UtcNow;
        RejectionReason = reason.Trim();
        Version = Guid.NewGuid();
        SetUpdated();
    }

    private static List<string> ParseExclusions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new DomainException("Danh sách khoản loại trừ phải là JSON array hợp lệ.");
        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json);
            if (values == null || values.Count > 50 || values.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 200))
                throw new DomainException("Danh sách khoản loại trừ phải là mảng tối đa 50 mục, mỗi mục tối đa 200 ký tự.");
            return values.Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToList();
        }
        catch (JsonException)
        {
            throw new DomainException("Danh sách khoản loại trừ phải là JSON array hợp lệ.");
        }
    }
}
