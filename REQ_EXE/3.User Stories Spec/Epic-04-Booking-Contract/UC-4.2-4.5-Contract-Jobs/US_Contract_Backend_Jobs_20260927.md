---
id: "US-4.2-4.5"
title: "User Story: Tiến Trình Nền Gửi Thông Báo Xác Thực & Tự Động Hủy Hợp Đồng Quá Hạn 72h"
epic: "Epic 4: Quản Lý Hợp Đồng & Xác Thực Giao Dịch 2 Chiều"
use_cases: ["UC-4.2", "UC-4.5"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-4.2 & US-4.5: TIẾN TRÌNH NỀN THÔNG BÁO XÁC THỰC & HỦY HỢP ĐỒNG QUÁ HẠN 72H

---

## 1. Tuyên Bố User Story (Story Statement)

* **US-4.2 (Job [Event] Bắn thông báo xác thực):**
  > **Là** một Hệ thống Backend sàn môi giới cưới,  
  > **Tôi muốn** tự động gửi tin nhắn Zalo ZNS và thông báo App In-App tới Khách hàng ngay sau khi NCC nộp hợp đồng,  
  > **Để** khách hàng kịp thời mở điện thoại kiểm tra số tiền cọc và xác nhận giao dịch.

* **US-4.5 (Job [Schedule] Quét SLA 72h):**
  > **Là** một Hệ thống Backend sàn môi giới cưới,  
  > **Tôi muốn** chạy tiến trình định kỳ mỗi 1 giờ để quét các hợp đồng ở trạng thái `PendingVerification` quá 72 giờ không có phản hồi,  
  > **Để** tự động hủy giao dịch (chuyển `Cancelled`), tránh việc treo hoa hồng và gửi cảnh báo nhắc nhở ở các mốc 24h và 48h.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

### Kịch bản 1: Nhắc nhở ở mốc 24h và 48h
```gherkin
Given Hợp đồng đang ở trạng thái "PendingVerification"
When Đã trôi qua 24 giờ (hoặc 48 giờ) kể từ lúc NCC nộp hợp đồng mà Khách hàng chưa bấm duyệt
Then Hệ thống tự động bắn tin nhắn nhắc nhở qua Zalo: "Bạn có 1 yêu cầu xác nhận hợp đồng tiệc cưới chưa hoàn tất. Xác nhận ngay để nhận quà cưới 500k từ Sàn!"
```

### Kịch bản 2: Tự động hủy hợp đồng quá hạn 72h (UC 4.5)
```gherkin
Given Hợp đồng đã quá 72 giờ ở trạng thái "PendingVerification"
When Tiến trình Job Schedule quét qua bản ghi này
Then Tự động cập nhật BookingContract sang trạng thái "Cancelled"
And Ghi log: "Tự động hủy do khách hàng không xác nhận trong vòng 72 giờ"
And Gửi thông báo cho cả NCC và Khách hàng
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-005` (SLA Xác thực 72h):** Ràng buộc thời gian xác thực 2 chiều.
- **Thực thể dữ liệu:** `BookingContract`, `User` (theo [[ba-data-model]]).
