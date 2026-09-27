---
title: "UI Specification: Khám Phá Danh Mục & Chi Tiết Gói Dịch Vụ Cưới (SCR-BROWSE-LISTING)"
epic: "Epic 1: Khám Phá Dịch Vụ Cưới & AI Stylist Matching"
use_cases: ["UC-1.1", "UC-1.2"]
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-01-Browse-And-Moodboard.html"
---

# ĐẶC TẢ GIAO DIỆN NGƯỜI DÙNG (UI SPECIFICATION)
## MÀN HÌNH: KHÁM PHÁ & CHI TIẾT GÓI DỊCH VỤ CƯỚI (SCR-BROWSE-LISTING)

---

## 1. Thông Tin Chung Màn Hình

- **Mã màn hình:** `SCR-BROWSE-LISTING`
- **Tên màn hình:** Khám Phá Danh Mục & Xem Chi Tiết Gói Dịch Vụ
- **Ứng dụng:** Customer Web Portal (Web Responsive / Mobile-first)
- **Đường dẫn (URL):**
  - Trang danh mục: `/danh-muc` hoặc `/danh-muc/[category_slug]`
  - Trang chi tiết: `/dich-vu/[listing_slug]`
- **Đối tượng sử dụng:** Khách vãng lai, Cô dâu / Chú rể
- **File Prototype tham chiếu:** [SCR-01-Browse-And-Moodboard.html](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Prototype/SCR-01-Browse-And-Moodboard.html)

---

## 2. Bố Cục & Phân Vùng Màn Hình (Layout Structure)

1. **Header Navigation (Cố định Sticky):** Logo sàn, menu điều hướng 7 ngành, nút kích hoạt `AI Stylist Quiz` và chuyển sang `Kênh Nhà Cung Cấp`.
2. **Hero Search Section:** Thanh tìm kiếm đa tiêu chí (Chọn Danh mục, Chọn Tỉnh/Thành phố, Chọn Khoảng ngân sách).
3. **Category Quick Pills Bar:** Thanh lướt ngang 7 danh mục cưới (Venue, Decor, Photo, Bridal, Makeup, Thiệp/Quà, Planner) có đếm số lượng dịch vụ.
4. **Sidebar Filter (Bộ lọc bên trái):** Lọc theo phong cách (Minimalist, Vintage, Rustic, Modern Floral), lọc theo số sao đánh giá (4.8+ Top Rated), lọc ưu đãi (Có Voucher, Hỗ trợ chia cọc 50/50).
5. **Listings Grid (Lưới danh sách gói dịch vụ):** Các thẻ dịch vụ dạng Card hiện ảnh cover chất lượng cao, nhãn giảm giá Voucher, tên NCC có tick xác thực, địa điểm, khoảng giá và nút kêu gọi hành động `Nhận Voucher`.
6. **Detail Drawer / Page (Chi tiết gói dịch vụ - UC 1.2):**
   - Slider ảnh/video portfolio (ListingMedia).
   - Bảng giá chi tiết các hạng mục và menu tiệc.
   - Bản đồ sảnh tiệc Google Maps nhúng.
   - Danh sách đánh giá Verified Buyer Review kèm số sao.
   - Form Floating Button: "Gửi Yêu Cầu Tư Vấn & Nhận Voucher".

---

## 3. Ma Trận Điều Khiển Giao Diện (UI Control Matrix)

| STT | Mã Control | Tên Phần Tử | Loại Control | Dữ Liệu / Tùy Chọn | Bắt Buộc | Ràng Buộc & Validation | Hành Vi Khi Tương Tác |
|:---|:---|:---|:---|:---|:---|:---|:---|
| 1 | `SEL_CATEGORY` | Chọn danh mục chính | Select Dropdown | 7 Danh mục cưới | Không | Mặc định: "Tất cả 7 ngành" | Khi đổi giá trị, tự động lọc lại lưới bài đăng không cần reload trang |
| 2 | `SEL_PROVINCE` | Chọn khu vực | Select Dropdown | TP.HCM, Hà Nội, Đà Nẵng, Khác | Không | Mặc định: "Toàn quốc" | Lọc theo trường `province_code` của Vendor/Listing |
| 3 | `SEL_BUDGET` | Khoảng ngân sách | Select Dropdown | Dưới 50M, 50-100M, 100-200M, Trên 200M | Không | Mặc định: "Mọi mức giá" | So khớp điều kiện `price_min <= max_range` và `price_max >= min_range` |
| 4 | `BTN_SEARCH` | Nút tìm kiếm | Button Primary | Text: "Tìm Kiếm" | — | — | Kích hoạt gọi API `GET /api/v1/listings` với bộ query params |
| 5 | `PILL_CAT_[ID]` | Nút danh mục nhanh | Pill Button | 7 Pills tương ứng 7 ngành | — | Chỉ chọn 1 pill tại 1 thời điểm | Đổi class `active`, cập nhật số lượng `totalCount` |
| 6 | `CHK_STYLE` | Lọc phong cách | Checkbox Group | Minimalist, Vintage, Rustic, Modern | Không | Cho phép chọn nhiều phong cách | So khớp với mảng `style_tags` trong metadata của Listing |
| 7 | `RAD_RATING` | Lọc theo số sao | Radio Button | 4.8+ sao, 4.5+ sao, Verified | Không | Mặc định: "4.8 sao trở lên" | Lọc theo trường `rating_avg >= giá_trị` |
| 8 | `CARD_LISTING` | Thẻ gói dịch vụ | Interactive Card | Dữ liệu từng Listing | — | — | Click vào vùng ảnh hoặc tiêu đề: Điều hướng sang trang chi tiết gói dịch vụ |
| 9 | `BTN_GET_VOUCHER`| Nút Nhận Voucher | Button Accent | Text: "Nhận Voucher" | — | — | Mở Modal gửi Lead tư vấn nhanh (UC 2.1) |
| 10 | `BTN_AI_QUIZ` | Nút mở Moodboard Quiz | Button Gradient | Icon ✨ + Text: "AI Stylist Quiz"| — | — | Mở Modal trắc nghiệm hình ảnh Moodboard (UC 1.3) |

---

## 4. Các Trạng Thái Giao Diện (UI States)

1. **Trạng thái đang tải (Loading Skeleton State):** Hiển thị 6 khung xám nhấp nháy (shimmer skeleton) tại vùng lưới bài đăng trong khi chờ API phản hồi.
2. **Trạng thái có kết quả (Success Loaded State):** Hiển thị đầy đủ danh sách thẻ dịch vụ kèm số lượng tổng `Hiển thị X gói dịch vụ chất lượng cao`.
3. **Trạng thái rỗng (Empty Search Result State):** Khi bộ lọc không tìm thấy kết quả phù hợp:
   - Hiển thị hình minh họa "Không tìm thấy gói dịch vụ phù hợp với bộ lọc".
   - Nút hành động: "Đặt lại bộ lọc" hoặc "Nhờ AI Stylist tư vấn gợi ý thay thế".
4. **Trạng thái lỗi kết nối (Error State):** Hiển thị thông báo Toast đỏ: "Không thể kết nối đến máy chủ. Vui lòng thử lại sau!".
