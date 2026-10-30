using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class CommissionComponentBasisApprovalConfiguration : IEntityTypeConfiguration<CommissionComponentBasisApproval>
{
    public void Configure(EntityTypeBuilder<CommissionComponentBasisApproval> builder)
    {
        builder.ToTable("CommissionComponentBasisApprovals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BasisKind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(5, 4).IsRequired();
        builder.Property(x => x.BasisDefinition).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ExcludedCostsJson).HasMaxLength(12000).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => new { x.ComboTemplateRevisionId, x.ServiceItemId, x.Sequence }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasOne(x => x.ComboTemplateRevision).WithMany().HasForeignKey(x => x.ComboTemplateRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.Restrict);
    }
}
