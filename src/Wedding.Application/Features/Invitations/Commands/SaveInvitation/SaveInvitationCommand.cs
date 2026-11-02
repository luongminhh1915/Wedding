using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Invitations.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;
using System.Text.Json;

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
    string? BankInfo,
    Guid? TemplateId = null, string Lang = "vi", string? ContentJson = null, bool IsPublished = true
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

    public Task<WeddingInvitationDto> Handle(SaveInvitationCommand request, CancellationToken cancellationToken) => _context.ExecuteSerializableAsync(ct => Save(request, ct), cancellationToken);
    private async Task<WeddingInvitationDto> Save(SaveInvitationCommand request, CancellationToken cancellationToken)
    {
        var actorId = _currentUserService.UserId ?? throw new DomainException("Bạn cần đăng nhập.");
        if (!await _context.Users.AnyAsync(user => user.Id == actorId && user.IsActive && user.Role == UserRole.Customer, cancellationToken))
            throw new DomainException("Chỉ tài khoản khách hàng đang hoạt động được chỉnh sửa thiệp cưới.");
        var scope = await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken, write: true);
        var userId = scope.OwnerId;
        if (string.IsNullOrWhiteSpace(request.GroomName) || request.GroomName.Length > 100 || string.IsNullOrWhiteSpace(request.BrideName) || request.BrideName.Length > 100)
            throw new DomainException("Tên hai bạn bắt buộc, tối đa 100 ký tự.");
        if (string.IsNullOrWhiteSpace(request.VenueName) || request.VenueName.Length > 200 || request.VenueAddress.Length > 300)
            throw new DomainException("Địa điểm tổ chức bắt buộc, tên tối đa 200 và địa chỉ 300 ký tự.");
        if (request.Lang is not ("vi" or "en")) throw new DomainException("Ngôn ngữ không được hỗ trợ.");
        foreach (var url in new[] { request.MapUrl })
            if (!string.IsNullOrWhiteSpace(url) && (url.Length > 500 || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
                throw new DomainException("Liên kết bản đồ phải là URL HTTP/HTTPS hợp lệ.");
        var musicUrl = InvitationMusicUrls.Normalize(request.MusicUrl, userId);
        if (!InvitationImageUrls.IsValid(request.CoverImageUrl, userId)) throw new DomainException("Ảnh bìa cần là ảnh đã tải lên hồ sơ này hoặc URL ảnh cũ hợp lệ.");
        if (request.LoveStory?.Length > 3000 || request.BankInfo?.Length > 300 || request.ContentJson?.Length > 30000)
            throw new DomainException("Nội dung vượt quá giới hạn cho phép.");
        var contentJson = request.ContentJson ?? "{}";
        try
        {
            using var content = JsonDocument.Parse(contentJson);
            if (content.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
            if (content.RootElement.TryGetProperty("album", out var album))
            {
                if (album.ValueKind != JsonValueKind.Array || album.GetArrayLength() > 12 || album.EnumerateArray().Any(image => image.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(image.GetString()) || !InvitationImageUrls.IsValid(image.GetString(), userId)))
                    throw new DomainException("Album tối đa 12 ảnh đã tải lên cùng hồ sơ hoặc URL ảnh cũ hợp lệ.");
            }
        }
        catch (JsonException) { throw new DomainException("Cấu hình nội dung thiệp không hợp lệ."); }
        var template = InvitationTemplate.Catalog.FirstOrDefault(t => request.TemplateId.HasValue ? t.Id == request.TemplateId : t.Key == request.TemplateStyle)
            ?? throw new DomainException("Mẫu thiệp không hợp lệ.");

        var invitation = await _context.WeddingInvitations
            .Include(w => w.Guests)
            .FirstOrDefaultAsync(w => w.CustomerId == userId, cancellationToken);

        // Chuẩn hóa Slug
        var rawSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug($"{request.GroomName}-{request.BrideName}-{request.EventDate.Year}")
            : GenerateSlug(request.Slug);
        if (rawSlug.Length > 120 || new[] { "api", "planner", "invitation", "thiep", "login", "register", "browse", "vendors", "combo", "wiki", "lich-hen", "customer", "vendor", "admin", "setup", "assets", "auth", "premium", "wedding-match" }.Contains(rawSlug))
            throw new DomainException("Đường dẫn thiệp không hợp lệ hoặc đã được hệ thống sử dụng.");

        // Kiểm tra Slug có bị trùng với thiệp của cặp đôi khác không
        var isSlugTaken = await _context.WeddingInvitations
            .AnyAsync(w => w.Slug == rawSlug && (invitation == null || w.Id != invitation.Id), cancellationToken);

        if (isSlugTaken)
        {
            throw new DomainException("Đường dẫn thiệp đã được sử dụng, vui lòng chọn tên khác.");
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
                musicUrl: musicUrl,
                templateStyle: string.IsNullOrWhiteSpace(request.TemplateStyle) ? "rustic" : request.TemplateStyle,
                bankInfo: request.BankInfo
            );
        }

        invitation.Update(request.GroomName, request.BrideName, request.EventDate, request.VenueName, request.VenueAddress,
            request.MapUrl, request.LoveStory, request.CoverImageUrl, musicUrl, template.Key, request.BankInfo);
        invitation.Configure(rawSlug, template.Id, request.Lang, contentJson, request.IsPublished);

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
