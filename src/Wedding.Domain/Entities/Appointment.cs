using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

/// <summary>P3 – Loại lịch hẹn NCC cấu hình (thử váy, xem sảnh, tasting...) kèm giờ làm việc.</summary>
public class AppointmentType : BaseEntity
{
    public Guid VendorId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DurationMinutes { get; private set; } = 60;
    public int Capacity { get; private set; } = 1;
    public string WorkingDays { get; private set; } = "1,2,3,4,5,6,0"; // DayOfWeek, CSV
    public TimeSpan OpenTime { get; private set; } = new(9, 0, 0);
    public TimeSpan CloseTime { get; private set; } = new(18, 0, 0);
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
        if (durationMinutes is < 15 or > 480) throw new DomainException("Thời lượng phải từ 15 đến 480 phút.");
        if (capacity is < 1 or > 50) throw new DomainException("Sức chứa mỗi khung giờ phải từ 1 đến 50.");
        if (closeTime <= openTime) throw new DomainException("Giờ đóng cửa phải sau giờ mở cửa.");
        Name = name.Trim(); Description = description; DurationMinutes = durationMinutes; Capacity = capacity;
        WorkingDays = workingDays; OpenTime = openTime; CloseTime = closeTime; DepositAmount = Math.Max(0, depositAmount);
        RequiresApproval = requiresApproval; IsActive = isActive;
        SetUpdated();
    }

    public bool WorksOn(DayOfWeek day) => WorkingDays.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(((int)day).ToString());
}

/// <summary>P3 – Lịch hẹn của cặp đôi với NCC.</summary>
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
    public string? VendorNote { get; private set; }
    public DateTime? CheckedInAt { get; private set; }

    public AppointmentType AppointmentType { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;
    public User Customer { get; private set; } = null!;

    private Appointment() { }

    public static Appointment Create(AppointmentType type, Guid customerId, DateTime startAt, int attendees, string? note, Guid? leadId)
    {
        if (startAt <= DateTime.UtcNow.AddHours(2)) throw new DomainException("Vui lòng đặt lịch trước ít nhất 2 giờ.");
        var status = type.DepositAmount > 0 ? AppointmentStatus.AwaitingDeposit
            : type.RequiresApproval ? AppointmentStatus.Pending : AppointmentStatus.Confirmed;
        return new Appointment
        {
            Code = $"LH-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}",
            AppointmentTypeId = type.Id, VendorId = type.VendorId, CustomerId = customerId, LeadId = leadId,
            StartAt = startAt, EndAt = startAt.AddMinutes(type.DurationMinutes), Attendees = Math.Clamp(attendees, 1, 10),
            Note = note?.Trim(), Status = status, DepositAmount = type.DepositAmount
        };
    }

    public bool IsActiveSlot => Status is AppointmentStatus.Pending or AppointmentStatus.AwaitingDeposit or AppointmentStatus.Confirmed;

    public void MarkDepositPaid(bool requiresApproval)
    {
        Ensure(AppointmentStatus.AwaitingDeposit);
        DepositPaid = true;
        Status = requiresApproval ? AppointmentStatus.Pending : AppointmentStatus.Confirmed;
        SetUpdated();
    }

    public void Confirm(string? note) { Ensure(AppointmentStatus.Pending); Status = AppointmentStatus.Confirmed; VendorNote = note; SetUpdated(); }
    public void Reject(string? note) { Ensure(AppointmentStatus.Pending, AppointmentStatus.AwaitingDeposit); Status = AppointmentStatus.Rejected; VendorNote = note; SetUpdated(); }

    public void Reschedule(DateTime newStart, int durationMinutes)
    {
        Ensure(AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.AwaitingDeposit);
        if (newStart <= DateTime.UtcNow.AddHours(2)) throw new DomainException("Thời gian mới phải sau ít nhất 2 giờ.");
        StartAt = newStart; EndAt = newStart.AddMinutes(durationMinutes);
        if (Status == AppointmentStatus.Confirmed) Status = AppointmentStatus.Pending;
        SetUpdated();
    }

    public void CancelByCustomer(string? reason)
    {
        Ensure(AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.AwaitingDeposit);
        Status = AppointmentStatus.Cancelled; Note = string.IsNullOrWhiteSpace(reason) ? Note : $"{Note} | Hủy: {reason}";
        SetUpdated();
    }

    public void CheckIn() { Ensure(AppointmentStatus.Confirmed); Status = AppointmentStatus.Completed; CheckedInAt = DateTime.UtcNow; SetUpdated(); }
    public void MarkNoShow() { Ensure(AppointmentStatus.Confirmed); if (DateTime.UtcNow < StartAt) throw new DomainException("Chưa tới giờ hẹn."); Status = AppointmentStatus.NoShow; SetUpdated(); }
    public void Expire() { Status = AppointmentStatus.Expired; SetUpdated(); }

    private void Ensure(params AppointmentStatus[] allowed)
    {
        if (!allowed.Contains(Status)) throw new DomainException($"Không thể thực hiện thao tác khi lịch hẹn đang ở trạng thái {Status}.");
    }
}
