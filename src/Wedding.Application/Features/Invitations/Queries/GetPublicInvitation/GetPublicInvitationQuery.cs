using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Invitations.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Invitations.Queries.GetPublicInvitation;

public record GetPublicInvitationQuery(string Slug) : IRequest<PublicInvitationDto>;

public class GetPublicInvitationQueryHandler : IRequestHandler<GetPublicInvitationQuery, PublicInvitationDto>
{
    private readonly IApplicationDbContext _context;

    public GetPublicInvitationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PublicInvitationDto> Handle(GetPublicInvitationQuery request, CancellationToken cancellationToken)
    {
        var cleanSlug = request.Slug.Trim().ToLowerInvariant();

        var invitation = await _context.WeddingInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Slug == cleanSlug && w.IsPublished, cancellationToken)
            ?? throw new DomainException("Thiệp cưới không tồn tại hoặc đã bị ngừng chia sẻ.");

        var recentWishes = await _context.GuestRSVPs.AsNoTracking()
            .Where(g => g.InvitationId == invitation.Id && g.Wishes != null && g.Wishes != "")
            .OrderByDescending(g => g.CreatedAt)
            .Take(20)
            .Select(g => new PublicWishItemDto(g.GuestName, g.Wishes!, g.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PublicInvitationDto(
            Id: invitation.Id,
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
            RecentWishes: recentWishes,
            TemplateId: invitation.TemplateId, Lang: invitation.Lang, ContentJson: invitation.ContentJson
        );
    }
}
