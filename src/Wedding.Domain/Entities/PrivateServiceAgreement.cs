using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>Private content-only proposal. Never replaces public content, original money or financial snapshots.</summary>
public class PrivateServiceAgreement : BaseEntity
{
    public string Kind { get; private set; } = "";
    public Guid TargetId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid CustomerId { get; private set; }
    public int Sequence { get; private set; }
    public Guid CreatedById { get; private set; }
    public string OriginalVersion { get; private set; } = "";
    public decimal OriginalTotal { get; private set; }
    public string ItemsJson { get; private set; } = "[]";
    public string Note { get; private set; } = "";
    public string Terms { get; private set; } = "";
    public string Status { get; private set; } = "PendingCustomer";
    public string? Response { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public Guid? RespondedById { get; private set; }
    private PrivateServiceAgreement() { }
    public static PrivateServiceAgreement Create(string kind, Guid targetId, Guid vendorId, Guid customerId,
        int sequence, Guid actorId, string originalVersion, decimal originalTotal, string itemsJson, string note, string terms)
        => new() { Kind = kind, TargetId = targetId, VendorId = vendorId, CustomerId = customerId, Sequence = sequence,
            CreatedById = actorId, OriginalVersion = originalVersion, OriginalTotal = originalTotal,
            ItemsJson = itemsJson, Note = note, Terms = terms };
    public void Respond(Guid actorId, bool accept, string? response)
    {
        if (actorId != CustomerId || Status != "PendingCustomer") throw new DomainException("Chỉ khách hàng của đề xuất đang chờ mới được xác nhận.");
        Status = accept ? "Accepted" : "Rejected"; Response = response; RespondedAt = DateTime.UtcNow; RespondedById = actorId; SetUpdated();
    }
    public void Withdraw(Guid actorId)
    {
        if (actorId != CreatedById || Status != "PendingCustomer") throw new DomainException("Chỉ NCC tạo đề xuất đang chờ mới được rút.");
        Status = "Withdrawn"; RespondedAt = DateTime.UtcNow; RespondedById = actorId; SetUpdated();
    }
}
