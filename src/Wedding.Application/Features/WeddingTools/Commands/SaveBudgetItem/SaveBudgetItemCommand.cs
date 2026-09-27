using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.WeddingTools.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.WeddingTools.Commands.SaveBudgetItem;

public record SaveBudgetItemCommand(
    Guid? Id,
    string ItemName,
    decimal PlannedCost,
    decimal ActualCost = 0,
    string? Notes = null
) : IRequest<BudgetItemDto>;

public class SaveBudgetItemCommandHandler : IRequestHandler<SaveBudgetItemCommand, BudgetItemDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SaveBudgetItemCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<BudgetItemDto> Handle(SaveBudgetItemCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thao tác ngân sách.");

        BudgetItem item;

        if (request.Id.HasValue && request.Id.Value != Guid.Empty)
        {
            item = await _context.BudgetItems
                .FirstOrDefaultAsync(b => b.Id == request.Id.Value && b.CustomerId == userId, cancellationToken)
                ?? throw new DomainException("Không tìm thấy hạng mục ngân sách.");

            item.UpdateCosts(request.PlannedCost, request.ActualCost, request.Notes, request.ActualCost > 0);
        }
        else
        {
            item = BudgetItem.Create(
                customerId: userId,
                categoryId: null,
                itemName: request.ItemName,
                plannedCost: request.PlannedCost,
                actualCost: request.ActualCost,
                notes: request.Notes
            );

            _context.BudgetItems.Add(item);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new BudgetItemDto(
            Id: item.Id,
            ItemName: item.ItemName,
            PlannedCost: item.PlannedCost,
            ActualCost: item.ActualCost,
            Notes: item.Notes,
            IsPaid: item.IsPaid,
            CreatedAt: item.CreatedAt
        );
    }
}
