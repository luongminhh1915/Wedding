using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", table => table.HasCheckConstraint("CK_Reviews_TargetReferences",
            "([TargetType] = 'Service' AND [ListingId] IS NOT NULL AND [ComboTemplateId] IS NULL AND [ComboOrderId] IS NULL) OR " +
            "([TargetType] = 'Shop' AND [ListingId] IS NULL AND [ComboTemplateId] IS NULL AND " +
            "(([BookingContractId] IS NOT NULL AND [ComboOrderId] IS NULL) OR ([BookingContractId] IS NULL AND [ComboOrderId] IS NOT NULL))) OR " +
            "([TargetType] = 'Combo' AND [ListingId] IS NULL AND [BookingContractId] IS NULL AND [ComboTemplateId] IS NOT NULL AND [ComboOrderId] IS NOT NULL)"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TargetType).HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(ReviewTargetType.Service).IsRequired();
        builder.Property(r => r.Rating).IsRequired();
        builder.Ignore(r => r.AverageRating);
        builder.HasIndex(r => new { r.BookingContractId, r.ListingId }).IsUnique()
            .HasFilter("[TargetType] = 'Service' AND [BookingContractId] IS NOT NULL");
        builder.HasIndex(r => new { r.CustomerId, r.ListingId }).IsUnique()
            .HasFilter("[TargetType] = 'Service' AND [BookingContractId] IS NULL");
        builder.HasIndex(r => new { r.BookingContractId, r.VendorId }).IsUnique()
            .HasFilter("[TargetType] = 'Shop' AND [BookingContractId] IS NOT NULL");
        builder.HasIndex(r => new { r.ComboOrderId, r.VendorId }).IsUnique()
            .HasFilter("[TargetType] = 'Shop' AND [ComboOrderId] IS NOT NULL");
        builder.HasIndex(r => new { r.ComboOrderId, r.ComboTemplateId }).IsUnique()
            .HasFilter("[TargetType] = 'Combo' AND [ComboOrderId] IS NOT NULL");
        builder.HasIndex(r => new { r.TargetType, r.ListingId, r.Status, r.CreatedAt });
        builder.HasIndex(r => new { r.TargetType, r.VendorId, r.Status, r.CreatedAt });
        builder.HasIndex(r => new { r.TargetType, r.ComboTemplateId, r.Status, r.CreatedAt });
        builder.Property(r => r.Content).HasMaxLength(3000).IsRequired();
        builder.Property(r => r.PhotosJson).HasMaxLength(4000);
        builder.Property(r => r.VendorReply).HasMaxLength(2000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.IsVerifiedBuyer);
        builder.HasOne(r => r.BookingContract).WithMany(c => c.Reviews)
            .HasForeignKey(r => r.BookingContractId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(r => r.ComboOrder).WithMany().HasForeignKey(r => r.ComboOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ComboTemplate).WithMany().HasForeignKey(r => r.ComboTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Listing).WithMany(l => l.Reviews)
            .HasForeignKey(r => r.ListingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Customer).WithMany(u => u.Reviews)
            .HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Vendor).WithMany(v => v.Reviews)
            .HasForeignKey(r => r.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}
