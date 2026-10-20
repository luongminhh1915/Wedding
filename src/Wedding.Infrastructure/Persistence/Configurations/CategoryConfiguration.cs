using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Icon).HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.DefaultCommissionRate).HasPrecision(5, 4);
        builder.Property(c => c.RequiresCommissionBasisApproval).HasDefaultValue(false);
        builder.HasOne(category => category.BusinessGroup).WithMany(group => group.Categories)
            .HasForeignKey(category => category.BusinessGroupId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.Slug).IsUnique();

        // Preserve existing category IDs/slugs; business groups describe the broader shop scope.
        builder.HasData(
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                Name = "Trung tâm Tiệc cưới (Venues & Sảnh tiệc)",
                Slug = "tiem-cuoi-venues",
                Icon = "Building2",
                Description = "Sảnh tiệc khách sạn, nhà hàng tiệc cưới sang trọng",
                DefaultCommissionRate = 0.03m, // 3%
                BusinessGroupId = BusinessGroupPolicy.VenueBanquetId,
                RequiresCommissionBasisApproval = false,
                DisplayOrder = 1,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111102"),
                Name = "Trang trí Tiệc cưới (Decor Concept)",
                Slug = "trang-tri-decor",
                Icon = "Flower2",
                Description = "Thiết kế concept gia tiên, backdrop sảnh tiệc, hoa tươi",
                DefaultCommissionRate = 0.08m, // 8%
                BusinessGroupId = BusinessGroupPolicy.DecorInvitationsId,
                RequiresCommissionBasisApproval = false,
                DisplayOrder = 2,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111103"),
                Name = "Quay phim & Chụp ảnh (Photo / Video)",
                Slug = "quay-chup-photo-video",
                Icon = "Camera",
                Description = "Chụp ảnh pre-wedding, phóng sự cưới, quay phim ngày cưới",
                DefaultCommissionRate = 0.10m, // 10%
                BusinessGroupId = BusinessGroupPolicy.PhotoBridalMakeupId,
                RequiresCommissionBasisApproval = false,
                DisplayOrder = 3,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111104"),
                Name = "Váy cưới & Vest cưới (Bridal & Suits)",
                Slug = "vay-cuoi-vest",
                Icon = "Sparkles",
                Description = "Thuê và may đo váy cưới haute couture, vest chú rể cao cấp",
                DefaultCommissionRate = 0.10m, // 10%
                BusinessGroupId = BusinessGroupPolicy.PhotoBridalMakeupId,
                RequiresCommissionBasisApproval = false,
                DisplayOrder = 4,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111105"),
                Name = "Trang điểm Cô dâu (Bridal Makeup)",
                Slug = "trang-diem-makeup",
                Icon = "Smile",
                Description = "Makeup cô dâu ngày cưới, ăn hỏi và mẹ cô dâu chú rể",
                DefaultCommissionRate = 0.10m, // 10%
                BusinessGroupId = BusinessGroupPolicy.PhotoBridalMakeupId,
                RequiresCommissionBasisApproval = false,
                DisplayOrder = 5,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111106"),
                Name = "Thiệp cưới & Quà cảm ơn (Invitations & Gifts)",
                Slug = "thiep-cuoi-qua-tang",
                Icon = "Mail",
                Description = "In ấn thiệp cưới thiết kế riêng và quà tặng tri ân khách mời",
                DefaultCommissionRate = 0.08m, // 8%
                BusinessGroupId = BusinessGroupPolicy.DecorInvitationsId,
                RequiresCommissionBasisApproval = false,
                DisplayOrder = 6,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111107"),
                Name = "Tổ chức cưới trọn gói (A–Z) – Full-Service Wedding Planning",
                Slug = "wedding-planner",
                Icon = "CalendarHeart",
                Description = "Lên kế hoạch, quản lý và điều phối đám cưới. Hoa hồng 12% cần cơ sở phí tổ chức/quản lý được duyệt; không tính mặc định trên chi hộ hoặc toàn ngân sách cưới.",
                DefaultCommissionRate = 0.12m,
                BusinessGroupId = BusinessGroupPolicy.FullServicePlanningId,
                RequiresCommissionBasisApproval = true,
                DisplayOrder = 7,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111108"),
                Name = "Tiệc & Sự kiện khác",
                Slug = "tiec-su-kien-khac",
                Icon = "PartyPopper",
                Description = "Tiệc độc thân, đính hôn, kỷ niệm ngày cưới, tuần trăng mật và sự kiện liên quan ngoài lễ cưới chính",
                DefaultCommissionRate = .03m,
                BusinessGroupId = BusinessGroupPolicy.OtherEventsId,
                RequiresCommissionBasisApproval = false,
                DisplayOrder = 8,
                IsActive = true,
                CreatedAt = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
