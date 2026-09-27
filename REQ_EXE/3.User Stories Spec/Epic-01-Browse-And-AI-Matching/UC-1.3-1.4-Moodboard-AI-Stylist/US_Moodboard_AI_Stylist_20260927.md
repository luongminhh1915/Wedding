---
id: "US-1.3-1.4"
title: "User Story: Trắc Nghiệm Visual Moodboard Quiz & Trợ Lý Ảo AI Stylist Matching"
epic: "Epic 1: Khám Phá Dịch Vụ Cưới & AI Stylist Matching"
use_cases: ["UC-1.3", "UC-1.4"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-01-Browse-And-Moodboard.html#moodboardModal"
ui_spec: "docs-BA/User Stories Spec/Epic-01-Browse-And-AI-Matching/UC-1.3-1.4-Moodboard-AI-Stylist/reference/UI_Spec_Moodboard_AI_Stylist_20260927.md"
sequence_spec: "docs-BA/User Stories Spec/Epic-01-Browse-And-AI-Matching/UC-1.3-1.4-Moodboard-AI-Stylist/reference/Sequence_Moodboard_AI_Stylist_20260927.md"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-1.3 & US-1.4: TRẮC NGHIỆM MOODBOARD & TƯ VẤN CÙNG AI STYLIST MATCHING

---

## 1. Tuyên Bố User Story (Story Statement)

* **US-1.3 (Visual Moodboard Quiz):**
  > **Là** một Cô dâu / Chú rể chưa định hình rõ ràng phong cách tiệc cưới,  
  > **Tôi muốn** làm một bài trắc nghiệm nhanh bằng hình ảnh (chọn 3 bức ảnh yêu thích) và kéo thanh ngân sách dự kiến,  
  > **Để** hệ thống AI tự động phân tích gu thẩm mỹ và gợi ý ngay 3–5 Nhà cung cấp phù hợp nhất mà không phải mất hàng tuần tìm kiếm thủ công.

* **US-1.4 (Chat Tư Vấn Cùng AI Stylist):**
  > **Là** một Cô dâu / Chú rể có nhiều băn khoăn về concept và cách phân bổ chi phí,  
  > **Tôi muốn** trò chuyện tự nhiên với Trợ lý ảo AI Stylist,  
  > **Để** được giải đáp các thắc mắc về xu hướng cưới, gợi ý màu sắc chủ đạo và đề xuất các gói dịch vụ phù hợp trong ngân sách.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Mở Modal và thực hiện chọn ảnh phong cách (Bước 1)
```gherkin
Given Khách hàng đang ở trang chủ hoặc trang danh mục
When Khách hàng bấm vào nút "✨ AI Stylist Quiz"
Then Hệ thống mở cửa sổ Modal Moodboard Quiz ở Bước 1
And Hiển thị lưới các ảnh đại diện cho các phong cách cưới khác nhau
When Khách hàng chọn bức ảnh thứ 1 và thứ 2
Then Số lượng đếm hiển thị "Đã chọn: 2/3 phong cách" và nút "Tiếp Tục" vẫn ở trạng thái vô hiệu hóa (Disabled)
When Khách hàng chọn tiếp bức ảnh thứ 3
Then Số lượng đếm hiển thị "Đã chọn: 3/3 phong cách" và nút "Tiếp Tục: Chọn Ngân Sách →" sáng lên cho phép click
```

### Kịch bản 2: Nhập ngân sách, địa điểm và nhận kết quả gợi ý AI (Bước 2)
```gherkin
Given Khách hàng đã chọn đủ 3 ảnh và chuyển sang Bước 2
When Khách hàng kéo thanh trượt ngân sách về mức "150 Triệu VNĐ", chọn địa điểm "TP. Hồ Chí Minh" và bấm "✨ Bắt Đầu Phân Tích & Gợi Ý AI"
Then Hệ thống hiển thị hiệu ứng xoay spinner phân tích AI trong thời gian <= 3 giây
And Trả về thẻ kết quả hiển thị 3-5 Nhà cung cấp khớp nối cao nhất (vd: 98%, 95%)
And Mỗi thẻ kết quả hiển thị rõ: Tên gói, Tên NCC, Tỷ lệ match, Lý do gợi ý và Mã ưu đãi Voucher đi kèm
```

### Kịch bản 3: Tương tác trò chuyện với AI Stylist (UC 1.4)
```gherkin
Given Khách hàng mở khung chat với AI Stylist
When Khách hàng gửi tin nhắn: "Mình muốn làm đám cưới ngoài trời tại TP.HCM ngân sách 120 triệu thì nên chia tiền thế nào?"
Then Trợ lý AI Stylist phân tích prompt và trả lời mạch lạc bằng tiếng Việt trong <= 3 giây
And Đề xuất bảng phân bổ mẫu (Venue: 60M, Decor: 30M, Photo: 15M, Makeup: 5M, Dự phòng: 10M)
And Tự động đính kèm 2 liên kết bài đăng thực tế của sàn phù hợp với concept này
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-004`:** Kết quả gợi ý từ AI tự động đính kèm mã ưu đãi độc quyền (Voucher 8 ký tự) để khuyến khích khách hàng bấm gửi yêu cầu tư vấn ngay.
- **Thực thể dữ liệu liên quan:** `User`, `Listing`, `Category`, `ListingMedia` (theo [[ba-data-model]]).

---

## 4. Tài Liệu Tham Chiếu Kỹ Thuật

- **Màn hình Prototype tương tác:** [SCR-01-Browse-And-Moodboard.html](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Prototype/SCR-01-Browse-And-Moodboard.html)
- **Đặc tả UI & Ma trận điều khiển:** [UI_Spec_Moodboard_AI_Stylist_20260927.md](file:///c:/Users/Admin/Desktop/EXE/User%20Stories%20Spec/Epic-01-Browse-And-AI-Matching/UC-1.3-1.4-Moodboard-AI-Stylist/reference/UI_Spec_Moodboard_AI_Stylist_20260927.md)
- **Sơ đồ Sequence & Đặc tả API:** [Sequence_Moodboard_AI_Stylist_20260927.md](file:///c:/Users/Admin/Desktop/EXE/User%20Stories%20Spec/Epic-01-Browse-And-AI-Matching/UC-1.3-1.4-Moodboard-AI-Stylist/reference/Sequence_Moodboard_AI_Stylist_20260927.md)
