using Microsoft.EntityFrameworkCore;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

Console.WriteLine("=================================================================");
Console.WriteLine(" WEDDING MATCH - SERVICE CATALOG DATA SEEDER");
Console.WriteLine("=================================================================");

var hasher = new PasswordHasher();
var defaultPasswordHash = hasher.Hash("123456");

// Category IDs
var venueCatId = Guid.Parse("11111111-1111-1111-1111-111111111101");
var decorCatId = Guid.Parse("11111111-1111-1111-1111-111111111102");
var photoCatId = Guid.Parse("11111111-1111-1111-1111-111111111103");
var dressCatId = Guid.Parse("11111111-1111-1111-1111-111111111104");
var makeupCatId = Guid.Parse("11111111-1111-1111-1111-111111111105");
var invitationsCatId = Guid.Parse("11111111-1111-1111-1111-111111111106");
var plannerCatId = Guid.Parse("11111111-1111-1111-1111-111111111107");
var otherEventsCatId = Guid.Parse("11111111-1111-1111-1111-111111111108");

// Business Group IDs
var venueBgId = BusinessGroupPolicy.VenueBanquetId;
var decorBgId = BusinessGroupPolicy.DecorInvitationsId;
var photoBgId = BusinessGroupPolicy.PhotoBridalMakeupId;
var plannerBgId = BusinessGroupPolicy.FullServicePlanningId;
var otherBgId = BusinessGroupPolicy.OtherEventsId;

var vendors = new List<VendorData>
{
    // =========================================================================
    // 1. Địa điểm + Tiệc cưới
    // =========================================================================
    new(
        BrandName: "CTM Palace",
        Slug: "ctm-palace",
        Aliases: new[] { "ctm-palace" },
        ContactPerson: "Ban Quản Lý CTM",
        Hotline: "0964257766",
        City: "Hà Nội",
        Address: "131 Nguyễn Phong Sắc, Dịch Vọng Hậu, Cầu Giấy, Hà Nội",
        Bio: "Trung tâm Hội nghị & Tiệc cưới 5 sao sang trọng tại Cầu Giấy với 4 sảnh tiệc trần cao không cột và sảnh ngoài trời lãng mạn.",
        LogoUrl: "https://images.unsplash.com/photo-1519167758481-83f550bb49b3?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1519167758481-83f550bb49b3?auto=format&fit=crop&w=1200&q=80",
        CategoryId: venueCatId,
        BusinessGroupId: venueBgId,
        Listings: new()
        {
            new(
                Title: "Cho thuê sảnh tiệc cưới trọn gói CTM Palace (Sảnh không cột, màn LED vòm cong)",
                Slug: "ctm-palace-cho-thue-sanh-tiec-cuoi-tron-goi",
                MinPrice: 50000000m,
                MaxPrice: 150000000m,
                Description: "Không gian sảnh tiệc trần cao 8m không cột chịu lực với sức chứa lên đến 1.200 khách, tích hợp hệ thống màn hình LED vòm cong 4K sắc nét và âm thanh ánh sáng hòa nhạc quốc tế.",
                Location: "Cầu Giấy, Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1519167758481-83f550bb49b3?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Tiệc cưới trọn gói CTM Palace - Thực đơn mâm cỗ 10-12 món Á - Âu thượng hạng",
                Slug: "ctm-palace-thuc-don-mam-co-10-12-mon-a-au",
                MinPrice: 35000000m,
                MaxPrice: 85000000m,
                Description: "Thực đơn mâm cỗ tiệc cưới tinh hoa 10 đến 12 món kết hợp phong vị Á - Âu cao cấp, chế biến bởi bếp trưởng 5 sao, đảm bảo độ tươi nóng và bài trí tinh tế.",
                Location: "Cầu Giấy, Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1555244162-803834f70033?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Gói trang trí sảnh cơ bản và hỗ trợ kỹ thuật ánh sáng CTM Palace",
                Slug: "ctm-palace-trang-tri-sanh-va-ho-tro-anh-sang",
                MinPrice: 15000000m,
                MaxPrice: 35000000m,
                Description: "Decor trang trí sảnh đón khách trang trọng, bàn gallery kỷ niệm, cổng hoa tươi đón tiếp và kỹ thuật viên vận hành ánh sáng sân khấu biểu diễn.",
                Location: "Cầu Giấy, Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1464366400600-7168b8af9bc3?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Trống Đồng Palace",
        Slug: "trong-dong-palace",
        Aliases: new[] { "trong-dong-palace" },
        ContactPerson: "Đại Diện Trống Đồng",
        Hotline: "0964257799",
        City: "Hà Nội",
        Address: "Hệ thống 12 cơ sở trung tâm tiệc cưới phủ khắp Hà Nội",
        Bio: "Chuỗi trung tâm tiệc cưới và sự kiện hàng đầu miền Bắc với 12 cơ sở đắc địa, ẩm thực truyền thống trứ danh và chi phí hợp lý.",
        LogoUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=1200&q=80",
        CategoryId: venueCatId,
        BusinessGroupId: venueBgId,
        Listings: new()
        {
            new(
                Title: "Hệ thống sảnh tiệc cưới Trống Đồng Palace đa dạng phong cách tại 12 cơ sở",
                Slug: "trong-dong-palace-he-thong-sanh-tiec-12-co-so",
                MinPrice: 40000000m,
                MaxPrice: 120000000m,
                Description: "Lựa chọn sảnh tiệc linh hoạt tại 12 cơ sở trung tâm khắp Hà Nội, diện tích đa dạng từ 200 đến 1.000 khách, thiết kế tân cổ điển và lãng mạn.",
                Location: "Hà Nội (12 cơ sở)",
                ImageUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Gói thực đơn mâm cỗ cưới truyền thống Trống Đồng Palace (Nổi bật với món xôi vò)",
                Slug: "trong-dong-palace-thuc-don-truyen-thong-xoi-vo",
                MinPrice: 32000000m,
                MaxPrice: 65000000m,
                Description: "Mâm cỗ cưới truyền thống chuẩn vị Bắc Bộ, món xôi vò hạt sen trứ danh kết hợp cùng gà hấp lá chanh, tôm chiên ngự thiện và các món đặc sản đậm đà.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dịch vụ cưới trọn gói đi kèm Trống Đồng Palace: MC, khánh tiết, tháp ly khai tiệc",
                Slug: "trong-dong-palace-dich-vu-mc-khanh-tiet-thap-ly",
                MinPrice: 8000000m,
                MaxPrice: 18000000m,
                Description: "Trọn bộ nhân sự nghi lễ chuyên nghiệp: MC dẫn chương trình song ngữ/truyền thống, đội ngũ khánh tiết đón khách, tháp ly champagne và bánh cưới trang trọng.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Vạn Hoa Premium",
        Slug: "van-hoa-premium",
        Aliases: new[] { "van-hoa-premium" },
        ContactPerson: "Ban Điều Hành Vạn Hoa",
        Hotline: "0964257788",
        City: "Hà Nội",
        Address: "Chuỗi trung tâm tiệc cưới Vạn Hoa tại Hà Nội",
        Bio: "Thương hiệu tiệc cưới 20 năm kinh nghiệm uy tín bậc nhất Hà Nội, mang đến giải pháp tiệc cưới chìa khóa trao tay trọn vẹn và an tâm.",
        LogoUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80",
        CategoryId: venueCatId,
        BusinessGroupId: venueBgId,
        Listings: new()
        {
            new(
                Title: "Tiệc cưới trọn gói 'Chìa khóa trao tay' Vạn Hoa Premium (20 năm kinh nghiệm)",
                Slug: "van-hoa-premium-tiec-cuoi-chia-khoa-trao-tay",
                MinPrice: 60000000m,
                MaxPrice: 180000000m,
                Description: "Giải pháp tiệc cưới toàn diện từ A-Z, cam kết chất lượng 20 năm thương hiệu Vạn Hoa, tối ưu chi phí và điều phối chuẩn xác từng phút trong hôn lễ.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Thực đơn mâm cỗ cưới hương vị truyền thống Bắc Bộ Vạn Hoa Premium",
                Slug: "van-hoa-premium-thuc-don-truyen-thong-bac-bo",
                MinPrice: 38000000m,
                MaxPrice: 75000000m,
                Description: "Mâm cỗ cưới đầy đặn, chuẩn chỉnh văn hóa ẩm thực tiệc cưới Hà Thành, nguyên liệu tươi ngon được kiểm định vệ sinh an toàn thực phẩm nghiêm ngặt.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Gói trang trí bàn gallery, sảnh đón khách và nhân sự phục vụ Vạn Hoa Premium",
                Slug: "van-hoa-premium-trang-tri-gallery-sanh-va-nhan-su",
                MinPrice: 12000000m,
                MaxPrice: 28000000m,
                Description: "Thiết kế bàn gallery nghệ thuật, backdrop check-in hoa tươi hiện đại và đội ngũ nhân viên phục vụ bàn chuyên nghiệp, chu đáo.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1465495976277-4387d4b0b4c6?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 2. Trang phục (Váy cưới & Vest)
    // =========================================================================
    new(
        BrandName: "Lek Chi Bridal",
        Slug: "lek-chi-bridal",
        Aliases: new[] { "lek-chi-bridal" },
        ContactPerson: "NTK Lek Chi",
        Hotline: "0906186666",
        City: "Hà Nội",
        Address: "184 Mai Hắc Đế, Hai Bà Trưng, Hà Nội",
        Bio: "Thương hiệu váy cưới thiết kế cao cấp độc bản đính kết thủ công của NTK Lek Chi, tôn vinh nét quyến rũ và cá tính của cô dâu Việt.",
        LogoUrl: "https://images.unsplash.com/photo-1594552072238-b8a33785b261?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1594552072238-b8a33785b261?auto=format&fit=crop&w=1200&q=80",
        CategoryId: dressCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Dịch vụ cho thuê váy cưới thủ công độc bản Lek Chi (Đính kết ren & pha lê)",
                Slug: "lek-chi-bridal-thue-vay-cuoi-thu-cong-doc-ban",
                MinPrice: 15000000m,
                MaxPrice: 45000000m,
                Description: "Váy cưới thủ công độc bản đính kết hàng ngàn hạt pha lê Swarovski và ren Pháp, thiết kế corsetry nẹp eo siết dáng chuẩn mực.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1594552072238-b8a33785b261?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Gói may đo váy cưới thiết kế riêng theo số đo Lek Chi Bridal",
                Slug: "lek-chi-bridal-may-do-vay-cuoi-thiet-ke-rieng",
                MinPrice: 35000000m,
                MaxPrice: 90000000m,
                Description: "Quy trình may đo Haute Couture riêng biệt từ khâu phác thảo bản vẽ concept, tuyển chọn lụa tơ tằm đến may hoàn thiện chuẩn từng centimet cơ thể.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dịch vụ tinh chỉnh form dáng váy và phụ kiện kèm theo (lúp, mấn, trang sức)",
                Slug: "lek-chi-bridal-tinh-chinh-form-dang-va-phu-kien",
                MinPrice: 3000000m,
                MaxPrice: 8000000m,
                Description: "Chỉnh sửa chuẩn xác số đo trước ngày cưới 3 ngày, tặng kèm lúp voan thêu tay, mấn cách tân và bộ trang sức cài tóc thiết kế đồng bộ.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1546804784-896d0dca3805?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Nicole Bridal",
        Slug: "nicole-bridal",
        Aliases: new[] { "nicole-bridal" },
        ContactPerson: "Đại Diện Nicole Bridal",
        Hotline: "0902640490",
        City: "TP. Hồ Chí Minh",
        Address: "49 Hồ Văn Huê, Phường 9, Quận Phú Nhuận, TP. HCM",
        Bio: "Thương hiệu váy cưới phong cách Minimalist tinh giản thanh lịch hàng đầu Việt Nam, hỗ trợ may đo từ xa cho cô dâu trong và ngoài nước.",
        LogoUrl: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=1200&q=80",
        CategoryId: dressCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Cho thuê váy cưới phong cách Minimalist cao cấp Nicole Bridal",
                Slug: "nicole-bridal-thue-vay-cuoi-minimalist-cao-cap",
                MinPrice: 8000000m,
                MaxPrice: 25000000m,
                Description: "Váy cưới tối giản chuẩn phong cách châu Âu, chất liệu lụa satin mờ, mikado cao cấp tôn lên nét thanh nhã vượt thời gian.",
                Location: "TP. Hồ Chí Minh & Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1583939003579-730e3918a45a?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dịch vụ may đo váy cưới từ xa theo yêu cầu Nicole Bridal",
                Slug: "nicole-bridal-may-do-vay-cuoi-tu-xa",
                MinPrice: 18000000m,
                MaxPrice: 40000000m,
                Description: "Quy trình lấy số đo online chuẩn quốc tế qua video call, gửi mẫu vải tận tay và giao áo hoàn thiện đến tận cửa cho cô dâu kiều bào hoặc ở xa.",
                Location: "Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1511285560929-80b456fea0bc?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dòng sản phẩm váy cưới phân khúc Premium và Luxury Nicole Bridal",
                Slug: "nicole-bridal-vay-cuoi-dong-premium-luxury",
                MinPrice: 25000000m,
                MaxPrice: 65000000m,
                Description: "Bộ sưu tập váy cưới cao cấp sử dụng chất liệu ren Chantilly nhập khẩu, thiết kế bồng bềnh công chúa và đuôi dài thướt tha cho lễ cưới nhà thờ/khách sạn.",
                Location: "TP. Hồ Chí Minh",
                ImageUrl: "https://images.unsplash.com/photo-1594552072238-b8a33785b261?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Chung Thanh Phong Bridal",
        Slug: "chung-thanh-phong-bridal",
        Aliases: new[] { "chung-thanh-phong-bridal" },
        ContactPerson: "NTK Chung Thanh Phong",
        Hotline: "0938999996",
        City: "TP. Hồ Chí Minh",
        Address: "189/1 Cống Quỳnh, Phường Nguyễn Cư Trinh, Quận 1, TP. HCM",
        Bio: "Nhà mốt váy cưới Haute Couture xa xỉ hàng đầu Việt Nam, được các ngôi sao hạng A và cô dâu thượng lưu tin chọn.",
        LogoUrl: "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=1200&q=80",
        CategoryId: dressCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Dịch vụ váy cưới Haute Couture xa xỉ Chung Thanh Phong cho sao hạng A",
                Slug: "chung-thanh-phong-vay-cuoi-haute-couture-xa-xi",
                MinPrice: 80000000m,
                MaxPrice: 250000000m,
                Description: "Tác phẩm váy cưới lộng lẫy được sáng tạo riêng biệt bởi chính NTK Chung Thanh Phong, đính hàng chục ngàn viên pha lê lấp lánh như dải ngân hà.",
                Location: "Quận 1, TP. Hồ Chí Minh",
                ImageUrl: "https://images.unsplash.com/photo-1594552072238-b8a33785b261?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Gói thiết kế và may đo váy cưới dòng phổ thông và cao cấp Chung Thanh Phong",
                Slug: "chung-thanh-phong-goi-thiet-ke-may-do-vay-cuoi",
                MinPrice: 40000000m,
                MaxPrice: 120000000m,
                Description: "Thiết kế may đo theo form dáng cô dâu với kỹ thuật siết eo độc quyền tôn vinh 3 vòng hoàn mỹ, chất liệu vải nhập khẩu từ các kinh đô thời trang Pháp & Ý.",
                Location: "TP. Hồ Chí Minh",
                ImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Adam Store",
        Slug: "adam-store",
        Aliases: new[] { "adam-store" },
        ContactPerson: "Bộ Phận Chăm Sóc Adam Store",
        Hotline: "0938888835",
        City: "Hà Nội",
        Address: "Hệ thống hơn 70 showroom toàn quốc",
        Bio: "Thương hiệu âu phục và vest cưới nam may sẵn & may đo số 1 Việt Nam, dẫn đầu xu hướng vest cưới chuẩn form dáng Hàn Quốc lịch lãm.",
        LogoUrl: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=1200&q=80",
        CategoryId: dressCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "May đo và bán sẵn Âu phục/Vest cưới chuẩn form dáng Hàn Quốc Adam Store",
                Slug: "adam-store-may-do-ban-san-vest-cuoi-han-quoc",
                MinPrice: 3500000m,
                MaxPrice: 12000000m,
                Description: "Bộ sưu tập vest chú rể tôn dáng, chất liệu vải wool pha cashmere đứng form, không nhăn, đa dạng màu sắc từ tuxedo đen cổ điển đến xanh navy, beige trẻ trung.",
                Location: "Toàn quốc (70 showroom)",
                ImageUrl: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Cung cấp các gói combo Vest cưới kèm sơ mi và phụ kiện thắt lưng Adam Store",
                Slug: "adam-store-combo-vest-cuoi-kem-so-mi-phu-kien",
                MinPrice: 4800000m,
                MaxPrice: 15000000m,
                Description: "Combo ngày cưới trọn bộ: 01 bộ vest cao cấp + 01 áo sơ mi may đo cotton 100% + 01 nơ/cà vạt lụa + 01 thắt lưng da thật + khăn cài túi áo vest sang trọng.",
                Location: "Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1594938298603-c8148c4dae35?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 3. Make up (Trang điểm cô dâu)
    // =========================================================================
    new(
        BrandName: "Tina Lê Make Up",
        Slug: "tina-le-make-up",
        Aliases: new[] { "tina-le-make-up", "tina-le-makeup" },
        ContactPerson: "Tina Lê",
        Hotline: "0966044714",
        City: "Hà Nội",
        Address: "116 Nguyễn Khuyến, Văn Miếu, Đống Đa, Hà Nội",
        Bio: "Học viện và chuyên gia trang điểm cô dâu hàng đầu thủ đô, tạo nên diện mạo rạng rỡ và thần thái tự tin nhất cho hàng ngàn cô dâu.",
        LogoUrl: "https://images.unsplash.com/photo-1487412720507-e7ab37603c6f?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1487412720507-e7ab37603c6f?auto=format&fit=crop&w=1200&q=80",
        CategoryId: makeupCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Dịch vụ trang điểm cô dâu chuyên nghiệp Tina Lê (Bao gồm làm tóc)",
                Slug: "tina-le-makeup-trang-diem-co-dau-chuyen-nghiep",
                MinPrice: 3500000m,
                MaxPrice: 8000000m,
                Description: "Trang điểm cô dâu ngày cưới với mỹ phẩm high-end (Dior, Chanel, Tom Ford), kỹ thuật đánh nền mỏng nhẹ như sương và tạo kiểu tóc cô dâu sang trọng.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1487412720507-e7ab37603c6f?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Gói dặm phấn và hỗ trợ đổi kiểu tóc trước/trong tiệc cưới Tina Lê",
                Slug: "tina-le-makeup-dam-phan-doi-kieu-toc",
                MinPrice: 2000000m,
                MaxPrice: 5000000m,
                Description: "Chuyên viên đi cùng cô dâu trong tiệc cưới để dặm lại lớp trang điểm, dặm son và thay đổi layout tóc từ lễ đón dâu sang dạ tiệc quẩy hết mình.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1560066984-138dadb4c035?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Hạnh Lâm Makeup & Academy",
        Slug: "hanh-lam-makeup",
        Aliases: new[] { "hanh-lam-makeup" },
        ContactPerson: "Hạnh Lâm",
        Hotline: "0968999868",
        City: "Hà Nội",
        Address: "106 Trung Phụng, Đống Đa, Hà Nội",
        Bio: "Nổi tiếng với phong cách makeup mỏng nhẹ trong trẻo chuẩn Glass Skin Hàn Quốc, giữ nền bền chặt suốt 16 tiếng không xuống tone.",
        LogoUrl: "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?auto=format&fit=crop&w=1200&q=80",
        CategoryId: makeupCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Trang điểm cô dâu chuyên sâu Hạnh Lâm (Phong cách mỏng nhẹ, trong trẻo, bám lâu)",
                Slug: "hanh-lam-makeup-co-dau-chuyen-sau-trong-treo",
                MinPrice: 3000000m,
                MaxPrice: 7500000m,
                Description: "Kỹ thuật phủ nền độc quyền mỏng mịn tự nhiên, che khuyết điểm hoàn hảo, mi sợi mảnh tự nhiên và phối màu tone cam đào/hồng đất ngọt ngào.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dịch vụ trang điểm trọn gói ngày cưới tại nhà hoặc studio Hạnh Lâm",
                Slug: "hanh-lam-makeup-tron-goi-ngay-cuoi-tai-nha-studio",
                MinPrice: 4000000m,
                MaxPrice: 9000000m,
                Description: "Ekip chuyên viên đến tận nhà cô dâu đúng giờ hẹn, bao gồm trang điểm & làm tóc cho cô dâu và dặm nhẹ cho mẹ cô dâu chú rể.",
                Location: "Hà Nội và các tỉnh lân cận",
                ImageUrl: "https://images.unsplash.com/photo-1512496015851-a90fb38ba796?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "John Kim Wedding",
        Slug: "john-kim-wedding",
        Aliases: new[] { "john-kim-wedding" },
        ContactPerson: "John Kim",
        Hotline: "0905368688",
        City: "Hà Nội",
        Address: "14 Nguyễn Đình Chiểu, Hai Bà Trưng, Hà Nội",
        Bio: "Chuyên gia trang điểm cô dâu hàng đầu được các cặp đôi và người nổi tiếng gửi gắm nhan sắc trong ngày trọng đại.",
        LogoUrl: "https://images.unsplash.com/photo-1508214751196-bcfd4ca60f91?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1508214751196-bcfd4ca60f91?auto=format&fit=crop&w=1200&q=80",
        CategoryId: makeupCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Dịch vụ trang điểm cao cấp cho cô dâu bởi chuyên gia hàng đầu John Kim",
                Slug: "john-kim-wedding-trang-diem-cao-cap-chuyen-gia",
                MinPrice: 5000000m,
                MaxPrice: 15000000m,
                Description: "Trực tiếp chuyên gia John Kim thực hiện, biến hóa linh hoạt từ layout Hàn Quốc tự nhiên trong trẻo đến tone Tây sắc sảo, nhấn mắt hút hồn.",
                Location: "Hà Nội & TP. HCM",
                ImageUrl: "https://images.unsplash.com/photo-1508214751196-bcfd4ca60f91?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 4. Hoa cưới
    // =========================================================================
    new(
        BrandName: "Liti Florist",
        Slug: "liti-florist",
        Aliases: new[] { "liti-florist" },
        ContactPerson: "Phòng Dự Án Cưới Liti",
        Hotline: "0964313366",
        City: "Hà Nội",
        Address: "3 Phố Huế, Hàng Bài, Hoàn Kiếm, Hà Nội",
        Bio: "Thương hiệu hoa tươi nhập khẩu cao cấp số 1 Việt Nam, mang đến những tác phẩm hoa cưới nghệ thuật và không gian ngập tràn hương sắc.",
        LogoUrl: "https://images.unsplash.com/photo-1561181286-d3fee7d55364?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1561181286-d3fee7d55364?auto=format&fit=crop&w=1200&q=80",
        CategoryId: decorCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Thiết kế hoa cầm tay cô dâu bằng hoa tươi nhập khẩu cao cấp (Peony, Tulip...)",
                Slug: "liti-florist-hoa-cam-tay-nhap-khau-peony-tulip",
                MinPrice: 1800000m,
                MaxPrice: 5500000m,
                Description: "Bó hoa cưới độc bản kết từ những đóa Mẫu Đơn Peony Hà Lan, Tulip New Zealand, hoa linh lan và thảo mộc tự nhiên theo bảng màu tiệc cưới.",
                Location: "Hà Nội & TP. HCM",
                ImageUrl: "https://images.unsplash.com/photo-1561181286-d3fee7d55364?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dịch vụ hoa cài áo chú rể và hoa trang trí xe cưới Liti Florist",
                Slug: "liti-florist-hoa-cai-ao-va-hoa-xe-cuoi",
                MinPrice: 2500000m,
                MaxPrice: 8000000m,
                Description: "Bộ hoa cài áo chú rể đồng điệu cùng hoa cầm tay cô dâu và cụm hoa tươi trang trí đầu xe rước dâu kết nơ lụa cao cấp chống gió.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Gói thi công trang trí không gian tiệc cưới (Floral Decoration) trọn gói Liti Florist",
                Slug: "liti-florist-floral-decoration-tron-goi",
                MinPrice: 35000000m,
                MaxPrice: 120000000m,
                Description: "Thiết kế và cắm hoa tươi toàn bộ cổng chào đón khách, lối đi sân khấu, photobooth check-in và trụ hoa bàn tiệc bằng 100% hoa tươi nhập khẩu.",
                Location: "Hà Nội & TP. HCM",
                ImageUrl: "https://images.unsplash.com/photo-1465495976277-4387d4b0b4c6?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Hands & Heart Floral Design",
        Slug: "hands-heart-floral",
        Aliases: new[] { "hands-heart-floral" },
        ContactPerson: "Florist Lead",
        Hotline: "0968112233",
        City: "Hà Nội",
        Address: "52 Tô Ngọc Vân, Tây Hồ, Hà Nội",
        Bio: "Xưởng hoa thủ công phong cách mộc mạc Rustic, Bohemian và hoa đồng nội tự nhiên đầy chất thơ.",
        LogoUrl: "https://images.unsplash.com/photo-1526047932273-341f2a7631f9?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1526047932273-341f2a7631f9?auto=format&fit=crop&w=1200&q=80",
        CategoryId: decorCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Thiết kế hoa cầm tay phong cách tự nhiên (Rustic/Wild) và Vintage Hands & Heart",
                Slug: "hands-heart-hoa-cam-tay-rustic-wild-vintage",
                MinPrice: 1500000m,
                MaxPrice: 4200000m,
                Description: "Dáng hoa thả tự nhiên phóng khoáng, kết hợp hoa dại nhập khẩu, lá bạch đàn, sen đá và cúc tana mang hơi thở đồng thảo châu Âu.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1526047932273-341f2a7631f9?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dịch vụ cắm hoa xe cưới và vòng hoa đội đầu Hands & Heart Floral",
                Slug: "hands-heart-hoa-xe-cuoi-vong-hoa-doi-dau",
                MinPrice: 2000000m,
                MaxPrice: 6000000m,
                Description: "Bộ trang trí xe hoa cưới nhẹ nhàng tinh tế cùng vòng hoa đội đầu bằng hoa bi tây và hoa baby trắng tinh khôi cho cô dâu.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1490750967868-88aa4486c946?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Métaphore Floral",
        Slug: "metaphore-floral",
        Aliases: new[] { "metaphore-floral" },
        ContactPerson: "Quản Lý Métaphore",
        Hotline: "0904889988",
        City: "Hà Nội",
        Address: "56A Lý Thường Kiệt, Hoàn Kiếm, Hà Nội",
        Bio: "Nghệ thuật hoa đương đại đậm chất Pháp, sử dụng những giống hoa hiếm và độc bản kiến tạo nên vẻ đẹp sang trọng đẳng cấp.",
        LogoUrl: "https://images.unsplash.com/photo-1582794543139-8ac9cb0f7b11?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1582794543139-8ac9cb0f7b11?auto=format&fit=crop&w=1200&q=80",
        CategoryId: decorCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Thiết kế hoa nghệ thuật đương đại độc bản (Contemporary Floral Art) Métaphore",
                Slug: "metaphore-floral-contemporary-floral-art",
                MinPrice: 4000000m,
                MaxPrice: 15000000m,
                Description: "Mỗi bó hoa là một tác phẩm điêu khắc nghệ thuật độc nhất vô nhị, được tuyển chọn hoa tươi trực tiếp từ các nhà vườn danh tiếng tại Hà Lan và Nam Phi.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1582794543139-8ac9cb0f7b11?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Cung cấp hoa cầm tay cô dâu sử dụng các dòng hoa hiếm nhập khẩu Métaphore",
                Slug: "metaphore-floral-hoa-cam-tay-hoa-hiem-nhap-khau",
                MinPrice: 2800000m,
                MaxPrice: 8500000m,
                Description: "Bó hoa cưới từ các loài hoa kiêu sa quý hiếm như Gloriosa, Calla Lily đen, David Austin Garden Rose và Orchids độc lạ.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1533616688419-b7a585564566?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 5. Trang trí & Wedding Planner
    // =========================================================================
    new(
        BrandName: "The Planners",
        Slug: "the-planners",
        Aliases: new[] { "the-planners" },
        ContactPerson: "Wedding Director",
        Hotline: "0968388688",
        City: "Hà Nội",
        Address: "Tầng 5, 23 Phan Chu Trinh, Hoàn Kiếm, Hà Nội & Quận 1 TP. HCM",
        Bio: "Đơn vị tiên phong tổ chức Destination Wedding trọn gói chuyên nghiệp tại Việt Nam, kiến tạo những đám cưới mang đậm dấu ấn cá nhân.",
        LogoUrl: "https://images.unsplash.com/photo-1511795409834-ef04bbd61622?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1511795409834-ef04bbd61622?auto=format&fit=crop&w=1200&q=80",
        CategoryId: plannerCatId,
        BusinessGroupId: plannerBgId,
        Listings: new()
        {
            new(
                Title: "Dịch vụ tổ chức Destination Wedding trọn gói The Planners",
                Slug: "the-planners-destination-wedding-tron-goi",
                MinPrice: 80000000m,
                MaxPrice: 250000000m,
                Description: "Lên kế hoạch và tổ chức đám cưới bãi biển, resort cao cấp tại Phú Quốc, Đà Nẵng, Nha Trang, Đà Lạt bao gồm hậu cần khách mời, tiệc chào mừng và tiệc chính.",
                Location: "Toàn quốc & Quốc tế",
                ImageUrl: "https://images.unsplash.com/photo-1511795409834-ef04bbd61622?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Lên ý tưởng concept cưới độc bản và quản lý toàn bộ dự án (Planner fee) The Planners",
                Slug: "the-planners-concept-doc-ban-quan-ly-du-an",
                MinPrice: 40000000m,
                MaxPrice: 90000000m,
                Description: "Đội ngũ Wedding Planner chuyên nghiệp đồng hành 6-12 tháng: lên kịch bản cưới, thiết kế Moodboard concept, quản trị ngân sách và điều phối toàn bộ nhà cung cấp.",
                Location: "Hà Nội & TP. HCM",
                ImageUrl: "https://images.unsplash.com/photo-1465495976277-4387d4b0b4c6?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Thi công trang trí không gian cưới cao cấp The Planners",
                Slug: "the-planners-thi-cong-trang-tri-khong-gian-cuoi",
                MinPrice: 60000000m,
                MaxPrice: 200000000m,
                Description: "Xưởng sản xuất và thi công mỹ thuật trực tiếp hiện thực hóa bản vẽ 3D với hệ thống sân khấu, visual ánh sáng và hoa tươi đẳng cấp nghệ thuật.",
                Location: "Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Lutece Wedding",
        Slug: "lutece-wedding",
        Aliases: new[] { "lutece-wedding" },
        ContactPerson: "Giám Đốc Sáng Tạo",
        Hotline: "0967888882",
        City: "Hà Nội",
        Address: "88 Phố Huế, Ngô Thì Nhậm, Hai Bà Trưng, Hà Nội",
        Bio: "Chuyên gia trang trí tiệc cưới không gian khách sạn 5 sao với khối lượng hoa tươi nhập khẩu khổng lồ và độ hoàn thiện tinh xảo.",
        LogoUrl: "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=1200&q=80",
        CategoryId: plannerCatId,
        BusinessGroupId: plannerBgId,
        Listings: new()
        {
            new(
                Title: "Trang trí không gian sảnh tiệc 5 sao với khối lượng hoa tươi nhập khẩu lớn Lutece",
                Slug: "lutece-wedding-trang-tri-sanh-tiec-5-sao-hoa-nhap-khau",
                MinPrice: 50000000m,
                MaxPrice: 180000000m,
                Description: "Biến sảnh tiệc cưới thành khu vườn địa đàng với hàng ngàn cành hoa tươi cao cấp, đèn chùm pha lê và hiệu ứng sương khói mờ ảo.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Thiết kế cổng đón khách, photobooth, bàn gallery và sân khấu Lutece Wedding",
                Slug: "lutece-wedding-thiet-ke-cong-photobooth-san-khau",
                MinPrice: 30000000m,
                MaxPrice: 85000000m,
                Description: "Hạng mục trang trí sảnh ngoài ấn tượng: cổng hoa vòm 3D, backdrop check-in kỷ niệm rộng 6 mét và con đường hạnh phúc dẫn lên lễ đường.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1464366400600-7168b8af9bc3?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Cleo Wedding",
        Slug: "cleo-wedding",
        Aliases: new[] { "cleo-wedding" },
        ContactPerson: "Trưởng Nhóm Thiết Kế",
        Hotline: "0903333339",
        City: "TP. Hồ Chí Minh",
        Address: "128 Nguyễn Đình Chiểu, Phường 6, Quận 3, TP. HCM",
        Bio: "Phong cách thiết kế trang trí tiệc cưới sáng tạo, hiện đại và trẻ trung, mang câu chuyện tình yêu riêng biệt vào không gian ngày trọng đại.",
        LogoUrl: "https://images.unsplash.com/photo-1519225438150-7d1b33b7b25a?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1519225438150-7d1b33b7b25a?auto=format&fit=crop&w=1200&q=80",
        CategoryId: plannerCatId,
        BusinessGroupId: plannerBgId,
        Listings: new()
        {
            new(
                Title: "Thiết kế concept trang trí tiệc cưới sáng tạo Cleo Wedding",
                Slug: "cleo-wedding-thiet-ke-concept-trang-tri-sang-tao",
                MinPrice: 45000000m,
                MaxPrice: 130000000m,
                Description: "Sáng tạo concept cưới đa dạng từ phong cách rừng nhiệt đới Tropical lãng mạn, Neon Futuristic hiện đại đến Retro cổ điển tinh tế.",
                Location: "TP. Hồ Chí Minh",
                ImageUrl: "https://images.unsplash.com/photo-1519225438150-7d1b33b7b25a?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Thi công trọn gói khu vực đón khách, lối đi sân khấu và bàn tiệc Cleo Wedding",
                Slug: "cleo-wedding-thi-cong-tron-goi-don-khach-san-khau",
                MinPrice: 35000000m,
                MaxPrice: 95000000m,
                Description: "Gói thi công mỹ thuật trọn vẹn, setup chỉn chu từng góc nhỏ với hoa tươi, bảng Welcome acrylic, nến thơm và phụ kiện cao cấp.",
                Location: "TP. Hồ Chí Minh & Bình Dương",
                ImageUrl: "https://images.unsplash.com/photo-1465495976277-4387d4b0b4c6?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 6. Nghi lễ (Tráp ăn hỏi & Nhân sự)
    // =========================================================================
    new(
        BrandName: "7799 Wedding Storyteller",
        Slug: "7799-wedding-storyteller",
        Aliases: new[] { "7799-wedding-storyteller" },
        ContactPerson: "Quản Lý 7799",
        Hotline: "0907799799",
        City: "Hà Nội",
        Address: "22 Hàng Buồm, Hoàn Kiếm, Hà Nội",
        Bio: "Thương hiệu tráp ăn hỏi nghệ thuật sơn mài cao cấp kết hợp hoa tươi hiện đại, lưu giữ truyền thống với ngôn ngữ thẩm mỹ thời thượng.",
        LogoUrl: "https://images.unsplash.com/photo-1523438885200-e635ba2c371e?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1523438885200-e635ba2c371e?auto=format&fit=crop&w=1200&q=80",
        CategoryId: decorCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Cung cấp mâm quả sơn mài ăn hỏi cao cấp kết hoa tươi nghệ thuật 7799",
                Slug: "7799-wedding-mam-qua-son-mai-an-hoi-cao-cap",
                MinPrice: 6500000m,
                MaxPrice: 22000000m,
                Description: "Bộ tráp sơn mài đỏ truyền thống hoặc sơn mài pastel khắc tên cô dâu chú rể, kết hợp hoa tươi nhập khẩu và các lễ vật bánh cốm, chè sen, rượu thuốc tuyển chọn.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1523438885200-e635ba2c371e?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Cung cấp nhân sự bê tráp và trang phục đồng bộ 7799 Storyteller",
                Slug: "7799-wedding-nhan-su-be-trap-trang-phuc-dong-bo",
                MinPrice: 1500000m,
                MaxPrice: 4500000m,
                Description: "Đội hình bê tráp nam thanh nữ tú trẻ trung, chiều cao đồng đều, tác phong lễ phép kèm áo dài cách tân gấm cao cấp đồng màu.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1511285560929-80b456fea0bc?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Lại Hằng Wedding",
        Slug: "lai-hang-wedding",
        Aliases: new[] { "lai-hang-wedding" },
        ContactPerson: "Cô Lại Hằng",
        Hotline: "0912345678",
        City: "Hà Nội",
        Address: "56 Nguyễn Hữu Huân, Hoàn Kiếm, Hà Nội",
        Bio: "Nghệ nhân cưới hỏi truyền thống lâu đời đất Hà Thành, nức tiếng với nghệ thuật kết tráp rồng phượng thủ công tinh xảo.",
        LogoUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=1200&q=80",
        CategoryId: decorCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Cung cấp tráp rồng phượng thủ công truyền thống Bắc Bộ Lại Hằng Wedding",
                Slug: "lai-hang-wedding-trap-rong-phuong-thu-cong",
                MinPrice: 5000000m,
                MaxPrice: 18000000m,
                Description: "Cặp tráp rồng bay phượng múa được kết tay tỉ mỉ từ ớt đỏ, cau trầu và hoa quả tươi tạo hình uy nghiêm, mang lại vượng khí tốt lành cho hôn nhân.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dịch vụ tráp ăn hỏi trọn gói chuẩn phong tục (5 - 7 - 9 tráp) Lại Hằng Wedding",
                Slug: "lai-hang-wedding-dich-vu-trap-an-hoi-tron-goi-chuan-phong-tuc",
                MinPrice: 4500000m,
                MaxPrice: 15000000m,
                Description: "Gói tráp ăn hỏi đầy đủ lễ vật chuẩn phong tục cưới hỏi truyền thống miền Bắc, bày trí đẹp mắt trên mâm đồng đỏ cổ kính.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Cưới Hỏi Quang Dũng",
        Slug: "cuoi-hoi-quang-dung",
        Aliases: new[] { "cuoi-hoi-quang-dung" },
        ContactPerson: "Anh Quang Dũng",
        Hotline: "0987654321",
        City: "Hà Nội",
        Address: "12 Tây Sơn, Đống Đa, Hà Nội",
        Bio: "Trung tâm cung cấp nhân sự bê tráp và cho thuê trang phục bưng quả chuyên nghiệp hàng đầu tại thủ đô.",
        LogoUrl: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=1200&q=80",
        CategoryId: decorCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Cho thuê đội hình bê tráp nam/nữ ngoại hình đồng đều, chuyên nghiệp Cưới Hỏi Quang Dũng",
                Slug: "cuoi-hoi-quang-dung-doi-hinh-be-trap-chuyen-nghiep",
                MinPrice: 1200000m,
                MaxPrice: 3500000m,
                Description: "Cung cấp đội ngũ bê đỡ tráp từ 5 đến 11 người, cam kết đúng giờ hẹn, tác phong nhanh nhẹn, chu đáo và thân thiện suốt buổi lễ gia tiên.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Cho thuê áo dài bưng quả và cung cấp các mâm tráp cơ bản Quang Dũng",
                Slug: "cuoi-hoi-quang-dung-thue-ao-dai-va-trap-co-ban",
                MinPrice: 3000000m,
                MaxPrice: 9000000m,
                Description: "Bộ sưu tập áo dài bưng quả đa dạng màu sắc (vàng, hồng, đỏ, pastel), vải lụa tơ mềm mại đi kèm các mâm tráp cưới lễ vật đầy đủ.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1583939003579-730e3918a45a?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 7. Thiệp & Quản lý khách hàng
    // =========================================================================
    new(
        BrandName: "Peonies Wedding",
        Slug: "peonies-wedding",
        Aliases: new[] { "peonies-wedding" },
        ContactPerson: "Phòng Thiết Kế Peonies",
        Hotline: "0909123456",
        City: "TP. Hồ Chí Minh",
        Address: "59 Lý Tự Trọng, Bến Nghé, Quận 1, TP. HCM",
        Bio: "Thương hiệu thiệp cưới luxury cao cấp, dẫn đầu xu hướng ép kim, dập nổi 3D và chất liệu giấy mỹ thuật nhập khẩu từ Ý.",
        LogoUrl: "https://images.unsplash.com/photo-1607344645866-009c320c5ab8?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1607344645866-009c320c5ab8?auto=format&fit=crop&w=1200&q=80",
        CategoryId: invitationsCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Thiết kế và in ấn thiệp cưới luxury (Giấy mỹ thuật ngoại nhập, ép kim, dập nổi 3D) Peonies",
                Slug: "peonies-wedding-thiet-ke-in-an-thiep-cuoi-luxury",
                MinPrice: 3000000m,
                MaxPrice: 15000000m,
                Description: "Bộ sưu tập thiệp cưới nghệ thuật cao cấp với công nghệ ép nhũ kim vàng/bạc, dập chìm họa tiết monogram riêng và nơ lụa thủ công trang nhã.",
                Location: "TP. Hồ Chí Minh & Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1607344645866-009c320c5ab8?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Traqué Wedding Paper",
        Slug: "traque-wedding-paper",
        Aliases: new[] { "traque-wedding-paper" },
        ContactPerson: "Traqué Studio",
        Hotline: "0909876543",
        City: "TP. Hồ Chí Minh",
        Address: "12 Mạc Đĩnh Chi, Đa Kao, Quận 1, TP. HCM",
        Bio: "Xưởng thiệp cưới thủ công eco-friendly tôn vinh lối sống xanh, sử dụng giấy tái chế mỹ thuật và con dấu sáp wax seal phong cách Rustic.",
        LogoUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=1200&q=80",
        CategoryId: invitationsCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Thiết kế thiệp cưới thủ công eco-friendly (Giấy tái chế, dấu sáp wax seal phong cách Rustic) Traqué",
                Slug: "traque-wedding-paper-thiep-thu-cong-eco-friendly",
                MinPrice: 3500000m,
                MaxPrice: 12000000m,
                Description: "Thiệp cưới thân thiện môi trường từ giấy sợi bông cotton và giấy hạt cỏ, đóng dấu sáp wax seal màu đồng cổ điển và buộc dây gai mộc mạc.",
                Location: "TP. Hồ Chí Minh & Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1544717305-2782549b5136?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Diamond Wedding Platform",
        Slug: "diamond",
        Aliases: new[] { "diamond", "diamond-wedding-tech" },
        ContactPerson: "Bộ Phận Hỗ Trợ Kỹ Thuật",
        Hotline: "0901888999",
        City: "Toàn quốc",
        Address: "Tòa nhà Diamond Tech, Cầu Giấy, Hà Nội",
        Bio: "Nền tảng công nghệ số hóa đám cưới hàng đầu, giúp cô dâu chú rể tạo website cưới riêng biệt và quản lý khách mời thông minh.",
        LogoUrl: "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=1200&q=80",
        CategoryId: invitationsCatId,
        BusinessGroupId: decorBgId,
        Listings: new()
        {
            new(
                Title: "Nền tảng số hóa tạo website đám cưới riêng biệt tích hợp quản lý khách mời qua mã QR Diamond",
                Slug: "diamond-wedding-tech-website-dam-cuoi-ma-qr",
                MinPrice: 1500000m,
                MaxPrice: 6000000m,
                Description: "Website cưới chuyên nghiệp có tên miền riêng, đếm ngược ngày cưới, album ảnh online, thiệp mời điện tử RSVP và mã QR check-in bàn tiệc tiện lợi.",
                Location: "Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 8. Quay chụp (Pre-wedding & Phóng sự)
    // =========================================================================
    new(
        BrandName: "Nupakachi Wedding",
        Slug: "nupakachi-wedding",
        Aliases: new[] { "nupakachi-wedding" },
        ContactPerson: "Nupakachi Media",
        Hotline: "0968668899",
        City: "Hà Nội",
        Address: "29 Trương Hán Siêu, Hoàn Kiếm, Hà Nội & Quận 3 TP. HCM",
        Bio: "Studio chụp ảnh cưới hàng đầu châu Á với các chi nhánh tại Hà Nội, Đà Nẵng, TP. HCM và Paris, nổi bật với nước ảnh màu trong trẻo sang trọng.",
        LogoUrl: "https://images.unsplash.com/photo-1537633552985-df8429e8048b?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1537633552985-df8429e8048b?auto=format&fit=crop&w=1200&q=80",
        CategoryId: photoCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Gói chụp ảnh Pre-wedding ngoại cảnh trong nước và quốc tế Nupakachi Wedding",
                Slug: "nupakachi-wedding-chup-anh-pre-wedding-ngoai-canh",
                MinPrice: 16000000m,
                MaxPrice: 45000000m,
                Description: "Ekip chụp ảnh chuyên nghiệp tại Đà Lạt, Nha Trang, Phú Quốc, Bali, Châu Âu; bao gồm trọn gói trang phục cao cấp, makeup và flycam 4K.",
                Location: "Hà Nội, Đà Nẵng, TP. HCM",
                ImageUrl: "https://images.unsplash.com/photo-1537633552985-df8429e8048b?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Dịch vụ chụp ảnh phóng sự ngày cưới chuyên nghiệp Nupakachi Wedding",
                Slug: "nupakachi-wedding-chup-anh-phong-su-ngay-cuoi",
                MinPrice: 12000000m,
                MaxPrice: 28000000m,
                Description: "Bắt trọn từng khoảnh khắc cảm xúc chân thực của cô dâu chú rể và hai họ trong ngày ăn hỏi và lễ cưới với 2-3 góc máy bắt khoảnh khắc nghệ thuật.",
                Location: "Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1511285560929-80b456fea0bc?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Cộng Studio",
        Slug: "cong-studio",
        Aliases: new[] { "cong-studio" },
        ContactPerson: "Cộng Production",
        Hotline: "0968475789",
        City: "Hà Nội",
        Address: "23/98 Thái Hà, Đống Đa, Hà Nội",
        Bio: "Ekip quay phim và chụp ảnh phóng sự cưới trẻ trung, phong cách điện ảnh chân thực với hơn 10 năm kinh nghiệm ghi dấu yêu thương.",
        LogoUrl: "https://images.unsplash.com/photo-1583939003579-730e3918a45a?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1583939003579-730e3918a45a?auto=format&fit=crop&w=1200&q=80",
        CategoryId: photoCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Chụp ảnh phóng sự cưới phong cách điện ảnh chân thực Cộng Studio",
                Slug: "cong-studio-chup-anh-phong-su-cuoi-dien-anh",
                MinPrice: 8000000m,
                MaxPrice: 20000000m,
                Description: "Bộ ảnh phóng sự cưới mang tone màu cinematic ấm áp, bắt trọn nụ cười, giọt nước mắt hạnh phúc của ngày trọng đại.",
                Location: "Hà Nội & Miền Bắc",
                ImageUrl: "https://images.unsplash.com/photo-1583939003579-730e3918a45a?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Quay video highlight ngắn lưu giữ khoảnh khắc tiệc cưới Cộng Studio",
                Slug: "cong-studio-quay-video-highlight-khoanh-khac-cuoi",
                MinPrice: 6000000m,
                MaxPrice: 16000000m,
                Description: "Video highlight thời lượng 3-5 phút lồng ghép âm nhạc xúc động, dựng Same Day Edit (SDE) để phát ngay tại sảnh tiệc trong ngày cưới.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Thien Tong Photography",
        Slug: "thien-tong-photography",
        Aliases: new[] { "thien-tong-photography" },
        ContactPerson: "Thiên Tống",
        Hotline: "0908123789",
        City: "TP. Hồ Chí Minh",
        Address: "42 Lý Tự Trọng, Bến Nghé, Quận 1, TP. HCM",
        Bio: "Nhiếp ảnh gia Fine-art với màu sắc ảnh film cổ điển, giàu chất thơ và tính nghệ thuật độc bản dành cho những tâm hồn lãng mạn.",
        LogoUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80",
        CategoryId: photoCatId,
        BusinessGroupId: photoBgId,
        Listings: new()
        {
            new(
                Title: "Chụp ảnh phóng sự cưới và Pre-wedding mang màu sắc film cổ điển, nghệ thuật Fine-art",
                Slug: "thien-tong-photography-phong-su-va-pre-wedding-film-fine-art",
                MinPrice: 15000000m,
                MaxPrice: 38000000m,
                Description: "Tác phẩm ảnh cưới chụp bằng máy medium format kết hợp digital, tone màu film hạt mịn hoài niệm và góc máy giàu cảm xúc.",
                Location: "TP. Hồ Chí Minh & Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 9. Âm thanh & MC
    // =========================================================================
    new(
        BrandName: "MC Hạnh Phúc",
        Slug: "mc-hanh-phuc",
        Aliases: new[] { "mc-hanh-phuc" },
        ContactPerson: "Người Dẫn Chương Trình Hạnh Phúc",
        Hotline: "0912111222",
        City: "Hà Nội",
        Address: "Ba Đình, Hà Nội",
        Bio: "MC truyền hình kỳ cựu với giọng nói truyền cảm, phong cách đĩnh đạc và lối dẫn chuyện lắng đọng chạm đến trái tim người nghe.",
        LogoUrl: "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Dịch vụ dẫn chương trình tiệc cưới chuyên nghiệp MC Hạnh Phúc (Tư vấn kịch bản riêng, điều phối trang trọng)",
                Slug: "mc-hanh-phuc-dan-chuong-trinh-tiec-cuoi-chuyen-nghiep",
                MinPrice: 8000000m,
                MaxPrice: 25000000m,
                Description: "Tư vấn và chấp bút kịch bản lời dẫn độc quyền theo câu chuyện tình yêu của cô dâu chú rể, dẫn dắt nghi lễ trang trọng, ấm áp và giàu cảm xúc.",
                Location: "Hà Nội & Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1470225620780-dba8ba36b745?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "MC Quang Bảo",
        Slug: "mc-quang-bao",
        Aliases: new[] { "mc-quang-bao" },
        ContactPerson: "Ekip MC Quang Bảo",
        Hotline: "0903888777",
        City: "TP. Hồ Chí Minh",
        Address: "Quận 1, TP. HCM",
        Bio: "MC song ngữ Anh - Việt tài năng với phong cách trẻ trung, năng động và khả năng khuấy động sân khấu tiệc cưới After-party bùng nổ.",
        LogoUrl: "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Dẫn chương trình tiệc cưới song ngữ (Anh - Việt) và điều phối tiệc năng động (After-party)",
                Slug: "mc-quang-bao-dan-song-ngu-va-dieu-phoi-after-party",
                MinPrice: 12000000m,
                MaxPrice: 30000000m,
                Description: "Dẫn dắt trôi chảy song ngữ Anh - Việt cho đám cưới có khách quốc tế, kết nối khán phòng và điều phối phần tiệc After-party sôi động.",
                Location: "TP. Hồ Chí Minh & Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1516450360452-9312f5e86fc7?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "xFat Sound & Light",
        Slug: "xfat-sound",
        Aliases: new[] { "xfat-sound" },
        ContactPerson: "Kỹ Thuật Trưởng xFat",
        Hotline: "0909456123",
        City: "Hà Nội",
        Address: "Khu đô thị Mễ Trì Hạ, Nam Từ Liêm, Hà Nội",
        Bio: "Nhà cung cấp giải pháp âm thanh và ánh sáng biểu diễn sân khấu đám cưới chuyên nghiệp với dàn loa Line Array nhập khẩu chuẩn quốc tế.",
        LogoUrl: "https://images.unsplash.com/photo-1508700115892-45ecd05ae2ad?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1508700115892-45ecd05ae2ad?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Cung cấp hệ thống âm thanh, ánh sáng sân khấu chuyên nghiệp (Loa Line Array, mixer, kỹ thuật viên)",
                Slug: "xfat-sound-he-thong-am-thanh-anh-sang-san-khau",
                MinPrice: 15000000m,
                MaxPrice: 50000000m,
                Description: "Trọn gói thiết bị âm thanh biểu diễn chuyên nghiệp: hệ thống loa treo Line Array d&b audiotechnik, đèn beam moving head, mixer digital và kỹ sư âm thanh trực tiếp điều phối.",
                Location: "Hà Nội & TP. HCM",
                ImageUrl: "https://images.unsplash.com/photo-1508700115892-45ecd05ae2ad?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 10. Di chuyển (Xe cưới)
    // =========================================================================
    new(
        BrandName: "Đức Vinh Trans",
        Slug: "duc-vinh-trans",
        Aliases: new[] { "duc-vinh-trans" },
        ContactPerson: "Điều Hành Xe Đức Vinh",
        Hotline: "0912204088",
        City: "Hà Nội",
        Address: "Tầng 10, Tòa nhà Yên Hòa Sunshine, Cầu Giấy, Hà Nội",
        Bio: "Đơn vị cho thuê xe cưới siêu sang số 1 miền Bắc, sở hữu dàn siêu xe hoa Rolls-Royce, Bentley, Mercedes Maybach đẳng cấp hoàng gia.",
        LogoUrl: "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Cho thuê siêu xe hoa rước dâu cao cấp (Rolls Royce, Bentley, Maybach) Đức Vinh Trans",
                Slug: "duc-vinh-trans-cho-thue-sieu-xe-hoa-ruoc-dau",
                MinPrice: 8000000m,
                MaxPrice: 35000000m,
                Description: "Dàn xe hoa triệu đô Rolls-Royce Phantom, Ghost, Bentley Flying Spur, Mercedes-Maybach S-Class đời mới, sáng bóng lộng lẫy trong ngày đón dâu.",
                Location: "Hà Nội & Miền Bắc",
                ImageUrl: "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Cung cấp tài xế VIP phục vụ theo ca nội thành Đức Vinh Trans",
                Slug: "duc-vinh-trans-tai-xe-vip-theo-ca-noi-thanh",
                MinPrice: 2000000m,
                MaxPrice: 6000000m,
                Description: "Tài xế VIP trang phục vest sang trọng, lịch thiệp, thông thạo lộ trình, mở cửa xe chu đáo và cam kết lộ trình rước dâu an toàn tuyệt đối.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1449965408869-eaa3f722e40d?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Xe Cưới Đông A",
        Slug: "xe-cuoi-dong-a",
        Aliases: new[] { "xe-cuoi-dong-a", "dong-a" },
        ContactPerson: "Ban Điều Hành Đông A",
        Hotline: "0933449988",
        City: "Hà Nội",
        Address: "91 Nguyễn Chí Thanh, Đống Đa, Hà Nội",
        Bio: "Công ty cho thuê xe cưới và xe chở quan viên hai họ uy tín với hơn 15 năm phục vụ hàng triệu cặp đôi.",
        LogoUrl: "https://images.unsplash.com/photo-1549399542-7e3f8b79c341?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1549399542-7e3f8b79c341?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Cho thuê xe hoa rước dâu kèm dịch vụ kết hoa tươi nghệ thuật Đông A",
                Slug: "xe-cuoi-dong-a-xe-hoa-kem-ket-hoa-tuoi-nghe-thuat",
                MinPrice: 2500000m,
                MaxPrice: 12000000m,
                Description: "Các dòng xe hoa Mercedes C, E, S-Class, BMW, Audi mui trần thời thượng trang trí trọn gói hoa tươi nghệ thuật đồng bộ.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1549399542-7e3f8b79c341?auto=format&fit=crop&w=1200&q=80"
            ),
            new(
                Title: "Cung cấp xe đưa đón quan viên hai họ (Từ 16 đến 45 chỗ) Đông A",
                Slug: "xe-cuoi-dong-a-xe-dua-don-quan-vien-hai-ho",
                MinPrice: 2000000m,
                MaxPrice: 7000000m,
                Description: "Dàn xe đời mới Hyundai Solati, Ford Transit 16 chỗ, Thaco 29 chỗ và Universe 45 chỗ đưa đón chu đáo hai bên họ hàng.",
                Location: "Hà Nội và các tỉnh",
                ImageUrl: "https://images.unsplash.com/photo-1570125909232-eb263c188f7e?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Vân Hải Wedding Cars",
        Slug: "van-hai",
        Aliases: new[] { "van-hai", "van-hai-trans" },
        ContactPerson: "Vân Hải Car",
        Hotline: "0969183183",
        City: "Hà Nội",
        Address: "Tầng 5, 14 Đặng Thùy Trâm, Cầu Giấy, Hà Nội",
        Bio: "Nổi tiếng với dịch vụ cho thuê các dòng xe cưới cổ điển phong cách Vintage / Retro độc đáo, mang vẻ đẹp vượt thời gian.",
        LogoUrl: "https://images.unsplash.com/photo-1502877338535-766e1452684a?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1502877338535-766e1452684a?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Cho thuê dòng xe cưới cổ điển phong cách Vintage/Retro độc đáo Vân Hải",
                Slug: "van-hai-trans-xe-cuoi-co-dien-vintage-retro",
                MinPrice: 4500000m,
                MaxPrice: 18000000m,
                Description: "Xe hoa Volkswagen bọ cánh cứng Beetle cổ điển mui trần, Citroen Traction cổ kính tạo điểm nhấn độc nhất vô nhị trong album ảnh rước dâu.",
                Location: "Hà Nội",
                ImageUrl: "https://images.unsplash.com/photo-1502877338535-766e1452684a?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),

    // =========================================================================
    // 11. Tuần trăng mật
    // =========================================================================
    new(
        BrandName: "Vietravel Honeymoon",
        Slug: "vietravel",
        Aliases: new[] { "vietravel", "vietravel-honeymoon" },
        ContactPerson: "Khối Du Lịch Trăng Mật",
        Hotline: "19001839",
        City: "Toàn quốc",
        Address: "190 Pasteur, Phường Võ Thị Sáu, Quận 3, TP. HCM & Chi nhánh toàn quốc",
        Bio: "Tập đoàn lữ hành hàng đầu châu Á, chuyên thiết kế tour du lịch trăng mật cao cấp trọn gói trong và ngoài nước.",
        LogoUrl: "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Tour du lịch trăng mật cao cấp trọn gói trong và ngoài nước (Châu Âu, Maldives...) Vietravel",
                Slug: "vietravel-tour-du-lich-trang-mat-cao-cap-maldives-chau-au",
                MinPrice: 25000000m,
                MaxPrice: 120000000m,
                Description: "Hành trình trăng mật lãng mạn tại thiên đường biển Maldives, Paris mộng mơ, Santorini Hy Lạp hoặc các bãi biển riêng tư trong nước với dịch vụ 5 sao chuẩn quốc tế.",
                Location: "Toàn quốc & Quốc tế",
                ImageUrl: "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "Saigontourist Honeymoon",
        Slug: "saigontourist",
        Aliases: new[] { "saigontourist", "saigontourist-honeymoon" },
        ContactPerson: "Ban Điều Hành Saigontourist",
        Hotline: "19001808",
        City: "Toàn quốc",
        Address: "45 Lê Thánh Tôn, Bến Nghé, Quận 1, TP. HCM",
        Bio: "Thương hiệu lữ hành lâu đời uy tín nhất Việt Nam, đối tác chiến lược của hệ thống resort biển đảo 5 sao hàng đầu cả nước.",
        LogoUrl: "https://images.unsplash.com/photo-1512100356356-de1b84283e18?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1512100356356-de1b84283e18?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Gói nghỉ dưỡng trăng mật 5 sao tại các resort biển đảo với dịch vụ setup phòng honeymoon (Hoa & rượu vang)",
                Slug: "saigontourist-goi-nghi-duong-trang-mat-5-sao-resort-bien",
                MinPrice: 18000000m,
                MaxPrice: 65000000m,
                Description: "Kỳ nghỉ ngọt ngào tại villa biển hướng hoàng hôn ở Phú Quốc, Côn Đảo, Đà Nẵng; tặng kèm gói trang trí giường hoa thiên nga, rượu vang nổ và bữa tối nến lãng mạn bên bờ biển.",
                Location: "Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1512100356356-de1b84283e18?auto=format&fit=crop&w=1200&q=80"
            )
        }
    ),
    new(
        BrandName: "BestPrice Travel Honeymoon",
        Slug: "bestprice-travel",
        Aliases: new[] { "bestprice-travel" },
        ContactPerson: "Tư Vấn Tour BestPrice",
        Hotline: "19002605",
        City: "Toàn quốc",
        Address: "57 Trần Quốc Toản, Trần Hưng Đạo, Hoàn Kiếm, Hà Nội",
        Bio: "Nền tảng du lịch trực tuyến hàng đầu, cung cấp các combo trăng mật tự túc linh hoạt, vé máy bay và resort tối ưu chi phí.",
        LogoUrl: "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=300&q=80",
        CoverImageUrl: "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=1200&q=80",
        CategoryId: otherEventsCatId,
        BusinessGroupId: otherBgId,
        Listings: new()
        {
            new(
                Title: "Cung cấp các combo trăng mật tự túc linh hoạt (Vé máy bay kết hợp khách sạn/resort tối ưu chi phí)",
                Slug: "bestprice-travel-combo-trang-mat-tu-tuc-linh-hoat",
                MinPrice: 8000000m,
                MaxPrice: 30000000m,
                Description: "Combo trọn gói vé máy bay khứ hồi giờ bay đẹp + 3 ngày 2 đêm khách sạn/resort 4-5 sao tại Đà Lạt, Phú Quốc, Quy Nhơn; tự do khám phá và tận hưởng không gian riêng.",
                Location: "Toàn quốc",
                ImageUrl: "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?auto=format&fit=crop&w=1200&q=80"
            )
        }
    )
};

// Target Databases:
// 1. Production SQL Server (sql1004.site4now.net) - Powers https://weddingmatch.website
// 2. Local SQL Server (WeddingMatch_Dev)
// 3. Local SQLite (WeddingPlatformDb.db)
var targetConfigs = new List<(string Name, DbContextOptions<ApplicationDbContext> Options)>
{
    ("Production SQL Server (sql1004.site4now.net - weddingmatch.website)", new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer("Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Max Pool Size=20;Connect Timeout=30")
        .Options),
    ("Local SQL Server (WeddingMatch_Dev)", new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer("Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=10")
        .Options)
};

var sqlitePath = Path.GetFullPath("src/Wedding.WebApi/WeddingPlatformDb.db");
if (File.Exists(sqlitePath))
{
    targetConfigs.Add(("Local SQLite (WeddingPlatformDb.db)", new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite($"Data Source={sqlitePath}")
        .Options));
}

foreach (var (targetName, options) in targetConfigs)
{
    Console.WriteLine($"\n--> Đang kết nối tới: {targetName}...");
    try
    {
        await using var db = new ApplicationDbContext(options);
        if (!await db.Database.CanConnectAsync())
        {
            Console.WriteLine($"[BỎ QUA] Không thể kết nối tới {targetName}.");
            continue;
        }

        var totalVendorsCreated = 0;
        var totalVendorsUpdated = 0;
        var totalListingsSeeded = 0;

        foreach (var vendorData in vendors)
        {
            // 1. Match or Create Vendor
            var aliases = vendorData.Aliases ?? new[] { vendorData.Slug };
            var existingVendor = await db.Vendors.Include(v => v.User)
                .FirstOrDefaultAsync(v => v.Slug == vendorData.Slug || aliases.Contains(v.Slug));
            
            Vendor vendor;
            if (existingVendor == null)
            {
                var email = $"vendor.{vendorData.Slug}@wedding.com";
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    user = User.Create(vendorData.BrandName, email, vendorData.Hotline, defaultPasswordHash, UserRole.VendorOwner);
                    user.UpdateProfile(vendorData.BrandName, vendorData.LogoUrl);
                    db.Users.Add(user);
                    await db.SaveChangesAsync();
                }

                vendor = Vendor.Create(user.Id, vendorData.BrandName, vendorData.Slug, vendorData.ContactPerson, vendorData.Hotline, vendorData.City, 0.08m);
                vendor.UpdateProfile(vendorData.BrandName, vendorData.ContactPerson, vendorData.Hotline, vendorData.City, vendorData.Address, vendorData.Bio, vendorData.LogoUrl, vendorData.CoverImageUrl);
                vendor.SetVerified(true);
                vendor.UpdateRating(5.0m, 1);
                db.Vendors.Add(vendor);
                await db.SaveChangesAsync();

                var existingBg = await db.VendorBusinessGroups.AnyAsync(bg => bg.VendorId == vendor.Id && bg.BusinessGroupId == vendorData.BusinessGroupId);
                if (!existingBg)
                {
                    db.VendorBusinessGroups.Add(VendorBusinessGroup.Create(vendor.Id, vendorData.BusinessGroupId));
                    await db.SaveChangesAsync();
                }

                totalVendorsCreated++;
            }
            else
            {
                vendor = existingVendor;
                // Update profile info if empty
                if (string.IsNullOrWhiteSpace(vendor.CoverImageUrl) || string.IsNullOrWhiteSpace(vendor.City) || string.IsNullOrWhiteSpace(vendor.Bio))
                {
                    vendor.UpdateProfile(
                        string.IsNullOrWhiteSpace(vendor.BrandName) ? vendorData.BrandName : vendor.BrandName,
                        string.IsNullOrWhiteSpace(vendor.ContactPerson) ? vendorData.ContactPerson : vendor.ContactPerson,
                        string.IsNullOrWhiteSpace(vendor.Hotline) ? vendorData.Hotline : vendor.Hotline,
                        string.IsNullOrWhiteSpace(vendor.City) ? vendorData.City : vendor.City,
                        string.IsNullOrWhiteSpace(vendor.Address) ? vendorData.Address : vendor.Address,
                        string.IsNullOrWhiteSpace(vendor.Bio) ? vendorData.Bio : vendor.Bio,
                        string.IsNullOrWhiteSpace(vendor.LogoUrl) ? vendorData.LogoUrl : vendor.LogoUrl,
                        string.IsNullOrWhiteSpace(vendor.CoverImageUrl) ? vendorData.CoverImageUrl : vendor.CoverImageUrl
                    );
                    vendor.SetVerified(true);
                    if (vendor.RatingAvg == 0) vendor.UpdateRating(5.0m, 1);
                    await db.SaveChangesAsync();
                    totalVendorsUpdated++;
                }
            }

            // 2. Check or Create Listings
            foreach (var listingData in vendorData.Listings)
            {
                var existingListing = await db.Listings.FirstOrDefaultAsync(l => l.Slug == listingData.Slug);
                if (existingListing == null)
                {
                    var listing = Listing.Create(
                        vendorId: vendor.Id,
                        categoryId: vendorData.CategoryId,
                        title: listingData.Title,
                        slug: listingData.Slug,
                        minPrice: listingData.MinPrice,
                        maxPrice: listingData.MaxPrice,
                        description: listingData.Description,
                        location: listingData.Location
                    );
                    listing.Approve();
                    db.Listings.Add(listing);
                    await db.SaveChangesAsync();

                    db.ListingMedias.Add(ListingMedia.Create(listing.Id, listingData.ImageUrl, null, "image", 0, true));
                    await db.SaveChangesAsync();

                    totalListingsSeeded++;
                }
            }
        }

        var activeListingCount = await db.Listings.CountAsync(l => l.Status == ListingStatus.Active);
        var totalVendorCount = await db.Vendors.CountAsync();

        Console.WriteLine($"[THÀNH CÔNG] Đã nạp dữ liệu cho {targetName}:");
        Console.WriteLine($"   + Tạo mới {totalVendorsCreated} vendors, cập nhật {totalVendorsUpdated} vendors, thêm mới {totalListingsSeeded} bài đăng dịch vụ.");
        Console.WriteLine($"   + Tổng hiện tại: {totalVendorCount} vendors, {activeListingCount} bài đăng hoạt động (Active).");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[LỖI] Xảy ra lỗi trên {targetName}: {ex.Message}");
    }
}

Console.WriteLine("\n=================================================================");
Console.WriteLine(" HOÀN THÀNH SEED DỮ LIỆU DỊCH VỤ CƯỚI 11 HẠNG MỤC!");
Console.WriteLine("=================================================================");

record ListingData(string Title, string Slug, decimal MinPrice, decimal MaxPrice, string Description, string Location, string ImageUrl);
record VendorData(string BrandName, string Slug, string[] Aliases, string ContactPerson, string Hotline, string City, string Address, string Bio, string LogoUrl, string CoverImageUrl, Guid CategoryId, Guid BusinessGroupId, List<ListingData> Listings);
