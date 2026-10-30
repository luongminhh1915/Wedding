using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class CommissionPolicyRuleConfiguration : IEntityTypeConfiguration<CommissionPolicyRule>
{
    public void Configure(EntityTypeBuilder<CommissionPolicyRule> builder)
    {
        builder.ToTable("CommissionPolicyRules", table => table.HasCheckConstraint(
            "CK_CommissionPolicyRules_TargetScope",
            """
            ([Scope] = 'System' AND [CategoryId] IS NULL AND [VendorId] IS NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')
            OR ([Scope] = 'Category' AND [CategoryId] IS NOT NULL AND [VendorId] IS NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')
            OR ([Scope] = 'Vendor' AND [CategoryId] IS NULL AND [VendorId] IS NOT NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')
            OR ([Scope] = 'VendorCategory' AND [CategoryId] IS NOT NULL AND [VendorId] IS NOT NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')
            OR ([Scope] = 'Service' AND [CategoryId] IS NULL AND [VendorId] IS NULL AND [ServiceItemId] IS NOT NULL AND [ComboTemplateId] IS NULL AND [ComboTemplateRevisionId] IS NULL AND [Rate] IS NOT NULL AND [BasisKind] <> 'ComboComponents')
            OR ([Scope] = 'Combo' AND [CategoryId] IS NULL AND [VendorId] IS NULL AND [ServiceItemId] IS NULL AND [ComboTemplateId] IS NOT NULL AND [ComboTemplateRevisionId] IS NOT NULL AND [Rate] IS NULL AND [BasisKind] = 'ComboComponents')
            """));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.BasisKind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Rate).HasPrecision(5, 4);
        builder.Property(x => x.PolicyVersion).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(2000);
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Scope, x.CategoryId, x.VendorId, x.ServiceItemId, x.ComboTemplateId, x.ComboTemplateRevisionId });
        builder.HasIndex(x => new { x.EffectiveFrom, x.EffectiveTo });

        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ComboTemplate).WithMany().HasForeignKey(x => x.ComboTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ComboTemplateRevision).WithMany().HasForeignKey(x => x.ComboTemplateRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.Restrict);
    }
}
