using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class CommissionAdjustmentRequest : BaseEntity
{
    public Guid ContractId { get; private set; }
    public Guid CommissionId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid RequestedById { get; private set; }
    public Guid CommissionVersionSnapshot { get; private set; }
    public decimal OriginalRateSnapshot { get; private set; }
    public decimal OriginalAmountSnapshot { get; private set; }
    public decimal OutstandingAmountSnapshot { get; private set; }
    public decimal ProposedRate { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public CommissionAdjustmentStatus Status { get; private set; } = CommissionAdjustmentStatus.Submitted;
    public Guid? DecidedById { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public string? DecisionReason { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();

    public BookingContract Contract { get; private set; } = null!;
    public Commission Commission { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;
    public User RequestedBy { get; private set; } = null!;
    public User? DecidedBy { get; private set; }

    private CommissionAdjustmentRequest() { }

    public static CommissionAdjustmentRequest Create(BookingContract contract, Commission commission, Guid requestedById,
        decimal proposedRate, string reason)
    {
        if (commission.ContractId != contract.Id || commission.VendorId != contract.VendorId ||
            requestedById == Guid.Empty || proposedRate is < .03m or > .12m || decimal.Round(proposedRate, 4) != proposedRate ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("Yêu cầu điều chỉnh hoa hồng không hợp lệ.");
        return new CommissionAdjustmentRequest
        {
            ContractId = contract.Id,
            CommissionId = commission.Id,
            VendorId = contract.VendorId,
            RequestedById = requestedById,
            CommissionVersionSnapshot = commission.Version,
            OriginalRateSnapshot = commission.CommissionRate,
            OriginalAmountSnapshot = commission.CommissionAmount,
            OutstandingAmountSnapshot = commission.OutstandingAmount,
            ProposedRate = proposedRate,
            Reason = reason.Trim(),
            Status = CommissionAdjustmentStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(Guid adminId, string reason)
    {
        Decide(adminId, reason, CommissionAdjustmentStatus.Approved);
    }

    public void Reject(Guid adminId, string reason)
    {
        Decide(adminId, reason, CommissionAdjustmentStatus.Rejected);
    }

    private void Decide(Guid adminId, string reason, CommissionAdjustmentStatus decision)
    {
        if (Status != CommissionAdjustmentStatus.Submitted || adminId == Guid.Empty ||
            string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new DomainException("Yêu cầu đang chờ cần Admin và lý do quyết định hợp lệ.");
        Status = decision;
        DecidedById = adminId;
        DecidedAt = DateTime.UtcNow;
        DecisionReason = reason.Trim();
        Version = Guid.NewGuid();
        SetUpdated();
    }
}
