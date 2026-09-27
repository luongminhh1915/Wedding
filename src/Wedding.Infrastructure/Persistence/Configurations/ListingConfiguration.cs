using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("Listings");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Title).HasMaxLength(250).IsRequired();
        builder.Property(l => l.Slug).HasMaxLength(250).IsRequired();
        builder.Property(l => l.Location).HasMaxLength(200).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(4000).IsRequired();

        builder.Property(l => l.MinPrice).HasPrecision(18, 2);
        builder.Property(l => l.MaxPrice).HasPrecision(18, 2);

        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(l => l.RejectionReason).HasMaxLength(1000);

        builder.HasIndex(l => l.Slug);
        builder.HasIndex(l => l.Status);

        builder.HasOne(l => l.Vendor)
               .WithMany(v => v.Listings)
               .HasForeignKey(l => l.VendorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Category)
               .WithMany(c => c.Listings)
               .HasForeignKey(l => l.CategoryId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
