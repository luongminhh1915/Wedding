---
id: "US-8.1"
title: "User Story: Quản Trị Viên Quản Lý Tài Khoản Người Dùng & Phân Quyền Vai Trò RBAC"
epic: "Epic 8: Quản Trị Hệ Thống, Vận Hành & Báo Cáo Toàn Sàn"
use_cases: ["UC-8.1"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-08-Admin-Backoffice-Dashboard.html#nav-rbac"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-8.1: QUẢN TRỊ TÀI KHOẢN NGƯỜI DÙNG & PHÂN QUYỀN VAI TRÒ RBAC

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Quản trị viên Tối cao (Super Admin),  
> **Tôi muốn** quản lý danh sách tài khoản nội bộ (Admin, Moderator, Finance, CSKH) và tài khoản đối tác (Vendor Owner), gán các nhóm quyền hạn chi tiết theo ma trận vai trò (RBAC),  
> **Để** đảm bảo an ninh hệ thống, nguyên tắc phân tách trách nhiệm (Separation of Duties) và ngăn chặn truy cập trái phép vào các dữ liệu tài chính, khách hàng nhạy cảm.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Tạo tài khoản nhân sự và gán vai trò
```gherkin
Given Super Admin mở danh sách Quản trị Người Dùng
When Bấm "+ Thêm Nhân Sự Mới"
And Nhập: Họ tên "Trần Thu Ngân", Email "finance@exe.wedding", chọn vai trò "FINANCE_ACCOUNTANT"
Then Hệ thống tự động gán các quyền hạn tương ứng:
    - `RECONCILE_COMMISSION`
    - `ISSUE_INVOICE`
    - `VIEW_FINANCIAL_REPORTS`
And Gửi email kích hoạt tài khoản kèm đường link thiết lập mật khẩu bảo mật (hạn 24h)
And Ghi nhật ký vào AuditLog: Super Admin đã tạo nhân sự mới
```

### Kịch bản 2: Khóa tài khoản nhân sự hoặc NCC vi phạm
```gherkin
Given Tài khoản NCC hoặc nhân sự có dấu hiệu gian lận hoặc vi phạm chính sách
When Super Admin chọn tài khoản và bấm "Khóa Tài Khoản (Suspend)"
Then Trạng thái tài khoản chuyển sang "Suspended"
And Toàn bộ Refresh Token và Access Token hiện tại của tài khoản bị thu hồi ngay lập tức (Force Logout trên mọi thiết bị)
And Người dùng không thể đăng nhập cho đến khi được mở khóa
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **RBAC Matrix:** Các vai trò chuẩn gồm `SUPER_ADMIN`, `CONTENT_MODERATOR`, `FINANCE_ACCOUNTANT`, `CSKH_SUPPORT`, `VENDOR_OWNER`, `CUSTOMER`.
- **Thực thể dữ liệu:** `User`, `Role`, `Permission`, `UserRole`, `AuditLog` (theo [[ba-data-model]]).
