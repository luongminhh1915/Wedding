using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class CommissionConfiguration : IEntityTypeConfiguration<Commission>
{
    public void Configure(EntityTypeBuilder<Commission> builder)
    {
        builder.ToTable("Commissions");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Period).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.Property(c => c.CommissionRate).HasPrecision(5, 4);
        builder.Property(c => c.CommissionAmount).HasPrecision(18, 2);

        builder.Property(c => c.PaymentReferenceCode).HasMaxLength(100);
        builder.Property(c => c.VietQrPayload).HasMaxLength(2000);

        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.DueDate);

        builder.HasOne(c => c.Contract)
               .WithMany(ct => ct.Commissions)
               .HasForeignKey(c => c.ContractId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Vendor)
               .WithMany(v => v.Commissions)
               .HasForeignKey(c => c.VendorId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
