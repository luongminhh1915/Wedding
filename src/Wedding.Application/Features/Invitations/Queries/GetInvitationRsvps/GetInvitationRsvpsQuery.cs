using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Invitations.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Invitations.Queries.GetInvitationRsvps;

public record GetInvitationRsvpsQuery(string? Status = null) : IRequest<List<GuestRsvpDto>>;

public class GetInvitationRsvpsQueryHandler : IRequestHandler<GetInvitationRsvpsQuery, List<GuestRsvpDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetInvitationRsvpsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<GuestRsvpDto>> Handle(GetInvitationRsvpsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem danh sách khách mời phản hồi.");

        var invitation = await _context.WeddingInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerId == userId, cancellationToken)
            ?? throw new DomainException("Bạn chưa tạo thiệp cưới nào.");

        var query = _context.GuestRSVPs
            .Where(g => g.InvitationId == invitation.Id);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<RSVPStatus>(request.Status, true, out var parsedStatus))
            {
                query = query.Where(g => g.Status == parsedStatus);
            }
        }

        var rsvps = await query
            .OrderByDescending(g => g.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rsvps.Select(g => new GuestRsvpDto(
            Id: g.Id,
            InvitationId: g.InvitationId,
            GuestName: g.GuestName,
            PhoneNumber: g.PhoneNumber,
            Status: g.Status.ToString(),
            CompanionCount: g.CompanionCount,
            Wishes: g.Wishes,
            DietaryPreference: g.DietaryPreference,
            CreatedAt: g.CreatedAt
        )).ToList();
    }
}
