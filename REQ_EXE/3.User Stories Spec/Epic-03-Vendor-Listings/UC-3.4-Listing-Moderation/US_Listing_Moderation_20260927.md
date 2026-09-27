---
id: "US-3.4"
title: "User Story: Kiểm Duyệt Bài Đăng Dịch Vụ Cưới (Moderation SLA 24h)"
epic: "Epic 3: Quản Lý Hồ Sơ & Bài Đăng Gói Dịch Vụ NCC"
use_cases: ["UC-3.4"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-03-04-Vendor-Listing-And-Booking.html#viewAdminMod"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-3.4: KIỂM DUYỆT BÀI ĐĂNG DỊCH VỤ CƯỚI (MODERATION SLA 24H)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Kiểm duyệt viên nội bộ (Moderator),  
> **Tôi muốn** thẩm định nội dung, bảng giá và hình ảnh portfolio của các bài đăng do NCC nộp lên trong vòng tối đa 24 giờ,  
> **Để** đảm bảo chất lượng hình ảnh không vi phạm bản quyền/thuần phong mỹ tục, bảo vệ quyền lợi người tiêu dùng và chỉ hiển thị bài đăng đạt chuẩn (Active).

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

### Kịch bản 1: Phê duyệt bài đăng hợp lệ
```gherkin
Given Moderator mở danh sách bài đăng ở trạng thái "PendingModeration"
When Kiểm tra hình ảnh rõ nét, bảng giá hợp lý, không chèn số điện thoại lậu lên ảnh
And Bấm nút "✓ Phê Duyệt Bài Đăng"
Then Hệ thống cập nhật trạng thái bài đăng sang "Active"
And Gửi thông báo chúc mừng tới tài khoản của NCC qua Vendor Portal
```

### Kịch bản 2: Từ chối bài đăng vi phạm chính sách
```gherkin
Given Bài đăng có chứa watermark của thương hiệu khác hoặc giá niêm yết bất hợp lý
When Moderator bấm nút "Từ Chối" và nhập lý do chi tiết (vd: "Ảnh bìa có chứa số điện thoại cá nhân vi phạm quy định")
Then Hệ thống cập nhật trạng thái bài đăng sang "Rejected"
And Gửi thông báo kèm lý do từ chối cho NCC để chỉnh sửa nộp lại
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-008` (SLA Kiểm duyệt):** Cam kết xử lý trong vòng 24 giờ.
- **Thực thể dữ liệu:** `Listing`, `ListingMedia`, `Vendor` (theo [[ba-data-model]]).
