using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ListingRevisionConfiguration : IEntityTypeConfiguration<ListingRevision>
{
    public void Configure(EntityTypeBuilder<ListingRevision> builder)
    {
        builder.ToTable("ListingRevisions"); builder.HasKey(x => x.Id);
        builder.Property(x => x.ContentJson).HasMaxLength(160000).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ListingId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.SubmittedAt });
        builder.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId).OnDelete(DeleteBehavior.Restrict);
    }
}
