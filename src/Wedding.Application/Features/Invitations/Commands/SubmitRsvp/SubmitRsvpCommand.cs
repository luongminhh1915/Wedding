using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Invitations.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Invitations.Commands.SubmitRsvp;

public record SubmitRsvpCommand(
    string Slug,
    string GuestName,
    string? PhoneNumber,
    string Status,
    int CompanionCount = 0,
    string? Wishes = null,
    string? DietaryPreference = null
) : IRequest<RsvpResponseDto>;

public class SubmitRsvpCommandHandler : IRequestHandler<SubmitRsvpCommand, RsvpResponseDto>
{
    private readonly IApplicationDbContext _context;

    public SubmitRsvpCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RsvpResponseDto> Handle(SubmitRsvpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GuestName))
            throw new DomainException("Vui lòng nhập tên của bạn để cô dâu & chú rể ghi nhận.");

        var cleanSlug = request.Slug.Trim().ToLowerInvariant();
        var invitation = await _context.WeddingInvitations
            .FirstOrDefaultAsync(w => w.Slug == cleanSlug, cancellationToken)
            ?? throw new DomainException("Không tìm thấy thiệp cưới hợp lệ.");

        var rsvpStatus = request.Status?.ToLowerInvariant() == "attending" || request.Status?.ToLowerInvariant() == "yes"
            ? RSVPStatus.Attending
            : RSVPStatus.NotAttending;

        var guest = GuestRSVP.Create(
            invitationId: invitation.Id,
            guestName: request.GuestName,
            phoneNumber: request.PhoneNumber,
            status: rsvpStatus,
            companionCount: Math.Max(0, request.CompanionCount),
            wishes: request.Wishes,
            dietary: request.DietaryPreference
        );

        _context.GuestRSVPs.Add(guest);
        await _context.SaveChangesAsync(cancellationToken);

        var message = rsvpStatus == RSVPStatus.Attending
            ? "Cảm ơn bạn đã xác nhận tham dự! Chúc bạn có những phút giây thật tuyệt vời tại lễ cưới."
            : "Cảm ơn bạn đã phản hồi. Cô dâu & chú rể rất trân trọng lời chúc của bạn!";

        return new RsvpResponseDto(
            Success: true,
            Message: message,
            RsvpId: guest.Id
        );
    }
}
