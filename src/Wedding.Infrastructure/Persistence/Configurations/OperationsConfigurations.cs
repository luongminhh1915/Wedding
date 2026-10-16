using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;
public class VendorTaxProfileConfiguration : IEntityTypeConfiguration<VendorTaxProfile>
{
    public void Configure(EntityTypeBuilder<VendorTaxProfile> b)
    {
        b.HasIndex(x => x.VendorId).IsUnique(); b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LegalName).HasMaxLength(250); b.Property(x => x.TaxNumber).HasMaxLength(20);
        b.Property(x => x.BankCode).HasMaxLength(20); b.Property(x => x.BankAccountNumber).HasMaxLength(30); b.Property(x => x.BankAccountName).HasMaxLength(150);
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(x => x.Channel).HasMaxLength(20); b.Property(x => x.Destination).HasMaxLength(200); b.Property(x => x.Message).HasMaxLength(4000);
        b.Property(x => x.DeduplicationKey).HasMaxLength(200); b.HasIndex(x => new { x.UserId, x.DeduplicationKey }).IsUnique().HasFilter("[DeduplicationKey] IS NOT NULL");
        b.HasIndex(x => new { x.UserId, x.ReadAt }); b.HasIndex(x => new { x.Channel, x.SentAt, x.NextAttemptAt });
    }
}
public class ArticleListingPinConfiguration : IEntityTypeConfiguration<ArticleListingPin>
{
    public void Configure(EntityTypeBuilder<ArticleListingPin> b)
    {
        b.HasIndex(x => new { x.ArticleId, x.ListingId }).IsUnique();
        b.HasOne(x => x.Article).WithMany().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
    }
}
public class EmergencyMatchConfiguration : IEntityTypeConfiguration<EmergencyMatch>
{
    public void Configure(EntityTypeBuilder<EmergencyMatch> b) { b.Property(x => x.Reason).HasMaxLength(2000); b.HasIndex(x => x.ContractId); }
}
