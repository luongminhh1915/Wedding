using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>Customer-only presentation and revision provenance. Never deletes the underlying business record.</summary>
public class CustomerOrderState : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public string Kind { get; private set; } = "";
    public Guid EntityId { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    public Guid? RevisionOfId { get; private set; }
    public Guid? SupersededById { get; private set; }
    public string? RevisionReason { get; private set; }
    public string? WithdrawalReason { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    private CustomerOrderState() { }
    public static CustomerOrderState Create(Guid customerId, string kind, Guid entityId)
        => new() { CustomerId = customerId, Kind = kind, EntityId = entityId };
    private void Touch() { Version = Guid.NewGuid(); SetUpdated(); }
    public void Archive(bool archived) { ArchivedAt = archived ? ArchivedAt ?? DateTime.UtcNow : null; Touch(); }
    public void RevisionOf(Guid source, string reason) { RevisionOfId = source; RevisionReason = reason; Touch(); }
    public void Supersede(Guid replacement)
    {
        if (SupersededById.HasValue && SupersededById != replacement) throw new DomainException("Yêu cầu đã được thay thế bởi một phiên bản khác.");
        SupersededById = replacement; Touch();
    }
    public void Withdraw(string reason) { WithdrawalReason = reason; Touch(); }
}

/// <summary>Acknowledgement workflow only. Reviewing a request NEVER amends contracts or moves money.</summary>
public class OrderChangeRequest : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public Guid VendorId { get; private set; }
    public string Kind { get; private set; } = "";
    public Guid EntityId { get; private set; }
    public string Type { get; private set; } = "Change";
    public string Status { get; private set; } = "PendingVendor";
    public string Reason { get; private set; } = "";
    public string? ProposedChanges { get; private set; }
    public bool RequiresFinance { get; private set; }
    public DateTime? VendorReviewedAt { get; private set; }
    public Guid? VendorReviewedBy { get; private set; }
    public string? VendorResponse { get; private set; }
    public DateTime? FinanceReviewedAt { get; private set; }
    public Guid? FinanceReviewedBy { get; private set; }
    public string? FinanceResponse { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    private OrderChangeRequest() { }
    public static OrderChangeRequest Create(Guid customer, Guid vendor, string kind, Guid entity, string type, string reason, string? proposed, bool requiresFinance)
    {
        ValidateReason(reason);
        if (type is not ("Change" or "Cancellation")) throw new DomainException("Loại yêu cầu không hợp lệ.");
        if (proposed?.Length > 4000) throw new DomainException("Nội dung đề nghị tối đa 4000 ký tự.");
        return new() { CustomerId = customer, VendorId = vendor, Kind = kind, EntityId = entity, Type = type, Reason = reason.Trim(), ProposedChanges = proposed?.Trim(), RequiresFinance = requiresFinance };
    }
    public static void ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10 || reason.Trim().Length > 2000) throw new DomainException("Vui lòng nêu lý do từ 10 đến 2000 ký tự.");
    }
    public void RequireFinance() { if (!RequiresFinance) { RequiresFinance = true; Version = Guid.NewGuid(); SetUpdated(); } }
    public void ReviewVendor(Guid actor, bool accept, string response)
    {
        if (Status != "PendingVendor") throw new DomainException("Yêu cầu không còn chờ NCC phản hồi.");
        ValidateReason(response); VendorReviewedAt = DateTime.UtcNow; VendorReviewedBy = actor; VendorResponse = response.Trim();
        Status = !accept ? "Rejected" : RequiresFinance ? "PendingFinance" : "Acknowledged"; Version = Guid.NewGuid(); SetUpdated();
    }
    public void ReviewFinance(Guid actor, bool accept, string response)
    {
        if (Status != "PendingFinance") throw new DomainException("Yêu cầu chưa được NCC tiếp nhận hoặc đã được xử lý.");
        ValidateReason(response); FinanceReviewedAt = DateTime.UtcNow; FinanceReviewedBy = actor; FinanceResponse = response.Trim();
        Status = accept ? "Acknowledged" : "Rejected"; Version = Guid.NewGuid(); SetUpdated();
    }
}
