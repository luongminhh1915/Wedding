using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Services;

/// <summary>
/// BR-007: Background Service đối soát hoa hồng định kỳ.
/// Quét các khoản hoa hồng quá hạn (DueDate) chuyển sang Overdue.
/// Nếu NCC trễ hạn quá 7 ngày, tự động tạm ẩn các bài đăng dịch vụ (chuyển Hidden).
/// </summary>
public class MonthlySettlementBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MonthlySettlementBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public MonthlySettlementBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MonthlySettlementBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Monthly Settlement Background Worker (BR-007) đã khởi động.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOverdueCommissionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình đối soát hoa hồng BR-007.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessOverdueCommissionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;

        // 1. Quét các khoản hoa hồng Pending đã quá hạn thanh toán
        var overdueCommissions = await context.Commissions
            .Where(c => c.Status == CommissionStatus.Pending && c.DueDate < now)
            .ToListAsync(cancellationToken);

        foreach (var c in overdueCommissions)
        {
            c.MarkOverdue();
        }

        // 2. BR-007: Phạt NCC trễ hạn quá 7 ngày -> Tạm ẩn các bài đăng dịch vụ
        var sevenDaysOverdueThreshold = now.AddDays(-7);
        var heavyOverdueVendorIds = await context.Commissions
            .Where(c => c.Status == CommissionStatus.Overdue && c.DueDate < sevenDaysOverdueThreshold)
            .Select(c => c.VendorId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (heavyOverdueVendorIds.Any())
        {
            var listingsToHide = await context.Listings
                .Where(l => heavyOverdueVendorIds.Contains(l.VendorId) && l.Status == ListingStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var listing in listingsToHide)
            {
                listing.SetStatus(ListingStatus.Hidden);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
