using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.WeddingTools.DTOs;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;

namespace Wedding.Application.Features.WeddingTools.Queries.GetBudget;

public record GetBudgetQuery : IRequest<BudgetSummaryDto>;

public class GetBudgetQueryHandler : IRequestHandler<GetBudgetQuery, BudgetSummaryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetBudgetQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<BudgetSummaryDto> Handle(GetBudgetQuery request, CancellationToken cancellationToken)
    {
        var scope = await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken);
        var userId = scope.OwnerId;

        var items = await _context.BudgetItems
            .Where(b => b.CustomerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Mặc định tổng ngân sách ban đầu nếu có items thì là tổng PlannedCost, hoặc ít nhất 200 triệu
        // Ưu tiên tổng ngân sách cặp đôi đã khai ở hồ sơ cưới (wizard); không có thì lấy tổng các khoản dự kiến.
        var profileBudget = await _context.WeddingProfiles.AsNoTracking().Where(p => p.CustomerId == userId)
            .Select(p => (decimal?)p.TotalBudget).FirstOrDefaultAsync(cancellationToken) ?? 0m;
        var totalPlanned = profileBudget > 0 ? profileBudget : items.Sum(b => b.PlannedCost);

        // FM-006: Remaining_Budget = Total_Planned_Budget - Sum(Actual_Spent)
        var totalActual = items.Sum(b => b.ActualCost);
        var remaining = totalPlanned - totalActual;
        var percentage = totalPlanned > 0 ? (double)(totalActual / totalPlanned) * 100 : 0;

        var dtos = items.Select(b => new BudgetItemDto(
            Id: b.Id,
            ItemName: b.ItemName,
            PlannedCost: b.PlannedCost,
            ActualCost: b.ActualCost,
            Notes: b.Notes,
            IsPaid: b.IsPaid,
            CreatedAt: b.CreatedAt, Owner: b.Owner, WeddingProfileId: b.WeddingProfileId, ContractId: b.ContractId
        )).ToList();

        return new BudgetSummaryDto(
            TotalPlannedBudget: totalPlanned,
            TotalActualCost: totalActual,
            RemainingBudget: remaining,
            SpentPercentage: Math.Round(percentage, 1),
            Items: dtos
        );
    }
}
