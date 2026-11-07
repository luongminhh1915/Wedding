using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class CommissionNegotiationRequest : BaseEntity
{
    public Guid VendorId { get; private set; }
    public CommissionRuleScope Scope { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? ServiceItemId { get; private set; }
    public decimal CurrentRate { get; private set; }
    public decimal ProposedRate { get; private set; }
    public decimal? CounterRate { get; private set; }
    public decimal? ApprovedRate { get; private set; }
    public NegotiationReasonCategory ReasonCategory { get; private set; }
    public string ReasonDetail { get; private set; } = string.Empty;
    public string? CooperationPlan { get; private set; }
    public NegotiationStatus Status { get; private set; } = NegotiationStatus.Submitted;
    public Guid CreatedById { get; private set; }
    public Guid? AssignedModeratorId { get; private set; }
    public Guid? DecidedById { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionReason { get; private set; }
    public Guid? ResultingPolicyRuleId { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();

    public Vendor Vendor { get; private set; } = null!;
    public Category? Category { get; private set; }
    public ServiceItem? ServiceItem { get; private set; }
    public User CreatedBy { get; private set; } = null!;
    public User? AssignedModerator { get; private set; }
    public User? DecidedBy { get; private set; }
    public CommissionPolicyRule? ResultingPolicyRule { get; private set; }
    public ICollection<CommissionNegotiationMessage> Messages { get; private set; } = new List<CommissionNegotiationMessage>();

    private CommissionNegotiationRequest() { }

    public static CommissionNegotiationRequest Create(
        Guid vendorId,
        CommissionRuleScope scope,
        Guid? categoryId,
        Guid? serviceItemId,
        decimal currentRate,
        decimal proposedRate,
        NegotiationReasonCategory reasonCategory,
        string reasonDetail,
        string? cooperationPlan,
        Guid createdById)
    {
        if (vendorId == Guid.Empty) throw new DomainException("Nhà cung cấp không hợp lệ.");
        if (createdById == Guid.Empty) throw new DomainException("Người tạo yêu cầu không hợp lệ.");
        ValidateScopeAndTargets(scope, categoryId, serviceItemId);

        if (currentRate < 0) throw new DomainException("Tỷ lệ hoa hồng hiện tại không hợp lệ.");
        if (proposedRate is < 0.03m or > 0.12m)
            throw new DomainException("Tỷ lệ hoa hồng đề xuất phải nằm trong khung 3–12%.");
        if (!Enum.IsDefined(reasonCategory))
            throw new DomainException("Nhóm lý do đàm phán không hợp lệ.");
        if (string.IsNullOrWhiteSpace(reasonDetail) || reasonDetail.Trim().Length > 2000)
            throw new DomainException("Chi tiết lý do đàm phán là bắt buộc và tối đa 2000 ký tự.");
        if (!string.IsNullOrWhiteSpace(cooperationPlan) && cooperationPlan.Trim().Length > 2000)
            throw new DomainException("Kế hoạch hợp tác tối đa 2000 ký tự.");

        var request = new CommissionNegotiationRequest
        {
            VendorId = vendorId,
            Scope = scope,
            CategoryId = categoryId,
            ServiceItemId = serviceItemId,
            CurrentRate = decimal.Round(currentRate, 4),
            ProposedRate = decimal.Round(proposedRate, 4),
            ReasonCategory = reasonCategory,
            ReasonDetail = reasonDetail.Trim(),
            CooperationPlan = string.IsNullOrWhiteSpace(cooperationPlan) ? null : cooperationPlan.Trim(),
            Status = NegotiationStatus.Submitted,
            CreatedById = createdById,
            Version = Guid.NewGuid()
        };

        // Add initial submission message
        var initialMsg = CommissionNegotiationMessage.Create(
            request.Id,
            createdById,
            nameof(UserRole.VendorOwner),
            $"Gửi yêu cầu đàm phán hoa hồng: Đề xuất mức {decimal.Round(proposedRate * 100, 2)}% (Mức hiện tại: {decimal.Round(currentRate * 100, 2)}%). Lý do: {reasonDetail.Trim()}",
            proposedRate);
        request.Messages.Add(initialMsg);

        return request;
    }

    private static void ValidateScopeAndTargets(CommissionRuleScope scope, Guid? categoryId, Guid? serviceItemId)
    {
        switch (scope)
        {
            case CommissionRuleScope.Vendor:
                if (categoryId.HasValue || serviceItemId.HasValue)
                    throw new DomainException("Đàm phán cấp Shop không được gắn CategoryId hoặc ServiceItemId.");
                break;
            case CommissionRuleScope.VendorCategory:
                if (!categoryId.HasValue || serviceItemId.HasValue)
                    throw new DomainException("Đàm phán cấp Ngành hàng của Shop phải có CategoryId và không có ServiceItemId.");
                break;
            case CommissionRuleScope.Service:
                if (!serviceItemId.HasValue)
                    throw new DomainException("Đàm phán cấp Dịch vụ phải có ServiceItemId.");
                break;
            default:
                throw new DomainException("Chỉ được đàm phán hoa hồng theo cấp Vendor, VendorCategory hoặc Service.");
        }
    }

    public void AssignModerator(Guid moderatorId)
    {
        if (moderatorId == Guid.Empty) throw new DomainException("Moderator không hợp lệ.");
        if (Status is NegotiationStatus.Approved or NegotiationStatus.Rejected or NegotiationStatus.Cancelled)
            throw new DomainException("Không thể tiếp nhận yêu cầu đàm phán đã kết thúc.");

        AssignedModeratorId = moderatorId;
        if (Status == NegotiationStatus.Submitted)
            Status = NegotiationStatus.InDiscussion;
        Version = Guid.NewGuid();
    }

    public CommissionNegotiationMessage AddCounterOffer(decimal counterRate, string message, Guid actorId, string actorRole)
    {
        if (Status is NegotiationStatus.Approved or NegotiationStatus.Rejected or NegotiationStatus.Cancelled)
            throw new DomainException("Yêu cầu đàm phán đã kết thúc; không thể đưa ra mức đề xuất mới.");
        if (counterRate is < 0.03m or > 0.12m)
            throw new DomainException("Mức hoa hồng đề xuất lại phải nằm trong khung 3–12%.");
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 4000)
            throw new DomainException("Nội dung giải thích mức đề xuất lại là bắt buộc (tối đa 4000 ký tự).");

        CounterRate = decimal.Round(counterRate, 4);
        Status = NegotiationStatus.CounterOffered;
        Version = Guid.NewGuid();

        var msg = CommissionNegotiationMessage.Create(
            Id,
            actorId,
            actorRole,
            $"Đề xuất mức hoa hồng khác: {decimal.Round(counterRate * 100, 2)}%. Ghi chú: {message.Trim()}",
            counterRate);
        Messages.Add(msg);
        return msg;
    }

    public CommissionNegotiationMessage RespondToCounter(bool accept, string message, Guid vendorUserId, decimal? newProposedRate = null)
    {
        if (Status != NegotiationStatus.CounterOffered)
            throw new DomainException("Chỉ có thể phản hồi khi Ban quản trị đã gửi mức đề xuất lại.");
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 4000)
            throw new DomainException("Nội dung phản hồi phải từ 1 đến 4000 ký tự.");

        if (accept)
        {
            if (!CounterRate.HasValue) throw new DomainException("Không có mức đề xuất lại để chấp thuận.");
            ProposedRate = CounterRate.Value;
            Status = NegotiationStatus.InDiscussion;
            Version = Guid.NewGuid();

            var msg = CommissionNegotiationMessage.Create(
                Id,
                vendorUserId,
                nameof(UserRole.VendorOwner),
                $"Đồng ý mức hoa hồng {decimal.Round(ProposedRate * 100, 2)}% do Ban quản trị đề xuất. Ghi chú: {message.Trim()}",
                ProposedRate);
            Messages.Add(msg);
            return msg;
        }
        else
        {
            if (newProposedRate.HasValue)
            {
                if (newProposedRate.Value is < 0.03m or > 0.12m)
                    throw new DomainException("Mức hoa hồng đề xuất mới phải nằm trong khung 3–12%.");
                ProposedRate = decimal.Round(newProposedRate.Value, 4);
            }
            Status = NegotiationStatus.InDiscussion;
            CounterRate = null;
            Version = Guid.NewGuid();

            var msg = CommissionNegotiationMessage.Create(
                Id,
                vendorUserId,
                nameof(UserRole.VendorOwner),
                $"Chưa đồng ý mức đề xuất lại. {(newProposedRate.HasValue ? $"Đề xuất mức mới: {decimal.Round(ProposedRate * 100, 2)}%." : "")} Ý kiến: {message.Trim()}",
                newProposedRate);
            Messages.Add(msg);
            return msg;
        }
    }

    public CommissionNegotiationMessage AddMessage(string message, Guid senderId, string senderRole, string? attachmentUrl = null)
    {
        if (Status is NegotiationStatus.Approved or NegotiationStatus.Rejected or NegotiationStatus.Cancelled)
            throw new DomainException("Yêu cầu đàm phán đã kết thúc; không thể gửi thêm tin nhắn.");

        var msg = CommissionNegotiationMessage.Create(Id, senderId, senderRole, message, null, attachmentUrl);
        Messages.Add(msg);
        if (Status == NegotiationStatus.Submitted && senderRole is nameof(UserRole.Moderator) or nameof(UserRole.SuperAdmin))
            Status = NegotiationStatus.InDiscussion;
        Version = Guid.NewGuid();
        return msg;
    }

    public CommissionNegotiationMessage Approve(Guid adminId, decimal approvedRate, string note, Guid? policyRuleId = null)
    {
        if (Status is NegotiationStatus.Approved or NegotiationStatus.Rejected or NegotiationStatus.Cancelled)
            throw new DomainException("Yêu cầu đàm phán đã có quyết định hoặc đã bị hủy.");
        if (adminId == Guid.Empty) throw new DomainException("Quản trị viên phê duyệt không hợp lệ.");
        if (approvedRate is < 0.03m or > 0.12m)
            throw new DomainException("Tỷ lệ phê duyệt phải nằm trong khung 3–12%.");
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > 2000)
            throw new DomainException("Lý do phê duyệt là bắt buộc và tối đa 2000 ký tự.");

        Status = NegotiationStatus.Approved;
        ApprovedRate = decimal.Round(approvedRate, 4);
        DecidedById = adminId;
        DecidedAt = DateTime.UtcNow;
        DecisionReason = note.Trim();
        ResultingPolicyRuleId = policyRuleId;
        Version = Guid.NewGuid();

        var msg = CommissionNegotiationMessage.Create(
            Id,
            adminId,
            nameof(UserRole.SuperAdmin),
            $"Phê duyệt đàm phán hoa hồng ở mức {decimal.Round(ApprovedRate.Value * 100, 2)}%. Lý do: {DecisionReason}",
            ApprovedRate);
        Messages.Add(msg);
        return msg;
    }

    public CommissionNegotiationMessage Reject(Guid adminId, string reason)
    {
        if (Status is NegotiationStatus.Approved or NegotiationStatus.Rejected or NegotiationStatus.Cancelled)
            throw new DomainException("Yêu cầu đàm phán đã có quyết định hoặc đã bị hủy.");
        if (adminId == Guid.Empty) throw new DomainException("Quản trị viên từ chối không hợp lệ.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("Lý do từ chối đàm phán là bắt buộc và tối đa 2000 ký tự.");

        Status = NegotiationStatus.Rejected;
        DecidedById = adminId;
        DecidedAt = DateTime.UtcNow;
        DecisionReason = reason.Trim();
        Version = Guid.NewGuid();

        var msg = CommissionNegotiationMessage.Create(
            Id,
            adminId,
            nameof(UserRole.SuperAdmin),
            $"Từ chối yêu cầu đàm phán hoa hồng. Lý do: {DecisionReason}");
        Messages.Add(msg);
        return msg;
    }

    public CommissionNegotiationMessage Cancel(Guid vendorUserId, string reason)
    {
        if (Status is NegotiationStatus.Approved or NegotiationStatus.Rejected or NegotiationStatus.Cancelled)
            throw new DomainException("Không thể hủy yêu cầu đàm phán đã kết thúc.");
        if (CreatedById != vendorUserId)
            throw new DomainException("Chỉ người tạo yêu cầu mới có quyền hủy đàm phán.");

        Status = NegotiationStatus.Cancelled;
        DecisionReason = string.IsNullOrWhiteSpace(reason) ? "Người dùng chủ động hủy đàm phán" : reason.Trim();
        Version = Guid.NewGuid();

        var msg = CommissionNegotiationMessage.Create(
            Id,
            vendorUserId,
            nameof(UserRole.VendorOwner),
            $"Đã hủy yêu cầu đàm phán. Ghi chú: {DecisionReason}");
        Messages.Add(msg);
        return msg;
    }
}
