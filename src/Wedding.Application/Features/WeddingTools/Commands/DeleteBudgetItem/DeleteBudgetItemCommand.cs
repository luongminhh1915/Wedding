using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;

namespace Wedding.Application.Features.WeddingTools.Commands.DeleteBudgetItem;

public record DeleteBudgetItemCommand(Guid Id) : IRequest<bool>;

public class DeleteBudgetItemCommandHandler : IRequestHandler<DeleteBudgetItemCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteBudgetItemCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(DeleteBudgetItemCommand request, CancellationToken cancellationToken)
    {
        var scope = await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken, write: true);
        var userId = scope.OwnerId;

        var item = await _context.BudgetItems
            .FirstOrDefaultAsync(b => b.Id == request.Id && b.CustomerId == userId, cancellationToken)
            ?? throw new DomainException("Không tìm thấy hạng mục ngân sách.");

        _context.BudgetItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
