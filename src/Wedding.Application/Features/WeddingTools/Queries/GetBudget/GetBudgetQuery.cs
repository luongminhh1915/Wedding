using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.WeddingTools.DTOs;
using Wedding.Domain.Exceptions;

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
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để quản lý ngân sách cưới.");

        var items = await _context.BudgetItems
            .Where(b => b.CustomerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Mặc định tổng ngân sách ban đầu nếu có items thì là tổng PlannedCost, hoặc ít nhất 200 triệu
        var totalPlanned = items.Sum(b => b.PlannedCost);
        if (totalPlanned <= 0) totalPlanned = 250000000;

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
            CreatedAt: b.CreatedAt
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
