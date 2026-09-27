---
id: "US-6.2"
title: "User Story: Kiểm Duyệt Viên Sàn Thẩm Định & Phê Duyệt Nội Dung Đánh Giá (SLA 24h)"
epic: "Epic 6: Đánh Giá Trải Nghiệm & Uy Tín Đối Tác"
use_cases: ["UC-6.2"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-05-06-Commission-And-Review.html#tab-reviews"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-6.2: KIỂM DUYỆT VIÊN PHÊ DUYỆT NỘI DUNG ĐÁNH GIÁ CỦA KHÁCH HÀNG

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Kiểm duyệt viên Sàn (Content Moderator),  
> **Tôi muốn** truy cập màn hình Hàng đợi kiểm duyệt đánh giá trên Admin Back-office để xem chi tiết nhận xét, số sao chấm, ảnh thực tế tải lên của khách và đối chiếu với Hợp đồng dịch vụ thật,  
> **Để** phê duyệt công khai bài đánh giá hoặc từ chối nếu vi phạm tiêu chuẩn cộng đồng, đảm bảo môi trường đánh giá minh bạch, khách quan và văn minh.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Phê duyệt đánh giá hợp lệ
```gherkin
Given Moderator mở hàng đợi Review có ID "REV-2026-0091"
And Nội dung đánh giá tích cực, ngôn từ lịch sự, ảnh cưới sắc nét không vi phạm bản quyền
When Moderator bấm nút "✅ Phê Duyệt & Công Khai"
Then Trạng thái Review chuyển sang "Approved"
And Bài đánh giá xuất hiện công khai trên trang Hồ sơ & Danh sách bài đăng của NCC
And Kích hoạt Job tính lại điểm trung bình Rating Avg của Vendor (UC-6.3)
And Gửi thông báo cho Vendor: "Bạn vừa nhận được 1 đánh giá 5 sao mới từ khách hàng!"
```

### Kịch bản 2: Từ chối đánh giá chứa nội dung phản cảm hoặc spam quảng cáo
```gherkin
Given Review chứa từ ngữ thô tục, xúc phạm cá nhân hoặc chèn số điện thoại quảng cáo dịch vụ khác
When Moderator bấm nút "❌ Từ Chối Đăng"
And Chọn lý do từ chối: "Nội dung vi phạm tiêu chuẩn cộng đồng: Chứa ngôn từ không phù hợp"
Then Trạng thái Review chuyển sang "Rejected"
And Review không hiển thị công khai
And Gửi email thông báo cho Khách hàng nêu rõ lý do bị từ chối và hướng dẫn sửa đổi
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **SLA Duyệt:** Mọi đánh giá phải được xử lý trong vòng 24 giờ kể từ khi khách gửi.
- **Thực thể dữ liệu:** `Review`, `ReviewModerationLog`, `AuditLog` (theo [[ba-data-model]]).
