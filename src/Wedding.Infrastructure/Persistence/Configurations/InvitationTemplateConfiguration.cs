using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class InvitationTemplateConfiguration : IEntityTypeConfiguration<InvitationTemplate>
{
    public void Configure(EntityTypeBuilder<InvitationTemplate> b)
    {
        b.HasKey(x => x.Id); b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.Key).HasMaxLength(50); b.Property(x => x.Name).HasMaxLength(160);
        b.Property(x => x.PrimaryColor).HasMaxLength(16); b.Property(x => x.BackgroundColor).HasMaxLength(16);
        b.Property(x => x.Category).HasMaxLength(50); b.Property(x => x.Motif).HasMaxLength(20);
        b.HasData(InvitationTemplate.Catalog);
    }
}
