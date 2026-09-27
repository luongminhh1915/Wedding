---
id: "US-3.5"
title: "User Story: Quản Trị Cây Danh Mục 7 Ngành Dịch Vụ Cưới & Cấu Hình Hoa Hồng Mặc Định"
epic: "Epic 3: Quản Lý Hồ Sơ & Bài Đăng Gói Dịch Vụ NCC"
use_cases: ["UC-3.5"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-3.5: QUẢN TRỊ DANH MỤC 7 NGÀNH CƯỚI & TỶ LỆ HOA HỒNG MẶC ĐỊNH

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Quản trị viên Cấp cao (Super Admin),  
> **Tôi muốn** quản lý danh sách 7 danh mục cưới (Tên, mã code, icon, thứ tự hiển thị) và cấu hình tỷ lệ hoa hồng mặc định (%) cho từng ngành,  
> **Để** làm cơ sở dữ liệu nền cho việc phân loại bài đăng của NCC và tự động tính toán hoa hồng khi phát sinh hợp đồng.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

### Kịch bản 1: Cấu hình tỷ lệ hoa hồng mặc định cho ngành hàng
```gherkin
Given Super Admin truy cập trang Quản trị danh mục trên Admin Portal
When Chọn ngành "Trung tâm Tiệc cưới (Venue)" và cập nhật "commission_rate_default = 3.0%"
And Chọn ngành "Trang trí (Decor)" và cập nhật "commission_rate_default = 8.0%"
And Bấm "Lưu Cấu Hình"
Then Hệ thống lưu lại tỷ lệ hoa hồng mới
And Mọi hợp đồng mới thuộc ngành hàng này sẽ tự động áp dụng tỷ lệ hoa hồng theo bảng quy định FM-001
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`FM-001` (Bảng tỷ lệ hoa hồng):** Quản lý tỷ lệ 3% - 12% theo từng ngành.
- **Thực thể dữ liệu:** `Category` (theo [[ba-data-model]]).
