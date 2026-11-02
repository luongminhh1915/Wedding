using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class AppointmentType : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DurationMinutes { get; private set; } = 60;
    public int Capacity { get; private set; } = 1;
    public string WorkingDays { get; private set; } = "1,2,3,4,5,6,0";
    public TimeSpan OpenTime { get; private set; } = new(9, 0, 0);
    public TimeSpan CloseTime { get; private set; } = new(18, 0, 0);
    public TimeSpan? BreakStart { get; private set; }
    public TimeSpan? BreakEnd { get; private set; }
    public int BufferBeforeMinutes { get; private set; }
    public int BufferAfterMinutes { get; private set; }
    public int MinBookingHours { get; private set; } = 12;
    public int MaxBookingDays { get; private set; } = 90;
    public int MaxAttendees { get; private set; } = 10;
    public int FreeCancellationHours { get; private set; } = 24;
    public string MeetingMode { get; private set; } = "Store";
    public string? Location { get; private set; }
    public decimal DepositAmount { get; private set; }
    public bool RequiresApproval { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public Vendor Vendor { get; private set; } = null!;
    private AppointmentType() { }

    public static AppointmentType Create(Guid vendorId, string name, string? description, int durationMinutes, int capacity,
        string workingDays, TimeSpan openTime, TimeSpan closeTime, decimal depositAmount, bool requiresApproval)
    {
        var t = new AppointmentType { VendorId = vendorId };
        t.Update(name, description, durationMinutes, capacity, workingDays, openTime, closeTime, depositAmount, requiresApproval, true);
        return t;
    }
    public void Update(string name, string? description, int durationMinutes, int capacity, string workingDays,
        TimeSpan openTime, TimeSpan closeTime, decimal depositAmount, bool requiresApproval, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tên loại lịch hẹn không được để trống.");
        if (durationMinutes is < 30 or > 120) throw new DomainException("Thời lượng phải từ 30 đến 120 phút.");
        if (capacity is < 1 or > 50) throw new DomainException("Sức chứa phải từ 1 đến 50.");
        var days = workingDays.Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        if (days.Length == 0 || days.Any(d => !int.TryParse(d, out var n) || n < 0 || n > 6)) throw new DomainException("Chọn ít nhất một ngày làm việc hợp lệ.");
        if (openTime < TimeSpan.Zero || closeTime > TimeSpan.FromDays(1) || closeTime <= openTime) throw new DomainException("Giờ đóng cửa phải sau giờ mở cửa trong cùng ngày.");
        if (depositAmount < 0) throw new DomainException("Tiền cọc không được âm.");
        Name = name.Trim(); Description = description; DurationMinutes = durationMinutes; Capacity = capacity;
        WorkingDays = string.Join(',', days); OpenTime = openTime; CloseTime = closeTime; DepositAmount = depositAmount;
        RequiresApproval = requiresApproval; IsActive = isActive; SetUpdated();
    }
    public void Configure(int bufferBefore, int bufferAfter, int minHours, int maxDays, int maxAttendees, int freeCancellationHours,
        string meetingMode, string? location, TimeSpan? breakStart, TimeSpan? breakEnd)
    {
        if (bufferBefore is < 0 or > 120 || bufferAfter is < 0 or > 120) throw new DomainException("Thời gian đệm phải từ 0 đến 120 phút.");
        if (minHours < 12 || minHours > 720 || maxDays < 1 || maxDays > 90 || minHours >= maxDays * 24) throw new DomainException("Đặt trước tối thiểu 12 giờ và tối đa 90 ngày; cửa sổ đặt lịch phải hợp lệ.");
        if (maxAttendees is < 1 or > 50 || freeCancellationHours < 24) throw new DomainException("Số người không hợp lệ hoặc thời hạn hủy miễn phí dưới 24 giờ.");
        if (meetingMode is not ("Store" or "Online" or "OnSite")) throw new DomainException("Hình thức lịch hẹn không hợp lệ.");
        if (breakStart.HasValue != breakEnd.HasValue || (breakStart.HasValue && (breakStart < OpenTime || breakEnd > CloseTime || breakEnd <= breakStart))) throw new DomainException("Giờ nghỉ phải nằm trong giờ làm việc.");
        BufferBeforeMinutes = bufferBefore; BufferAfterMinutes = bufferAfter; MinBookingHours = minHours; MaxBookingDays = maxDays;
        MaxAttendees = maxAttendees; FreeCancellationHours = freeCancellationHours; MeetingMode = meetingMode; Location = location;
        BreakStart = breakStart; BreakEnd = breakEnd; SetUpdated();
    }
    public void ValidateBookingTime(DateTime startAt, DateTime? now = null)
    {
        var utcNow = now ?? DateTime.UtcNow;
        if (startAt < utcNow.AddHours(MinBookingHours) || startAt > utcNow.AddDays(MaxBookingDays)) throw new DomainException($"Vui lòng đặt trước ít nhất {MinBookingHours} giờ và không quá {MaxBookingDays} ngày.");
    }
    public bool WorksOn(DayOfWeek day) => WorkingDays.Split(',').Contains(((int)day).ToString());
}

public class AppointmentAvailabilityException : BaseEntity
{
    public Guid AppointmentTypeId { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsClosed { get; private set; }
    public TimeSpan? OpenTime { get; private set; }
    public TimeSpan? CloseTime { get; private set; }
    public string? Reason { get; private set; }
    private AppointmentAvailabilityException() { }
    public static AppointmentAvailabilityException Create(Guid typeId, DateOnly date, bool isClosed, TimeSpan? openTime, TimeSpan? closeTime, string? reason)
    {
        if (!isClosed && (!openTime.HasValue || !closeTime.HasValue || openTime < TimeSpan.Zero || closeTime > TimeSpan.FromDays(1) || closeTime <= openTime)) throw new DomainException("Nhập giờ mở/đóng cửa đặc biệt hợp lệ.");
        return new() { AppointmentTypeId = typeId, Date = date, IsClosed = isClosed, OpenTime = openTime, CloseTime = closeTime, Reason = reason };
    }
}

public class Appointment : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public Guid AppointmentTypeId { get; private set; }
    public Guid VendorId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? LeadId { get; private set; }
    public DateTime StartAt { get; private set; }
    public DateTime EndAt { get; private set; }
    public int Attendees { get; private set; } = 2;
    public string? Note { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public decimal DepositAmount { get; private set; }
    public bool DepositPaid { get; private set; }
    public DateTime? DepositReportedAt { get; private set; }
    public DateTime? DepositConfirmedAt { get; private set; }
    public DateTime ResponseDueAt { get; private set; }
    public DateTime? ProposedStartAt { get; private set; }
    public Guid? ProposedById { get; private set; }
    public int RescheduleCount { get; private set; }
    public decimal RefundDue { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public string? RefundReference { get; private set; }
    public bool VendorSlaViolation { get; private set; }
    public Guid ConcurrencyStamp { get; private set; } = Guid.NewGuid();
    public string? VendorNote { get; private set; }
    public DateTime? CheckedInAt { get; private set; }
    public DateTime? CustomerReminder24At { get; private set; }
    public DateTime? CustomerReminder2At { get; private set; }
    public DateTime? VendorReminder1At { get; private set; }
    public AppointmentType AppointmentType { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;
    public User Customer { get; private set; } = null!;
    private Appointment() { }
    public static Appointment Create(AppointmentType type, Guid customerId, DateTime startAt, int attendees, string? note, Guid? leadId)
    {
        type.ValidateBookingTime(startAt);
        if (attendees < 1 || attendees > type.MaxAttendees) throw new DomainException($"Số người phải từ 1 đến {type.MaxAttendees}.");
        return new()
        {
            Code = $"LH-{Guid.NewGuid():N}"[..15].ToUpperInvariant(), AppointmentTypeId = type.Id, VendorId = type.VendorId,
            CustomerId = customerId, LeadId = leadId, StartAt = startAt, EndAt = startAt.AddMinutes(type.DurationMinutes),
            Attendees = attendees, Note = note?.Trim(), DepositAmount = type.DepositAmount, ResponseDueAt = DateTime.UtcNow.AddHours(24),
            Status = type.DepositAmount > 0 ? AppointmentStatus.AwaitingDeposit : type.RequiresApproval ? AppointmentStatus.Pending : AppointmentStatus.Confirmed
        };
    }
    public bool IsActiveSlot => Status is AppointmentStatus.Pending or AppointmentStatus.AwaitingDeposit or AppointmentStatus.RescheduleProposed or AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn;
    private void Touch() { ConcurrencyStamp = Guid.NewGuid(); SetUpdated(); }
    public void AttachLead(Guid leadId) { LeadId = leadId; Touch(); }
    public void ReportDeposit() { Ensure(AppointmentStatus.AwaitingDeposit); if (DateTime.UtcNow > CreatedAt.AddMinutes(30)) throw new DomainException("Đã hết thời gian giữ chỗ."); DepositReportedAt = DateTime.UtcNow; Touch(); }
    public void MarkDepositPaid(bool requiresApproval)
    {
        Ensure(AppointmentStatus.AwaitingDeposit);
        if (DateTime.UtcNow > CreatedAt.AddMinutes(30)) throw new DomainException("Đã hết thời gian giữ chỗ, cần đối soát/hoàn tiền trước khi đặt lại.");
        DepositPaid = true; DepositConfirmedAt = DateTime.UtcNow; ResponseDueAt = DateTime.UtcNow.AddHours(24);
        Status = requiresApproval ? AppointmentStatus.Pending : AppointmentStatus.Confirmed; Touch();
    }
    public void Confirm(string? note) { Ensure(AppointmentStatus.Pending); Status = AppointmentStatus.Confirmed; VendorNote = note; Touch(); }
    public void Reject(string? note)
    {
        Ensure(AppointmentStatus.Pending, AppointmentStatus.AwaitingDeposit, AppointmentStatus.RescheduleProposed);
        if (string.IsNullOrWhiteSpace(note)) throw new DomainException("Vui lòng nêu lý do từ chối.");
        Status = AppointmentStatus.Rejected; VendorNote = note; RefundDue = DepositPaid ? DepositAmount : 0; Touch();
    }
    public void ProposeReschedule(DateTime newStart, Guid proposedBy, AppointmentType type)
    {
        Ensure(AppointmentStatus.Pending, AppointmentStatus.Confirmed);
        if (RescheduleCount >= 2) throw new DomainException("Chỉ được đổi lịch tối đa 2 lần.");
        type.ValidateBookingTime(newStart); ProposedStartAt = newStart; ProposedById = proposedBy;
        Status = AppointmentStatus.RescheduleProposed; ResponseDueAt = DateTime.UtcNow.AddHours(24); Touch();
    }
    public void AcceptProposal(Guid userId, int durationMinutes)
    {
        Ensure(AppointmentStatus.RescheduleProposed);
        if (userId == ProposedById) throw new DomainException("Đề xuất cần được phía còn lại chấp nhận.");
        if (ResponseDueAt <= DateTime.UtcNow) throw new DomainException("Đề xuất đã hết hạn.");
        StartAt = ProposedStartAt ?? throw new DomainException("Chưa có giờ đề xuất."); EndAt = StartAt.AddMinutes(durationMinutes);
        ProposedStartAt = null; ProposedById = null; RescheduleCount++; Status = AppointmentStatus.Confirmed; Touch();
    }
    public void Cancel(string? reason, bool byVendor, int freeCancellationHours)
    {
        Ensure(AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.AwaitingDeposit, AppointmentStatus.RescheduleProposed);
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("Vui lòng nhập lý do hủy.");
        RefundDue = DepositPaid && (byVendor || StartAt > DateTime.UtcNow.AddHours(freeCancellationHours)) ? DepositAmount : 0;
        VendorSlaViolation = byVendor; Status = AppointmentStatus.Cancelled; VendorNote = reason.Trim(); Touch();
    }
    public void RecordRefund(string reference)
    {
        if (RefundDue <= 0 || RefundedAt.HasValue || string.IsNullOrWhiteSpace(reference)) throw new DomainException("Không có khoản hoàn hoặc thiếu mã giao dịch.");
        RefundedAt = DateTime.UtcNow; RefundReference = reference.Trim(); Touch();
    }
    public void CheckIn() { Ensure(AppointmentStatus.Confirmed); if (DateTime.UtcNow < StartAt.AddMinutes(-30)) throw new DomainException("Chỉ check-in từ 30 phút trước giờ hẹn."); Status = AppointmentStatus.CheckedIn; CheckedInAt = DateTime.UtcNow; Touch(); }
    public void Complete() { Ensure(AppointmentStatus.CheckedIn); Status = AppointmentStatus.Completed; Touch(); }
    public void MarkNoShow() { Ensure(AppointmentStatus.Confirmed); if (DateTime.UtcNow < StartAt.AddMinutes(30)) throw new DomainException("Chỉ đánh dấu vắng mặt sau 30 phút."); Status = AppointmentStatus.NoShow; Touch(); }
    public void Expire()
    {
        Ensure(AppointmentStatus.Pending, AppointmentStatus.AwaitingDeposit, AppointmentStatus.RescheduleProposed);
        RefundDue = DepositPaid ? DepositAmount : 0; VendorSlaViolation = Status == AppointmentStatus.Pending; Status = AppointmentStatus.Expired; Touch();
    }
    public void MarkReminder(string kind)
    {
        if (kind == "customer24") CustomerReminder24At = DateTime.UtcNow;
        else if (kind == "customer2") CustomerReminder2At = DateTime.UtcNow;
        else if (kind == "vendor1") VendorReminder1At = DateTime.UtcNow;
        Touch();
    }
    private void Ensure(params AppointmentStatus[] allowed)
    { if (!allowed.Contains(Status)) throw new DomainException($"Không thể thao tác khi lịch hẹn ở trạng thái {Status}."); }
}

public class AppointmentEvent : BaseEntity
{
    public Guid AppointmentId { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    private AppointmentEvent() { }
    public static AppointmentEvent Create(Guid appointmentId, Guid? actorId, string action, string? note)
        => new() { AppointmentId = appointmentId, ActorId = actorId, Action = action, Note = note };
}
