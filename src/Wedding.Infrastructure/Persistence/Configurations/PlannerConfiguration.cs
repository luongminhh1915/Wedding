using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class WeddingMemberConfiguration : IEntityTypeConfiguration<WeddingMember>
{
    public void Configure(EntityTypeBuilder<WeddingMember> b)
    { b.HasKey(x => x.Id); b.HasIndex(x => x.UserId).IsUnique(); b.Property(x => x.Role).HasMaxLength(20); b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); b.HasOne<WeddingProfile>().WithMany().HasForeignKey(x => x.WeddingProfileId).OnDelete(DeleteBehavior.Restrict); }
}
public class WeddingJoinInvitationConfiguration : IEntityTypeConfiguration<WeddingJoinInvitation>
{
    public void Configure(EntityTypeBuilder<WeddingJoinInvitation> b)
    { b.HasKey(x => x.Id); b.HasIndex(x => x.Code).IsUnique(); b.Property(x => x.Code).HasMaxLength(48); b.Property(x => x.Role).HasMaxLength(20); b.Property(x => x.Email).HasMaxLength(256); b.HasOne<WeddingProfile>().WithMany().HasForeignKey(x => x.WeddingProfileId).OnDelete(DeleteBehavior.Restrict); }
}
public class WeddingGuestConfiguration : IEntityTypeConfiguration<WeddingGuest>
{
    public void Configure(EntityTypeBuilder<WeddingGuest> b)
    { b.HasKey(x => x.Id); b.HasIndex(x => new { x.WeddingProfileId, x.Name }); b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.Phone).HasMaxLength(30); b.Property(x => x.Group).HasMaxLength(100); b.Property(x => x.Side).HasMaxLength(20); b.Property(x => x.Status).HasMaxLength(20); b.Property(x => x.Dietary).HasMaxLength(500); b.Property(x => x.Notes).HasMaxLength(2000); b.HasOne<WeddingProfile>().WithMany().HasForeignKey(x => x.WeddingProfileId).OnDelete(DeleteBehavior.Restrict); b.HasOne<SeatTable>().WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.Restrict); }
}
public class SeatTableConfiguration : IEntityTypeConfiguration<SeatTable>
{
    public void Configure(EntityTypeBuilder<SeatTable> b)
    { b.HasKey(x => x.Id); b.Property(x => x.Name).HasMaxLength(100); b.HasIndex(x => x.WeddingProfileId); b.HasOne<WeddingProfile>().WithMany().HasForeignKey(x => x.WeddingProfileId).OnDelete(DeleteBehavior.Restrict); }
}
public class GuestSubmissionConfiguration : IEntityTypeConfiguration<GuestRSVP>
{
    public void Configure(EntityTypeBuilder<GuestRSVP> b)
    { b.Property(x => x.SubmissionKey).HasMaxLength(36); b.Property(x => x.Type).HasMaxLength(20); b.HasIndex(x => new { x.InvitationId, x.SubmissionKey }).IsUnique().HasFilter("[SubmissionKey] IS NOT NULL"); }
}
