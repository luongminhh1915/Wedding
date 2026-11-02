using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class AppointmentAvailabilityExceptionConfiguration : IEntityTypeConfiguration<AppointmentAvailabilityException>
{
    public void Configure(EntityTypeBuilder<AppointmentAvailabilityException> b)
    {
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.AppointmentTypeId, x.Date }).IsUnique();
        b.HasOne<AppointmentType>().WithMany().HasForeignKey(x => x.AppointmentTypeId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Reason).HasMaxLength(500);
    }
}
public class AppointmentEventConfiguration : IEntityTypeConfiguration<AppointmentEvent>
{
    public void Configure(EntityTypeBuilder<AppointmentEvent> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => new { x.AppointmentId, x.CreatedAt });
        b.HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.Action).HasMaxLength(60); b.Property(x => x.Note).HasMaxLength(2000);
    }
}
public class AppointmentConcurrencyConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        b.Property(x => x.RefundDue).HasPrecision(18, 0);
        b.Property(x => x.RefundReference).HasMaxLength(200);
        b.HasIndex(x => new { x.CustomerId, x.Status });
    }
}
public class ServiceItemConfiguration : IEntityTypeConfiguration<ServiceItem>
{
    public void Configure(EntityTypeBuilder<ServiceItem> b)
    {
        b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(250); b.Property(x => x.Unit).HasMaxLength(20);
        b.Property(x => x.BasePrice).HasPrecision(18, 0); b.Property(x => x.Regions).HasMaxLength(1000);
        b.Property(x => x.CancellationPolicy).HasMaxLength(4000); b.Property(x => x.ModerationStatus).HasMaxLength(30);
        b.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Variants).WithOne().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.AddOns).WithOne().HasForeignKey(x => x.ServiceItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.VendorId, x.ModerationStatus });
    }
}
public class ItemVariantConfiguration : IEntityTypeConfiguration<ItemVariant>
{
    public void Configure(EntityTypeBuilder<ItemVariant> b) { b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.PriceDelta).HasPrecision(18, 0); }
}
public class ItemAddOnConfiguration : IEntityTypeConfiguration<ItemAddOn>
{
    public void Configure(EntityTypeBuilder<ItemAddOn> b) { b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(200); b.Property(x => x.Price).HasPrecision(18, 0); }
}
public class BundleDiscountRuleConfiguration : IEntityTypeConfiguration<BundleDiscountRule>
{
    public void Configure(EntityTypeBuilder<BundleDiscountRule> b) { b.HasKey(x => x.Id); b.HasIndex(x => x.MinItems).IsUnique(); b.Property(x => x.Percent).HasPrecision(8, 5); }
}
public class ComboEnhancementsConfiguration : IEntityTypeConfiguration<ComboOrderItem>
{
    public void Configure(EntityTypeBuilder<ComboOrderItem> b)
    {
        b.Property(x => x.SelectionJson); b.Property(x => x.CancellationPolicySnapshot).HasMaxLength(4000);
        b.Property(x => x.Unit).HasMaxLength(20); b.HasIndex(x => x.LeadId);
    }
}
