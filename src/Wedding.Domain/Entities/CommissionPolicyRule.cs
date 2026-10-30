using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class CommissionPolicyRule : BaseEntity
{
    public CommissionRuleScope Scope { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? VendorId { get; private set; }
    public Guid? ServiceItemId { get; private set; }
    public Guid? ComboTemplateId { get; private set; }
    public Guid? ComboTemplateRevisionId { get; private set; }
    public decimal? Rate { get; private set; }
    public CommissionBasisKind BasisKind { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
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

    public Category? Category { get; private set; }
    public Vendor? Vendor { get; private set; }
    public ServiceItem? ServiceItem { get; private set; }
    public ComboTemplate? ComboTemplate { get; private set; }
    public ComboTemplateRevision? ComboTemplateRevision { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public User? ApprovedBy { get; private set; }

    private CommissionPolicyRule() { }

    public static CommissionPolicyRule Create(
        CommissionRuleScope scope,
        Guid? categoryId,
        Guid? vendorId,
        Guid? serviceItemId,
        Guid? comboTemplateId,
        Guid? comboTemplateRevisionId,
        decimal? rate,
        CommissionBasisKind basisKind,
        string policyVersion,
        string reason,
        DateTime effectiveFrom,
        DateTime? effectiveTo,
        Guid createdById)
    {
        ValidateTarget(scope, categoryId, vendorId, serviceItemId, comboTemplateId, comboTemplateRevisionId);
        if (!Enum.IsDefined(basisKind)) throw new DomainException("Cơ sở tính hoa hồng không hợp lệ.");
        if (scope == CommissionRuleScope.Combo)
        {
            if (basisKind != CommissionBasisKind.ComboComponents || rate.HasValue)
                throw new DomainException("Policy Combo chỉ xác định phiên bản; rate và basis phải được duyệt riêng cho từng dịch vụ thành phần.");
        }
        else if (basisKind == CommissionBasisKind.ComboComponents || !rate.HasValue || rate is < .03m or > .12m)
            throw new DomainException("Policy không-Combo cần cơ sở hợp lệ và tỷ lệ trong khung 3–12%.");
        if (rate.HasValue && rate.Value != decimal.Round(rate.Value, 4))
            throw new DomainException("Tỷ lệ hoa hồng chỉ được có tối đa 4 chữ số thập phân để lưu chính xác.");
        if (string.IsNullOrWhiteSpace(policyVersion) || policyVersion.Trim().Length > 100)
            throw new DomainException("Phiên bản chính sách là bắt buộc, tối đa 100 ký tự.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("Cần ghi lý do chính sách, tối đa 2000 ký tự.");
        if (effectiveFrom.Kind != DateTimeKind.Utc || (effectiveTo.HasValue && effectiveTo.Value.Kind != DateTimeKind.Utc) ||
            (effectiveTo.HasValue && effectiveTo <= effectiveFrom))
            throw new DomainException("Thời gian hiệu lực phải là UTC và ngày kết thúc phải sau ngày bắt đầu.");
        if (createdById == Guid.Empty) throw new DomainException("Người tạo chính sách không hợp lệ.");

        return new CommissionPolicyRule
        {
            Scope = scope,
            CategoryId = categoryId,
            VendorId = vendorId,
            ServiceItemId = serviceItemId,
            ComboTemplateId = comboTemplateId,
            ComboTemplateRevisionId = comboTemplateRevisionId,
            Rate = rate,
            BasisKind = basisKind,
            PolicyVersion = policyVersion.Trim(),
            Reason = reason.Trim(),
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Submit()
    {
        if (Status != CommissionPolicyStatus.Draft) throw new DomainException("Chỉ chính sách nháp mới được gửi duyệt.");
        Status = CommissionPolicyStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public void Approve(Guid adminId)
    {
        if (Status != CommissionPolicyStatus.Submitted) throw new DomainException("Chỉ chính sách đã gửi mới được duyệt.");
        if (adminId == Guid.Empty) throw new DomainException("Người duyệt không hợp lệ.");
        Status = CommissionPolicyStatus.Approved;
        ApprovedById = adminId;
        ApprovedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public void Reject(Guid adminId, string reason)
    {
        if (Status != CommissionPolicyStatus.Submitted) throw new DomainException("Chỉ chính sách đã gửi mới được từ chối.");
        if (adminId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("Người duyệt và lý do từ chối hợp lệ là bắt buộc.");
        Status = CommissionPolicyStatus.Rejected;
        ApprovedById = adminId;
        RejectionReason = reason.Trim();
        ApprovedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public static bool HasSameTarget(CommissionPolicyRule left, CommissionPolicyRule right) =>
        left.Scope == right.Scope && left.CategoryId == right.CategoryId && left.VendorId == right.VendorId &&
        left.ServiceItemId == right.ServiceItemId && left.ComboTemplateId == right.ComboTemplateId &&
        left.ComboTemplateRevisionId == right.ComboTemplateRevisionId;

    public bool Overlaps(CommissionPolicyRule other)
    {
        if (!HasSameTarget(this, other)) return false;
        return (!EffectiveTo.HasValue || other.EffectiveFrom <= EffectiveTo.Value) &&
            (!other.EffectiveTo.HasValue || EffectiveFrom <= other.EffectiveTo.Value);
    }

    private static void ValidateTarget(CommissionRuleScope scope, Guid? categoryId, Guid? vendorId, Guid? serviceItemId,
        Guid? comboTemplateId, Guid? comboTemplateRevisionId)
    {
        if (!Enum.IsDefined(scope)) throw new DomainException("Phạm vi chính sách không hợp lệ.");
        var valid = scope switch
        {
            CommissionRuleScope.System => !categoryId.HasValue && !vendorId.HasValue && !serviceItemId.HasValue && !comboTemplateId.HasValue && !comboTemplateRevisionId.HasValue,
            CommissionRuleScope.Category => categoryId.HasValue && !vendorId.HasValue && !serviceItemId.HasValue && !comboTemplateId.HasValue && !comboTemplateRevisionId.HasValue,
            CommissionRuleScope.Vendor => !categoryId.HasValue && vendorId.HasValue && !serviceItemId.HasValue && !comboTemplateId.HasValue && !comboTemplateRevisionId.HasValue,
            CommissionRuleScope.VendorCategory => categoryId.HasValue && vendorId.HasValue && !serviceItemId.HasValue && !comboTemplateId.HasValue && !comboTemplateRevisionId.HasValue,
            CommissionRuleScope.Service => !categoryId.HasValue && !vendorId.HasValue && serviceItemId.HasValue && !comboTemplateId.HasValue && !comboTemplateRevisionId.HasValue,
            CommissionRuleScope.Combo => !categoryId.HasValue && !vendorId.HasValue && !serviceItemId.HasValue && comboTemplateId.HasValue && comboTemplateRevisionId.HasValue,
            _ => false
        };
        if (!valid || categoryId == Guid.Empty || vendorId == Guid.Empty || serviceItemId == Guid.Empty ||
            comboTemplateId == Guid.Empty || comboTemplateRevisionId == Guid.Empty)
            throw new DomainException("Các đối tượng đích phải khớp chính xác phạm vi chính sách.");
    }
}
