using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Services;

/// <summary>
/// BR-005: Background Service quét kiểm tra hợp đồng chờ xác nhận 2 chiều.
/// Định kỳ mỗi 30 phút quét các hợp đồng ở trạng thái PendingVerification quá 72h mà khách không duyệt -> Chuyển sang Cancelled.
/// </summary>
public class ContractVerificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ContractVerificationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

    public ContractVerificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ContractVerificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Contract Verification SLA Background Worker đã khởi động.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredContractsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét hợp đồng quá hạn 72h.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredContractsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTime.UtcNow;

        // BR-005: Quét các hợp đồng PendingVerification đã vượt quá thời hạn 72h
        var expiredContracts = await context.BookingContracts
            .Where(c => c.Status == ContractStatus.PendingVerification && c.VerificationDeadline <= now)
            .ToListAsync(cancellationToken);

        if (expiredContracts.Any())
        {
            _logger.LogWarning("Phát hiện {Count} hợp đồng quá hạn 72h chưa được khách hàng xác nhận. Hủy tự động theo BR-005.", expiredContracts.Count);

            foreach (var contract in expiredContracts)
            {
                contract.Cancel("Quá hạn 72h khách hàng không xác nhận hợp đồng (BR-005).");
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Đã cập nhật trạng thái hủy {Count} hợp đồng quá hạn 72h.", expiredContracts.Count);
        }
    }
}
