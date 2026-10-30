using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;
public class PrivateServiceAgreementConfiguration : IEntityTypeConfiguration<PrivateServiceAgreement>
{
    public void Configure(EntityTypeBuilder<PrivateServiceAgreement> b)
    {
        b.ToTable("PrivateServiceAgreements"); b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasMaxLength(20).IsRequired(); b.Property(x => x.Status).HasMaxLength(30).IsRequired();
        b.Property(x => x.Status).IsConcurrencyToken();
        b.Property(x => x.OriginalVersion).HasMaxLength(64).IsRequired(); b.Property(x => x.OriginalTotal).HasPrecision(18, 0);
        b.Property(x => x.ItemsJson).HasMaxLength(40000).IsRequired();
        b.Property(x => x.Note).HasMaxLength(2000); b.Property(x => x.Terms).HasMaxLength(4000); b.Property(x => x.Response).HasMaxLength(2000);
        b.HasIndex(x => new { x.Kind, x.TargetId, x.Sequence }).IsUnique();
        b.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.NoAction);
    }
}
