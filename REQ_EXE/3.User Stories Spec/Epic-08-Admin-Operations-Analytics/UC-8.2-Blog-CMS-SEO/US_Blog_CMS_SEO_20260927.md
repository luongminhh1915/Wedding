---
id: "US-8.2"
title: "User Story: Biên Tập Viên Soạn Thảo & Xuất Bản Bài Viết Cẩm Nang Cưới Chuẩn SEO (CMS SEO)"
epic: "Epic 8: Quản Trị Hệ Thống, Vận Hành & Báo Cáo Toàn Sàn"
use_cases: ["UC-8.2"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-08-Admin-Backoffice-Dashboard.html#nav-cms"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-8.2: BIÊN TẬP VÀ XUẤT BẢN BÀI VIẾT CẨM NANG CƯỚI (CMS SEO)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Biên tập viên Nội dung (Content Creator / Moderator),  
> **Tôi muốn** sử dụng hệ thống CMS quản trị bài viết chuẩn SEO để soạn thảo cẩm nang cưới, tối ưu hóa các thẻ meta SEO, thiết lập slug thân thiện và ghim trực tiếp các gói dịch vụ cưới của Nhà Cung Cấp vào chân bài viết,  
> **Để** thu hút lượng truy cập tự nhiên từ Google (Organic Search Traffic) và chuyển đổi người đọc thành Lead tiềm năng cho Sàn.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Xuất bản bài viết chuẩn SEO có ghim gói dịch vụ NCC
```gherkin
Given Biên tập viên mở trình soạn thảo CMS Blog
When Soạn bài viết: "Top 10 Xu Hướng Trang Trí Cưới Rustic Được Yêu Thích 2026"
And Cài đặt SEO:
    - Focus Keyword: "trang trí cưới rustic"
    - Meta Title: "Top 10 Xu Hướng Trang Trí Cưới Rustic 2026 | EXE Wedding"
    - Meta Description: "Khám phá phong cách trang trí tiệc cưới rustic mộc mạc, lãng mạn cùng ưu đãi voucher 5 triệu tại EXE..."
    - Slug URL: "xu-huong-trang-tri-rustic-2026"
And Ghim gói dịch vụ liên quan: Chọn gói "Trang Trí Gia Tiên Rustic" của NCC White Peony Studio
And Bấm "Xuất Bản (Publish)"
Then Bài viết được đăng tải công khai trên Customer Web Portal (`/cam-nang/xu-huong-trang-tri-rustic-2026`)
And Ở chân bài viết xuất hiện widget: "Đặt Gói Dịch Vụ Này Với Ưu Đãi Độc Quyền" dẫn trực tiếp về màn hình gửi Lead (UC-2.1)
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Thực thể dữ liệu:** `BlogPost`, `BlogCategory`, `Listing` (theo [[ba-data-model]]).
