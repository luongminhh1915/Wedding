using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers");
        builder.HasKey(v => v.Id);

        // BR-004: Mã 8 ký tự độc nhất
        builder.Property(v => v.Code).HasMaxLength(16).IsRequired();
        builder.HasIndex(v => v.Code).IsUnique();

        builder.Property(v => v.DiscountValue).HasPrecision(18, 2);
        builder.Property(v => v.DiscountPercent).HasPrecision(5, 2);

        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasOne(v => v.Lead)
               .WithOne(l => l.Voucher)
               .HasForeignKey<Voucher>(v => v.LeadId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Customer)
               .WithMany()
               .HasForeignKey(v => v.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
