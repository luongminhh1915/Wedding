using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.ActorRole).HasMaxLength(30);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.CreatedAt);
    }
}

public class AdvancePaymentConfiguration : IEntityTypeConfiguration<AdvancePayment>
{
    public void Configure(EntityTypeBuilder<AdvancePayment> b)
    {
        b.ToTable("AdvancePayments");
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasMaxLength(30);
        b.Property(x => x.ContractCode).HasMaxLength(50);
        b.HasIndex(x => new { x.ContractId, x.Status });
    }
}

public class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> b)
    {
        b.ToTable("OtpCodes");
        b.Property(x => x.Target).HasMaxLength(150).IsRequired();
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Attempts).IsConcurrencyToken();
        b.Property(x => x.IsUsed).IsConcurrencyToken();
        b.HasIndex(x => new { x.Target, x.CreatedAt });
    }
}

public class WeddingProfileConfiguration : IEntityTypeConfiguration<WeddingProfile>
{
    public void Configure(EntityTypeBuilder<WeddingProfile> b)
    {
        b.ToTable("WeddingProfiles");
        b.Property(x => x.TotalBudget).HasPrecision(18, 2);
        b.Property(x => x.Style).HasMaxLength(50);
        b.HasIndex(x => x.CustomerId).IsUnique();
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppointmentTypeConfiguration : IEntityTypeConfiguration<AppointmentType>
{
    public void Configure(EntityTypeBuilder<AppointmentType> b)
    {
        b.ToTable("AppointmentTypes");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.WorkingDays).HasMaxLength(20);
        b.Property(x => x.DepositAmount).HasPrecision(18, 2);
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("Appointments");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.DepositAmount).HasPrecision(18, 2);
        b.HasIndex(x => new { x.AppointmentTypeId, x.StartAt });
        b.HasOne(x => x.AppointmentType).WithMany().HasForeignKey(x => x.AppointmentTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ComboTemplateConfiguration : IEntityTypeConfiguration<ComboTemplate>
{
    public void Configure(EntityTypeBuilder<ComboTemplate> b)
    {
        b.ToTable("ComboTemplates");
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Tier).HasMaxLength(20);
        b.Property(x => x.Style).HasMaxLength(50);
        b.HasMany(x => x.Items).WithOne(i => i.ComboTemplate).HasForeignKey(i => i.ComboTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ComboTemplateItemConfiguration : IEntityTypeConfiguration<ComboTemplateItem>
{
    public void Configure(EntityTypeBuilder<ComboTemplateItem> b)
    {
        b.ToTable("ComboTemplateItems");
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ComboOrderConfiguration : IEntityTypeConfiguration<ComboOrder>
{
    public void Configure(EntityTypeBuilder<ComboOrder> b)
    {
        b.ToTable("ComboOrders");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.DiscountPercent).HasPrecision(5, 4);
        b.Property(x => x.Total).HasPrecision(18, 2);
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne(i => i.ComboOrder).HasForeignKey(i => i.ComboOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ComboOrderItemConfiguration : IEntityTypeConfiguration<ComboOrderItem>
{
    public void Configure(EntityTypeBuilder<ComboOrderItem> b)
    {
        b.ToTable("ComboOrderItems");
        b.Property(x => x.Title).HasMaxLength(250);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.DiscountShare).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => x.LeadId);
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> b)
    {
        b.ToTable("Quotes");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.DepositAmount).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasIndex(x => new { x.LeadId, x.Status });
        b.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CommissionSettlementConfiguration : IEntityTypeConfiguration<CommissionSettlement>
{
    public void Configure(EntityTypeBuilder<CommissionSettlement> b)
    {
        b.ToTable("CommissionSettlements");
        b.Property(x => x.Period).HasMaxLength(7).IsRequired();
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.PaidAmount).HasPrecision(18, 2);
        b.Property(x => x.PaymentReference).HasMaxLength(40);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.VendorId, x.Period }).IsUnique();
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VendorVerificationConfiguration : IEntityTypeConfiguration<VendorVerification>
{
    public void Configure(EntityTypeBuilder<VendorVerification> b)
    {
        b.ToTable("VendorVerifications");
        b.Property(x => x.BusinessLicenseNumber).HasMaxLength(50);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> b)
    {
        b.ToTable("Disputes");
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.Property(x => x.OpenedByRole).HasMaxLength(30);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict);
    }
}
