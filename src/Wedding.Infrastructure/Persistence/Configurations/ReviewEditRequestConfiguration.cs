using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ReviewEditRequestConfiguration : IEntityTypeConfiguration<ReviewEditRequest>
{
    public void Configure(EntityTypeBuilder<ReviewEditRequest> builder)
    {
        builder.ToTable("ReviewEditRequests");
        builder.HasKey(edit => edit.Id);
        builder.Property(edit => edit.BeforeContent).HasMaxLength(3000).IsRequired();
        builder.Property(edit => edit.BeforePhotosJson).HasMaxLength(4000);
        builder.Property(edit => edit.AfterContent).HasMaxLength(3000).IsRequired();
        builder.Property(edit => edit.AfterPhotosJson).HasMaxLength(4000);
        builder.Property(edit => edit.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(edit => edit.RejectionReason).HasMaxLength(1000);
        builder.HasIndex(edit => new { edit.ReviewId, edit.AttemptNumber }).IsUnique();
        builder.HasIndex(edit => new { edit.ReviewId, edit.Status }).IsUnique()
            .HasFilter("[Status] IN ('Pending', 'Approved')");
        builder.HasIndex(edit => new { edit.Status, edit.RequestedAt });
        builder.HasOne(edit => edit.Review).WithMany(review => review.EditRequests)
            .HasForeignKey(edit => edit.ReviewId).OnDelete(DeleteBehavior.Restrict);
    }
}
