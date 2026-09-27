---
id: "US-8.5"
title: "User Story: Quản Trị Viên & An Ninh Hệ Thống Tra Cứu Nhật Ký Kiểm Toán (System Audit Trail)"
epic: "Epic 8: Quản Trị Hệ Thống, Vận Hành & Báo Cáo Toàn Sàn"
use_cases: ["UC-8.5"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-08-Admin-Backoffice-Dashboard.html#nav-audit"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-8.5: TRA CỨU AUDIT LOG VÀ NHẬT KÝ HỆ THỐNG (AUDIT TRAIL)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** Quản trị viên Tối cao & Chuyên viên An ninh Hệ thống (Security / Auditor),  
> **Tôi muốn** tra cứu, tìm kiếm và xuất báo cáo toàn bộ nhật ký truy vết hệ thống (Audit Log) ghi nhận chi tiết: Ai đã thực hiện, vào thời gian nào, từ IP nào, thao tác trên đối tượng dữ liệu nào và nội dung thay đổi trước/sau (Diff Before/After),  
> **Để** phục vụ công tác thanh tra nội bộ, phát hiện các hành vi gian lận tài chính, rò rỉ dữ liệu và đáp ứng các tiêu chuẩn bảo mật an toàn thông tin (ISO 27001 / OWASP).

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Tra cứu lịch sử thay đổi dữ liệu nhạy cảm
```gherkin
Given Quản trị viên mở màn hình "Nhật Ký Audit Log"
When Tìm kiếm theo Module: "Finance" và Hành động: "SETTLE_COMMISSION"
Then Hệ thống lọc ra toàn bộ các bản ghi kiểm toán liên quan:
    | Field | Value |
    | timestamp | "2026-09-27 10:15:32" |
    | actor | "System (Webhook VietQR)" |
    | ipAddress | "14.225.212.18" |
    | action | "SETTLE_COMMISSION" |
    | targetEntity | "CommissionSettlement" |
    | targetId | "SETTLE-202609-V001" |
    | changeset | `{"status": {"old": "PendingPayment", "new": "Paid"}, "amount": 12500000}` |
```

### Kịch bản 2: Bất biến của dữ liệu kiểm toán (Immutable Audit Log)
```gherkin
Given Dữ liệu nhật ký đã được ghi vào bảng AuditLog
When Bất kỳ người dùng nào (kể cả Super Admin) cố gắng cập nhật (UPDATE) hoặc xóa (DELETE) bản ghi log
Then Hệ thống cơ sở dữ liệu từ chối thực thi (Bảng AuditLog chỉ có quyền INSERT và SELECT - Append-Only)
And Ghi nhận ngay cảnh báo nghiêm trọng nếu có hành vi can thiệp trái phép
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Bảo mật:** Log được lưu trữ an toàn tối thiểu 24 tháng theo quy định pháp luật về an ninh mạng.
- **Thực thể dữ liệu:** `AuditLog` (theo [[ba-data-model]]).
