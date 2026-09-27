using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetService<ILogger<ApplicationDbContext>>();

        try
        {
            // Đảm bảo Database đã được tạo hoặc migrate
            await context.Database.MigrateAsync();

            var defaultPasswordHash = hasher.Hash("123456");

            // 1. Seed Users (Mật khẩu chuẩn: 123456)
            var customerUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "customer@wedding.com");
            if (customerUser == null)
            {
                customerUser = User.Create(
                    fullName: "Nguyễn Văn An",
                    email: "customer@wedding.com",
                    phoneNumber: "0901234567",
                    passwordHash: defaultPasswordHash,
                    role: UserRole.Customer
                );
                customerUser.UpdateProfile("Nguyễn Văn An & Trần Thị Bình", "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=300&q=80");
                context.Users.Add(customerUser);
            }
            else
            {
                customerUser.UpdatePassword(defaultPasswordHash);
            }

            var vendor1User = await context.Users.FirstOrDefaultAsync(u => u.Email == "vendor.studio@wedding.com");
            if (vendor1User == null)
            {
                vendor1User = User.Create(
                    fullName: "Mai Wedding Studio",
                    email: "vendor.studio@wedding.com",
                    phoneNumber: "0902345678",
                    passwordHash: defaultPasswordHash,
                    role: UserRole.VendorOwner
                );
                context.Users.Add(vendor1User);
            }
            else
            {
                vendor1User.UpdatePassword(defaultPasswordHash);
            }

            var vendor2User = await context.Users.FirstOrDefaultAsync(u => u.Email == "vendor.palace@wedding.com");
            if (vendor2User == null)
            {
                vendor2User = User.Create(
                    fullName: "White Palace Convention",
                    email: "vendor.palace@wedding.com",
                    phoneNumber: "0903456789",
                    passwordHash: defaultPasswordHash,
                    role: UserRole.VendorOwner
                );
                context.Users.Add(vendor2User);
            }
            else
            {
                vendor2User.UpdatePassword(defaultPasswordHash);
            }

            var vendor3User = await context.Users.FirstOrDefaultAsync(u => u.Email == "vendor.decor@wedding.com");
            if (vendor3User == null)
            {
                vendor3User = User.Create(
                    fullName: "Dream Wedding Decor",
                    email: "vendor.decor@wedding.com",
                    phoneNumber: "0904567890",
                    passwordHash: defaultPasswordHash,
                    role: UserRole.VendorOwner
                );
                context.Users.Add(vendor3User);
            }
            else
            {
                vendor3User.UpdatePassword(defaultPasswordHash);
            }

            var moderatorUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "moderator@wedding.com");
            if (moderatorUser == null)
            {
                moderatorUser = User.Create(
                    fullName: "Ban Kiểm Duyệt (Moderator)",
                    email: "moderator@wedding.com",
                    phoneNumber: "0905678901",
                    passwordHash: defaultPasswordHash,
                    role: UserRole.Moderator
                );
                context.Users.Add(moderatorUser);
            }
            else
            {
                moderatorUser.UpdatePassword(defaultPasswordHash);
            }

            var financeUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "finance@wedding.com");
            if (financeUser == null)
            {
                financeUser = User.Create(
                    fullName: "Bộ Phận Tài Chính (Finance)",
                    email: "finance@wedding.com",
                    phoneNumber: "0906789012",
                    passwordHash: defaultPasswordHash,
                    role: UserRole.Finance
                );
                context.Users.Add(financeUser);
            }
            else
            {
                financeUser.UpdatePassword(defaultPasswordHash);
            }

            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@wedding.com");
            if (adminUser == null)
            {
                adminUser = User.Create(
                    fullName: "Quản Trị Viên Hệ Thống (SuperAdmin)",
                    email: "admin@wedding.com",
                    phoneNumber: "0909999999",
                    passwordHash: defaultPasswordHash,
                    role: UserRole.SuperAdmin
                );
                context.Users.Add(adminUser);
            }
            else
            {
                adminUser.UpdatePassword(defaultPasswordHash);
            }

            await context.SaveChangesAsync();

            // 2. Seed Vendors
            var vendor1 = await context.Vendors.FirstOrDefaultAsync(v => v.UserId == vendor1User.Id);
            if (vendor1 == null)
            {
                vendor1 = Vendor.Create(
                    userId: vendor1User.Id,
                    brandName: "Mai Wedding Studio & Bridal",
                    slug: "mai-wedding-studio",
                    contactPerson: "Nguyễn Mai",
                    hotline: "0902345678",
                    city: "Hồ Chí Minh",
                    commissionRate: 0.10m
                );
                vendor1.UpdateProfile(
                    brandName: "Mai Wedding Studio & Bridal",
                    contactPerson: "Nguyễn Mai",
                    hotline: "0902345678",
                    city: "Hồ Chí Minh",
                    address: "128 Hồ Văn Huê, Phường 9, Quận Phú Nhuận, TP. HCM",
                    bio: "Studio chụp ảnh cưới phóng sự và áo cưới cao cấp phong cách Hàn Quốc, Vintage tinh tế.",
                    logoUrl: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=300&q=80",
                    coverImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80"
                );
                vendor1.SetVerified(true);
                context.Vendors.Add(vendor1);
            }

            var vendor2 = await context.Vendors.FirstOrDefaultAsync(v => v.UserId == vendor2User.Id);
            if (vendor2 == null)
            {
                vendor2 = Vendor.Create(
                    userId: vendor2User.Id,
                    brandName: "White Palace Convention Center",
                    slug: "white-palace-convention",
                    contactPerson: "Phan Đình Trọng",
                    hotline: "0903456789",
                    city: "Hồ Chí Minh",
                    commissionRate: 0.05m
                );
                vendor2.UpdateProfile(
                    brandName: "White Palace Convention Center",
                    contactPerson: "Phan Đình Trọng",
                    hotline: "0903456789",
                    city: "Hồ Chí Minh",
                    address: "194 Hoàng Văn Thụ, Phường 9, Quận Phú Nhuận, TP. HCM",
                    bio: "Trung tâm hội nghị tiệc cưới 5 sao mang kiến trúc tân cổ điển lộng lẫy và ẩm thực chuẩn quốc tế.",
                    logoUrl: "https://images.unsplash.com/photo-1560179707-f14e90ef3623?auto=format&fit=crop&w=300&q=80",
                    coverImageUrl: "https://images.unsplash.com/photo-1519167758481-83f550bb49b3?auto=format&fit=crop&w=1200&q=80"
                );
                vendor2.SetVerified(true);
                context.Vendors.Add(vendor2);
            }

            var vendor3 = await context.Vendors.FirstOrDefaultAsync(v => v.UserId == vendor3User.Id);
            if (vendor3 == null)
            {
                vendor3 = Vendor.Create(
                    userId: vendor3User.Id,
                    brandName: "Dream Wedding Concept & Decor",
                    slug: "dream-wedding-decor",
                    contactPerson: "Hoàng Yến",
                    hotline: "0904567890",
                    city: "Hồ Chí Minh",
                    commissionRate: 0.08m
                );
                vendor3.UpdateProfile(
                    brandName: "Dream Wedding Concept & Decor",
                    contactPerson: "Hoàng Yến",
                    hotline: "0904567890",
                    city: "Hồ Chí Minh",
                    address: "45 Lê Lợi, Phường Bến Nghé, Quận 1, TP. HCM",
                    bio: "Chuyên thiết kế trang trí tiệc cưới hoa tươi cao cấp, phong cách Rustic, Bohemian và Minimalist.",
                    logoUrl: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=300&q=80",
                    coverImageUrl: "https://images.unsplash.com/photo-1465495976277-4387d4b0b4c6?auto=format&fit=crop&w=1200&q=80"
                );
                vendor3.SetVerified(true);
                context.Vendors.Add(vendor3);
            }

            await context.SaveChangesAsync();

            // Category Ids
            var venueCatId = Guid.Parse("11111111-1111-1111-1111-111111111101");
            var decorCatId = Guid.Parse("11111111-1111-1111-1111-111111111102");
            var photoCatId = Guid.Parse("11111111-1111-1111-1111-111111111103");
            var dressCatId = Guid.Parse("11111111-1111-1111-1111-111111111104");

            // 3. Seed Listings
            var listing1 = await context.Listings.FirstOrDefaultAsync(l => l.Slug == "goi-chup-anh-cuoi-pre-wedding-da-lat");
            if (listing1 == null)
            {
                listing1 = Listing.Create(
                    vendorId: vendor1.Id,
                    categoryId: photoCatId,
                    title: "Gói Chụp Ảnh Cưới Pre-Wedding Đà Lạt & Phim Phóng Sự Cưới",
                    slug: "goi-chup-anh-cuoi-pre-wedding-da-lat",
                    minPrice: 15000000m,
                    maxPrice: 26000000m,
                    description: "Trọn gói chụp ảnh ngoại cảnh 2 ngày 1 đêm tại Đà Lạt bao gồm 03 váy cưới cao cấp, 02 vest chú rể, ekip makeup riêng và tặng kèm 01 video flycam cinematic.",
                    location: "TP. Hồ Chí Minh & Đà Lạt"
                );
                listing1.Approve();
                context.Listings.Add(listing1);
                await context.SaveChangesAsync();

                context.ListingMedias.AddRange(
                    ListingMedia.Create(listing1.Id, "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80", null, "image", 0, true),
                    ListingMedia.Create(listing1.Id, "https://images.unsplash.com/photo-1583939003579-730e3918a45a?auto=format&fit=crop&w=800&q=80", null, "image", 1, false),
                    ListingMedia.Create(listing1.Id, "https://images.unsplash.com/photo-1511285560929-80b456fea0bc?auto=format&fit=crop&w=800&q=80", null, "image", 2, false)
                );
            }

            var listing2 = await context.Listings.FirstOrDefaultAsync(l => l.Slug == "sanh-tiec-cuoi-grand-crystal-ballroom");
            if (listing2 == null)
            {
                listing2 = Listing.Create(
                    vendorId: vendor2.Id,
                    categoryId: venueCatId,
                    title: "Sảnh Tiệc Cưới Hoàng Gia Grand Crystal Ballroom",
                    slug: "sanh-tiec-cuoi-grand-crystal-ballroom",
                    minPrice: 85000000m,
                    maxPrice: 220000000m,
                    description: "Không gian sảnh tiệc trần cao 9m không cột, sức chứa lên đến 800 khách. Hệ thống màn hình LED cong 4K và âm thanh ánh sáng hòa nhạc sống động.",
                    location: "Quận Phú Nhuận, TP. Hồ Chí Minh"
                );
                listing2.Approve();
                context.Listings.Add(listing2);
                await context.SaveChangesAsync();

                context.ListingMedias.AddRange(
                    ListingMedia.Create(listing2.Id, "https://images.unsplash.com/photo-1519167758481-83f550bb49b3?auto=format&fit=crop&w=1200&q=80", null, "image", 0, true),
                    ListingMedia.Create(listing2.Id, "https://images.unsplash.com/photo-1464366400600-7168b8af9bc3?auto=format&fit=crop&w=800&q=80", null, "image", 1, false)
                );
            }

            var listing3 = await context.Listings.FirstOrDefaultAsync(l => l.Slug == "goi-trang-tri-hoa-tuoi-rustic-garden");
            if (listing3 == null)
            {
                listing3 = Listing.Create(
                    vendorId: vendor3.Id,
                    categoryId: decorCatId,
                    title: "Gói Trang Trí Hoa Tươi Rustic Garden Concept",
                    slug: "goi-trang-tri-hoa-tuoi-rustic-garden",
                    minPrice: 28000000m,
                    maxPrice: 55000000m,
                    description: "Concept hoa tươi nhập khẩu tông màu Pastel và Rustic mộc mạc bao gồm backdrop chụp ảnh check-in 3D, bàn gallery kỷ niệm và lối đi sân khấu hoa tươi tự nhiên.",
                    location: "TP. Hồ Chí Minh"
                );
                listing3.Approve();
                context.Listings.Add(listing3);
                await context.SaveChangesAsync();

                context.ListingMedias.AddRange(
                    ListingMedia.Create(listing3.Id, "https://images.unsplash.com/photo-1465495976277-4387d4b0b4c6?auto=format&fit=crop&w=1200&q=80", null, "image", 0, true),
                    ListingMedia.Create(listing3.Id, "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=800&q=80", null, "image", 1, false)
                );
            }

            var listing4 = await context.Listings.FirstOrDefaultAsync(l => l.Slug == "thiet-ke-may-do-vay-cuoi-haute-couture");
            if (listing4 == null)
            {
                listing4 = Listing.Create(
                    vendorId: vendor1.Id,
                    categoryId: dressCatId,
                    title: "Bộ Sưu Tập Váy Cưới Công Chúa Haute Couture 2026",
                    slug: "thiet-ke-may-do-vay-cuoi-haute-couture",
                    minPrice: 12000000m,
                    maxPrice: 35000000m,
                    description: "Được đính kết thủ công từ hàng ngàn viên pha lê Swarovski, chất liệu ren Pháp nhập khẩu tôn vinh vẻ đẹp kiêu sa và thanh lịch của cô dâu.",
                    location: "TP. Hồ Chí Minh"
                );
                listing4.Approve();
                context.Listings.Add(listing4);
                await context.SaveChangesAsync();

                context.ListingMedias.AddRange(
                    ListingMedia.Create(listing4.Id, "https://images.unsplash.com/photo-1594552072238-b8a33785b261?auto=format&fit=crop&w=1200&q=80", null, "image", 0, true)
                );
            }

            await context.SaveChangesAsync();

            // 4. Seed Lead & Voucher cho Customer
            var testLead1 = await context.Leads.FirstOrDefaultAsync(l => l.CustomerId == customerUser.Id && l.VendorId == vendor1.Id);
            if (testLead1 == null)
            {
                testLead1 = Lead.Create(
                    customerId: customerUser.Id,
                    vendorId: vendor1.Id,
                    listingId: listing1.Id,
                    rawPhone: customerUser.PhoneNumber,
                    weddingDate: DateTime.UtcNow.AddMonths(2),
                    estimatedGuests: 250,
                    estimatedBudget: 25000000m,
                    notes: "Cần tư vấn gói chụp Đà Lạt tháng sau và thử váy cưới."
                );
                testLead1.Accept();
                testLead1.UnlockPhone();
                context.Leads.Add(testLead1);
                await context.SaveChangesAsync();

                // Tạo voucher 8 ký tự
                var voucher = Voucher.Create(
                    leadId: testLead1.Id,
                    customerId: customerUser.Id,
                    customCode: "WED88888",
                    discountValue: 1000000m
                );
                context.Vouchers.Add(voucher);
                await context.SaveChangesAsync();
            }

            var testLead2 = await context.Leads.FirstOrDefaultAsync(l => l.CustomerId == customerUser.Id && l.VendorId == vendor2.Id);
            if (testLead2 == null)
            {
                testLead2 = Lead.Create(
                    customerId: customerUser.Id,
                    vendorId: vendor2.Id,
                    listingId: listing2.Id,
                    rawPhone: customerUser.PhoneNumber,
                    weddingDate: DateTime.UtcNow.AddMonths(3),
                    estimatedGuests: 400,
                    estimatedBudget: 120000000m,
                    notes: "Cần tư vấn tiệc cưới sảnh Grand Crystal Ballroom."
                );
                testLead2.Accept();
                testLead2.UnlockPhone();
                context.Leads.Add(testLead2);
                await context.SaveChangesAsync();
            }

            // 5. Seed BookingContracts:
            // Contract 1: COMPLETED (gắn với testLead1 của Mai Wedding Studio - để test Verified Buyer BR-009)
            var contractCompleted = await context.BookingContracts.FirstOrDefaultAsync(c => c.CustomerId == customerUser.Id && c.Status == ContractStatus.Completed);
            if (contractCompleted == null)
            {
                contractCompleted = BookingContract.Create(
                    leadId: testLead1.Id,
                    voucherId: null,
                    vendorId: vendor1.Id,
                    customerId: customerUser.Id,
                    contractValue: 22000000m,
                    depositAmount: 7000000m,
                    contractImageUrl: "https://images.unsplash.com/photo-1450133064473-71024230f91b?auto=format&fit=crop&w=800&q=80",
                    weddingDate: DateTime.UtcNow.AddDays(-15)
                );
                contractCompleted.ConfirmByCustomer();
                contractCompleted.CompleteContract();
                testLead1.MarkContracted();
                context.BookingContracts.Add(contractCompleted);
                await context.SaveChangesAsync();
            }

            // Contract 2: PENDING VERIFICATION (gắn với testLead2 của White Palace Convention - để khách duyệt BR-005)
            var contractPending = await context.BookingContracts.FirstOrDefaultAsync(c => c.CustomerId == customerUser.Id && c.Status == ContractStatus.PendingVerification);
            if (contractPending == null)
            {
                contractPending = BookingContract.Create(
                    leadId: testLead2.Id,
                    voucherId: null,
                    vendorId: vendor2.Id,
                    customerId: customerUser.Id,
                    contractValue: 95000000m,
                    depositAmount: 25000000m,
                    contractImageUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=800&q=80",
                    weddingDate: DateTime.UtcNow.AddMonths(3)
                );
                testLead2.MarkContracted();
                context.BookingContracts.Add(contractPending);
                await context.SaveChangesAsync();
            }

            // 6. Seed Reviews (Verified Buyer vs Regular)
            var existingReview = await context.Reviews.FirstOrDefaultAsync(r => r.CustomerId == customerUser.Id && r.VendorId == vendor1.Id);
            if (existingReview == null)
            {
                var review1 = Review.Create(
                    contractId: contractCompleted.Id,
                    listingId: listing1.Id,
                    customerId: customerUser.Id,
                    vendorId: vendor1.Id,
                    rating: 5,
                    content: "Ekip chụp rất nhiệt tình và chuyên nghiệp! Bộ ảnh cưới tại Đà Lạt của chúng mình vượt xa mong đợi. Váy cưới rất mới và đẹp.",
                    photosJson: "[\"https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=600&q=80\"]",
                    isVerifiedBuyer: true
                );
                review1.Approve();
                review1.AddVendorReply("Cảm ơn hai bạn đã tin tưởng Mai Wedding Studio! Chúc hai bạn trăm năm hạnh phúc!");
                context.Reviews.Add(review1);

                // Cập nhật rating vendor theo FM-005
                vendor1.UpdateRating(5.0m, 1);
            }

            // Review chờ duyệt (Pending) cho Moderator test
            var pendingReview = await context.Reviews.FirstOrDefaultAsync(r => r.Status == ReviewStatus.Pending);
            if (pendingReview == null)
            {
                var reviewPending = Review.Create(
                    contractId: null,
                    listingId: listing2.Id,
                    customerId: customerUser.Id,
                    vendorId: vendor2.Id,
                    rating: 5,
                    content: "Sảnh tiệc cưới sang trọng, âm thanh ánh sáng chuẩn tiệc cưới hoàng gia. Đồ ăn ngon và phục vụ chu đáo.",
                    photosJson: null,
                    isVerifiedBuyer: false
                );
                context.Reviews.Add(reviewPending);
            }

            // 7. Seed Wedding Tools (Ngân sách & Kế hoạch)
            var existingBudget = await context.BudgetItems.AnyAsync(b => b.CustomerId == customerUser.Id);
            if (!existingBudget)
            {
                context.BudgetItems.AddRange(
                    BudgetItem.Create(customerUser.Id, venueCatId, "Tiệc cưới sảnh White Palace (30 bàn)", 120000000m, 115000000m, "Đã cọc 25 triệu"),
                    BudgetItem.Create(customerUser.Id, photoCatId, "Chụp ảnh Pre-Wedding Đà Lạt & Phóng sự cưới", 25000000m, 22000000m, "Ekip Mai Wedding Studio"),
                    BudgetItem.Create(customerUser.Id, decorCatId, "Trang trí hoa tươi sảnh tiệc & Bàn gia tiên", 30000000m, 0m, "Đang chọn concept hoa"),
                    BudgetItem.Create(customerUser.Id, dressCatId, "Thuê váy cưới cô dâu & May vest chú rể", 15000000m, 16500000m, "Đã thử váy"),
                    BudgetItem.Create(customerUser.Id, null, "Thiệp cưới thiết kế & Quà cảm ơn khách mời", 10000000m, 8000000m, "In 350 bộ thiệp")
                );
            }

            // 8. Seed Checklist Tasks
            var existingChecklist = await context.ChecklistTasks.AnyAsync(c => c.CustomerId == customerUser.Id);
            if (!existingChecklist)
            {
                var t1 = ChecklistTask.Create(customerUser.Id, "Họp mặt hai bên gia đình xem ngày cưới", "12_MONTHS");
                t1.ToggleComplete();

                var t2 = ChecklistTask.Create(customerUser.Id, "Dự trù ngân sách và số lượng khách mời", "12_MONTHS");
                t2.ToggleComplete();

                var t3 = ChecklistTask.Create(customerUser.Id, "Chọn và đặt cọc trung tâm tiệc cưới", "9_MONTHS");
                t3.ToggleComplete();

                var t4 = ChecklistTask.Create(customerUser.Id, "Chọn studio chụp ảnh cưới Pre-Wedding", "6_MONTHS");
                t4.ToggleComplete();

                var t5 = ChecklistTask.Create(customerUser.Id, "Chọn concept trang trí tiệc cưới hoa tươi", "3_MONTHS");
                var t6 = ChecklistTask.Create(customerUser.Id, "Gửi thiệp cưới điện tử & nhận phản hồi RSVP", "1_MONTH");
                var t7 = ChecklistTask.Create(customerUser.Id, "Thử lại váy cưới & vest lần cuối", "1_WEEK");
                var t8 = ChecklistTask.Create(customerUser.Id, "Kiểm tra nhẫn cưới & hoa cầm tay", "WEDDING_DAY");

                context.ChecklistTasks.AddRange(t1, t2, t3, t4, t5, t6, t7, t8);
            }

            await context.SaveChangesAsync();
            logger?.LogInformation("===> [DB Seeder] Đã nạp thành công bộ dữ liệu mẫu với mật khẩu mặc định 123456 cho toàn bộ users!");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Lỗi xảy ra trong quá trình Seed Database: {Message}", ex.Message);
        }
    }
}
