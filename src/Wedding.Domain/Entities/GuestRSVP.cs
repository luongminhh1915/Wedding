using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class GuestRSVP : BaseEntity
{
    public Guid InvitationId { get; private set; }
    public string GuestName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public RSVPStatus Status { get; private set; } = RSVPStatus.Attending;
    public int CompanionCount { get; private set; } = 0; // Số người đi cùng
    public string? Wishes { get; private set; } // Lời chúc gửi dâu rể
    public string? DietaryPreference { get; private set; } // Ăn chay, kiêng...

    // Navigation Property
    public WeddingInvitation Invitation { get; private set; } = null!;

    private GuestRSVP() { } // Dành cho EF Core

    public static GuestRSVP Create(Guid invitationId, string guestName, string? phoneNumber, 
        RSVPStatus status, int companionCount = 0, string? wishes = null, string? dietary = null)
    {
        if (string.IsNullOrWhiteSpace(guestName))
            throw new DomainException("Tên khách mời không được để trống.");
        if (companionCount < 0)
            throw new DomainException("Số người đi cùng không hợp lệ.");

        return new GuestRSVP
        {
            Id = Guid.NewGuid(),
            InvitationId = invitationId,
            GuestName = guestName.Trim(),
            PhoneNumber = phoneNumber?.Trim(),
            Status = status,
            CompanionCount = companionCount,
            Wishes = wishes?.Trim(),
            DietaryPreference = dietary?.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
