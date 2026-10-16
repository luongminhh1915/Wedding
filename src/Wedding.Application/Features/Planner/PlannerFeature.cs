using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;
using System.Text.Json;

namespace Wedding.Application.Features.Planner;

public record WeddingProfileDto(Guid Id, string GroomName, string BrideName, DateTime? WeddingDate, string City,
    decimal TotalBudget, int GuestCount, string Style, string? PartnerEmail, int? DaysLeft,
    DateTime? EngagementDate = null, string PlanningRole = "Couple", string[]? Events = null, string[]? Priorities = null,
    DateTime? OnboardingCompletedAt = null);

public record PlannerOverviewDto(WeddingProfileDto? Profile, int ChecklistTotal, int ChecklistDone, decimal BudgetPlanned,
    decimal BudgetActual, int LeadCount, int ContractCount, int AppointmentUpcoming, int GuestCount, int RsvpAttending);

internal static class PlannerMap
{
    public static WeddingProfileDto ToDto(WeddingProfile p) => new(p.Id, p.GroomName, p.BrideName, p.WeddingDate, p.City,
        p.TotalBudget, p.GuestCount, p.Style, p.PartnerEmail,
        p.WeddingDate.HasValue ? (int)Math.Ceiling((p.WeddingDate.Value.Date - DateTime.UtcNow.Date).TotalDays) : null,
        p.EngagementDate, p.PlanningRole, JsonSerializer.Deserialize<string[]>(p.EventsJson), JsonSerializer.Deserialize<string[]>(p.PrioritiesJson), p.OnboardingCompletedAt);
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
        var scope = await WeddingAccess.ResolveAsync(_context, _user, ct);
        var p = await _context.WeddingProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == scope.ProfileId, ct);
        return p == null ? null : PlannerMap.ToDto(p);
    }
}

/// <summary>P1 – Wizard: lưu hồ sơ cưới, tùy chọn sinh checklist + ngân sách mẫu theo ngày cưới/ngân sách.</summary>
public record SaveWeddingProfileCommand(string GroomName, string BrideName, DateTime? WeddingDate, string City,
    decimal TotalBudget, int GuestCount, string Style, string? PartnerEmail, bool GeneratePlan,
    DateTime? EngagementDate = null, string PlanningRole = "Couple", string[]? Events = null, string[]? Priorities = null) : IRequest<WeddingProfileDto>;

public class SaveWeddingProfileCommandHandler : IRequestHandler<SaveWeddingProfileCommand, WeddingProfileDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    public SaveWeddingProfileCommandHandler(IApplicationDbContext context, ICurrentUserService user) { _context = context; _user = user; }

    // Tỷ lệ ngân sách gợi ý theo hạng mục (tổng = 100%)
    private static readonly (string Name, decimal Ratio)[] BudgetSplit =
    {
        ("Nhà hàng tiệc cưới", .40m), ("Chụp ảnh cưới", .08m), ("Váy cưới & vest", .07m), ("Trang điểm cô dâu", .04m),
        ("Trang trí & hoa cưới", .08m), ("Nhẫn cưới", .08m), ("Thiệp cưới", .02m), ("Xe hoa", .025m),
        ("Lễ ăn hỏi & gia tiên", .05m), ("MC & âm nhạc", .035m), ("Quà cảm ơn", .02m), ("Lưu trú & di chuyển", .02m),
        ("Phí pháp lý & đăng ký", .005m), ("Dự phòng", .075m)
    };

    // (tiêu đề, mốc, số tháng trước ngày cưới)
    private static readonly (string Title, string Milestone, int MonthsBefore)[] ChecklistTemplate =
    {
        ("Thống nhất ngân sách & số lượng khách", "12 tháng trước", 12), ("Chọn ngày cưới & xem tuổi", "12 tháng trước", 12),
        ("Đặt nhà hàng tiệc cưới", "9 tháng trước", 9), ("Đặt lịch chụp ảnh cưới", "9 tháng trước", 9),
        ("Thử & chọn váy cưới", "6 tháng trước", 6), ("Đặt makeup cô dâu", "6 tháng trước", 6),
        ("Chọn nhẫn cưới", "4 tháng trước", 4), ("Lên danh sách khách mời", "3 tháng trước", 3),
        ("Thiết kế & gửi thiệp cưới online", "2 tháng trước", 2), ("Chốt trang trí, hoa, xe hoa", "2 tháng trước", 2),
        ("Xác nhận RSVP & sơ đồ bàn", "1 tháng trước", 1), ("Tổng duyệt với các nhà cung cấp", "1 tuần trước", 0),
        ("Thống nhất vai trò và người hỗ trợ", "12 tháng trước", 12), ("Chọn phong cách và bảng màu cưới", "12 tháng trước", 12),
        ("Tìm hiểu yêu cầu đăng ký kết hôn", "9 tháng trước", 9), ("Lên danh sách nhà cung cấp dự phòng", "9 tháng trước", 9),
        ("Lên kế hoạch lễ ăn hỏi", "6 tháng trước", 6), ("Đặt MC và ban nhạc", "6 tháng trước", 6),
        ("Chọn trang phục hai bên gia đình", "4 tháng trước", 4), ("Chụp ảnh và chọn album", "4 tháng trước", 4),
        ("Thống nhất thực đơn tiệc", "3 tháng trước", 3), ("Lên kế hoạch di chuyển khách ở xa", "3 tháng trước", 3),
        ("Đặt phòng lưu trú cho gia đình", "3 tháng trước", 3), ("Đặt quà cảm ơn khách mời", "2 tháng trước", 2),
        ("Hoàn tất thủ tục đăng ký kết hôn", "2 tháng trước", 2), ("Chọn nhạc và kịch bản buổi lễ", "2 tháng trước", 2),
        ("Chốt danh sách ảnh và video cần quay", "1 tháng trước", 1), ("Thử trang điểm và làm tóc", "1 tháng trước", 1),
        ("Kiểm tra trang phục và phụ kiện", "1 tháng trước", 1), ("Xác nhận lịch thanh toán nhà cung cấp", "1 tháng trước", 1),
        ("Phân công điều phối ngày cưới", "1 tháng trước", 1), ("Chuẩn bị túi đồ khẩn cấp", "1 tuần trước", 0),
        ("Gửi lịch trình cuối cho hai gia đình", "1 tuần trước", 0), ("Xác nhận phương tiện và giờ đón", "1 tuần trước", 0),
        ("Nghỉ ngơi và chuẩn bị lời cảm ơn", "1 tuần trước", 0)
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

    public Task<WeddingProfileDto> Handle(SaveWeddingProfileCommand r, CancellationToken ct) => _context.ExecuteSerializableAsync(inner => Save(r, inner), ct);
    private async Task<WeddingProfileDto> Save(SaveWeddingProfileCommand r, CancellationToken ct)
    {
        var scope = await WeddingAccess.ResolveAsync(_context, _user, ct, write: true);
        var uid = scope.OwnerId;
        if (string.IsNullOrWhiteSpace(r.GroomName) || string.IsNullOrWhiteSpace(r.BrideName)) throw new DomainException("Vui lòng nhập tên hai bạn.");
        if (r.GroomName.Length > 100 || r.BrideName.Length > 100 || r.City?.Length > 150 || r.Style?.Length > 50 || r.PartnerEmail?.Length > 256)
            throw new DomainException("Thông tin hồ sơ cưới vượt giới hạn cho phép.");
        if (r.PlanningRole is not ("Couple" or "Bride" or "Groom")) throw new DomainException("Vai trò lập kế hoạch không hợp lệ.");
        if ((r.Events?.Length ?? 0) > 20 || (r.Priorities?.Length ?? 0) > 20 || (r.Events ?? []).Concat(r.Priorities ?? []).Any(x => x == null || x.Length > 100))
            throw new DomainException("Danh sách sự kiện hoặc ưu tiên không hợp lệ.");
        if (r.TotalBudget < 0 || r.TotalBudget > 100_000_000_000m || r.GuestCount < 0 || r.GuestCount > 100000) throw new DomainException("Ngân sách hoặc số khách không hợp lệ.");
        if (r.WeddingDate.HasValue && r.WeddingDate.Value.Date < DateTime.UtcNow.Date) throw new DomainException("Ngày cưới phải ở tương lai.");
        var p = await _context.WeddingProfiles.FirstOrDefaultAsync(x => x.CustomerId == uid, ct);
        if (p == null) { p = WeddingProfile.Create(uid); _context.WeddingProfiles.Add(p); }
        var dateChanged = p.WeddingDate?.Date != r.WeddingDate?.Date;
        p.Update(r.GroomName ?? "", r.BrideName ?? "", r.WeddingDate, r.City ?? "", r.TotalBudget, r.GuestCount, r.Style ?? "Classic", r.PartnerEmail);
        p.Configure(r.EngagementDate, r.PlanningRole, JsonSerializer.Serialize(r.Events ?? []), JsonSerializer.Serialize(r.Priorities ?? []));

        // Upgrade existing personal resources in place when the wedding is first configured; never fork a second copy for a partner.
        foreach (var item in await _context.BudgetItems.Where(b => b.CustomerId == uid && b.WeddingProfileId == null).ToListAsync(ct)) item.AttachToWedding(p.Id, item.Owner);
        foreach (var task in await _context.ChecklistTasks.Where(t => t.CustomerId == uid && t.WeddingProfileId == null).ToListAsync(ct)) task.AttachToWedding(p.Id, task.Owner);
        var priorRsvps = await _context.GuestRSVPs.AsNoTracking().Where(g => g.Invitation.CustomerId == uid && g.Type == "Attend").ToListAsync(ct);
        var linkedRsvps = await _context.WeddingGuests.Where(g => g.WeddingProfileId == p.Id && g.RsvpId != null).Select(g => g.RsvpId).ToListAsync(ct);
        foreach (var response in priorRsvps.Where(g => !linkedRsvps.Contains(g.Id))) {
            var guest = WeddingGuest.Create(p.Id);
            guest.Update(response.GuestName, response.PhoneNumber, "RSVP online", "Shared", 1 + response.CompanionCount,
                response.Status == Domain.Enums.RSVPStatus.Tentative ? "Maybe" : response.Status.ToString(), response.DietaryPreference, response.Wishes);
            guest.LinkRsvp(response.Id); _context.WeddingGuests.Add(guest);
        }

        if (r.GeneratePlan)
        {
            p.CompleteOnboarding();
            var existingBudget = await _context.BudgetItems.Where(b => b.CustomerId == uid).Select(b => b.ItemName).ToListAsync(ct);
            if (r.TotalBudget > 0)
                foreach (var (name, ratio) in BudgetSplit)
                    if (!existingBudget.Contains(name)) {
                        var item = BudgetItem.Create(uid, null, name, Math.Round(r.TotalBudget * ratio));
                        item.AttachToWedding(p.Id); _context.BudgetItems.Add(item);
                    }

            var existingTasks = await _context.ChecklistTasks.Where(c => c.CustomerId == uid).Select(c => c.Title).ToListAsync(ct);
                foreach (var (title, milestone, months) in ChecklistTemplate)
                    if (!existingTasks.Contains(title)) {
                        var task = ChecklistTask.Create(uid, title, milestone, ComputeDue(r.WeddingDate, months));
                        task.AttachToWedding(p.Id); _context.ChecklistTasks.Add(task);
                    }
        }

        // Đổi ngày cưới → tính lại hạn các việc mẫu chưa hoàn thành (kể cả checklist đã sinh trước đó bị hạn quá khứ).
        var templateMonths = ChecklistTemplate.ToDictionary(t => t.Title, t => t.MonthsBefore);
        var generated = await _context.ChecklistTasks.Where(c => c.CustomerId == uid && !c.IsCompleted).ToListAsync(ct);
        foreach (var t in generated.Where(t => dateChanged && templateMonths.ContainsKey(t.Title)))
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
        var scope = await WeddingAccess.ResolveAsync(_context, _user, ct);
        var uid = scope.OwnerId;
        var p = await _context.WeddingProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == uid, ct);
        var tasks = await _context.ChecklistTasks.AsNoTracking().Where(t => t.CustomerId == uid).ToListAsync(ct);
        var budget = await _context.BudgetItems.AsNoTracking().Where(b => b.CustomerId == uid).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var guests = await _context.WeddingGuests.AsNoTracking().Where(g => g.WeddingProfileId == scope.ProfileId).ToListAsync(ct);
        return new PlannerOverviewDto(p == null ? null : PlannerMap.ToDto(p), tasks.Count, tasks.Count(t => t.IsCompleted),
            budget.Sum(b => b.PlannedCost), budget.Sum(b => b.ActualCost),
            await _context.Leads.CountAsync(l => l.CustomerId == uid, ct),
            await _context.BookingContracts.CountAsync(c => c.CustomerId == uid, ct),
            await _context.Appointments.CountAsync(a => a.CustomerId == uid && a.StartAt > now &&
                (a.Status == Domain.Enums.AppointmentStatus.Confirmed || a.Status == Domain.Enums.AppointmentStatus.Pending), ct),
            guests.Count, guests.Where(g => g.Status == "Attending").Sum(g => g.PartySize));
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
    private readonly IBillingService _billing;

    public ActivateInvitationPremiumCommandHandler(IApplicationDbContext context, ICurrentUserService user, IBillingService billing)
    { _context = context; _user = user; _billing = billing; }

    public async Task<InvitationPremiumDto> Handle(ActivateInvitationPremiumCommand r, CancellationToken ct)
    {
        var uid = _user.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        var inv = await _context.WeddingInvitations.FirstOrDefaultAsync(i => i.CustomerId == uid, ct)
            ?? throw new DomainException("Bạn cần tạo thiệp trước khi nâng cấp Premium.");
        if (inv.IsPremium)
            return new InvitationPremiumDto(true, PremiumPrice, inv.PremiumPaymentRef ?? "", "", inv.PremiumActivatedAt);
        if (r.ConfirmPaid) throw new DomainException("Không thể tự xác nhận đã trả tiền. Premium chỉ kích hoạt sau khi giao dịch ngân hàng được đối soát.");
        var order = await _billing.CreatePremiumOrderAsync(uid, ct);
        return new InvitationPremiumDto(order.IsPremium, order.Price, order.PaymentRef, order.QrUrl, order.ActivatedAt);
    }
}
