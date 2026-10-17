using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;

namespace Wedding.Application.Features.Appointments;

public record AppointmentTypeDto(Guid Id, Guid VendorId, string VendorName, string Name, string? Description, int DurationMinutes,
    int Capacity, string WorkingDays, string OpenTime, string CloseTime, decimal DepositAmount, bool RequiresApproval, bool IsActive,
    int BufferBeforeMinutes = 0, int BufferAfterMinutes = 0, int MinBookingHours = 12, int MaxBookingDays = 90,
    int MaxAttendees = 10, int FreeCancellationHours = 24, string MeetingMode = "Store", string? Location = null,
    string? BreakStart = null, string? BreakEnd = null);
public record SlotDto(DateTime StartAt, string LocalTime, int Remaining);
public record AppointmentDto(Guid Id, string Code, Guid AppointmentTypeId, string TypeName, Guid VendorId, string VendorName,
    Guid CustomerId, string CustomerName, DateTime StartAt, DateTime EndAt, int Attendees, string? Note, string Status,
    decimal DepositAmount, bool DepositPaid, string? VendorNote, string? DepositQrUrl,
    DateTime? ProposedStartAt = null, Guid? ProposedById = null, int RescheduleCount = 0, decimal RefundDue = 0,
    DateTime? RefundedAt = null, DateTime? DepositReportedAt = null, string? Location = null, Guid? LeadId = null,
    DateTime? DepositHoldUntil = null, int FreeCancellationHours = 24);
public record AvailabilityExceptionDto(Guid Id, Guid AppointmentTypeId, DateOnly Date, bool IsClosed, string? OpenTime, string? CloseTime, string? Reason);

/// <summary>Vietnam does not observe DST. All persisted appointment instants are UTC.</summary>
public static class AppointmentScheduling
{
    public static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);
    public static DateTime Utc(DateTime input) => input.Kind == DateTimeKind.Utc ? input : input.Kind == DateTimeKind.Local ? input.ToUniversalTime() : DateTime.SpecifyKind(input, DateTimeKind.Utc);
    public static bool Holds(Appointment a, DateTime now) => a.Status switch
    {
        AppointmentStatus.AwaitingDeposit => a.CreatedAt.AddMinutes(30) > now,
        AppointmentStatus.Pending or AppointmentStatus.RescheduleProposed => a.ResponseDueAt > now,
        AppointmentStatus.Confirmed or AppointmentStatus.CheckedIn => true,
        _ => false
    };
    // Count at every interval boundary, not merely equal start times. Each appointment is one resource,
    // even while holding both its original slot and a proposed replacement.
    public static int Remaining(AppointmentType type, DateTime start, IEnumerable<Appointment> appointments, DateTime now)
    {
        var from = start.AddMinutes(-type.BufferBeforeMinutes);
        var until = start.AddMinutes(type.DurationMinutes + type.BufferAfterMinutes);
        var windows = appointments.Where(a => Holds(a, now)).Select(a => new[]
        {
            (Start: a.StartAt.AddMinutes(-type.BufferBeforeMinutes), End: a.EndAt.AddMinutes(type.BufferAfterMinutes)),
            (Start: (a.ProposedStartAt ?? a.StartAt).AddMinutes(-type.BufferBeforeMinutes), End: (a.ProposedStartAt ?? a.StartAt).AddMinutes(type.DurationMinutes + type.BufferAfterMinutes))
        }).ToList();
        var points = windows.SelectMany(w => w.SelectMany(i => new[] { i.Start, i.End })).Where(t => t >= from && t < until).Append(from);
        var peak = points.Select(t => windows.Count(w => w.Any(i => i.Start <= t && i.End > t))).DefaultIfEmpty(0).Max();
        return Math.Max(0, type.Capacity - peak);
    }
}

internal static class Appt
{
    public static readonly TimeSpan VnOffset = AppointmentScheduling.VnOffset;
    public static async Task<string?> DepositQrAsync(IApplicationDbContext context, Appointment a, CancellationToken ct, bool required = false)
    {
        if (a.Status != AppointmentStatus.AwaitingDeposit) return null;
        var bank = await context.VendorTaxProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.VendorId == a.VendorId && p.Status == VerificationStatus.Approved, ct);
        if (bank == null)
        {
            if (required) throw new DomainException("NCC chưa có tài khoản ngân hàng đã xác minh để nhận cọc.");
            return null;
        }
        return $"https://img.vietqr.io/image/{Uri.EscapeDataString(bank.BankCode)}-{Uri.EscapeDataString(bank.BankAccountNumber)}-compact2.png?amount={a.DepositAmount:0}&addInfo={Uri.EscapeDataString("HEN " + a.Code)}&accountName={Uri.EscapeDataString(bank.BankAccountName)}";
    }
    public static AppointmentTypeDto ToDto(AppointmentType t) => new(t.Id, t.VendorId, t.Vendor?.BrandName ?? "", t.Name, t.Description,
        t.DurationMinutes, t.Capacity, t.WorkingDays, t.OpenTime.ToString(@"hh\:mm"), t.CloseTime.ToString(@"hh\:mm"), t.DepositAmount,
        t.RequiresApproval, t.IsActive, t.BufferBeforeMinutes, t.BufferAfterMinutes, t.MinBookingHours, t.MaxBookingDays,
        t.MaxAttendees, t.FreeCancellationHours, t.MeetingMode, t.Location, t.BreakStart?.ToString(@"hh\:mm"), t.BreakEnd?.ToString(@"hh\:mm"));
    public static AppointmentDto ToDto(Appointment a, string? qr = null) => new(a.Id, a.Code, a.AppointmentTypeId, a.AppointmentType?.Name ?? "",
        a.VendorId, a.Vendor?.BrandName ?? "", a.CustomerId, a.Customer?.FullName ?? "", AppointmentScheduling.Utc(a.StartAt), AppointmentScheduling.Utc(a.EndAt),
        a.Attendees, a.Note, a.Status.ToString(), a.DepositAmount, a.DepositPaid, a.VendorNote, qr,
        a.ProposedStartAt.HasValue ? AppointmentScheduling.Utc(a.ProposedStartAt.Value) : null, a.ProposedById, a.RescheduleCount,
        a.RefundDue, a.RefundedAt, a.DepositReportedAt, a.AppointmentType?.Location ?? a.Vendor?.Address, a.LeadId,
        a.Status == AppointmentStatus.AwaitingDeposit ? AppointmentScheduling.Utc(a.CreatedAt.AddMinutes(30)) : null, a.AppointmentType?.FreeCancellationHours ?? 24);
    public static IQueryable<Appointment> WithRefs(IApplicationDbContext ctx) => ctx.Appointments.Include(a => a.AppointmentType).Include(a => a.Vendor).ThenInclude(v => v.User).Include(a => a.Customer);
    public static async Task<Vendor> CurrentVendorAsync(IApplicationDbContext ctx, ICurrentUserService user, CancellationToken ct)
        => await ctx.Vendors.FirstOrDefaultAsync(v => v.UserId == user.UserId, ct) ?? throw new DomainException("Tài khoản chưa gắn với nhà cung cấp.");
    public static async Task<(TimeSpan Open, TimeSpan Close)?> HoursAsync(IApplicationDbContext ctx, AppointmentType type, DateOnly date, CancellationToken ct)
    {
        var exception = await ctx.AppointmentAvailabilityExceptions.AsNoTracking().FirstOrDefaultAsync(e => e.AppointmentTypeId == type.Id && e.Date == date, ct);
        if (exception?.IsClosed == true || (exception == null && !type.WorksOn(date.DayOfWeek))) return null;
        return (exception?.OpenTime ?? type.OpenTime, exception?.CloseTime ?? type.CloseTime);
    }
    public static async Task EnsureCapacityAsync(IApplicationDbContext ctx, AppointmentType type, DateTime startAt, Guid? excludeId, CancellationToken ct, bool validateBookingWindow = true)
    {
        if (validateBookingWindow) type.ValidateBookingTime(startAt);
        else if (startAt <= DateTime.UtcNow) throw new DomainException("Giờ hẹn đã qua.");
        var local = startAt + VnOffset;
        var hours = await HoursAsync(ctx, type, DateOnly.FromDateTime(local), ct) ?? throw new DomainException("Nhà cung cấp nghỉ ngày này.");
        var start = local.TimeOfDay.Subtract(TimeSpan.FromMinutes(type.BufferBeforeMinutes));
        var end = local.TimeOfDay.Add(TimeSpan.FromMinutes(type.DurationMinutes + type.BufferAfterMinutes));
        if (start < hours.Open || end > hours.Close || (local.TimeOfDay - hours.Open).TotalMinutes % type.DurationMinutes != 0
            || (type.BreakStart.HasValue && start < type.BreakEnd && end > type.BreakStart)) throw new DomainException("Khung giờ nằm ngoài giờ làm việc hoặc trùng giờ nghỉ.");
        var day = local.Date - VnOffset;
        var next = day.AddDays(1);
        var booked = await ctx.Appointments.Where(a => a.AppointmentTypeId == type.Id && a.Id != excludeId &&
            ((a.StartAt < next && a.EndAt > day) || (a.ProposedStartAt >= day && a.ProposedStartAt < next))).ToListAsync(ct);
        if (AppointmentScheduling.Remaining(type, startAt, booked, DateTime.UtcNow) <= 0) throw new DomainException("Khung giờ này vừa hết chỗ. Vui lòng chọn giờ khác.");
    }
    public static async Task SyncConfirmedAsync(IApplicationDbContext ctx, Appointment a, CancellationToken ct)
    {
        if (a.Status != AppointmentStatus.Confirmed) return;
        var lead = a.LeadId.HasValue ? await ctx.Leads.FirstOrDefaultAsync(l => l.Id == a.LeadId, ct) : null;
        lead ??= await ctx.Leads.FirstOrDefaultAsync(l => l.CustomerId == a.CustomerId && l.VendorId == a.VendorId && l.Status != LeadStatus.Cancelled && l.Status != LeadStatus.Expired, ct);
        if (lead == null)
        {
            lead = Lead.Create(a.CustomerId, a.VendorId, null, a.Customer.PhoneNumber, null, null, null, $"Từ lịch hẹn {a.Code}");
            lead.AttachSource("Appointment"); lead.Accept(); ctx.Leads.Add(lead);
            ctx.Vouchers.Add(Voucher.Create(lead.Id, a.CustomerId, Guid.NewGuid().ToString("N")[..8], discountValue: 0));
        }
        lead.UnlockPhone(); a.AttachLead(lead.Id);
        var marker = $"[Appointment:{a.Id}]";
        var task = await ctx.ChecklistTasks.FirstOrDefaultAsync(t => t.Notes == marker, ct);
        var title = $"{a.AppointmentType.Name} tại {a.Vendor.BrandName} – {(a.StartAt + VnOffset):HH:mm dd/MM}";
        if (task == null)
        {
            var profile = await ctx.WeddingProfiles.FirstOrDefaultAsync(w => w.CustomerId == a.CustomerId, ct);
            if (profile == null)
            {
                var member = await ctx.WeddingMembers.FirstOrDefaultAsync(m => m.UserId == a.CustomerId, ct);
                if (member != null) profile = await ctx.WeddingProfiles.FirstOrDefaultAsync(w => w.Id == member.WeddingProfileId, ct);
            }
            task = ChecklistTask.Create(profile?.CustomerId ?? a.CustomerId, title, "Lịch hẹn", a.StartAt, marker);
            if (profile != null) task.AttachToWedding(profile.Id);
            ctx.ChecklistTasks.Add(task);
        }
        else task.Update(title, "Lịch hẹn", a.StartAt, marker);
    }
}

public record GetVendorAppointmentTypesQuery(Guid VendorId) : IRequest<List<AppointmentTypeDto>>;
public class GetVendorAppointmentTypesQueryHandler(IApplicationDbContext context) : IRequestHandler<GetVendorAppointmentTypesQuery, List<AppointmentTypeDto>>
{
    public async Task<List<AppointmentTypeDto>> Handle(GetVendorAppointmentTypesQuery r, CancellationToken ct)
        => (await context.AppointmentTypes.AsNoTracking().Include(t => t.Vendor).Where(t => t.VendorId == r.VendorId && t.IsActive && t.Vendor.IsActive).ToListAsync(ct)).Select(Appt.ToDto).ToList();
}
public record GetAvailableSlotsQuery(Guid TypeId, DateOnly Date) : IRequest<List<SlotDto>>;
public class GetAvailableSlotsQueryHandler(IApplicationDbContext context) : IRequestHandler<GetAvailableSlotsQuery, List<SlotDto>>
{
    public async Task<List<SlotDto>> Handle(GetAvailableSlotsQuery r, CancellationToken ct)
    {
        var type = await context.AppointmentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == r.TypeId && t.IsActive, ct) ?? throw new NotFoundException("Loại lịch hẹn không tồn tại.");
        var hours = await Appt.HoursAsync(context, type, r.Date, ct);
        if (!hours.HasValue) return new();
        var day = r.Date.ToDateTime(TimeOnly.MinValue) - Appt.VnOffset; var next = day.AddDays(1);
        var booked = await context.Appointments.AsNoTracking().Where(a => a.AppointmentTypeId == type.Id &&
            ((a.StartAt < next && a.EndAt > day) || (a.ProposedStartAt >= day && a.ProposedStartAt < next))).ToListAsync(ct);
        var now = DateTime.UtcNow; var slots = new List<SlotDto>();
        for (var t = hours.Value.Open; t.Add(TimeSpan.FromMinutes(type.DurationMinutes + type.BufferAfterMinutes)) <= hours.Value.Close; t = t.Add(TimeSpan.FromMinutes(type.DurationMinutes)))
        {
            if (t.Subtract(TimeSpan.FromMinutes(type.BufferBeforeMinutes)) < hours.Value.Open || (type.BreakStart.HasValue && t.Subtract(TimeSpan.FromMinutes(type.BufferBeforeMinutes)) < type.BreakEnd && t.Add(TimeSpan.FromMinutes(type.DurationMinutes + type.BufferAfterMinutes)) > type.BreakStart)) continue;
            var start = DateTime.SpecifyKind(day + t, DateTimeKind.Utc);
            if (start < now.AddHours(type.MinBookingHours) || start > now.AddDays(type.MaxBookingDays)) continue;
            slots.Add(new(start, t.ToString(@"hh\:mm"), AppointmentScheduling.Remaining(type, start, booked, now)));
        }
        return slots;
    }
}
public record BookAppointmentCommand(Guid AppointmentTypeId, DateTime StartAt, int Attendees, string? Note, Guid? LeadId) : IRequest<AppointmentDto>;
public class BookAppointmentCommandHandler(IApplicationDbContext context, ICurrentUserService user, INotificationSender notify) : IRequestHandler<BookAppointmentCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(BookAppointmentCommand r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        await WeddingAccess.ResolveAsync(context, user, ct, write: true);
        return await context.ExecuteSerializableAsync(async token =>
        {
            var type = await context.AppointmentTypes.Include(t => t.Vendor).ThenInclude(v => v.User).FirstOrDefaultAsync(t => t.Id == r.AppointmentTypeId && t.IsActive && t.Vendor.IsActive, token) ?? throw new NotFoundException("Loại lịch hẹn không tồn tại.");
            var customer = await context.Users.FirstAsync(u => u.Id == uid, token);
            if (customer.PhoneNumber.Length < 9 || customer.PhoneNumber.Any(c => !char.IsDigit(c) && c != '+')) throw new DomainException("Vui lòng cập nhật số điện thoại hợp lệ trước khi đặt hẹn.");
            if (r.LeadId.HasValue && !await context.Leads.AnyAsync(l => l.Id == r.LeadId && l.CustomerId == uid && l.VendorId == type.VendorId && l.Status != LeadStatus.Cancelled && l.Status != LeadStatus.Expired, token)) throw new DomainException("Lead không thuộc tài khoản/NCC này hoặc đã rút/hết hạn.");
            var open = (await context.Appointments.Where(a => a.CustomerId == uid).ToListAsync(token)).Where(a => AppointmentScheduling.Holds(a, DateTime.UtcNow)).ToList();
            if (open.Count >= 5) throw new DomainException("Bạn chỉ được có tối đa 5 lịch hẹn đang mở.");
            if (open.Any(a => a.VendorId == type.VendorId && a.AppointmentTypeId == type.Id)) throw new DomainException("Bạn đã có lịch hẹn đang mở với loại hẹn và nhà cung cấp này.");
            var start = AppointmentScheduling.Utc(r.StartAt);
            await Appt.EnsureCapacityAsync(context, type, start, null, token);
            var a = Appointment.Create(type, uid, start, r.Attendees, r.Note, r.LeadId); context.Appointments.Add(a);
            await context.SaveChangesAsync(token);
            var saved = await Appt.WithRefs(context).FirstAsync(x => x.Id == a.Id, token);
            await Appt.SyncConfirmedAsync(context, saved, token);
            context.AppointmentEvents.Add(AppointmentEvent.Create(a.Id, uid, "Booked", r.Note));
            await notify.SendAsync("Email", type.Vendor.User.Email, $"Lịch hẹn mới {a.Code}: {(start + Appt.VnOffset):HH:mm dd/MM/yyyy}.", token);
            var qr = await Appt.DepositQrAsync(context, saved, token, required: true);
            await context.SaveChangesAsync(token); return Appt.ToDto(saved, qr);
        }, ct);
    }
}
public record GetMyAppointmentsQuery : IRequest<List<AppointmentDto>>;
public class GetMyAppointmentsQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetMyAppointmentsQuery, List<AppointmentDto>>
{
    public async Task<List<AppointmentDto>> Handle(GetMyAppointmentsQuery r, CancellationToken ct)
    {
        var items = await Appt.WithRefs(context).AsNoTracking().Where(a => a.CustomerId == user.UserId).OrderBy(a => a.StartAt).ToListAsync(ct);
        var result = new List<AppointmentDto>();
        foreach (var a in items) result.Add(Appt.ToDto(a, await Appt.DepositQrAsync(context, a, ct)));
        return result;
    }
}
public record AppointmentActionCommand(Guid Id, string Action, string? Note, DateTime? NewStartAt) : IRequest<AppointmentDto>;
public class AppointmentActionCommandHandler(IApplicationDbContext context, ICurrentUserService user, INotificationSender notify) : IRequestHandler<AppointmentActionCommand, AppointmentDto>
{
    public async Task<AppointmentDto> Handle(AppointmentActionCommand r, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        return await context.ExecuteSerializableAsync(async token =>
        {
            var a = await Appt.WithRefs(context).FirstOrDefaultAsync(x => x.Id == r.Id, token) ?? throw new NotFoundException("Lịch hẹn không tồn tại.");
            var customer = a.CustomerId == uid; var vendor = a.Vendor.UserId == uid;
            if (!customer && !vendor) throw new DomainException("Bạn không có quyền xử lý lịch hẹn này.");
            switch (r.Action)
            {
                case "cancel": a.Cancel(r.Note, vendor, a.AppointmentType.FreeCancellationHours); break;
                // Customer reports are never treated as confirmed money. Only the receiving vendor can reconcile it.
                case "pay-deposit" when customer: a.ReportDeposit(); break;
                case "confirm-deposit" when vendor: a.MarkDepositPaid(a.AppointmentType.RequiresApproval); break;
                case "refund" when vendor: a.RecordRefund(r.Note ?? ""); break;
                case "confirm" when vendor:
                    if (!AppointmentScheduling.Holds(a, DateTime.UtcNow)) throw new DomainException("Lịch hẹn đã hết hạn phản hồi.");
                    a.Confirm(r.Note); break;
                case "reject" when vendor: a.Reject(r.Note); break;
                case "check-in" when vendor: a.CheckIn(); break;
                case "complete" when vendor: a.Complete(); break;
                case "no-show" when vendor: a.MarkNoShow(); break;
                case "reschedule": case "propose":
                    var start = AppointmentScheduling.Utc(r.NewStartAt ?? throw new DomainException("Chọn giờ mới."));
                    await Appt.EnsureCapacityAsync(context, a.AppointmentType, start, a.Id, token);
                    a.ProposeReschedule(start, uid, a.AppointmentType); break;
                case "accept-proposal":
                    await Appt.EnsureCapacityAsync(context, a.AppointmentType, a.ProposedStartAt ?? throw new DomainException("Chưa có đề xuất."), a.Id, token, validateBookingWindow: false);
                    a.AcceptProposal(uid, a.AppointmentType.DurationMinutes); break;
                case "decline-proposal":
                    if (a.Status != AppointmentStatus.RescheduleProposed || a.ProposedById == uid) throw new DomainException("Không có đề xuất từ phía còn lại.");
                    a.Cancel(r.Note ?? "Từ chối đề xuất đổi lịch", vendor, a.AppointmentType.FreeCancellationHours); break;
                default: throw new DomainException("Thao tác không hợp lệ hoặc không đủ quyền.");
            }
            await Appt.SyncConfirmedAsync(context, a, token);
            if (!a.IsActiveSlot)
            {
                var marker = $"[Appointment:{a.Id}]";
                var task = await context.ChecklistTasks.FirstOrDefaultAsync(t => t.Notes == marker, token);
                if (task != null && !task.IsCompleted) task.ToggleComplete();
            }
            context.AppointmentEvents.Add(AppointmentEvent.Create(a.Id, uid, r.Action, r.Note));
            await notify.SendAsync("Email", vendor ? a.Customer.Email : a.Vendor.User.Email, $"Lịch hẹn {a.Code}: {a.Status}.", token);
            await context.SaveChangesAsync(token); return Appt.ToDto(a, await Appt.DepositQrAsync(context, a, token));
        }, ct);
    }
}
public record GetVendorAppointmentsQuery(DateTime? From, DateTime? To) : IRequest<List<AppointmentDto>>;
public class GetVendorAppointmentsQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetVendorAppointmentsQuery, List<AppointmentDto>>
{
    public async Task<List<AppointmentDto>> Handle(GetVendorAppointmentsQuery r, CancellationToken ct)
    {
        var v = await Appt.CurrentVendorAsync(context, user, ct);
        return (await Appt.WithRefs(context).AsNoTracking().Where(a => a.VendorId == v.Id && (!r.From.HasValue || a.StartAt >= r.From) && (!r.To.HasValue || a.StartAt <= r.To)).OrderBy(a => a.StartAt).ToListAsync(ct)).Select(a => Appt.ToDto(a)).ToList();
    }
}
public record GetMyAppointmentTypesQuery : IRequest<List<AppointmentTypeDto>>;
public class GetMyAppointmentTypesQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetMyAppointmentTypesQuery, List<AppointmentTypeDto>>
{
    public async Task<List<AppointmentTypeDto>> Handle(GetMyAppointmentTypesQuery r, CancellationToken ct)
    {
        var v = await Appt.CurrentVendorAsync(context, user, ct);
        return (await context.AppointmentTypes.Include(t => t.Vendor).AsNoTracking().Where(t => t.VendorId == v.Id).ToListAsync(ct)).Select(Appt.ToDto).ToList();
    }
}
public record SaveAppointmentTypeCommand(Guid? Id, string Name, string? Description, int DurationMinutes, int Capacity,
    string WorkingDays, string OpenTime, string CloseTime, decimal DepositAmount, bool RequiresApproval, bool IsActive,
    int BufferBeforeMinutes = 0, int BufferAfterMinutes = 0, int MinBookingHours = 12, int MaxBookingDays = 90,
    int MaxAttendees = 10, int FreeCancellationHours = 24, string MeetingMode = "Store", string? Location = null,
    string? BreakStart = null, string? BreakEnd = null) : IRequest<AppointmentTypeDto>;
public class SaveAppointmentTypeCommandHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<SaveAppointmentTypeCommand, AppointmentTypeDto>
{
    public async Task<AppointmentTypeDto> Handle(SaveAppointmentTypeCommand r, CancellationToken ct)
    {
        var vendor = await Appt.CurrentVendorAsync(context, user, ct);
        if (!TimeSpan.TryParse(r.OpenTime, out var open) || !TimeSpan.TryParse(r.CloseTime, out var close)) throw new DomainException("Giờ mở/đóng cửa không hợp lệ.");
        var type = r.Id.HasValue ? await context.AppointmentTypes.FirstOrDefaultAsync(t => t.Id == r.Id && t.VendorId == vendor.Id, ct) ?? throw new NotFoundException("Loại lịch hẹn không tồn tại.")
            : AppointmentType.Create(vendor.Id, r.Name, r.Description, r.DurationMinutes, r.Capacity, r.WorkingDays, open, close, r.DepositAmount, r.RequiresApproval);
        type.Update(r.Name, r.Description, r.DurationMinutes, r.Capacity, r.WorkingDays, open, close, r.DepositAmount, r.RequiresApproval, r.IsActive);
        TimeSpan? bs = string.IsNullOrWhiteSpace(r.BreakStart) ? null : TimeSpan.TryParse(r.BreakStart, out var pbs) ? pbs : throw new DomainException("Giờ nghỉ không hợp lệ.");
        TimeSpan? be = string.IsNullOrWhiteSpace(r.BreakEnd) ? null : TimeSpan.TryParse(r.BreakEnd, out var pbe) ? pbe : throw new DomainException("Giờ nghỉ không hợp lệ.");
        type.Configure(r.BufferBeforeMinutes, r.BufferAfterMinutes, r.MinBookingHours, r.MaxBookingDays, r.MaxAttendees, r.FreeCancellationHours, r.MeetingMode, r.Location, bs, be);
        if (!r.Id.HasValue) context.AppointmentTypes.Add(type);
        await context.SaveChangesAsync(ct); return Appt.ToDto(type) with { VendorName = vendor.BrandName };
    }
}
public record GetAvailabilityExceptionsQuery(Guid TypeId) : IRequest<List<AvailabilityExceptionDto>>;
public class GetAvailabilityExceptionsQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetAvailabilityExceptionsQuery, List<AvailabilityExceptionDto>>
{
    public async Task<List<AvailabilityExceptionDto>> Handle(GetAvailabilityExceptionsQuery r, CancellationToken ct)
    {
        var v = await Appt.CurrentVendorAsync(context, user, ct);
        if (!await context.AppointmentTypes.AnyAsync(t => t.Id == r.TypeId && t.VendorId == v.Id, ct)) throw new NotFoundException("Loại hẹn không tồn tại.");
        return (await context.AppointmentAvailabilityExceptions.AsNoTracking().Where(e => e.AppointmentTypeId == r.TypeId).OrderBy(e => e.Date).ToListAsync(ct)).Select(e => new AvailabilityExceptionDto(e.Id, e.AppointmentTypeId, e.Date, e.IsClosed, e.OpenTime?.ToString(@"hh\:mm"), e.CloseTime?.ToString(@"hh\:mm"), e.Reason)).ToList();
    }
}
public record SaveAvailabilityExceptionCommand(Guid TypeId, DateOnly Date, bool IsClosed, string? OpenTime, string? CloseTime, string? Reason, bool Delete = false) : IRequest<bool>;
public class SaveAvailabilityExceptionCommandHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<SaveAvailabilityExceptionCommand, bool>
{
    public async Task<bool> Handle(SaveAvailabilityExceptionCommand r, CancellationToken ct)
    {
        var v = await Appt.CurrentVendorAsync(context, user, ct);
        if (!await context.AppointmentTypes.AnyAsync(t => t.Id == r.TypeId && t.VendorId == v.Id, ct)) throw new NotFoundException("Loại hẹn không tồn tại.");
        var old = await context.AppointmentAvailabilityExceptions.FirstOrDefaultAsync(e => e.AppointmentTypeId == r.TypeId && e.Date == r.Date, ct);
        if (old != null) context.AppointmentAvailabilityExceptions.Remove(old);
        if (!r.Delete) context.AppointmentAvailabilityExceptions.Add(AppointmentAvailabilityException.Create(r.TypeId, r.Date, r.IsClosed,
            TimeSpan.TryParse(r.OpenTime, out var open) ? open : null, TimeSpan.TryParse(r.CloseTime, out var close) ? close : null, r.Reason));
        await context.SaveChangesAsync(ct); return true;
    }
}
public record GetAppointmentCalendarQuery(Guid Id) : IRequest<string>;
public class GetAppointmentCalendarQueryHandler(IApplicationDbContext context, ICurrentUserService user) : IRequestHandler<GetAppointmentCalendarQuery, string>
{
    public async Task<string> Handle(GetAppointmentCalendarQuery r, CancellationToken ct)
    {
        var a = await Appt.WithRefs(context).AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException("Lịch hẹn không tồn tại.");
        if (a.CustomerId != user.UserId && a.Vendor.UserId != user.UserId) throw new DomainException("Không có quyền xem lịch hẹn.");
        static string Escape(string? text) => (text ?? "").Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace(",", "\\,").Replace(";", "\\;");
        return string.Join("\r\n", "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Wedding Match//Appointments//VI", "BEGIN:VEVENT",
            $"UID:{a.Id}@weddingmatch", $"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}", $"DTSTART:{a.StartAt:yyyyMMddTHHmmssZ}", $"DTEND:{a.EndAt:yyyyMMddTHHmmssZ}",
            $"SUMMARY:{Escape(a.AppointmentType.Name)} - {Escape(a.Vendor.BrandName)}", $"LOCATION:{Escape(a.AppointmentType.Location ?? a.Vendor.Address)}", $"DESCRIPTION:{Escape(a.Code)}", "END:VEVENT", "END:VCALENDAR", "");
    }
}
