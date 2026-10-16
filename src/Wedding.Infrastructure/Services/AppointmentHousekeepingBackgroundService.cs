using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;

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
                var stale = await context.Appointments
                    .Where(a => (a.Status == AppointmentStatus.AwaitingDeposit && a.CreatedAt <= depositCutoff)
                             || (a.Status == AppointmentStatus.Pending && a.StartAt <= now))
                    .ToListAsync(stoppingToken);
                foreach (var a in stale) a.Expire();
                if (stale.Count > 0)
                {
                    await context.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Đã hết hạn {Count} lịch hẹn.", stale.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dọn lịch hẹn quá hạn.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
