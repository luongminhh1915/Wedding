using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class WeddingInvitationConfiguration : IEntityTypeConfiguration<WeddingInvitation>
{
    public void Configure(EntityTypeBuilder<WeddingInvitation> builder)
    {
        builder.ToTable("WeddingInvitations");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Slug).HasMaxLength(150).IsRequired();
        builder.HasIndex(w => w.Slug).IsUnique();

        builder.Property(w => w.GroomName).HasMaxLength(100).IsRequired();
        builder.Property(w => w.BrideName).HasMaxLength(100).IsRequired();
        builder.Property(w => w.VenueName).HasMaxLength(200).IsRequired();
        builder.Property(w => w.VenueAddress).HasMaxLength(300).IsRequired();
        builder.Property(w => w.MapUrl).HasMaxLength(500);
        builder.Property(w => w.CoverImageUrl).HasMaxLength(500);
        builder.Property(w => w.MusicUrl).HasMaxLength(500);
        builder.Property(w => w.TemplateStyle).HasMaxLength(50).IsRequired();
        builder.Property(w => w.LoveStory).HasMaxLength(3000);

        builder.HasOne(w => w.Customer)
               .WithMany(u => u.WeddingInvitations)
               .HasForeignKey(w => w.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
