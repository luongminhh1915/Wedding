---
id: "US-2.3-2.4"
title: "User Story: Nhà Cung Cấp Tiếp Nhận Lead & Phòng Chat Báo Giá Trực Tiếp"
epic: "Epic 2: Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead"
use_cases: ["UC-2.3", "UC-2.4"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-02-Lead-And-Vendor-Portal.html"
ui_spec: "docs-BA/User Stories Spec/Epic-02-Lead-And-Voucher/UC-2.3-2.4-Vendor-Accept-Chat/reference/UI_Spec_Vendor_Lead_Chat_20260927.md"
sequence_spec: "docs-BA/User Stories Spec/Epic-02-Lead-And-Voucher/UC-2.3-2.4-Vendor-Accept-Chat/reference/Sequence_Vendor_Lead_Chat_20260927.md"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-2.3 & US-2.4: NCC TIẾP NHẬN LEAD & CHAT BÁO GIÁ TRỰC TIẾP

---

## 1. Tuyên Bố User Story (Story Statement)

* **US-2.3 (Tiếp nhận Lead):**
  > **Là** một Chủ Nhà Cung Cấp (Vendor Owner),  
  > **Tôi muốn** nhận thông báo tức thì và bấm nút tiếp nhận yêu cầu tư vấn của khách hàng trong vòng 2 giờ,  
  > **Để** tôi không bỏ lỡ khách hàng tiềm năng và giữ vững chỉ số phản hồi nhanh (SLA cao) trên sàn.

* **US-2.4 (Chat Báo Giá Trực Tiếp):**
  > **Là** một Chủ Nhà Cung Cấp,  
  > **Tôi muốn** mở phòng chat trực tiếp với dâu rể để trao đổi concept và gửi file bảng báo giá đính kèm,  
  > **Để** tư vấn thuyết phục khách hàng chốt hợp đồng và sử dụng mã ưu đãi của Sàn.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Xem hàng đợi và bấm tiếp nhận Lead (UC 2.3)
```gherkin
Given Chủ NCC đăng nhập vào Vendor Portal tại mục "Hàng Đợi Yêu Cầu Tư Vấn"
When Có Lead mới phát sinh
Then Thẻ Lead hiển thị viền đỏ nổi bật, nhãn "Lead Mới" và đồng hồ đếm ngược SLA 2h
When NCC bấm nút "✓ Tiếp Nhận Lead"
Then Hệ thống cập nhật trạng thái Lead sang "Accepted"
And Đồng hồ SLA chuyển sang nhãn xanh: "Đã tiếp nhận (SLA Đạt)"
And Nút hành động chuyển thành "💬 Mở Phòng Chat"
```

### Kịch bản 2: Trao đổi tin nhắn và gửi file báo giá trong phòng chat (UC 2.4)
```gherkin
Given NCC và Khách hàng đang ở trong phòng chat trực tiếp của Lead
When NCC nhập nội dung tin nhắn và đính kèm file báo giá dạng PDF (dung lượng <= 10MB) rồi bấm "Gửi"
Then Tin nhắn và file đính kèm được đẩy ngay lập tức sang màn hình của Khách hàng qua WebSocket (độ trễ < 1 giây)
And Khách hàng có thể bấm tải file báo giá về máy hoặc xem trực tiếp trên trình duyệt
```

### Kịch bản 3: Chuyển đổi sang bước chốt hợp đồng
```gherkin
Given Hai bên đã thống nhất xong gói dịch vụ và bảng giá trong phòng chat
When NCC bấm nút "📝 Tạo Hợp Đồng & Nhập Voucher" trên thanh công cụ của phòng chat
Then Hệ thống mở Modal nhập mã Voucher và giá trị hợp đồng (UC 4.1) với thông tin khách hàng đã được điền sẵn
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-001` (Lean Vendor):** Chỉ có 1 tài khoản Vendor Owner duy nhất quản lý toàn bộ lead và phòng chat.
- **`BR-003` (SLA Lead):** Phải tiếp nhận trong vòng 2 giờ để giữ vững điểm uy tín đối tác.
- **Thực thể dữ liệu:** `Lead`, `Vendor`, `User`, `Voucher` (theo [[ba-data-model]]).
