using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;

namespace Wedding.Application.Features.WeddingTools.Commands.ToggleChecklistTask;

public record ToggleChecklistTaskCommand(Guid Id) : IRequest<bool>;

public class ToggleChecklistTaskCommandHandler : IRequestHandler<ToggleChecklistTaskCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ToggleChecklistTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(ToggleChecklistTaskCommand request, CancellationToken cancellationToken)
    {
        var scope = await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken, write: true);
        var userId = scope.OwnerId;

        var task = await _context.ChecklistTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.CustomerId == userId, cancellationToken)
            ?? throw new DomainException("Không tìm thấy công việc trong danh sách.");

        task.ToggleComplete();
        await _context.SaveChangesAsync(cancellationToken);
        return task.IsCompleted;
    }
}
