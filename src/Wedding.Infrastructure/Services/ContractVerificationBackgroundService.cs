using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Entities;
using Wedding.Application.Features.CustomerOrders;

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

        var count = await ExpirePendingAsync(context, DateTime.UtcNow, cancellationToken);
        if (count > 0) _logger.LogInformation("Đã cập nhật trạng thái hủy {Count} hợp đồng quá hạn chưa có giao dịch cần đối soát.", count);
    }

    public static Task<int> ExpirePendingAsync(IApplicationDbContext context, DateTime now, CancellationToken cancellationToken)
        => context.ExecuteSerializableAsync(async ct =>
        {
            var expired = await context.BookingContracts.Where(c => c.Status == ContractStatus.PendingVerification && c.VerificationDeadline <= now).ToListAsync(ct);
            var cancelled = 0;
            foreach (var contract in expired)
            {
                if (await CustomerOrderJourney.HasMoneyAsync(context, new[] { contract.LeadId }, ct))
                {
                    var key = contract.Id.ToString();
                    if (!await context.AuditLogs.AnyAsync(a => a.Action == "Contract.ExpiryNeedsFinance" && a.EntityId == key, ct))
                    {
                        context.AuditLogs.Add(AuditLog.Create(null, "System", "Contract.ExpiryNeedsFinance", "BookingContract", key));
                        var vendorUserId = await context.Vendors.Where(v => v.Id == contract.VendorId).Select(v => v.UserId).SingleAsync(ct);
                        var recipients = await context.Users.Where(u => u.Role == UserRole.Finance || u.Role == UserRole.SuperAdmin).Select(u => u.Id).ToListAsync(ct);
                        recipients.Add(contract.CustomerId); recipients.Add(vendorUserId);
                        foreach (var id in recipients.Distinct()) context.Notifications.Add(Notification.Create(id, "InApp", "", $"Hợp đồng {contract.ContractCode} quá hạn duyệt nhưng có cọc/chuyển khoản cần đối soát. Hệ thống giữ nguyên hợp đồng và ngân sách; NCC/Tài chính cần phối hợp, không tự hủy hoặc hoàn tiền."));
                    }
                    continue;
                }
                contract.Cancel("Quá hạn 72h khách hàng không xác nhận hợp đồng (BR-005)."); cancelled++;
            }
            await context.SaveChangesAsync(ct); return cancelled;
        }, cancellationToken);
}
