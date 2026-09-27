using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.Content).HasMaxLength(3000).IsRequired();
        builder.Property(r => r.PhotosJson).HasMaxLength(4000);
        builder.Property(r => r.VendorReply).HasMaxLength(2000);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.IsVerifiedBuyer);

        builder.HasOne(r => r.BookingContract)
               .WithOne(c => c.Review)
               .HasForeignKey<Review>(r => r.BookingContractId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.Listing)
               .WithMany(l => l.Reviews)
               .HasForeignKey(r => r.ListingId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Customer)
               .WithMany(u => u.Reviews)
               .HasForeignKey(r => r.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Vendor)
               .WithMany(v => v.Reviews)
               .HasForeignKey(r => r.VendorId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
