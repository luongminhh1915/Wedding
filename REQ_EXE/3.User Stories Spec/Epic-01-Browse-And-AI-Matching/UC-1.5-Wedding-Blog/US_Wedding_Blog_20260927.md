---
id: "US-1.5"
title: "User Story: Đọc Cẩm Nang Cưới & Xu Hướng Mùa Cưới (SEO Articles)"
epic: "Epic 1: Khám Phá Dịch Vụ Cưới & AI Stylist Matching"
use_cases: ["UC-1.5"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-1.5: ĐỌC CẨM NANG CƯỚI & XU HƯỚNG MÙA CƯỚI (SEO ARTICLES)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Cô dâu / Chú rể hoặc Khách truy cập từ Google Search,  
> **Tôi muốn** đọc các bài viết cẩm nang cưới, hướng dẫn lập ngân sách, kinh nghiệm chọn váy cưới và các bộ sưu tập ảnh xu hướng,  
> **Để** tôi có thêm kiến thức thực tế để chuẩn bị ngày cưới chu đáo, đồng thời khám phá các Nhà cung cấp được gợi ý trong bài viết.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

### Kịch bản 1: Xem danh mục cẩm nang cưới
```gherkin
Given Người dùng truy cập trang "/cam-nang-cuoi"
When Trang tải hoàn tất
Then Hiển thị danh sách các bài viết đã xuất bản ("is_published = true")
And Phân loại theo các chủ đề: "Kinh nghiệm chọn sảnh", "Xu hướng váy cưới", "Bí quyết chụp phóng sự", "Dự toán ngân sách"
```

### Kịch bản 2: Đọc chi tiết bài viết và click liên kết Nhà cung cấp
```gherkin
Given Người dùng bấm vào một bài viết bất kỳ (vd: "/cam-nang-cuoi/kinh-nghiem-chon-trang-tri-hoa-tuoi")
When Đọc nội dung bài viết
Then Hiển thị bài viết chuẩn SEO với hình ảnh rõ nét, mục lục tự động (Table of Contents)
And Trong bài viết có gắn các thẻ Box gợi ý Nhà cung cấp liên quan (Widget Listing)
When Người dùng click vào Box Nhà cung cấp
Then Điều hướng mượt mà sang trang chi tiết gói dịch vụ tương ứng
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Thực thể dữ liệu:** `Article`, `Category`, `User` (tác giả).
- **Quy tắc SEO:** Mỗi bài viết phải có đầy đủ `seo_title`, `seo_description`, thẻ `canonical` và tự động sinh slug tiếng Việt không dấu.
