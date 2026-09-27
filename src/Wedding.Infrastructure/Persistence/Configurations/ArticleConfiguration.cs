using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("Articles");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Slug).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Excerpt).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.Content).IsRequired();
        builder.Property(a => a.ThumbnailUrl).HasMaxLength(500);
        builder.Property(a => a.Category).HasMaxLength(100);
        builder.Property(a => a.TagsJson).HasMaxLength(1000);

        builder.HasIndex(a => a.Slug).IsUnique();
        builder.HasIndex(a => a.IsPublished);

        builder.HasOne(a => a.Author)
               .WithMany()
               .HasForeignKey(a => a.AuthorId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
