namespace Wedding.Application.Features.Invitations.DTOs;

public record WeddingInvitationDto(
    Guid Id,
    Guid CustomerId,
    string Slug,
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
    bool IsPublished,
    DateTime CreatedAt,
    int TotalGuests,
    int AttendingCount,
    int NotAttendingCount,
    int TotalAccompanying,
    int EstimatedTables
);

public record GuestRsvpDto(
    Guid Id,
    Guid InvitationId,
    string GuestName,
    string? PhoneNumber,
    string Status,
    int CompanionCount,
    string? Wishes,
    string? DietaryPreference,
    DateTime CreatedAt
);

public record PublicInvitationDto(
    Guid Id,
    string Slug,
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
    List<PublicWishItemDto> RecentWishes
);

public record PublicWishItemDto(
    string GuestName,
    string Wishes,
    DateTime CreatedAt
);

public record RsvpResponseDto(
    bool Success,
    string Message,
    Guid? RsvpId
);
