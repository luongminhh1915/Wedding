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

        builder.HasIndex(c => c.Slug).IsUnique();

        // Seed 7 ngành hàng dịch vụ cưới chuẩn theo FM-001 PRD
        builder.HasData(
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111101"),
                Name = "Trung tâm Tiệc cưới (Venues & Sảnh tiệc)",
                Slug = "tiem-cuoi-venues",
                Icon = "Building2",
                Description = "Sảnh tiệc khách sạn, nhà hàng tiệc cưới sang trọng",
                DefaultCommissionRate = 0.03m, // 3%
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
                DisplayOrder = 6,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111107"),
                Name = "Wedding Planner trọn gói (Planning)",
                Slug = "wedding-planner",
                Icon = "CalendarHeart",
                Description = "Lên kế hoạch, điều phối trọn gói toàn bộ đám cưới",
                DefaultCommissionRate = 0.10m, // 10%
                DisplayOrder = 7,
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}
