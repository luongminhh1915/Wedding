---
id: "US-1.1-1.2"
title: "User Story: Khám Phá Danh Mục & Xem Chi Tiết Gói Dịch Vụ Cưới"
epic: "Epic 1: Khám Phá Dịch Vụ Cưới & AI Stylist Matching"
use_cases: ["UC-1.1", "UC-1.2"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-01-Browse-And-Moodboard.html"
ui_spec: "docs-BA/User Stories Spec/Epic-01-Browse-And-AI-Matching/UC-1.1-1.2-Browse-Listing/reference/UI_Spec_Browse_Listing_20260927.md"
sequence_spec: "docs-BA/User Stories Spec/Epic-01-Browse-And-AI-Matching/UC-1.1-1.2-Browse-Listing/reference/Sequence_Browse_Listing_20260927.md"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-1.1 & US-1.2: KHÁM PHÁ DANH MỤC & XEM CHI TIẾT GÓI DỊCH VỤ CƯỚI

---

## 1. Tuyên Bố User Story (Story Statement)

* **US-1.1 (Khám phá & Tìm kiếm):**
  > **Là** một Cô dâu / Chú rể (hoặc Khách vãng lai chuẩn bị cưới),  
  > **Tôi muốn** duyệt danh sách và lọc các gói dịch vụ cưới theo 7 ngành hàng, khu vực địa lý, phong cách và mức ngân sách,  
  > **Để** tôi có thể dễ dàng so sánh bảng giá, tìm kiếm các nhà cung cấp uy tín phù hợp nhất với kế hoạch ngày trọng đại của mình.

* **US-1.2 (Xem chi tiết & Portfolio):**
  > **Là** một Cô dâu / Chú rể,  
  > **Tôi muốn** xem chi tiết album ảnh thực tế, bảng giá niêm yết, ưu đãi voucher và các bài đánh giá có kiểm duyệt của từng gói dịch vụ,  
  > **Để** tôi có đầy đủ thông tin minh bạch trước khi quyết định gửi thông tin liên hệ tư vấn.

---

## 2. Điều Kiện Tiên Quyết & Kết Quả (Pre/Post Conditions)

- **Tiền điều kiện (Pre-conditions):**
  - Người dùng truy cập vào trang web Customer Web Portal (không bắt buộc phải đăng nhập tài khoản).
  - Hệ thống có ít nhất các bài đăng ở trạng thái `Active`.
- **Hậu điều kiện (Post-conditions):**
  - Người dùng xem được danh sách kết quả phù hợp và có thể bấm `Nhận Voucher` để kích hoạt luồng gửi Lead tư vấn (UC 2.1).

---

## 3. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Duyệt danh mục mặc định trên trang chủ
```gherkin
Given Khách hàng truy cập vào đường dẫn "/danh-muc"
When Trang tải hoàn tất
Then Hệ thống hiển thị thanh lướt 7 danh mục cưới ở trạng thái chọn "Tất cả dịch vụ"
And Hiển thị danh sách các bài đăng dịch vụ đang có trạng thái "Active" (phân trang 12 bài/trang)
And Mỗi thẻ bài đăng hiển thị đầy đủ: Ảnh đại diện, Tên nhà cung cấp có huy hiệu xác thực, Tiêu đề gói, Điểm sao trung bình, Mức giá từ và Nút "Nhận Voucher"
```

### Kịch bản 2: Lọc bài đăng theo ngành và khu vực
```gherkin
Given Khách hàng đang ở trang danh mục dịch vụ
When Khách hàng bấm chọn Pill "Trung tâm Tiệc cưới (Venue)" và chọn khu vực "TP. Hồ Chí Minh"
Then Hệ thống lọc và chỉ hiển thị các bài đăng thuộc ngành Venue tại khu vực TP.HCM
And Cập nhật lại số lượng kết quả đếm được (vd: "Hiển thị 8 gói dịch vụ chất lượng cao")
```

### Kịch bản 3: Lọc theo khoảng ngân sách và phong cách
```gherkin
Given Khách hàng đang tìm kiếm dịch vụ
When Khách hàng chọn khoảng ngân sách "50 - 100 triệu" và tích chọn phong cách "Minimalist"
Then Hệ thống trả về các gói dịch vụ thỏa mãn điều kiện giá nằm trong khoảng 50M - 100M và có gắn thẻ "minimalist"
```

### Kịch bản 4: Xử lý trường hợp không tìm thấy kết quả (Empty State)
```gherkin
Given Khách hàng áp dụng bộ lọc quá hẹp
When Không có bài đăng nào thỏa mãn điều kiện tìm kiếm
Then Hệ thống hiển thị giao diện thông báo rỗng: "Không tìm thấy gói dịch vụ phù hợp với bộ lọc"
And Hiển thị 2 nút gợi ý: "Đặt lại bộ lọc" và "Nhờ AI Stylist tư vấn gợi ý thay thế"
```

### Kịch bản 5: Xem chi tiết gói dịch vụ và portfolio (UC 1.2)
```gherkin
Given Khách hàng bấm vào một thẻ bài đăng bất kỳ trên danh sách
When Hệ thống điều hướng sang trang chi tiết "/dich-vu/[slug]"
Then Hiển thị đầy đủ slider album ảnh chất lượng cao (ListingMedia)
And Hiển thị thông tin pháp lý/liên hệ của NCC, địa chỉ sảnh tiệc có bản đồ định vị
And Hiển thị chính sách ưu đãi Voucher hiện hành của sàn (Vd: "Giảm 5% + Tặng Bàn Gallery")
And Nút hành động cố định "Gửi Yêu Cầu Tư Vấn & Nhận Voucher" luôn hiển thị ở vị trí dễ bấm
```

### Kịch bản 6: Xem đánh giá thực tế từ khách hàng cũ (Verified Buyer Review)
```gherkin
Given Khách hàng đang ở trang chi tiết gói dịch vụ
When Khách hàng cuộn xuống khu vực "Đánh giá từ các cặp đôi"
Then Chỉ hiển thị các bài đánh giá đã được Moderator duyệt ("Approved")
And Các bài đánh giá từ người dùng đã hoàn tất hợp đồng được gắn huy hiệu nổi bật "[Verified Buyer]"
```

---

## 4. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-008`:** Chỉ hiển thị các bài đăng đã được kiểm duyệt ở trạng thái `Active`.
- **`BR-009`:** Đánh giá có huy hiệu `[Verified Buyer]` được tính trọng số cao vào điểm sao trung bình `rating_avg` của Nhà cung cấp.
- **Thực thể dữ liệu liên quan:** `Listing`, `ListingMedia`, `Category`, `Vendor`, `Review` (theo [[ba-data-model]]).

---

## 5. Tài Liệu Tham Chiếu Kỹ Thuật

- **Màn hình Prototype tương tác:** [SCR-01-Browse-And-Moodboard.html](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Prototype/SCR-01-Browse-And-Moodboard.html)
- **Đặc tả UI & Ma trận điều khiển:** [UI_Spec_Browse_Listing_20260927.md](file:///c:/Users/Admin/Desktop/EXE/User%20Stories%20Spec/Epic-01-Browse-And-AI-Matching/UC-1.1-1.2-Browse-Listing/reference/UI_Spec_Browse_Listing_20260927.md)
- **Sơ đồ Sequence & Đặc tả API:** [Sequence_Browse_Listing_20260927.md](file:///c:/Users/Admin/Desktop/EXE/User%20Stories%20Spec/Epic-01-Browse-And-AI-Matching/UC-1.1-1.2-Browse-Listing/reference/Sequence_Browse_Listing_20260927.md)
