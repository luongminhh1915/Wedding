using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class BusinessGroupConfiguration : IEntityTypeConfiguration<BusinessGroup>
{
    public void Configure(EntityTypeBuilder<BusinessGroup> builder)
    {
        builder.ToTable("BusinessGroups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Code).HasMaxLength(80).IsRequired();
        builder.Property(group => group.Name).HasMaxLength(150).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(1000).IsRequired();
        builder.Property(group => group.DefaultCommissionRate).HasPrecision(5, 4);
        builder.Property(group => group.PolicyVersion).HasMaxLength(50).IsRequired();
        builder.HasIndex(group => group.Code).IsUnique();

        var createdAt = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
        builder.HasData(
            new
            {
                Id = BusinessGroupPolicy.VenueBanquetId, Code = "venue-banquet", Name = "Địa điểm & Tiệc cưới",
                Description = "Địa điểm tổ chức, sảnh tiệc và dịch vụ tiệc cho lễ cưới chính.",
                DefaultCommissionRate = .03m, RequiresCommissionBasisApproval = false,
                PolicyVersion = BusinessGroupPolicy.InitialVersion, DisplayOrder = 1, IsActive = true, CreatedAt = createdAt
            },
            new
            {
                Id = BusinessGroupPolicy.DecorInvitationsId, Code = "decor-invitations", Name = "Trang trí & Thiệp cưới",
                Description = "Trang trí, thiết kế concept, thiệp cưới và quà cảm ơn.",
                DefaultCommissionRate = .08m, RequiresCommissionBasisApproval = false,
                PolicyVersion = BusinessGroupPolicy.InitialVersion, DisplayOrder = 2, IsActive = true, CreatedAt = createdAt
            },
            new
            {
                Id = BusinessGroupPolicy.PhotoBridalMakeupId, Code = "photo-bridal-makeup", Name = "Quay chụp, Váy cưới & Makeup",
                Description = "Quay phim, chụp ảnh, trang phục cưới và trang điểm.",
                DefaultCommissionRate = .10m, RequiresCommissionBasisApproval = false,
                PolicyVersion = BusinessGroupPolicy.InitialVersion, DisplayOrder = 3, IsActive = true, CreatedAt = createdAt
            },
            new
            {
                Id = BusinessGroupPolicy.FullServicePlanningId, Code = "full-service-planning", Name = "Tổ chức cưới trọn gói (A–Z)",
                Description = "Đơn vị chịu trách nhiệm lên kế hoạch, quản lý, phối hợp và điều hành đám cưới. Không đồng nghĩa với combo nhiều dịch vụ. Mức 12% cần cơ sở tính phí tổ chức/quản lý được phê duyệt, không mặc định tính trên ngân sách cưới hay khoản chi hộ.",
                DefaultCommissionRate = .12m, RequiresCommissionBasisApproval = true,
                PolicyVersion = BusinessGroupPolicy.InitialVersion, DisplayOrder = 4, IsActive = true, CreatedAt = createdAt
            },
            new
            {
                Id = BusinessGroupPolicy.OtherEventsId, Code = "other-events", Name = "Tiệc & Sự kiện khác",
                Description = "Tiệc độc thân, đính hôn, kỷ niệm ngày cưới, tuần trăng mật và sự kiện liên quan ngoài lễ cưới chính.",
                DefaultCommissionRate = .03m, RequiresCommissionBasisApproval = false,
                PolicyVersion = BusinessGroupPolicy.InitialVersion, DisplayOrder = 5, IsActive = true, CreatedAt = createdAt
            });
    }
}

public class VendorBusinessGroupConfiguration : IEntityTypeConfiguration<VendorBusinessGroup>
{
    public void Configure(EntityTypeBuilder<VendorBusinessGroup> builder)
    {
        builder.ToTable("VendorBusinessGroups");
        builder.HasKey(membership => new { membership.VendorId, membership.BusinessGroupId });
        builder.HasOne(membership => membership.Vendor).WithMany(vendor => vendor.BusinessGroups)
            .HasForeignKey(membership => membership.VendorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(membership => membership.BusinessGroup).WithMany(group => group.Vendors)
            .HasForeignKey(membership => membership.BusinessGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
