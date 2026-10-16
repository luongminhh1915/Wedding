using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Planner;

public record WeddingProfileDto(Guid Id, string GroomName, string BrideName, DateTime? WeddingDate, string City,
    decimal TotalBudget, int GuestCount, string Style, string? PartnerEmail, int? DaysLeft);

public record PlannerOverviewDto(WeddingProfileDto? Profile, int ChecklistTotal, int ChecklistDone, decimal BudgetPlanned,
    decimal BudgetActual, int LeadCount, int ContractCount, int AppointmentUpcoming, int GuestCount, int RsvpAttending);

internal static class PlannerMap
{
    public static WeddingProfileDto ToDto(WeddingProfile p) => new(p.Id, p.GroomName, p.BrideName, p.WeddingDate, p.City,
        p.TotalBudget, p.GuestCount, p.Style, p.PartnerEmail,
        p.WeddingDate.HasValue ? (int)Math.Ceiling((p.WeddingDate.Value.Date - DateTime.UtcNow.Date).TotalDays) : null);
}

public record GetWeddingProfileQuery : IRequest<WeddingProfileDto?>;

public class GetWeddingProfileQueryHandler : IRequestHandler<GetWeddingProfileQuery, WeddingProfileDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetWeddingProfileQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<WeddingProfileDto?> Handle(GetWeddingProfileQuery request, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var p = await _context.WeddingProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == uid, ct);
        return p == null ? null : PlannerMap.ToDto(p);
    }
}

/// <summary>P1 – Wizard: lưu hồ sơ cưới, tùy chọn sinh checklist + ngân sách mẫu theo ngày cưới/ngân sách.</summary>
public record SaveWeddingProfileCommand(string GroomName, string BrideName, DateTime? WeddingDate, string City,
    decimal TotalBudget, int GuestCount, string Style, string? PartnerEmail, bool GeneratePlan) : IRequest<WeddingProfileDto>;

public class SaveWeddingProfileCommandHandler : IRequestHandler<SaveWeddingProfileCommand, WeddingProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public SaveWeddingProfileCommandHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    // Tỷ lệ ngân sách gợi ý theo hạng mục (tổng = 100%)
    private static readonly (string Name, decimal Ratio)[] BudgetSplit =
    {
        ("Nhà hàng tiệc cưới", 0.45m), ("Chụp ảnh cưới", 0.10m), ("Váy cưới & vest", 0.08m), ("Trang điểm cô dâu", 0.04m),
        ("Trang trí & hoa cưới", 0.10m), ("Nhẫn cưới", 0.10m), ("Thiệp cưới", 0.02m), ("Xe hoa", 0.03m), ("Dự phòng", 0.08m)
    };

    // (tiêu đề, mốc, số tháng trước ngày cưới)
    private static readonly (string Title, string Milestone, int MonthsBefore)[] ChecklistTemplate =
    {
        ("Thống nhất ngân sách & số lượng khách", "12 tháng trước", 12), ("Chọn ngày cưới & xem tuổi", "12 tháng trước", 12),
        ("Đặt nhà hàng tiệc cưới", "9 tháng trước", 9), ("Đặt lịch chụp ảnh cưới", "9 tháng trước", 9),
        ("Thử & chọn váy cưới", "6 tháng trước", 6), ("Đặt makeup cô dâu", "6 tháng trước", 6),
        ("Chọn nhẫn cưới", "4 tháng trước", 4), ("Lên danh sách khách mời", "3 tháng trước", 3),
        ("Thiết kế & gửi thiệp cưới online", "2 tháng trước", 2), ("Chốt trang trí, hoa, xe hoa", "2 tháng trước", 2),
        ("Xác nhận RSVP & sơ đồ bàn", "1 tháng trước", 1), ("Tổng duyệt với các nhà cung cấp", "1 tuần trước", 0)
    };

    private static readonly int MaxOffsetDays = OffsetDays(ChecklistTemplate.Max(t => t.MonthsBefore));
    private static int OffsetDays(int months) => months == 0 ? 7 : (int)Math.Round(months * 30.44);

    /// <summary>
    /// Hạn = ngày cưới − mốc. Nếu lịch mẫu không đủ chỗ (cưới sớm hơn 12 tháng), nén tuyến tính toàn bộ mốc vào
    /// khoảng [hôm nay + 3 ngày, ngày cưới − 7 ngày] để giữ thứ tự và không bao giờ có hạn trong quá khứ.
    /// </summary>
    internal static DateTime? ComputeDue(DateTime? weddingDate, int monthsBefore)
    {
        if (!weddingDate.HasValue) return null;
        var wedding = weddingDate.Value.Date;
        var start = DateTime.UtcNow.Date.AddDays(3);
        var offset = OffsetDays(monthsBefore);
        if (wedding.AddDays(-MaxOffsetDays) >= start) return wedding.AddDays(-offset);
        var end = wedding.AddDays(-7);
        if (end <= start) return start < wedding ? start : DateTime.UtcNow.Date.AddDays(Math.Min(1, (wedding - DateTime.UtcNow.Date).Days));
        var ratio = (double)(offset - 7) / (MaxOffsetDays - 7); // 1 = mốc sớm nhất, 0 = 1 tuần trước
        return end.AddDays(-Math.Round((end - start).TotalDays * ratio));
    }

    public async Task<WeddingProfileDto> Handle(SaveWeddingProfileCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        if (r.WeddingDate.HasValue && r.WeddingDate.Value.Date < DateTime.UtcNow.Date) throw new DomainException("Ngày cưới phải ở tương lai.");
        var p = await _context.WeddingProfiles.FirstOrDefaultAsync(x => x.CustomerId == uid, ct);
        if (p == null) { p = WeddingProfile.Create(uid); _context.WeddingProfiles.Add(p); }
        p.Update(r.GroomName ?? "", r.BrideName ?? "", r.WeddingDate, r.City ?? "", r.TotalBudget, r.GuestCount, r.Style ?? "Classic", r.PartnerEmail);

        if (r.GeneratePlan)
        {
            if (!await _context.BudgetItems.AnyAsync(b => b.CustomerId == uid, ct) && r.TotalBudget > 0)
                foreach (var (name, ratio) in BudgetSplit)
                    _context.BudgetItems.Add(BudgetItem.Create(uid, null, name, Math.Round(r.TotalBudget * ratio / 100_000m) * 100_000m));

            if (!await _context.ChecklistTasks.AnyAsync(c => c.CustomerId == uid, ct))
                foreach (var (title, milestone, months) in ChecklistTemplate)
                    _context.ChecklistTasks.Add(ChecklistTask.Create(uid, title, milestone, ComputeDue(r.WeddingDate, months)));
        }

        // Đổi ngày cưới → tính lại hạn các việc mẫu chưa hoàn thành (kể cả checklist đã sinh trước đó bị hạn quá khứ).
        var templateMonths = ChecklistTemplate.ToDictionary(t => t.Title, t => t.MonthsBefore);
        var generated = await _context.ChecklistTasks.Where(c => c.CustomerId == uid && !c.IsCompleted).ToListAsync(ct);
        foreach (var t in generated.Where(t => templateMonths.ContainsKey(t.Title)))
            t.Update(t.Title, t.Milestone, ComputeDue(r.WeddingDate, templateMonths[t.Title]), t.Notes);

        await _context.SaveChangesAsync(ct);
        return PlannerMap.ToDto(p);
    }
}

public record GetPlannerOverviewQuery : IRequest<PlannerOverviewDto>;

public class GetPlannerOverviewQueryHandler : IRequestHandler<GetPlannerOverviewQuery, PlannerOverviewDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public GetPlannerOverviewQueryHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    public async Task<PlannerOverviewDto> Handle(GetPlannerOverviewQuery request, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var p = await _context.WeddingProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == uid, ct);
        var tasks = await _context.ChecklistTasks.AsNoTracking().Where(t => t.CustomerId == uid).ToListAsync(ct);
        var budget = await _context.BudgetItems.AsNoTracking().Where(b => b.CustomerId == uid).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var guests = await _context.GuestRSVPs.AsNoTracking().Where(g => g.Invitation.CustomerId == uid).ToListAsync(ct);
        return new PlannerOverviewDto(p == null ? null : PlannerMap.ToDto(p), tasks.Count, tasks.Count(t => t.IsCompleted),
            budget.Sum(b => b.PlannedCost), budget.Sum(b => b.ActualCost),
            await _context.Leads.CountAsync(l => l.CustomerId == uid, ct),
            await _context.BookingContracts.CountAsync(c => c.CustomerId == uid, ct),
            await _context.Appointments.CountAsync(a => a.CustomerId == uid && a.StartAt > now &&
                (a.Status == Domain.Enums.AppointmentStatus.Confirmed || a.Status == Domain.Enums.AppointmentStatus.Pending), ct),
            guests.Count, guests.Where(g => g.Status == Domain.Enums.RSVPStatus.Attending).Sum(g => 1 + g.CompanionCount));
    }
}

/// <summary>P2 – Kích hoạt thiệp Premium 130.000đ. Thanh toán mock: trả mã tham chiếu + QR, client gọi confirm.</summary>
public record ActivateInvitationPremiumCommand(bool ConfirmPaid) : IRequest<InvitationPremiumDto>;
public record InvitationPremiumDto(bool IsPremium, decimal Price, string PaymentRef, string QrUrl, DateTime? ActivatedAt);

public class ActivateInvitationPremiumCommandHandler : IRequestHandler<ActivateInvitationPremiumCommand, InvitationPremiumDto>
{
    public const decimal PremiumPrice = 130000m;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly IMockPaymentGateway _pay;
    private readonly IAuditLogger _audit;

    public ActivateInvitationPremiumCommandHandler(IApplicationDbContext context, ICurrentUserService user, IMockPaymentGateway pay, IAuditLogger audit)
    { _context = context; _user = user; _pay = pay; _audit = audit; }

    public async Task<InvitationPremiumDto> Handle(ActivateInvitationPremiumCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var inv = await _context.WeddingInvitations.FirstOrDefaultAsync(i => i.CustomerId == uid, ct)
            ?? throw new DomainException("Bạn cần tạo thiệp trước khi nâng cấp Premium.");
        if (inv.IsPremium)
            return new InvitationPremiumDto(true, PremiumPrice, inv.PremiumPaymentRef ?? "", "", inv.PremiumActivatedAt);
        var reference = _pay.CreatePaymentRef("WMP");
        if (r.ConfirmPaid)
        {
            inv.ActivatePremium(reference);
            await _audit.LogAsync("Invitation.PremiumActivated", nameof(WeddingInvitation), inv.Id.ToString(), new { reference, PremiumPrice, mock = true });
            await _context.SaveChangesAsync(ct);
        }
        return new InvitationPremiumDto(inv.IsPremium, PremiumPrice, reference, _pay.BuildVietQrUrl(PremiumPrice, reference), inv.PremiumActivatedAt);
    }
}
