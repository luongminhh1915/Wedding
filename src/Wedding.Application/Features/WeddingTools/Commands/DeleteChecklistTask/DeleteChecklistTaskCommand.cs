using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;

namespace Wedding.Application.Features.WeddingTools.Commands.DeleteChecklistTask;

public record DeleteChecklistTaskCommand(Guid Id) : IRequest<bool>;

public class DeleteChecklistTaskCommandHandler : IRequestHandler<DeleteChecklistTaskCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteChecklistTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(DeleteChecklistTaskCommand request, CancellationToken cancellationToken)
    {
        var scope = await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken, write: true);
        var userId = scope.OwnerId;

        var task = await _context.ChecklistTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.CustomerId == userId, cancellationToken)
            ?? throw new DomainException("Không tìm thấy công việc.");

        _context.ChecklistTasks.Remove(task);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
