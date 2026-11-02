using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Invitations.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;

namespace Wedding.Application.Features.Invitations.Queries.GetMyInvitation;

public record GetMyInvitationQuery : IRequest<WeddingInvitationDto?>;

public class GetMyInvitationQueryHandler : IRequestHandler<GetMyInvitationQuery, WeddingInvitationDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyInvitationQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<WeddingInvitationDto?> Handle(GetMyInvitationQuery request, CancellationToken cancellationToken)
    {
        var userId = (await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken)).OwnerId;

        var invitation = await _context.WeddingInvitations
            .Include(w => w.Guests)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerId == userId, cancellationToken);

        if (invitation == null)
            return null;

        var guests = invitation.Guests ?? new List<GuestRSVP>();
        var totalGuests = guests.Count;
        var attending = guests.Count(g => g.Status == RSVPStatus.Attending);
        var notAttending = guests.Count(g => g.Status == RSVPStatus.NotAttending);
        var totalAccompanying = guests.Where(g => g.Status == RSVPStatus.Attending).Sum(g => g.CompanionCount);
        var totalAttendingPeople = attending + totalAccompanying;
        var estimatedTables = (int)Math.Ceiling(totalAttendingPeople / 10.0);

        return new WeddingInvitationDto(
            Id: invitation.Id,
            CustomerId: invitation.CustomerId,
            Slug: invitation.Slug,
            GroomName: invitation.GroomName,
            BrideName: invitation.BrideName,
            EventDate: invitation.GetEventDateWithOffset(),
            VenueName: invitation.VenueName,
            VenueAddress: invitation.VenueAddress,
            MapUrl: invitation.MapUrl,
            LoveStory: invitation.LoveStory,
            CoverImageUrl: invitation.CoverImageUrl,
            MusicUrl: invitation.MusicUrl,
            TemplateStyle: invitation.TemplateStyle,
            BankInfo: invitation.BankInfo,
            IsPublished: invitation.IsPublished,
            CreatedAt: invitation.CreatedAt,
            TotalGuests: totalGuests,
            AttendingCount: attending,
            NotAttendingCount: notAttending,
            TotalAccompanying: totalAccompanying,
            EstimatedTables: Math.Max(estimatedTables, 1),
            IsPremium: invitation.IsPremium,
            TemplateId: invitation.TemplateId, Lang: invitation.Lang, ContentJson: invitation.ContentJson
        );
    }
}
