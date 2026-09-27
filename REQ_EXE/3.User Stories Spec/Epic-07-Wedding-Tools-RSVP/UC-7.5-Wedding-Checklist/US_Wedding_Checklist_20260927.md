---
id: "US-7.5"
title: "User Story: Cặp Đôi Quản Lý Kế Hoạch 12 Tháng Chuẩn Bị Cưới (Timeline Checklist)"
epic: "Epic 7: Tiện Ích Chuẩn Bị Cưới & Thiệp Mời Online"
use_cases: ["UC-7.5"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-07-Wedding-Invitation-RSVP.html#tab-checklist"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-7.5: CẶP ĐÔI QUẢN LÝ KẾ HOẠCH CÔNG VIỆC CHUẨN BỊ CƯỚI (12-MONTH CHECKLIST)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** Cặp đôi Cô dâu / Chú rể,  
> **Tôi muốn** sử dụng lộ trình 12 tháng chuẩn bị cưới được chuẩn hóa theo từng cột mốc (9-12 tháng, 6 tháng, 3 tháng, 1 tháng, 1 tuần trước ngày cưới), phân công rõ việc nào của Chú rể, việc nào của Cô dâu và tích chọn hoàn thành,  
> **Để** không bao giờ bị bỏ sót việc quan trọng, giảm thiểu căng thẳng và có một lễ cưới trọn vẹn, chỉn chu.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Tích chọn hoàn thành công việc và cập nhật tiến độ
```gherkin
Given Cặp đôi mở danh mục Checklist giai đoạn "3 - 6 Tháng Trước Ngày Cưới"
When Cô dâu tích chọn vào đầu việc: "Chụp album ảnh cưới Pre-Wedding & chọn váy cưới chính"
Then Hệ thống cập nhật trạng thái công việc sang "Completed"
And Gạch ngang tên công việc và hiển thị badge "Hoàn tất"
And Cập nhật thanh tiến độ tổng thể của kế hoạch cưới (VD: Đạt 14/28 việc hoàn thành - 50%)
```

### Kịch bản 2: Thêm mới công việc tùy chỉnh và đặt hạn chót (Deadline)
```gherkin
Given Cặp đôi bấm nút "+ Thêm Việc Cần Làm"
When Nhập tên việc: "Đặt xe hoa mui trần đón dâu", người phụ trách: "Chú Rể", hạn chót: "20/10/2026"
Then Công việc được lưu vào danh sách
And Đến trước hạn chót 3 ngày, hệ thống tự động gửi thông báo Zalo / App Notification nhắc nhở Chú rể
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Thực thể dữ liệu:** `WeddingChecklist`, `ChecklistItem`, `CoupleProfile` (theo [[ba-data-model]]).
