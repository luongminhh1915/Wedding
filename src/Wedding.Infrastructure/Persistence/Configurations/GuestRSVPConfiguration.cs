using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class GuestRSVPConfiguration : IEntityTypeConfiguration<GuestRSVP>
{
    public void Configure(EntityTypeBuilder<GuestRSVP> builder)
    {
        builder.ToTable("GuestRSVPs");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.GuestName).HasMaxLength(150).IsRequired();
        builder.Property(g => g.PhoneNumber).HasMaxLength(30);
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(g => g.Wishes).HasMaxLength(1000);
        builder.Property(g => g.DietaryPreference).HasMaxLength(200);

        builder.HasIndex(g => g.Status);

        builder.HasOne(g => g.Invitation)
               .WithMany(w => w.Guests)
               .HasForeignKey(g => g.InvitationId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
