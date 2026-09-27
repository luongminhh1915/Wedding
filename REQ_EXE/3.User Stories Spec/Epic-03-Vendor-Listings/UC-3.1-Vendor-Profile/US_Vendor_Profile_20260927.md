---
id: "US-3.1"
title: "User Story: Đăng Ký Tài Khoản Đối Tác & Cập Nhật Hồ Sơ Nhà Cung Cấp"
epic: "Epic 3: Quản Lý Hồ Sơ & Bài Đăng Gói Dịch Vụ NCC"
use_cases: ["UC-3.1"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-03-04-Vendor-Listing-And-Booking.html"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-3.1: ĐĂNG KÝ TÀI KHOẢN ĐỐI TÁC & CẬP NHẬT HỒ SƠ NCC

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Chủ Doanh nghiệp Cưới (Vendor Owner),  
> **Tôi muốn** đăng ký tài khoản đối tác và cập nhật hồ sơ thương hiệu, mã số thuế (hoặc CCCD), số Hotline và tài khoản ngân hàng nhận tiền đối soát,  
> **Để** thương hiệu của tôi được xác thực trên Sàn, sẵn sàng đăng tải các gói dịch vụ cưới tiếp cận khách hàng.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Đăng ký tài khoản Vendor Owner mới
```gherkin
Given Doanh nghiệp cưới truy cập trang "/vendor/register"
When Điền đầy đủ: Email, Mật khẩu, Tên thương hiệu, Số điện thoại chủ sở hữu
And Bấm nút "Đăng Ký Đối Tác"
Then Hệ thống tạo tài khoản User với vai trò "VENDOR" và bản ghi Vendor ở trạng thái chờ kích hoạt
And Gửi email xác thực tài khoản kèm hướng dẫn hoàn thiện hồ sơ
```

### Kịch bản 2: Cập nhật thông tin thanh toán ngân hàng phục vụ đối soát
```gherkin
Given Chủ NCC đã đăng nhập vào Vendor Portal tại mục "Cài đặt hồ sơ & Thanh toán"
When Nhập thông tin: Số tài khoản ngân hàng, Tên ngân hàng, Tên chủ tài khoản và Mã số thuế
And Bấm "Lưu Thay Đổi"
Then Hệ thống lưu thông tin và kiểm tra định dạng số tài khoản qua Napas/VietQR
And Hiển thị thông báo thành công: "Hồ sơ đối tác đã được cập nhật thành công!"
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-001` (Lean Vendor):** Một NCC chỉ có 1 tài khoản duy nhất `Vendor Owner`.
- **Thực thể dữ liệu:** `Vendor`, `User` (theo [[ba-data-model]]).
