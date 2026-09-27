using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class WeddingInvitation : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public string Slug { get; private set; } = string.Empty; // Định danh đường dẫn thiệp /invitation/{slug}
    public string GroomName { get; private set; } = string.Empty;
    public string BrideName { get; private set; } = string.Empty;
    public DateTime EventDate { get; private set; }
    public string VenueName { get; private set; } = string.Empty;
    public string VenueAddress { get; private set; } = string.Empty;
    public string? MapUrl { get; private set; } // Link Google Maps sảnh tiệc
    public string? LoveStory { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string? MusicUrl { get; private set; }
    public string TemplateStyle { get; private set; } = "ClassicRose";
    public bool IsPublished { get; private set; } = true;

    // Navigation Properties
    public User Customer { get; private set; } = null!;
    public ICollection<GuestRSVP> Guests { get; private set; } = new List<GuestRSVP>();

    private WeddingInvitation() { } // Dành cho EF Core

    public static WeddingInvitation Create(Guid customerId, string slug, string groomName, 
        string brideName, DateTime eventDate, string venueName, string venueAddress, 
        string? mapUrl, string? coverImageUrl, string templateStyle = "ClassicRose")
    {
        if (string.IsNullOrWhiteSpace(groomName) || string.IsNullOrWhiteSpace(brideName))
            throw new DomainException("Tên cô dâu và chú rể là bắt buộc.");
        if (string.IsNullOrWhiteSpace(venueName))
            throw new DomainException("Tên địa điểm sảnh tiệc là bắt buộc.");

        return new WeddingInvitation
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            Slug = slug.Trim().ToLowerInvariant(),
            GroomName = groomName.Trim(),
            BrideName = brideName.Trim(),
            EventDate = eventDate,
            VenueName = venueName.Trim(),
            VenueAddress = venueAddress.Trim(),
            MapUrl = mapUrl,
            CoverImageUrl = coverImageUrl,
            TemplateStyle = templateStyle,
            IsPublished = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string groomName, string brideName, DateTime eventDate, string venueName, 
        string venueAddress, string? mapUrl, string? loveStory, string? coverImageUrl, string? musicUrl, string templateStyle)
    {
        GroomName = groomName.Trim();
        BrideName = brideName.Trim();
        EventDate = eventDate;
        VenueName = venueName.Trim();
        VenueAddress = venueAddress.Trim();
        MapUrl = mapUrl;
        LoveStory = loveStory;
        CoverImageUrl = coverImageUrl;
        MusicUrl = musicUrl;
        TemplateStyle = templateStyle;
        SetUpdated();
    }
}
