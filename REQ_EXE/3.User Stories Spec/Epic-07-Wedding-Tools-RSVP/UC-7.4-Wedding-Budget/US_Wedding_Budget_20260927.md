---
id: "US-7.4"
title: "User Story: Cặp Đôi Quản Lý Dự Toán & Chi Phí Thực Tế Đám Cưới (Wedding Budget Planner)"
epic: "Epic 7: Tiện Ích Chuẩn Bị Cưới & Thiệp Mời Online"
use_cases: ["UC-7.4"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-07-Wedding-Invitation-RSVP.html#tab-budget"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-7.4: CẶP ĐÔI QUẢN LÝ DỰ TOÁN NGÂN SÁCH CƯỚI (WEDDING BUDGET)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** Cặp đôi Cô dâu / Chú rể chuẩn bị cưới,  
> **Tôi muốn** nhập tổng ngân sách đám cưới dự kiến, sử dụng tỷ lệ phân bổ chi phí chuẩn ngành cưới (50% tiệc cưới, 15% chụp ảnh/váy, 15% trang trí...), theo dõi số tiền đã đặt cọc vs số tiền còn lại phải trả theo từng hợp đồng,  
> **Để** kiểm soát dòng tiền chặt chẽ, không bị bội chi hoặc áp lực tài chính sát ngày trọng đại.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Phân bổ ngân sách chuẩn và nhập chi phí
```gherkin
Given Cặp đôi nhập Tổng ngân sách kỳ vọng: 300.000.000 đ
When Bấm áp dụng "Tỷ lệ chuẩn gợi ý"
Then Hệ thống tự động chia ngân sách dự tính cho các hạng mục:
    - Tiệc cưới nhà hàng (50%): 150.000.000 đ
    - Trang trí tiệc & gia tiên (15%): 45.000.000 đ
    - Ảnh cưới & Trang phục (15%): 45.000.000 đ
    - Nhẫn & Trang sức cưới (10%): 30.000.000 đ
    - Dự phòng phát sinh (10%): 30.000.000 đ
When Cặp đôi liên kết các Hợp đồng đã ký qua Sàn (Epic 4) vào từng hạng mục
Then Hệ thống tự động tính:
    - Tổng đã chi thực tế: 185.000.000 đ (61.6%)
    - Đã đặt cọc: 95.000.000 đ
    - Còn phải thanh toán: 90.000.000 đ
    - Ngân sách khả dụng còn lại: 115.000.000 đ
```

### Kịch bản 2: Cảnh báo nguy cơ vượt ngân sách (Budget Overrun Alert)
```gherkin
Given Cặp đôi nhập thêm một hạng mục dịch vụ khiến Tổng chi phí thực tế > Tổng ngân sách (VD: 315.000.000 đ > 300.000.000 đ)
When Hệ thống tính toán lại
Then Thanh tiến độ chuyển sang màu đỏ cảnh báo
And Hiển thị thông báo: "Cảnh báo: Bạn đang vượt dự toán ngân sách ban đầu 15.000.000 đ (5%). Vui lòng cân nhắc điều chỉnh các hạng mục phụ trợ!"
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Thực thể dữ liệu:** `WeddingBudget`, `BudgetItem`, `BookingContract` (theo [[ba-data-model]]).
