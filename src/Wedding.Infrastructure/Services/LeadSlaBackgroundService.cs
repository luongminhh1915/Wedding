using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Services;

/// <summary>
/// BR-003: Background Service tự động quét kiểm tra SLA tiếp nhận Lead.
/// Định kỳ mỗi 15 phút quét các Lead ở trạng thái New quá 24h và tự động chuyển Cancelled/Expired.
/// </summary>
public class LeadSlaBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LeadSlaBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public LeadSlaBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<LeadSlaBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Lead SLA Background Worker đã khởi động.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredLeadsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét SLA Lead.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredLeadsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var deadline = DateTime.UtcNow.AddHours(-24);

        // BR-003: Quét các Lead ở trạng thái New quá 24h không được NCC tiếp nhận
        var overdueLeads = await context.Leads
            .Where(l => l.Status == LeadStatus.New && l.CreatedAt <= deadline)
            .ToListAsync(cancellationToken);

        if (overdueLeads.Any())
        {
            _logger.LogWarning("Phát hiện {Count} Lead quá hạn 24h chưa được NCC tiếp nhận. Bắt đầu hủy tự động.", overdueLeads.Count);

            foreach (var lead in overdueLeads)
            {
                lead.Expire();
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Đã cập nhật trạng thái hủy {Count} Lead vi phạm SLA.", overdueLeads.Count);
        }
    }
}
