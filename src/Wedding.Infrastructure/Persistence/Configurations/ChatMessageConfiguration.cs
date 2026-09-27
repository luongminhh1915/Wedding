using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(m => m.QuoteAmount)
            .HasPrecision(18, 2);

        builder.Property(m => m.QuoteDescription)
            .HasMaxLength(500);

        builder.HasIndex(m => m.LeadId);
        builder.HasIndex(m => m.CreatedAt);

        builder.HasOne(m => m.Lead)
            .WithMany()
            .HasForeignKey(m => m.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
