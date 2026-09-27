using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.WeddingTools.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.WeddingTools.Commands.SaveChecklistTask;

public record SaveChecklistTaskCommand(
    Guid? Id,
    string Title,
    string Milestone,
    DateTime? DueDate = null,
    string? Notes = null
) : IRequest<ChecklistTaskDto>;

public class SaveChecklistTaskCommandHandler : IRequestHandler<SaveChecklistTaskCommand, ChecklistTaskDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SaveChecklistTaskCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ChecklistTaskDto> Handle(SaveChecklistTaskCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thêm công việc.");

        ChecklistTask task;

        if (request.Id.HasValue && request.Id.Value != Guid.Empty)
        {
            task = await _context.ChecklistTasks
                .FirstOrDefaultAsync(t => t.Id == request.Id.Value && t.CustomerId == userId, cancellationToken)
                ?? throw new DomainException("Không tìm thấy công việc.");

            task.Update(request.Title, request.Milestone, request.DueDate, request.Notes);
        }
        else
        {
            task = ChecklistTask.Create(
                customerId: userId,
                title: request.Title,
                milestone: request.Milestone,
                dueDate: request.DueDate,
                notes: request.Notes
            );

            _context.ChecklistTasks.Add(task);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ChecklistTaskDto(
            Id: task.Id,
            Title: task.Title,
            Milestone: task.Milestone,
            DueDate: task.DueDate,
            IsCompleted: task.IsCompleted,
            Notes: task.Notes,
            CreatedAt: task.CreatedAt
        );
    }
}
