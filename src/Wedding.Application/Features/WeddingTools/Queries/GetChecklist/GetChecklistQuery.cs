using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.WeddingTools.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;

namespace Wedding.Application.Features.WeddingTools.Queries.GetChecklist;

public record GetChecklistQuery : IRequest<List<ChecklistTaskDto>>;

public class GetChecklistQueryHandler : IRequestHandler<GetChecklistQuery, List<ChecklistTaskDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetChecklistQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ChecklistTaskDto>> Handle(GetChecklistQuery request, CancellationToken cancellationToken)
    {
        var scope = await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken);
        var userId = scope.OwnerId;

        var tasks = await _context.ChecklistTasks
            .Where(t => t.CustomerId == userId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        // A GET must never create data. The onboarding command generates the canonical 35-task plan once.

        return tasks.Select(t => new ChecklistTaskDto(
            Id: t.Id,
            Title: t.Title,
            Milestone: t.Milestone,
            DueDate: t.DueDate,
            IsCompleted: t.IsCompleted,
            Notes: t.Notes,
            CreatedAt: t.CreatedAt, Owner: t.Owner, WeddingProfileId: t.WeddingProfileId, ContractId: t.ContractId
        )).ToList();
    }
}
