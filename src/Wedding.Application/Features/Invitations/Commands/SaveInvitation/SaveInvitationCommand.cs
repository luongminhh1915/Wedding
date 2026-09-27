using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Invitations.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Invitations.Commands.SaveInvitation;

public record SaveInvitationCommand(
    string? Slug,
    string GroomName,
    string BrideName,
    DateTime EventDate,
    string VenueName,
    string VenueAddress,
    string? MapUrl,
    string? LoveStory,
    string? CoverImageUrl,
    string? MusicUrl,
    string TemplateStyle,
    string? BankInfo
) : IRequest<WeddingInvitationDto>;

public class SaveInvitationCommandHandler : IRequestHandler<SaveInvitationCommand, WeddingInvitationDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SaveInvitationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<WeddingInvitationDto> Handle(SaveInvitationCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thiết kế và lưu thiệp cưới.");

        var invitation = await _context.WeddingInvitations
            .Include(w => w.Guests)
            .FirstOrDefaultAsync(w => w.CustomerId == userId, cancellationToken);

        // Chuẩn hóa Slug
        var rawSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug($"{request.GroomName}-{request.BrideName}-{request.EventDate.Year}")
            : GenerateSlug(request.Slug);

        // Kiểm tra Slug có bị trùng với thiệp của cặp đôi khác không
        var isSlugTaken = await _context.WeddingInvitations
            .AnyAsync(w => w.Slug == rawSlug && (invitation == null || w.Id != invitation.Id), cancellationToken);

        if (isSlugTaken)
        {
            rawSlug = $"{rawSlug}-{Guid.NewGuid().ToString("N")[..4].ToLowerInvariant()}";
        }

        if (invitation == null)
        {
            invitation = WeddingInvitation.Create(
                customerId: userId,
                slug: rawSlug,
                groomName: request.GroomName,
                brideName: request.BrideName,
                eventDate: request.EventDate,
                venueName: request.VenueName,
                venueAddress: request.VenueAddress,
                mapUrl: request.MapUrl,
                coverImageUrl: request.CoverImageUrl,
                templateStyle: string.IsNullOrWhiteSpace(request.TemplateStyle) ? "rustic" : request.TemplateStyle,
                bankInfo: request.BankInfo,
                loveStory: request.LoveStory
            );

            _context.WeddingInvitations.Add(invitation);
        }
        else
        {
            invitation.Update(
                groomName: request.GroomName,
                brideName: request.BrideName,
                eventDate: request.EventDate,
                venueName: request.VenueName,
                venueAddress: request.VenueAddress,
                mapUrl: request.MapUrl,
                loveStory: request.LoveStory,
                coverImageUrl: request.CoverImageUrl,
                musicUrl: request.MusicUrl,
                templateStyle: string.IsNullOrWhiteSpace(request.TemplateStyle) ? "rustic" : request.TemplateStyle,
                bankInfo: request.BankInfo
            );
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Tính toán thống kê RSVP
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
            EventDate: invitation.EventDate,
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
            EstimatedTables: Math.Max(estimatedTables, 1)
        );
    }

    private static string GenerateSlug(string text)
    {
        var s = text.Trim().ToLowerInvariant();
        // Bỏ dấu tiếng Việt
        string[] signs = ["aAeEoOuUiIdDyY", "áàạảãâấầậẩẫăắằặẳẵ", "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ", "éèẹẻẽêếềệểễ", "ÉÈẸẺẼÊẾỀỆỂỄ", "óòọỏõôốồộổỗơớờợởỡ", "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ", "úùụủũưứừựửữ", "ÚÙỤỦŨƯỨỪỰỬỮ", "íìịỉĩ", "ÍÌỊỈĨ", "đ", "Đ", "ýỳỵỷỹ", "ÝỲỴỶỸ"];
        for (int i = 1; i < signs.Length; i++)
        {
            for (int j = 0; j < signs[i].Length; j++)
            {
                s = s.Replace(signs[i][j], signs[0][i - 1]);
            }
        }
        s = Regex.Replace(s, @"[^a-z0-9\s-]", "");
        s = Regex.Replace(s, @"\s+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(s) ? "wedding-invitation" : s;
    }
}
