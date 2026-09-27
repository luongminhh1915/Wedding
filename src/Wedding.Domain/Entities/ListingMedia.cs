using Wedding.Domain.Common;

namespace Wedding.Domain.Entities;

public class ListingMedia : BaseEntity
{
    public Guid ListingId { get; private set; }
    public string MediaUrl { get; private set; } = string.Empty;
    public string? ThumbnailUrl { get; private set; }
    public string MediaType { get; private set; } = "image"; // "image" hoặc "video"
    public int DisplayOrder { get; private set; } = 0;
    public bool IsFeatured { get; private set; } = false;

    // Navigation Property
    public Listing Listing { get; private set; } = null!;

    private ListingMedia() { } // Dành cho EF Core

    public static ListingMedia Create(Guid listingId, string mediaUrl, string? thumbnailUrl, 
        string mediaType = "image", int displayOrder = 0, bool isFeatured = false)
    {
        return new ListingMedia
        {
            Id = Guid.NewGuid(),
            ListingId = listingId,
            MediaUrl = mediaUrl,
            ThumbnailUrl = thumbnailUrl,
            MediaType = mediaType,
            DisplayOrder = displayOrder,
            IsFeatured = isFeatured,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetFeatured(bool isFeatured)
    {
        IsFeatured = isFeatured;
        SetUpdated();
    }
}
