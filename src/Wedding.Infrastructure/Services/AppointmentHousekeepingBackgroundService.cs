using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Services;

/// <summary>
/// P3: Mỗi 15 phút – lịch chờ cọc quá 30 phút hoặc lịch chờ duyệt đã qua giờ hẹn → Expired (giải phóng slot).
/// </summary>
public class AppointmentHousekeepingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppointmentHousekeepingBackgroundService> _logger;

    public AppointmentHousekeepingBackgroundService(IServiceProvider serviceProvider, ILogger<AppointmentHousekeepingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var now = DateTime.UtcNow;
                var depositCutoff = now.AddMinutes(-30);
                await context.ExecuteSerializableAsync(async ct =>
                {
                    var stale = await context.Appointments.Where(a =>
                        (a.Status == AppointmentStatus.AwaitingDeposit && a.CreatedAt <= depositCutoff) ||
                        ((a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.RescheduleProposed) && a.ResponseDueAt <= now)).ToListAsync(ct);
                    var notify = scope.ServiceProvider.GetRequiredService<INotificationSender>();
                    foreach (var a in stale)
                    {
                        a.Expire(); context.AppointmentEvents.Add(AppointmentEvent.Create(a.Id, null, "Expired", "Hết thời hạn phản hồi/giữ cọc"));
                        var customer = await context.Users.FirstOrDefaultAsync(u => u.Id == a.CustomerId, ct);
                        if (customer != null) await notify.SendAsync("Email", customer.NotificationRecipient, $"Lịch hẹn {a.Code} đã hết hạn. Khoản cọc cần hoàn: {a.RefundDue:N0}đ.", ct);
                    }
                    var noShows = await context.Appointments.Where(a => a.Status == AppointmentStatus.Confirmed && a.StartAt <= now.AddMinutes(-30)).ToListAsync(ct);
                    foreach (var a in noShows)
                    {
                        a.MarkNoShow(); context.AppointmentEvents.Add(AppointmentEvent.Create(a.Id, null, "NoShow", "Quá giờ hẹn 30 phút, chưa check-in"));
                    }
                    var upcoming = await context.Appointments.Include(a => a.Customer).Include(a => a.Vendor).ThenInclude(v => v.User)
                        .Where(a => a.Status == AppointmentStatus.Confirmed && a.StartAt > now && a.StartAt <= now.AddHours(24)).ToListAsync(ct);
                    foreach (var a in upcoming)
                    {
                        var hours = (a.StartAt - now).TotalHours;
                        if (!a.CustomerReminder24At.HasValue)
                        {
                            await notify.SendAsync("Email", a.Customer.NotificationRecipient, $"Nhắc lịch hẹn {a.Code}: {(a.StartAt.AddHours(7)):HH:mm dd/MM/yyyy}.", ct); a.MarkReminder("customer24");
                        }
                        if (hours <= 2 && !a.CustomerReminder2At.HasValue)
                        {
                            await notify.SendAsync("Email", a.Customer.NotificationRecipient, $"Lịch hẹn {a.Code} còn dưới 2 giờ.", ct); a.MarkReminder("customer2");
                        }
                        if (hours <= 1 && !a.VendorReminder1At.HasValue)
                        {
                            await notify.SendAsync("Email", a.Vendor.User.NotificationRecipient, $"Lịch hẹn {a.Code} còn dưới 1 giờ.", ct); a.MarkReminder("vendor1");
                        }
                    }
                    await ExpireComboLocksAsync(context, now, ct);
                    await context.SaveChangesAsync(ct); return stale.Count;
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dọn lịch hẹn quá hạn.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    public static Task<int> ExpireComboLocksAsync(IApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        => context.ExecuteSerializableAsync(async ct =>
        {
            var orders = await context.ComboOrders.Include(o => o.Items).Where(o => o.PriceLockUntil <= now &&
                (o.Status == ComboOrderStatus.Submitted || o.Status == ComboOrderStatus.InProgress || o.Status == ComboOrderStatus.PartiallyQuoted || o.Status == ComboOrderStatus.Quoted)).ToListAsync(ct);
            foreach (var order in orders)
            {
                order.ExpirePriceLock();
                foreach (var item in order.Items)
                {
                    // Expiring an indicative quote is not cancellation of a real contract/deposit obligation.
                    if (item.LeadId.HasValue && await Wedding.Application.Features.CustomerOrders.CustomerOrderJourney.WithdrawalBlockAsync(context, new[] { item.LeadId.Value }, ct) != null) continue;
                    await Wedding.Application.Features.Combos.ComboBudgetSynchronization.ReleaseAsync(context, item, ct);
                }
            }
            await context.SaveChangesAsync(ct); return orders.Count;
        }, cancellationToken);
}
