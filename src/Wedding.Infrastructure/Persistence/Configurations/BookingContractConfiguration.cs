using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class BookingContractConfiguration : IEntityTypeConfiguration<BookingContract>
{
    public void Configure(EntityTypeBuilder<BookingContract> builder)
    {
        builder.ToTable("BookingContracts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ContractCode).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.ContractCode).IsUnique();

        builder.Property(c => c.ContractValue).HasPrecision(18, 2);
        builder.Property(c => c.DepositAmount).HasPrecision(18, 2);
        builder.Property(c => c.ContractImageUrl).HasMaxLength(500);
        builder.Property(c => c.CancellationReason).HasMaxLength(500);

        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.VerificationDeadline);

        builder.HasOne(c => c.Lead)
               .WithOne(l => l.BookingContract)
               .HasForeignKey<BookingContract>(c => c.LeadId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Voucher)
               .WithOne(v => v.BookingContract)
               .HasForeignKey<BookingContract>(c => c.VoucherId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Vendor)
               .WithMany(v => v.Contracts)
               .HasForeignKey(c => c.VendorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Customer)
               .WithMany(u => u.Contracts)
               .HasForeignKey(c => c.CustomerId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
