using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class BudgetItemConfiguration : IEntityTypeConfiguration<BudgetItem>
{
    public void Configure(EntityTypeBuilder<BudgetItem> builder)
    {
        builder.ToTable("BudgetItems");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.PlannedCost).HasPrecision(18, 2);
        builder.Property(b => b.ActualCost).HasPrecision(18, 2);
        builder.Property(b => b.Notes).HasMaxLength(500);

        builder.HasOne(b => b.Customer)
               .WithMany(u => u.BudgetItems)
               .HasForeignKey(b => b.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Category)
               .WithMany()
               .HasForeignKey(b => b.CategoryId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
