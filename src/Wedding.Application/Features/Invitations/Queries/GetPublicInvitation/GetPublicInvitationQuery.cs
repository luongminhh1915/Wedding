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
            .Include(w => w.Guests)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Slug == cleanSlug, cancellationToken)
            ?? throw new DomainException("Thiệp cưới không tồn tại hoặc đã bị ngừng chia sẻ.");

        var recentWishes = invitation.Guests
            .Where(g => !string.IsNullOrWhiteSpace(g.Wishes))
            .OrderByDescending(g => g.CreatedAt)
            .Take(20)
            .Select(g => new PublicWishItemDto(
                GuestName: g.GuestName,
                Wishes: g.Wishes!,
                CreatedAt: g.CreatedAt
            ))
            .ToList();

        return new PublicInvitationDto(
            Id: invitation.Id,
            Slug: invitation.Slug,
            GroomName: invitation.GroomName,
            BrideName: invitation.BrideName,
            EventDate: invitation.EventDate,
            VenueName: invitation.VenueName,
            VenueAddress: invitation.VenueAddress,
            MapUrl: invitation.MapUrl,
            LoveStory: invitation.LoveStory,
            CoverImageUrl: invitation.CoverImageUrl,
            MusicUrl: invitation.MusicUrl,
            TemplateStyle: invitation.TemplateStyle,
            BankInfo: invitation.BankInfo,
            RecentWishes: recentWishes
        );
    }
}
