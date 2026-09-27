---
id: "US-3.2-3.3"
title: "User Story: Tạo Bài Đăng Gói Dịch Vụ Mới & Quản Lý Trạng Thái Hiển Thị"
epic: "Epic 3: Quản Lý Hồ Sơ & Bài Đăng Gói Dịch Vụ NCC"
use_cases: ["UC-3.2", "UC-3.3"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-03-04-Vendor-Listing-And-Booking.html#viewListings"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-3.2 & US-3.3: TẠO BÀI ĐĂNG GÓI DỊCH VỤ & QUẢN LÝ TRẠNG THÁI HIỂN THỊ

---

## 1. Tuyên Bố User Story (Story Statement)

* **US-3.2 (Tạo & Nộp bài đăng):**
  > **Là** một Chủ Nhà Cung Cấp (Vendor Owner),  
  > **Tôi muốn** tự soạn gói dịch vụ cưới, điền khoảng giá min-max, chọn ngành hàng và tải lên album ảnh/video portfolio chất lượng cao,  
  > **Để** nộp lên cho Ban quản trị Sàn kiểm duyệt và hiển thị tiếp cận khách hàng.

* **US-3.3 (Quản lý trạng thái bài đăng):**
  > **Là** một Chủ Nhà Cung Cấp,  
  > **Tôi muốn** bật/tắt ẩn bài đăng dịch vụ (Active / Hidden) hoặc chỉnh sửa nội dung bài đăng,  
  > **Để** chủ động kiểm soát số lượng đơn nhận khi cửa hàng quá tải hoặc hết ngày trống lịch.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

### Kịch bản 1: Tạo gói dịch vụ và tải ảnh portfolio (UC 3.2)
```gherkin
Given Chủ NCC đang ở mục "Quản lý gói dịch vụ" và bấm "+ Đăng Gói Dịch Vụ Mới"
When NCC nhập tên gói, chọn ngành "Trang trí Tiệc cưới", nhập giá min 35M - max 45M
And Tải lên 6 ảnh portfolio HD (ảnh tự động nén và lưu trữ qua Cloudinary WebP)
And Bấm nút "Nộp Bài Đăng Lên Sàn"
Then Bài đăng được tạo ở trạng thái "PendingModeration"
And Đưa vào hàng đợi kiểm duyệt của Moderator với cam kết SLA <= 24 giờ
```

### Kịch bản 2: Bật/Tắt ẩn bài đăng dịch vụ (UC 3.3)
```gherkin
Given Bài đăng của NCC đang ở trạng thái "Active" (công khai)
When Doanh nghiệp kín lịch và bấm nút "Tạm Ẩn"
Then Trạng thái bài đăng chuyển sang "Hidden"
And Bài đăng lập tức không còn xuất hiện trên kết quả tìm kiếm của khách hàng
When Doanh nghiệp muốn nhận khách trở lại và bấm "Bật Lại"
Then Trạng thái chuyển về "Active" ngay lập tức
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-008` (SLA Kiểm duyệt 24h):** Mọi bài đăng mới bắt buộc phải qua kiểm duyệt trước khi Active.
- **Thực thể dữ liệu:** `Listing`, `ListingMedia`, `Category`, `Vendor` (theo [[ba-data-model]]).
