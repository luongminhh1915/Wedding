using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class Article : BaseEntity
{
    public Guid AuthorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Excerpt { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string? ThumbnailUrl { get; private set; }
    public string Category { get; private set; } = "Cẩm nang cưới";
    public string? TagsJson { get; private set; }
    public bool IsPublished { get; private set; } = false;
    public DateTime? PublishedAt { get; private set; }
    public int ViewCount { get; private set; } = 0;

    // Navigation Property
    public User Author { get; private set; } = null!;

    private Article() { } // Dành cho EF Core

    public static Article Create(Guid authorId, string title, string slug, string excerpt, 
        string content, string? thumbnailUrl, string category = "Cẩm nang cưới", string? tagsJson = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Tiêu đề bài viết không được để trống.");
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Nội dung bài viết không được để trống.");

        return new Article
        {
            Id = Guid.NewGuid(),
            AuthorId = authorId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Excerpt = excerpt.Trim(),
            Content = content.Trim(),
            ThumbnailUrl = thumbnailUrl,
            Category = category,
            TagsJson = tagsJson,
            IsPublished = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Publish()
    {
        IsPublished = true;
        PublishedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Unpublish()
    {
        IsPublished = false;
        SetUpdated();
    }

    public void IncrementViewCount()
    {
        ViewCount++;
    }
}
