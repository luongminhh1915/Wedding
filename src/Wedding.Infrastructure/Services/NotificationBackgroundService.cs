using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Services;
public class NotificationBackgroundService(IServiceScopeFactory scopes, ILogger<NotificationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let schema initialization finish before the first read.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var smtp = scope.ServiceProvider.GetRequiredService<SmtpDelivery>();
                await db.ExecuteSerializableAsync(async ct =>
                {
                    var now = DateTime.UtcNow;
                    var contracts = await db.BookingContracts.Where(c => c.Status == ContractStatus.PendingVerification).ToListAsync(ct);
                    foreach (var contract in contracts)
                        foreach (var hour in new[] { 24, 48 })
                            if (contract.CreatedAt.AddHours(hour) <= now) await AddReminder(db, contract.CustomerId, $"contract:{contract.Id}:{hour}", $"Hợp đồng {contract.ContractCode} đang chờ xác nhận (hạn 72 giờ).", "contract", contract.Id, ct);
                    var tasks = await db.ChecklistTasks.Where(t => !t.IsCompleted && t.DueDate != null && t.DueDate <= now.AddDays(3) && t.DueDate >= now).ToListAsync(ct);
                    foreach (var task in tasks) await AddReminder(db, task.CustomerId, $"task:{task.Id}:{task.DueDate:yyyyMMdd}", $"Nhắc việc: {task.Title} — hạn {task.DueDate:dd/MM/yyyy}.", "task", task.Id, ct);
                    await db.SaveChangesAsync(ct);
                    return true;
                }, stoppingToken);
                    var deliveryTime = DateTime.UtcNow;
                    var queue = await db.Notifications.Where(n => n.Channel == "Email" && n.SentAt == null && n.Attempts < 8 && n.NextAttemptAt <= deliveryTime).OrderBy(n => n.CreatedAt).Take(20).ToListAsync(stoppingToken);
                    foreach (var notification in queue)
                    {
                        // Atomic five-minute lease; SMTP is at-least-once, never holds the booking lock.
                        var claimed = await db.Notifications.Where(n => n.Id == notification.Id && n.SentAt == null && n.NextAttemptAt <= deliveryTime)
                            .ExecuteUpdateAsync(update => update.SetProperty(n => n.NextAttemptAt, deliveryTime.AddMinutes(5)), stoppingToken);
                        if (claimed == 0) continue;
                        if (!smtp.EmailEnabled) { notification.Failed("SMTP chưa được cấu hình; thông báo vẫn hiển thị trong ứng dụng."); continue; }
                        try { await smtp.SendAsync(notification.Destination, "Thông báo Wedding Match", notification.Message, stoppingToken); notification.Delivered(); }
                        catch (Exception ex) when (ex is not OperationCanceledException) { notification.Failed("Không gửi được email; sẽ thử lại."); }
                    }
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notification outbox processing failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
    private static async Task AddReminder(IApplicationDbContext db, Guid userId, string key, string message, string contextKind, Guid contextId, CancellationToken ct)
    {
        if (await db.Notifications.AnyAsync(n => n.UserId == userId && n.DeduplicationKey == key, ct)) return;
        var recipient = await db.Users.Where(u => u.Id == userId).Select(u => new { u.Email }).FirstOrDefaultAsync(ct);
        if (recipient != null) db.Notifications.Add(Notification.Create(userId,
            string.IsNullOrWhiteSpace(recipient.Email) ? "InApp" : "Email", recipient.Email ?? "", message, key, contextKind, contextId));
    }
}
