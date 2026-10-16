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
    string? DietaryPreference = null,
    string Type = "Attend", string? SubmissionKey = null
) : IRequest<RsvpResponseDto>;

public class SubmitRsvpCommandHandler : IRequestHandler<SubmitRsvpCommand, RsvpResponseDto>
{
    private readonly IApplicationDbContext _context;

    public SubmitRsvpCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public Task<RsvpResponseDto> Handle(SubmitRsvpCommand request, CancellationToken cancellationToken) => _context.ExecuteSerializableAsync(ct => Submit(request, ct), cancellationToken);
    private async Task<RsvpResponseDto> Submit(SubmitRsvpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GuestName))
            throw new DomainException("Vui lòng nhập tên của bạn để cô dâu & chú rể ghi nhận.");
        if (request.GuestName.Length > 100 || request.PhoneNumber?.Length > 30 || request.Wishes?.Length > 2000 || request.DietaryPreference?.Length > 500)
            throw new DomainException("Tên, số điện thoại hoặc lời chúc quá dài.");
        if (request.CompanionCount is < 0 or > 19) throw new DomainException("Số người đi cùng tối đa 19.");
        if (request.Type is not ("Attend" or "Wish")) throw new DomainException("Loại phản hồi không hợp lệ.");
        if (request.SubmissionKey != null && !Guid.TryParse(request.SubmissionKey, out _)) throw new DomainException("Mã gửi phản hồi không hợp lệ.");

        var cleanSlug = request.Slug.Trim().ToLowerInvariant();
        var invitation = await _context.WeddingInvitations
            .FirstOrDefaultAsync(w => w.Slug == cleanSlug && w.IsPublished, cancellationToken)
            ?? throw new DomainException("Không tìm thấy thiệp cưới hợp lệ.");

        var rsvpStatus = request.Status?.ToLowerInvariant() switch {
            "attending" or "yes" => RSVPStatus.Attending,
            "notattending" or "no" => RSVPStatus.NotAttending,
            "tentative" or "maybe" => RSVPStatus.Tentative,
            _ => throw new DomainException("Trạng thái tham dự không hợp lệ.")
        };
        if (request.SubmissionKey != null && await _context.GuestRSVPs.AnyAsync(g => g.InvitationId == invitation.Id && g.SubmissionKey == request.SubmissionKey, cancellationToken))
            return new RsvpResponseDto(true, "Phản hồi này đã được ghi nhận. Cảm ơn bạn!", null);

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
        guest.Update(request.GuestName, rsvpStatus, request.CompanionCount, request.Wishes, request.DietaryPreference, request.Type);
        guest.SetSubmissionKey(request.SubmissionKey);
        var profile = await _context.WeddingProfiles.FirstOrDefaultAsync(p => p.CustomerId == invitation.CustomerId, cancellationToken);
        if (profile != null && request.Type == "Attend") {
            var plannerGuest = WeddingGuest.Create(profile.Id);
            plannerGuest.Update(request.GuestName, request.PhoneNumber, "RSVP online", "Shared", 1 + request.CompanionCount,
                rsvpStatus == RSVPStatus.Tentative ? "Maybe" : rsvpStatus.ToString(), request.DietaryPreference, request.Wishes);
            plannerGuest.LinkRsvp(guest.Id); _context.WeddingGuests.Add(plannerGuest);
        }
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
