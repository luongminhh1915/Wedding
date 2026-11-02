using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", table => table.HasCheckConstraint("CK_Users_SocialEmail",
            "[Email] IS NOT NULL OR ([Role] = 'Customer' AND ([GoogleSubject] IS NOT NULL OR [FacebookSubject] IS NOT NULL)) OR ([Role] = 'VendorOwner' AND [LoginName] IS NOT NULL AND [LoginName] <> '')"));
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasField("_email").UsePropertyAccessMode(PropertyAccessMode.Field).HasMaxLength(150).IsRequired(false);
        builder.Property(u => u.LoginName).HasMaxLength(80);
        builder.HasIndex(u => u.LoginName).IsUnique().HasFilter("[LoginName] IS NOT NULL");
        builder.Ignore(u => u.NotificationRecipient);
        builder.Property(u => u.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.SecurityVersion).HasDefaultValue(0).IsConcurrencyToken();
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.GoogleSubject).HasMaxLength(255);
        builder.HasIndex(u => u.GoogleSubject).IsUnique().HasFilter("[GoogleSubject] IS NOT NULL");
        builder.Property(u => u.FacebookSubject).HasMaxLength(255);
        builder.HasIndex(u => u.FacebookSubject).IsUnique().HasFilter("[FacebookSubject] IS NOT NULL");
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        builder.HasIndex(u => u.PhoneNumber);

        // BR-001: Quan hệ 1-1 với Vendor
        builder.HasOne(u => u.Vendor)
               .WithOne(v => v.User)
               .HasForeignKey<Vendor>(v => v.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
