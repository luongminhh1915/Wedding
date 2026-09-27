using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ChecklistTaskConfiguration : IEntityTypeConfiguration<ChecklistTask>
{
    public void Configure(EntityTypeBuilder<ChecklistTask> builder)
    {
        builder.ToTable("ChecklistTasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).HasMaxLength(250).IsRequired();
        builder.Property(t => t.Milestone).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasIndex(t => t.IsCompleted);

        builder.HasOne(t => t.Customer)
               .WithMany(u => u.ChecklistTasks)
               .HasForeignKey(t => t.CustomerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
