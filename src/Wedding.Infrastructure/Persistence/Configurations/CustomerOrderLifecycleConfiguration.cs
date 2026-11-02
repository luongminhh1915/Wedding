using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class CustomerOrderStateConfiguration : IEntityTypeConfiguration<CustomerOrderState>
{
    public void Configure(EntityTypeBuilder<CustomerOrderState> b)
    {
        b.ToTable("CustomerOrderStates"); b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        b.Property(x => x.RevisionReason).HasMaxLength(2000); b.Property(x => x.WithdrawalReason).HasMaxLength(2000);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.CustomerId, x.Kind, x.EntityId }).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class OrderChangeRequestConfiguration : IEntityTypeConfiguration<OrderChangeRequest>
{
    public void Configure(EntityTypeBuilder<OrderChangeRequest> b)
    {
        b.ToTable("OrderChangeRequests"); b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(20).IsRequired(); b.Property(x => x.Type).HasMaxLength(20).IsRequired(); b.Property(x => x.Status).HasMaxLength(30).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(2000).IsRequired(); b.Property(x => x.ProposedChanges).HasMaxLength(4000);
        b.Property(x => x.VendorResponse).HasMaxLength(2000); b.Property(x => x.FinanceResponse).HasMaxLength(2000);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => new { x.CustomerId, x.Kind, x.EntityId }); b.HasIndex(x => new { x.VendorId, x.Status });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}
