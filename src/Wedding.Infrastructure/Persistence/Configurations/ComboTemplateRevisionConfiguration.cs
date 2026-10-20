using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ComboTemplateRevisionConfiguration : IEntityTypeConfiguration<ComboTemplateRevision>
{
    public void Configure(EntityTypeBuilder<ComboTemplateRevision> builder)
    {
        builder.ToTable("ComboTemplateRevisions");
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.SnapshotJson).HasMaxLength(2000000).IsRequired();
        builder.Property(revision => revision.Status).HasMaxLength(30).IsRequired();
        builder.Property(revision => revision.CreatedByRole).HasMaxLength(30).IsRequired();
        builder.Property(revision => revision.ReviewedByRole).HasMaxLength(30);
        builder.Property(revision => revision.ReviewNote).HasMaxLength(2000);
        builder.Property(revision => revision.SupersededFromStatus).HasMaxLength(30);
        builder.HasIndex(revision => new { revision.ComboTemplateId, revision.Sequence }).IsUnique();
        builder.HasOne(revision => revision.ComboTemplate).WithMany()
            .HasForeignKey(revision => revision.ComboTemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
