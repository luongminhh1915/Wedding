---
id: "US-2.6"
title: "User Story: Điều Phối Khớp Nối Khẩn Cấp Khi Có Sự Cố (Emergency Matching)"
epic: "Epic 2: Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead"
use_cases: ["UC-2.6"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-2.6: ĐIỀU PHỐI KHỚP NỐI KHẨN CẤP (EMERGENCY MATCHING)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Nhân viên Chăm sóc Khách hàng (CSKH / Admin Operations),  
> **Tôi muốn** có công cụ điều phối khẩn cấp trên Admin Portal để gán lại Lead hoặc Hợp đồng sang một Nhà cung cấp dự phòng tương đương khi NCC chính gặp sự cố hoặc từ chối phục vụ,  
> **Để** đảm bảo cô dâu chú rể không bị gián đoạn kế hoạch đám cưới và duy trì uy tín của Sàn.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

### Kịch bản 1: Kích hoạt điều phối khẩn cấp từ Admin Portal
```gherkin
Given CSKH nhận được khiếu nại của Khách hàng hoặc NCC thông báo hủy dịch vụ bất khả kháng
When CSKH mở trang chi tiết Lead trên Admin Portal và bấm nút "Điều Phối Khẩn Cấp (Emergency Matching)"
Then Hệ thống đề xuất danh sách 3 Nhà cung cấp tương đương (cùng ngành hàng, cùng khu vực, cùng phân khúc ngân sách)
When CSKH chọn NCC thay thế và nhập lý do điều phối rồi bấm "Xác Nhận Chuyển Giao"
Then Hệ thống chuyển giao Lead sang NCC mới và gửi thông báo khẩn cấp qua SMS/Zalo cho Khách hàng
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-010` (Emergency Matching):** Thẩm quyền điều phối thuộc về vai trò CSKH / Super Admin trên Admin Back-office Portal.
- **Thực thể dữ liệu:** `Lead`, `Vendor`, `User` (theo [[ba-data-model]]).
