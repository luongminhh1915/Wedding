using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Appointments;

public record AppointmentTypeDto(Guid Id, Guid VendorId, string VendorName, string Name, string? Description, int DurationMinutes,
    int Capacity, string WorkingDays, string OpenTime, string CloseTime, decimal DepositAmount, bool RequiresApproval, bool IsActive);

public record SlotDto(DateTime StartAt, string LocalTime, int Remaining);

public record AppointmentDto(Guid Id, string Code, Guid AppointmentTypeId, string TypeName, Guid VendorId, string VendorName,
    Guid CustomerId, string CustomerName, DateTime StartAt, DateTime EndAt, int Attendees, string? Note, string Status,
    decimal DepositAmount, bool DepositPaid, string? VendorNote, string? DepositQrUrl);

internal static class Appt
{
    public static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    public static AppointmentTypeDto ToDto(AppointmentType t) => new(t.Id, t.VendorId, t.Vendor?.BrandName ?? "", t.Name, t.Description,
        t.DurationMinutes, t.Capacity, t.WorkingDays, t.OpenTime.ToString(@"hh\:mm"), t.CloseTime.ToString(@"hh\:mm"),
        t.DepositAmount, t.RequiresApproval, t.IsActive);

    public static AppointmentDto ToDto(Appointment a, IMockPaymentGateway? pay = null) => new(a.Id, a.Code, a.AppointmentTypeId,
        a.AppointmentType?.Name ?? "", a.VendorId, a.Vendor?.BrandName ?? "", a.CustomerId, a.Customer?.FullName ?? "",
        DateTime.SpecifyKind(a.StartAt, DateTimeKind.Utc), DateTime.SpecifyKind(a.EndAt, DateTimeKind.Utc), a.Attendees, a.Note,
        a.Status.ToString(), a.DepositAmount, a.DepositPaid, a.VendorNote,
        a.Status == AppointmentStatus.AwaitingDeposit && pay != null ? pay.BuildVietQrUrl(a.DepositAmount, a.Code) : null);

    public static async Task<Vendor> CurrentVendorAsync(IApplicationDbContext ctx, ICurrentUserService user, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        return await ctx.Vendors.FirstOrDefaultAsync(v => v.UserId == uid, ct) ?? throw new DomainException("Tài khoản chưa gắn với nhà cung cấp.");
    }

    public static IQueryable<Appointment> WithRefs(IApplicationDbContext ctx)
        => ctx.Appointments.Include(a => a.AppointmentType).Include(a => a.Vendor).Include(a => a.Customer);

    /// <summary>Chống overbooking: đếm lịch còn hiệu lực trùng khung giờ phải nhỏ hơn sức chứa.</summary>
    public static async Task EnsureCapacityAsync(IApplicationDbContext ctx, AppointmentType type, DateTime startAt, Guid? excludeId, CancellationToken ct)
    {
        var local = startAt + VnOffset;
        if (!type.WorksOn(local.DayOfWeek) || local.TimeOfDay < type.OpenTime || local.TimeOfDay.Add(TimeSpan.FromMinutes(type.DurationMinutes)) > type.CloseTime)
            throw new DomainException("Khung giờ nằm ngoài giờ làm việc của nhà cung cấp.");
        if ((local.TimeOfDay - type.OpenTime).TotalMinutes % type.DurationMinutes != 0)
            throw new DomainException("Khung giờ không hợp lệ.");
        var taken = await ctx.Appointments.CountAsync(a => a.AppointmentTypeId == type.Id && a.StartAt == startAt && a.Id != excludeId &&
            (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.AwaitingDeposit), ct);
        if (taken >= type.Capacity) throw new DomainException("Khung giờ này vừa được đặt hết, vui lòng chọn giờ khác.");
    }
}

// ---------- Public / Customer ----------

public record GetVendorAppointmentTypesQuery(Guid VendorId) : IRequest<List<AppointmentTypeDto>>;

public class GetVendorAppointmentTypesQueryHandler : IRequestHandler<GetVendorAppointmentTypesQuery, List<AppointmentTypeDto>>
{
    private readonly IApplicationDbContext _context;
    public GetVendorAppointmentTypesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<AppointmentTypeDto>> Handle(GetVendorAppointmentTypesQuery r, CancellationToken ct)
        => (await _context.AppointmentTypes.AsNoTracking().Include(t => t.Vendor).Where(t => t.VendorId == r.VendorId && t.IsActive).ToListAsync(ct))
            .Select(Appt.ToDto).ToList();
}

public record GetAvailableSlotsQuery(Guid TypeId, DateOnly Date) : IRequest<List<SlotDto>>;

public class GetAvailableSlotsQueryHandler : IRequestHandler<GetAvailableSlotsQuery, List<SlotDto>>
{
    private readonly IApplicationDbContext _context;
    public GetAvailableSlotsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<SlotDto>> Handle(GetAvailableSlotsQuery r, CancellationToken ct)
    {
        var type = await _context.AppointmentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == r.TypeId && t.IsActive, ct)
            ?? throw new NotFoundException("Loại lịch hẹn không tồn tại.");
        if (!type.WorksOn(r.Date.DayOfWeek)) return new();
        var dayStartUtc = r.Date.ToDateTime(TimeOnly.MinValue) - Appt.VnOffset;
        var dayEndUtc = dayStartUtc.AddDays(1);
        var booked = await _context.Appointments.AsNoTracking()
            .Where(a => a.AppointmentTypeId == type.Id && a.StartAt >= dayStartUtc && a.StartAt < dayEndUtc &&
                (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.AwaitingDeposit))
            .Select(a => a.StartAt).ToListAsync(ct);
        var minStart = DateTime.UtcNow.AddHours(2);
        var slots = new List<SlotDto>();
        for (var t = type.OpenTime; t.Add(TimeSpan.FromMinutes(type.DurationMinutes)) <= type.CloseTime; t = t.Add(TimeSpan.FromMinutes(type.DurationMinutes)))
        {
            var startUtc = DateTime.SpecifyKind(dayStartUtc + t, DateTimeKind.Utc);
            if (startUtc < minStart) continue;
            slots.Add(new SlotDto(startUtc, t.ToString(@"hh\:mm"), type.Capacity - booked.Count(b => b == startUtc)));
        }
        return slots;
    }
}

public record BookAppointmentCommand(Guid AppointmentTypeId, DateTime StartAt, int Attendees, string? Note, Guid? LeadId) : IRequest<AppointmentDto>;

public class BookAppointmentCommandHandler : IRequestHandler<BookAppointmentCommand, AppointmentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly INotificationSender _notify;
    private readonly IMockPaymentGateway _pay;

    public BookAppointmentCommandHandler(IApplicationDbContext context, ICurrentUserService user, INotificationSender notify, IMockPaymentGateway pay)
    { _context = context; _user = user; _notify = notify; _pay = pay; }

    public async Task<AppointmentDto> Handle(BookAppointmentCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập để đặt lịch hẹn.");
        var type = await _context.AppointmentTypes.Include(t => t.Vendor).FirstOrDefaultAsync(t => t.Id == r.AppointmentTypeId && t.IsActive, ct)
            ?? throw new NotFoundException("Loại lịch hẹn không tồn tại.");
        var startUtc = DateTime.SpecifyKind(r.StartAt.ToUniversalTime(), DateTimeKind.Utc);
        if (await _context.Appointments.AnyAsync(a => a.CustomerId == uid && a.VendorId == type.VendorId &&
                (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.AwaitingDeposit), ct))
            throw new DomainException("Bạn đang có một lịch hẹn chờ xử lý với nhà cung cấp này.");
        await Appt.EnsureCapacityAsync(_context, type, startUtc, null, ct);
        var appt = Appointment.Create(type, uid, startUtc, r.Attendees, r.Note, r.LeadId);
        _context.Appointments.Add(appt);
        await _context.SaveChangesAsync(ct);
        await _notify.SendAsync("Zalo", type.Vendor.Hotline, $"Lịch hẹn mới {appt.Code} lúc {(startUtc + Appt.VnOffset):HH:mm dd/MM}", ct);
        var saved = await Appt.WithRefs(_context).AsNoTracking().FirstAsync(a => a.Id == appt.Id, ct);
        return Appt.ToDto(saved, _pay);
    }
}

public record GetMyAppointmentsQuery : IRequest<List<AppointmentDto>>;

public class GetMyAppointmentsQueryHandler : IRequestHandler<GetMyAppointmentsQuery, List<AppointmentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly IMockPaymentGateway _pay;
    public GetMyAppointmentsQueryHandler(IApplicationDbContext context, ICurrentUserService user, IMockPaymentGateway pay) { _context = context; _user = user; _pay = pay; }

    public async Task<List<AppointmentDto>> Handle(GetMyAppointmentsQuery r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        return (await Appt.WithRefs(_context).AsNoTracking().Where(a => a.CustomerId == uid).ToListAsync(ct))
            .OrderBy(a => a.StartAt).Select(a => Appt.ToDto(a, _pay)).ToList();
    }
}

/// <summary>Hành động trên lịch hẹn: khách (cancel, pay-deposit[mock]) / NCC (confirm, reject, reschedule, check-in, no-show).</summary>
public record AppointmentActionCommand(Guid Id, string Action, string? Note, DateTime? NewStartAt) : IRequest<AppointmentDto>;

public class AppointmentActionCommandHandler : IRequestHandler<AppointmentActionCommand, AppointmentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly INotificationSender _notify;
    private readonly IMockPaymentGateway _pay;

    public AppointmentActionCommandHandler(IApplicationDbContext context, ICurrentUserService user, INotificationSender notify, IMockPaymentGateway pay)
    { _context = context; _user = user; _notify = notify; _pay = pay; }

    public async Task<AppointmentDto> Handle(AppointmentActionCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var a = await Appt.WithRefs(_context).FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException("Lịch hẹn không tồn tại.");
        var isCustomer = a.CustomerId == uid;
        var isVendor = a.Vendor.UserId == uid;
        switch (r.Action)
        {
            case "cancel" when isCustomer: a.CancelByCustomer(r.Note); break;
            case "pay-deposit" when isCustomer: a.MarkDepositPaid(a.AppointmentType.RequiresApproval); break;
            case "confirm" when isVendor: a.Confirm(r.Note); break;
            case "reject" when isVendor: a.Reject(r.Note); break;
            case "check-in" when isVendor: a.CheckIn(); break;
            case "no-show" when isVendor: a.MarkNoShow(); break;
            case "reschedule" when isVendor || isCustomer:
                var start = DateTime.SpecifyKind((r.NewStartAt ?? throw new DomainException("Vui lòng chọn giờ mới.")).ToUniversalTime(), DateTimeKind.Utc);
                await Appt.EnsureCapacityAsync(_context, a.AppointmentType, start, a.Id, ct);
                a.Reschedule(start, a.AppointmentType.DurationMinutes);
                break;
            default: throw new DomainException("Bạn không có quyền thực hiện thao tác này.");
        }
        await _context.SaveChangesAsync(ct);
        await _notify.SendAsync("Zalo", isVendor ? a.Customer.PhoneNumber : a.Vendor.Hotline, $"Lịch hẹn {a.Code}: {a.Status}", ct);
        return Appt.ToDto(a, _pay);
    }
}

// ---------- Vendor ----------

public record GetVendorAppointmentsQuery(DateTime? From, DateTime? To) : IRequest<List<AppointmentDto>>;

public class GetVendorAppointmentsQueryHandler : IRequestHandler<GetVendorAppointmentsQuery, List<AppointmentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetVendorAppointmentsQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<List<AppointmentDto>> Handle(GetVendorAppointmentsQuery r, CancellationToken ct)
    {
        var vendor = await Appt.CurrentVendorAsync(_context, _user, ct);
        var q = Appt.WithRefs(_context).AsNoTracking().Where(a => a.VendorId == vendor.Id);
        if (r.From.HasValue) q = q.Where(a => a.StartAt >= r.From.Value);
        if (r.To.HasValue) q = q.Where(a => a.StartAt <= r.To.Value);
        return (await q.ToListAsync(ct)).OrderBy(a => a.StartAt).Select(a => Appt.ToDto(a)).ToList();
    }
}

public record GetMyAppointmentTypesQuery : IRequest<List<AppointmentTypeDto>>;

public class GetMyAppointmentTypesQueryHandler : IRequestHandler<GetMyAppointmentTypesQuery, List<AppointmentTypeDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetMyAppointmentTypesQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<List<AppointmentTypeDto>> Handle(GetMyAppointmentTypesQuery r, CancellationToken ct)
    {
        var vendor = await Appt.CurrentVendorAsync(_context, _user, ct);
        return (await _context.AppointmentTypes.AsNoTracking().Include(t => t.Vendor).Where(t => t.VendorId == vendor.Id).ToListAsync(ct))
            .Select(Appt.ToDto).ToList();
    }
}

public record SaveAppointmentTypeCommand(Guid? Id, string Name, string? Description, int DurationMinutes, int Capacity,
    string WorkingDays, string OpenTime, string CloseTime, decimal DepositAmount, bool RequiresApproval, bool IsActive) : IRequest<AppointmentTypeDto>;

public class SaveAppointmentTypeCommandHandler : IRequestHandler<SaveAppointmentTypeCommand, AppointmentTypeDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public SaveAppointmentTypeCommandHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<AppointmentTypeDto> Handle(SaveAppointmentTypeCommand r, CancellationToken ct)
    {
        var vendor = await Appt.CurrentVendorAsync(_context, _user, ct);
        if (!TimeSpan.TryParse(r.OpenTime, out var open) || !TimeSpan.TryParse(r.CloseTime, out var close))
            throw new DomainException("Giờ mở/đóng cửa không hợp lệ (HH:mm).");
        AppointmentType type;
        if (r.Id.HasValue)
        {
            type = await _context.AppointmentTypes.FirstOrDefaultAsync(t => t.Id == r.Id && t.VendorId == vendor.Id, ct)
                ?? throw new NotFoundException("Loại lịch hẹn không tồn tại.");
            type.Update(r.Name, r.Description, r.DurationMinutes, r.Capacity, r.WorkingDays, open, close, r.DepositAmount, r.RequiresApproval, r.IsActive);
        }
        else
        {
            type = AppointmentType.Create(vendor.Id, r.Name, r.Description, r.DurationMinutes, r.Capacity, r.WorkingDays, open, close, r.DepositAmount, r.RequiresApproval);
            _context.AppointmentTypes.Add(type);
        }
        await _context.SaveChangesAsync(ct);
        return Appt.ToDto(type) with { VendorName = vendor.BrandName };
    }
}
