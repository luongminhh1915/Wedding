using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

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
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thao tác checklist.");

        var task = await _context.ChecklistTasks
            .FirstOrDefaultAsync(t => t.Id == request.Id && t.CustomerId == userId, cancellationToken)
            ?? throw new DomainException("Không tìm thấy công việc trong danh sách.");

        task.ToggleComplete();
        await _context.SaveChangesAsync(cancellationToken);
        return task.IsCompleted;
    }
}
