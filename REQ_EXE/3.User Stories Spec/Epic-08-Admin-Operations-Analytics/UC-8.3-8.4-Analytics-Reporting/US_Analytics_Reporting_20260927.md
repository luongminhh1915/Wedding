---
id: "US-8.3-8.4"
title: "User Story: Ban Quản Trị & Kế Toán Xem Dashboard Phân Tích Doanh Thu Hoa Hồng & Hiệu Suất Chuyển Đổi Lead"
epic: "Epic 8: Quản Trị Hệ Thống, Vận Hành & Báo Cáo Toàn Sàn"
use_cases: ["UC-8.3", "UC-8.4"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-08-Admin-Backoffice-Dashboard.html#nav-analytics"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-8.3 & US-8.4: DASHBOARD DOANH THU HOA HỒNG & BÁO CÁO HIỆU SUẤT CHUYỂN ĐỔI LEAD

---

## 1. Tuyên Bố User Story (Story Statements)

### Cho UC-8.3 (Phân tích doanh thu hoa hồng toàn sàn):
> **Là** Ban Quản Trị & Đội ngũ Kế toán Sàn,  
> **Tôi muốn** xem Dashboard báo cáo tổng quan GMV giao dịch, doanh thu hoa hồng thực thu (Kỳ 1, Kỳ 2), tỷ lệ thu hồi hoa hồng đúng hạn và công nợ tồn đọng theo thời gian thực,  
> **Để** nắm bắt tình hình sức khỏe tài chính của nền tảng và đưa ra các quyết định kinh doanh kịp thời.

### Cho UC-8.4 (Báo cáo hiệu suất chuyển đổi Lead theo NCC):
> **Là** Đội ngũ Vận hành & Chăm sóc Khách hàng (CSKH),  
> **Tôi muốn** xem bảng xếp hạng chi tiết phễu chuyển đổi của từng Nhà Cung Cấp (Tỷ lệ tiếp nhận &lt; 2h, Tỷ lệ chốt Hợp đồng, Doanh số mang lại),  
> **Để** vinh danh các NCC xuất sắc, đồng thời phát hiện sớm các đối tác có tỷ lệ hủy lead cao hoặc vi phạm cam kết SLA để tư vấn, cảnh báo hoặc chế tài xử lý.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1 (UC-8.3): Xem Dashboard tài chính theo chu kỳ tháng
```gherkin
Given Quản trị viên truy cập màn hình "Doanh Thu & Giao Dịch"
When Chọn bộ lọc thời gian: "Tháng 09/2026"
Then Hệ thống tổng hợp và hiển thị trực quan:
    - Tổng GMV hợp đồng phát sinh: 5.82 Tỷ đ
    - Tổng Hoa hồng Sàn ghi nhận: 582 Triệu đ (Trong đó Kỳ 1: 350 Triệu đ, Kỳ 2: 232 Triệu đ)
    - Tỷ lệ thu hồi hoa hồng đúng hạn: 96.5%
    - Biểu đồ phân bổ doanh thu theo 7 nhóm danh mục dịch vụ cưới (Tiệc cưới chiếm 45%, Trang trí 22%, Chụp ảnh 18%...)
```

### Kịch bản 2 (UC-8.4): Tra cứu hiệu suất và phát hiện NCC vi phạm SLA
```gherkin
Given Quản trị viên mở bảng "Hiệu Suất Chuyển Đổi Lead NCC"
When Hệ thống hiển thị bảng xếp hạng:
    - NCC "White Peony Wedding Studio": 94 Lead nhận, Tỷ lệ phản hồi &lt; 2h đạt 98.9%, Chốt 38 HĐ (Tỷ lệ chuyển đổi: 40.4%) -> Gắn nhãn "Top Performer"
    - NCC "Studio X": 50 Lead nhận, Tỷ lệ phản hồi &lt; 2h chỉ đạt 45.0% (Vi phạm SLA nhiều lần), Tỷ lệ chuyển đổi 8%
When Bấm vào dòng chi tiết của NCC vi phạm
Then Hệ thống cung cấp nút: "Gửi Cảnh Báo Vi Phạm SLA" hoặc "Tạm Ngưng Điều Phối Lead Mới trong 7 ngày"
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Thực thể dữ liệu:** `CommissionSettlement`, `Lead`, `BookingContract`, `Vendor` (theo [[ba-data-model]]).
