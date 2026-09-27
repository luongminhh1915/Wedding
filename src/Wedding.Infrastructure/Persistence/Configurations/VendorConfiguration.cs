using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.BrandName).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Slug).HasMaxLength(200).IsRequired();
        builder.Property(v => v.ContactPerson).HasMaxLength(150).IsRequired();
        builder.Property(v => v.Hotline).HasMaxLength(20).IsRequired();
        builder.Property(v => v.Address).HasMaxLength(300);
        builder.Property(v => v.City).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Bio).HasMaxLength(2000);
        builder.Property(v => v.LogoUrl).HasMaxLength(500);
        builder.Property(v => v.CoverImageUrl).HasMaxLength(500);

        builder.Property(v => v.CommissionRate).HasPrecision(5, 4); // VD: 0.0800 = 8%
        builder.Property(v => v.RatingAvg).HasPrecision(3, 2);      // VD: 4.85

        builder.HasIndex(v => v.Slug).IsUnique();
        builder.HasIndex(v => v.City);
    }
}
