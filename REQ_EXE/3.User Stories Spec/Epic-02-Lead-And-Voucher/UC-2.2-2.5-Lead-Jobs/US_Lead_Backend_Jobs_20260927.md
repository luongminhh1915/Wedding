---
id: "US-2.2-2.5"
title: "User Story: Tiến Trình Nền Điều Phối Lead Zalo ZNS & Tự Động Hủy Quá Hạn SLA 24h"
epic: "Epic 2: Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead"
use_cases: ["UC-2.2", "UC-2.5"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-2.2 & US-2.5: TIẾN TRÌNH NỀN BẮN THÔNG BÁO ZALO ZNS & QUÉT HỦY LEAD QUÁ HẠN

---

## 1. Tuyên Bố User Story (Story Statement)

* **US-2.2 (Job [Event] Bắn Zalo ZNS):**
  > **Là** một Hệ thống Backend sàn môi giới cưới,  
  > **Tôi muốn** tự động gửi tin nhắn Zalo ZNS tới số điện thoại của Chủ Nhà cung cấp ngay sau khi có Lead mới phát sinh (trong vòng <= 10 giây),  
  > **Để** NCC nhận được thông báo kịp thời trên điện thoại mà không cần phải liên tục mở trình duyệt máy tính.

* **US-2.5 (Job [Schedule] Quét SLA 24h):**
  > **Là** một Hệ thống Backend sàn môi giới cưới,  
  > **Tôi muốn** chạy tiến trình định kỳ mỗi 15 phút để quét các Lead ở trạng thái `New` đã quá 24 giờ không ai tiếp nhận,  
  > **Để** tự động chuyển trạng thái sang `Cancelled`, gửi cảnh báo vi phạm SLA cho NCC và thông báo xin lỗi kèm gợi ý NCC thay thế cho khách hàng.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

### Kịch bản 1: Kích hoạt Job Event gửi Zalo ZNS (UC 2.2)
```gherkin
Given Sự kiện Khách hàng gửi Lead mới thành công (UC 2.1)
When Event Bus phát tín hiệu "lead.created"
Then Job [Event] gọi API Zalo ZNS với template thông báo Lead mới đã được duyệt
And Nội dung tin nhắn Zalo hiển thị: Tên khách hàng, Ngày cưới, Khoảng ngân sách, Mã Voucher và Link mở nhanh Vendor Portal
And Toàn bộ thời gian xử lý và nhận tin nhắn trên máy NCC không quá 10 giây
```

### Kịch bản 2: Kích hoạt Job Schedule quét và hủy Lead quá hạn 24h (UC 2.5)
```gherkin
Given Có Lead ở trạng thái "New" được tạo lúc "2026-09-26 10:00:00"
When Tiến trình nền chạy lúc "2026-09-27 10:15:00" (Đã vượt quá 24 giờ)
Then Hệ thống tự động cập nhật trạng thái Lead từ "New" sang "Cancelled"
And Ghi log lý do: "Hệ thống tự động hủy do NCC không tiếp nhận trong vòng 24 giờ"
And Gửi thông báo tới tài khoản Khách hàng đề xuất kết nối với Nhà cung cấp dự phòng
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-003` (SLA Lead):** Ràng buộc tối đa 24 giờ cho một Lead chưa tiếp nhận.
- **Thực thể dữ liệu:** `Lead`, `Vendor`, `User` (theo [[ba-data-model]]).
