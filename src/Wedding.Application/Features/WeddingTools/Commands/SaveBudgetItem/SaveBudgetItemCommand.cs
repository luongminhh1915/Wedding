using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.WeddingTools.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;

namespace Wedding.Application.Features.WeddingTools.Commands.SaveBudgetItem;

public record SaveBudgetItemCommand(
    Guid? Id,
    string ItemName,
    decimal PlannedCost,
    decimal ActualCost = 0,
    string? Notes = null,
    string? Owner = null
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
        var scope = await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken, write: true);
        var userId = scope.OwnerId;

        BudgetItem item;

        if (request.Id.HasValue && request.Id.Value != Guid.Empty)
        {
            item = await _context.BudgetItems
                .FirstOrDefaultAsync(b => b.Id == request.Id.Value && b.CustomerId == userId, cancellationToken)
                ?? throw new DomainException("Không tìm thấy hạng mục ngân sách.");

            item.UpdateCosts(request.PlannedCost, request.ActualCost, request.Notes, request.ActualCost > 0);
            item.Rename(request.ItemName);
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

        item.AssignOwner(request.Owner ?? item.Owner);
        if (scope.ProfileId.HasValue) item.AttachToWedding(scope.ProfileId.Value, item.Owner);
        await _context.SaveChangesAsync(cancellationToken);

        return new BudgetItemDto(
            Id: item.Id,
            ItemName: item.ItemName,
            PlannedCost: item.PlannedCost,
            ActualCost: item.ActualCost,
            Notes: item.Notes,
            IsPaid: item.IsPaid,
            CreatedAt: item.CreatedAt, Owner: item.Owner, WeddingProfileId: item.WeddingProfileId, ContractId: item.ContractId
        );
    }
}
