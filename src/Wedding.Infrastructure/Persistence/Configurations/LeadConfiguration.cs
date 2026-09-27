using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(l => l.Id);

        // BR-002: Smart Privacy
        builder.Property(l => l.RawPhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(l => l.MaskedPhoneNumber).HasMaxLength(30).IsRequired();

        builder.Property(l => l.EstimatedBudget).HasPrecision(18, 2);
        builder.Property(l => l.Notes).HasMaxLength(1000);
        builder.Property(l => l.CancellationReason).HasMaxLength(500);

        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(l => l.Status);
        builder.HasIndex(l => l.SlaDeadline);

        builder.HasOne(l => l.Customer)
               .WithMany(u => u.Leads)
               .HasForeignKey(l => l.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Vendor)
               .WithMany(v => v.Leads)
               .HasForeignKey(l => l.VendorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Listing)
               .WithMany(lst => lst.Leads)
               .HasForeignKey(l => l.ListingId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
