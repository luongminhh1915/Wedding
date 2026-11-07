using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class CommissionNegotiationRequestConfiguration : IEntityTypeConfiguration<CommissionNegotiationRequest>
{
    public void Configure(EntityTypeBuilder<CommissionNegotiationRequest> builder)
    {
        builder.ToTable("CommissionNegotiationRequests", table => table.HasCheckConstraint(
            "CK_CommissionNegotiationRequests_TargetScope",
            """
            ([Scope] = 'Vendor' AND [CategoryId] IS NULL AND [ServiceItemId] IS NULL)
            OR ([Scope] = 'VendorCategory' AND [CategoryId] IS NOT NULL AND [ServiceItemId] IS NULL)
            OR ([Scope] = 'Service' AND [ServiceItemId] IS NOT NULL)
            """));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReasonCategory).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.CurrentRate).HasPrecision(5, 4);
        builder.Property(x => x.ProposedRate).HasPrecision(5, 4);
        builder.Property(x => x.CounterRate).HasPrecision(5, 4);
        builder.Property(x => x.ApprovedRate).HasPrecision(5, 4);
        builder.Property(x => x.ReasonDetail).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CooperationPlan).HasMaxLength(2000);
        builder.Property(x => x.DecisionReason).HasMaxLength(2000);
        builder.Property(x => x.Version).IsConcurrencyToken();

        builder.HasIndex(x => x.VendorId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Scope, x.CategoryId, x.ServiceItemId });

        builder.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceItem).WithMany().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedModerator).WithMany().HasForeignKey(x => x.AssignedModeratorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResultingPolicyRule).WithMany().HasForeignKey(x => x.ResultingPolicyRuleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Messages).WithOne(x => x.NegotiationRequest).HasForeignKey(x => x.NegotiationRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommissionNegotiationMessageConfiguration : IEntityTypeConfiguration<CommissionNegotiationMessage>
{
    public void Configure(EntityTypeBuilder<CommissionNegotiationMessage> builder)
    {
        builder.ToTable("CommissionNegotiationMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SenderRole).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ProposedRateSnapshot).HasPrecision(5, 4);
        builder.Property(x => x.AttachmentUrl).HasMaxLength(500);

        builder.HasIndex(x => x.NegotiationRequestId);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
    }
}

