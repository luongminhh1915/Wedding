using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ListingMediaConfiguration : IEntityTypeConfiguration<ListingMedia>
{
    public void Configure(EntityTypeBuilder<ListingMedia> builder)
    {
        builder.ToTable("ListingMedias");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.MediaUrl).HasMaxLength(500).IsRequired();
        builder.Property(m => m.ThumbnailUrl).HasMaxLength(500);
        builder.Property(m => m.MediaType).HasMaxLength(20).IsRequired();

        builder.HasOne(m => m.Listing)
               .WithMany(l => l.Media)
               .HasForeignKey(m => m.ListingId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
