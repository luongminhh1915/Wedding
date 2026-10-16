using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Persistence;

/// <summary>
/// Dữ liệu demo cho hành trình Weddy (chỉ chạy khi Database:SeedDemoData = true, idempotent):
/// loại lịch hẹn cho từng NCC, 3 combo mẫu, tài khoản CSKH, bài viết cẩm nang.
/// </summary>
public static class WeddyJourneySeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, IPasswordHasher hasher)
    {
        if (!await context.Users.AnyAsync(u => u.Email == "care@wedding.com"))
            context.Users.Add(User.Create("Chăm sóc khách hàng", "care@wedding.com", "0909000111", hasher.Hash("123456"), UserRole.CustomerCare));

        var vendors = await context.Vendors.Include(v => v.Listings).ThenInclude(l => l.Category).ToListAsync();
        if (!await context.AppointmentTypes.AnyAsync())
        {
            foreach (var v in vendors)
            {
                var cat = v.Listings.FirstOrDefault()?.Category?.Name ?? "";
                var (name, desc, minutes, deposit) = cat.Contains("tiệc", StringComparison.OrdinalIgnoreCase) || cat.Contains("Venue", StringComparison.OrdinalIgnoreCase)
                    ? ("Tham quan sảnh & thử món (tasting)", "Tham quan sảnh, thử thực đơn cho 2–4 người.", 90, 500000m)
                    : cat.Contains("chụp", StringComparison.OrdinalIgnoreCase) || cat.Contains("Studio", StringComparison.OrdinalIgnoreCase) || cat.Contains("Photo", StringComparison.OrdinalIgnoreCase)
                        ? ("Tư vấn concept & xem album", "Gặp photographer, chọn concept và xem album mẫu.", 60, 0m)
                        : ("Tư vấn trực tiếp tại showroom", "Gặp tư vấn viên, xem mẫu thực tế.", 60, 0m);
                context.AppointmentTypes.Add(AppointmentType.Create(v.Id, name, desc, minutes, 2, "1,2,3,4,5,6,0",
                    new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), deposit, true));
                context.AppointmentTypes.Add(AppointmentType.Create(v.Id, "Gọi video tư vấn nhanh", "Gọi video 30 phút qua Zalo/Meet.", 30, 1,
                    "1,2,3,4,5", new TimeSpan(19, 0, 0), new TimeSpan(21, 0, 0), 0m, false));
            }
        }

        // Thêm biến thể giá (tiết kiệm / cao cấp) cho mỗi hạng mục để combo builder có lựa chọn thật
        if (!await context.ComboTemplates.AnyAsync() && await context.Listings.CountAsync(l => l.Status == ListingStatus.Active) < 8)
        {
            var baseListings = vendors.SelectMany(v => v.Listings).Where(l => l.Status == ListingStatus.Active).ToList();
            var images = await context.ListingMedias.ToListAsync();
            foreach (var l in baseListings)
            {
                foreach (var (suffix, factor, slug) in new[] { ("Gói Tiết kiệm", 0.6m, "tiet-kiem"), ("Gói Cao cấp", 1.8m, "cao-cap") })
                {
                    var variant = Listing.Create(l.VendorId, l.CategoryId, $"{l.Title} – {suffix}", $"{l.Slug}-{slug}",
                        Math.Round(l.MinPrice * factor / 100_000m) * 100_000m, Math.Round(l.MaxPrice * factor / 100_000m) * 100_000m, l.Description, l.Location);
                    variant.Approve();
                    context.Listings.Add(variant);
                    var img = images.FirstOrDefault(m => m.ListingId == l.Id);
                    if (img != null) context.ListingMedias.Add(ListingMedia.Create(variant.Id, img.MediaUrl, img.ThumbnailUrl));
                }
            }
            await context.SaveChangesAsync();
            vendors = await context.Vendors.Include(v => v.Listings).ThenInclude(l => l.Category).ToListAsync();
        }

        if (!await context.ComboTemplates.AnyAsync())
        {
            var listings = vendors.SelectMany(v => v.Listings).Where(l => l.Status == ListingStatus.Active).ToList();
            var byCat = listings.GroupBy(l => l.CategoryId).Select(g => g.OrderBy(l => l.MinPrice).ToList()).ToList();
            var tiers = new[]
            {
                ("Combo Tiết kiệm", "Saving", "Minimal", "Đủ hạng mục thiết yếu, tối ưu chi phí cho đám cưới ấm cúng.", 0),
                ("Combo Phổ biến", "Popular", "Classic", "Lựa chọn được nhiều cặp đôi đặt nhất – cân bằng chất lượng và giá.", 1),
                ("Combo Sang trọng", "Luxury", "Luxury", "Dịch vụ cao cấp nhất trên Wedding Match cho ngày trọng đại.", 2)
            };
            foreach (var (name, tier, style, desc, idx) in tiers)
            {
                var t = ComboTemplate.Create(name, tier, style, desc, null, idx);
                context.ComboTemplates.Add(t);
                foreach (var cat in byCat)
                {
                    var pick = idx == 0 ? cat.First() : idx == 2 ? cat.Last() : cat[cat.Count / 2];
                    context.ComboTemplateItems.Add(ComboTemplateItem.Create(t.Id, pick.CategoryId, pick.Id, false));
                }
            }
        }

        var author = await context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.SuperAdmin || u.Role == UserRole.Moderator);
        if (author != null && !await context.Articles.AnyAsync())
        {
            var a1 = Article.Create(author.Id, "Lộ trình 12 tháng chuẩn bị đám cưới", "lo-trinh-12-thang-chuan-bi-dam-cuoi",
                "Checklist từng mốc thời gian giúp cặp đôi không bỏ sót việc gì.",
                "Từ 12 tháng trước: chốt ngân sách, ngày cưới. 9 tháng: đặt nhà hàng, studio. 6 tháng: váy cưới, makeup. 3 tháng: danh sách khách mời. 2 tháng: gửi thiệp online. 1 tháng: xác nhận RSVP và sơ đồ bàn.",
                "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=800&q=70");
            a1.Publish();
            var a2 = Article.Create(author.Id, "Cách chia ngân sách cưới hợp lý", "cach-chia-ngan-sach-cuoi",
                "Tỷ lệ gợi ý: tiệc 45%, chụp ảnh 10%, trang phục 8%, dự phòng 8%...",
                "Hãy dành khoảng 45% cho tiệc cưới, 10% chụp ảnh, 10% nhẫn, 10% trang trí, phần còn lại cho trang phục, makeup, thiệp, xe hoa và quỹ dự phòng.",
                "https://images.unsplash.com/photo-1511285560929-80b456fea0bc?auto=format&fit=crop&w=800&q=70");
            a2.Publish();
            context.Articles.AddRange(a1, a2);
        }

        await context.SaveChangesAsync();
    }
}
