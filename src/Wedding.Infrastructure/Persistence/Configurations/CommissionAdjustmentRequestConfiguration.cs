using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class CommissionAdjustmentRequestConfiguration : IEntityTypeConfiguration<CommissionAdjustmentRequest>
{
    public void Configure(EntityTypeBuilder<CommissionAdjustmentRequest> builder)
    {
        builder.ToTable("CommissionAdjustmentRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OriginalRateSnapshot).HasPrecision(5, 4).IsRequired();
        builder.Property(x => x.OriginalAmountSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.OutstandingAmountSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ProposedRate).HasPrecision(5, 4).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.DecisionReason).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.CommissionId).IsUnique().HasFilter("[Status] = 'Submitted'");
        builder.HasIndex(x => x.Status);
        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Commission).WithMany().HasForeignKey(x => x.CommissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);
    }
}
