using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class BookingContract : BaseEntity
{
    public string ContractCode { get; private set; } = string.Empty;
    public Guid LeadId { get; private set; }
    public Guid? VoucherId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal ContractValue { get; private set; }
    public decimal DepositAmount { get; private set; }
    public string? ContractImageUrl { get; private set; }
    public DateTime? WeddingDate { get; private set; }

    // BR-005: Xác thực giao dịch 2 chiều trong 72 giờ
    public ContractStatus Status { get; private set; } = ContractStatus.PendingVerification;
    public DateTime VerificationDeadline { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public Guid? QuoteId { get; private set; } // P4: HĐ được điền sẵn từ báo giá
    public decimal? CommissionRateSnapshot { get; private set; } // P4: chốt tỷ lệ hoa hồng tại thời điểm xác nhận
    // Nullable for historical contracts: never infer their original category/policy from today's catalog.
    public Guid? CommissionCategoryIdSnapshot { get; private set; }
    public string? CommissionCategoryNameSnapshot { get; private set; }
    public Guid? CommissionBusinessGroupIdSnapshot { get; private set; }
    public string? CommissionBusinessGroupNameSnapshot { get; private set; }
    public string? CommissionRateSourceSnapshot { get; private set; }
    public string? CommissionPolicyVersionSnapshot { get; private set; }
    public string CancellationPolicySnapshot { get; private set; } = "Chính sách hoàn cọc phải được hai bên thống nhất bằng văn bản trước khi xác nhận.";
    public string? RejectReason { get; private set; }
    public int RevisionCount { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public decimal AppointmentDepositCredit { get; private set; }
    public void ApplyAppointmentDepositCredit(decimal paid) { if (paid < 0) throw new DomainException("Cọc lịch hẹn không hợp lệ."); AppointmentDepositCredit = Math.Min(DepositAmount, paid); SetUpdated(); }

    // Navigation Properties
    public Lead Lead { get; private set; } = null!;
    public Voucher? Voucher { get; private set; }
    public Vendor Vendor { get; private set; } = null!;
    public User Customer { get; private set; } = null!;
    public ICollection<Commission> Commissions { get; private set; } = new List<Commission>();
    public ICollection<Review> Reviews { get; private set; } = new List<Review>();

    private BookingContract() { } // Dành cho EF Core

    public static BookingContract Create(Guid leadId, Guid? voucherId, Guid vendorId, Guid customerId, 
        decimal contractValue, decimal depositAmount, string? contractImageUrl, DateTime? weddingDate)
    {
        if (contractValue <= 0)
            throw new DomainException("Giá trị hợp đồng phải lớn hơn 0 VNĐ.");
        if (depositAmount < 0 || depositAmount > contractValue)
            throw new DomainException("Tiền đặt cọc không hợp lệ (0 <= Tiền cọc <= Giá trị HĐ).");

        var now = DateTime.UtcNow;
        return new BookingContract
        {
            Id = Guid.NewGuid(),
            ContractCode = $"HD-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            LeadId = leadId,
            VoucherId = voucherId,
            VendorId = vendorId,
            CustomerId = customerId,
            ContractValue = contractValue,
            DepositAmount = depositAmount,
            ContractImageUrl = contractImageUrl,
            WeddingDate = weddingDate,
            Status = ContractStatus.PendingVerification, // BR-005
            VerificationDeadline = now.AddHours(72),    // BR-005: 72 giờ duyệt
            CreatedAt = now
        };
    }

    public void ConfirmByCustomer()
    {
        if (Status != ContractStatus.PendingVerification)
            throw new DomainException("Hợp đồng không ở trạng thái chờ khách duyệt.");

        if (DateTime.UtcNow > VerificationDeadline)
        {
            Status = ContractStatus.Cancelled;
            CancellationReason = "Quá hạn 72h khách hàng không xác nhận hợp đồng (BR-005).";
            throw new DomainException(CancellationReason);
        }

        Status = ContractStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public void RejectByCustomer(string reason)
    {
        if (Status != ContractStatus.PendingVerification || string.IsNullOrWhiteSpace(reason)) throw new DomainException("Chỉ được từ chối hợp đồng chờ duyệt và cần ghi lý do.");
        Status = ContractStatus.Draft;
        RejectReason = reason.Trim();
        Version = Guid.NewGuid();
        CancellationReason = $"Khách hàng từ chối do sai lệch: {reason}";
        SetUpdated();
    }

    public void CompleteContract()
    {
        if (Status != ContractStatus.Confirmed)
            throw new DomainException("Chỉ hợp đồng đã xác thực mới có thể hoàn tất sau ngày cưới.");
        if (!WeddingDate.HasValue || DateTime.UtcNow < WeddingDate.Value.Date.AddDays(1)) throw new DomainException("Chỉ hoàn tất sau ngày cưới đã khai báo.");

        Status = ContractStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        Version = Guid.NewGuid();
        SetUpdated();
    }

    public void Cancel(string reason)
    {
        Status = ContractStatus.Cancelled;
        Version = Guid.NewGuid();
        CancellationReason = reason;
        SetUpdated();
    }

    public void LinkQuote(Guid quoteId) { QuoteId = quoteId; SetUpdated(); }

    public void SnapshotCommissionRate(decimal rate) { if (rate is < 0.03m or > 0.12m) throw new DomainException("Hoa hồng phải trong khung 3–12%."); CommissionRateSnapshot ??= rate; SetUpdated(); }
    public void SnapshotCommissionCategory(Category category, string rateSource, string? policyVersion = null)
    {
        if (Status != ContractStatus.Confirmed || !ConfirmedAt.HasValue || !CommissionRateSnapshot.HasValue)
            throw new DomainException("Chỉ lưu danh mục/chính sách hoa hồng cùng lần xác nhận hợp đồng.");
        if (CommissionCategoryIdSnapshot.HasValue)
            throw new DomainException("Danh mục/chính sách hoa hồng đã chốt không được ghi đè.");
        if (category.Id == Guid.Empty || !category.IsActive || string.IsNullOrWhiteSpace(category.Name))
            throw new DomainException("Danh mục dịch vụ không hợp lệ để xác nhận hợp đồng.");
        if (rateSource is not ("LegacyVendor" or "CategoryDefault") && !rateSource.StartsWith("PolicyRule:", StringComparison.Ordinal))
            throw new DomainException("Nguồn chính sách hoa hồng không hợp lệ.");
        if (rateSource == "CategoryDefault" && (category.BusinessGroup == null || !category.BusinessGroup.IsActive ||
            category.BusinessGroupId != category.BusinessGroup.Id || string.IsNullOrWhiteSpace(category.BusinessGroup.PolicyVersion)))
            throw new DomainException("Danh mục dịch vụ chưa có phiên bản chính sách hợp lệ.");

        CommissionCategoryIdSnapshot = category.Id;
        CommissionCategoryNameSnapshot = category.Name;
        CommissionBusinessGroupIdSnapshot = category.BusinessGroupId;
        CommissionBusinessGroupNameSnapshot = category.BusinessGroup?.Name;
        CommissionRateSourceSnapshot = rateSource;
        // The current group policy did not establish a positive historical shop-wide rate.
        CommissionPolicyVersionSnapshot = rateSource == "CategoryDefault" ? category.BusinessGroup!.PolicyVersion : policyVersion;
        SetUpdated();
    }
    public void SetCancellationPolicy(string? policy) { if (policy?.Length > 4000) throw new DomainException("Chính sách hủy tối đa 4000 ký tự."); if (!string.IsNullOrWhiteSpace(policy)) CancellationPolicySnapshot = policy.Trim(); }
    public void Revise(decimal value, decimal deposit, string? image, DateTime? weddingDate, string? policy)
    {
        if (Status != ContractStatus.Draft) throw new DomainException("Chỉ sửa hợp đồng đã bị trả về bản nháp.");
        if (value <= 0 || deposit < 0 || deposit > value) throw new DomainException("Giá trị hợp đồng hoặc cọc không hợp lệ.");
        ContractValue = value; DepositAmount = deposit; ContractImageUrl = image; WeddingDate = weddingDate;
        AppointmentDepositCredit = Math.Min(AppointmentDepositCredit, deposit);
        SetCancellationPolicy(policy); RevisionCount++; Status = ContractStatus.PendingVerification;
        VerificationDeadline = DateTime.UtcNow.AddHours(72); Version = Guid.NewGuid(); SetUpdated();
    }

    public void MarkDisputed()
    {
        if (Status is not (ContractStatus.Confirmed or ContractStatus.Completed))
            throw new DomainException("Chỉ mở tranh chấp sau khi khách đã xác nhận hợp đồng. Hợp đồng chờ duyệt cần dùng chức năng báo sai lệch.");
        Status = ContractStatus.Disputed; Version = Guid.NewGuid(); SetUpdated();
    }

    public void ResolveDispute(bool cancelContract, string note)
    {
        if (Status != ContractStatus.Disputed) return;
        if (!cancelContract && ConfirmedAt == null && CompletedAt == null) throw new DomainException("Tranh chấp HĐ cũ chưa xác thực cần kiểm tra thủ công, không tự nâng thành Confirmed.");
        Status = cancelContract ? ContractStatus.Cancelled : CompletedAt.HasValue ? ContractStatus.Completed : ContractStatus.Confirmed;
        if (cancelContract) CancellationReason = $"Hủy sau tranh chấp: {note}";
        Version = Guid.NewGuid();
        SetUpdated();
    }
}
