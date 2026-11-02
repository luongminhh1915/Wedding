using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Settlements;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Services;

public class ReleasePeriod2BackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReleasePeriod2BackgroundService> _logger;
    public ReleasePeriod2BackgroundService(IServiceScopeFactory scopes, ILogger<ReleasePeriod2BackgroundService> logger) { _scopes = scopes; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        do
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var audit = scope.ServiceProvider.GetRequiredService<IAuditLogger>();
                var now = DateTime.UtcNow;
                var locked = await db.Commissions.Include(c => c.Contract).Where(c => c.Status == CommissionStatus.EscrowLocked && c.ReleaseAt <= now).ToListAsync(stoppingToken);
                foreach (var c in locked)
                {
                    if (c.Contract.Status is not (ContractStatus.Confirmed or ContractStatus.Completed)) continue;
                    if (await db.Disputes.AnyAsync(d => d.ContractId == c.ContractId && (d.Status == DisputeStatus.Open || d.Status == DisputeStatus.InReview), stoppingToken)) continue;
                    c.Release(now);
                    if (c.Contract.Status == ContractStatus.Confirmed) c.Contract.CompleteContract();
                    await audit.LogAsync("Commission.Period2Released", "Commission", c.Id.ToString(), new { c.ContractId, c.CommissionAmount });
                }
                foreach (var s in await db.CommissionSettlements.Where(s => s.Status == SettlementStatus.Closed).ToListAsync(stoppingToken)) s.MarkOverdue();
                await db.SaveChangesAsync(stoppingToken);
                var vn = now.AddHours(7);
                if (vn.Day >= 25) await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RunSettlementCommand(vn.ToString("yyyy-MM")), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Financial release/settlement job failed; no fake payment is recorded."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
