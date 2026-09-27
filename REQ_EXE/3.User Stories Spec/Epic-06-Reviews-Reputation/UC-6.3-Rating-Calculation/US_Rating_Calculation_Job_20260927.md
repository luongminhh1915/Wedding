---
id: "US-6.3"
title: "User Story: Hệ Thống Tự Động Tính Toán Lại Điểm Uy Tín (Rating Avg) & Xếp Hạng NCC"
epic: "Epic 6: Đánh Giá Trải Nghiệm & Uy Tín Đối Tác"
use_cases: ["UC-6.3"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-05-06-Commission-And-Review.html#tab-reviews"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-6.3: TIẾN TRÌNH NỀN TÍNH TOÁN ĐIỂM UY TÍN (RATING AVG) VÀ LƯỢT REVIEW CỦA NCC

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** Hệ thống Nền tảng (Platform Reputation Engine),  
> **Tôi muốn** tự động kích hoạt tính toán lại Điểm đánh giá trung bình (`Vendor.ratingAvg`), tổng số lượt đánh giá (`Vendor.totalReviews`) và huy hiệu uy tín (`VendorBadge`) ngay khi có bài đánh giá mới được duyệt hoặc cập nhật,  
> **Để** phản ánh chính xác chất lượng dịch vụ hiện tại của Nhà Cung Cấp trên các bảng xếp hạng và trang tìm kiếm.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Tính toán lại điểm số sau khi có Review được duyệt
```gherkin
Given Review ID "REV-2026-0091" của Vendor "V001" vừa chuyển sang status = "Approved" với rating = 5.0
When Hệ thống kích hoạt Job "RecalculateVendorRatingJob"
Then Quét toàn bộ các Review có vendorId = "V001" và status = "Approved"
And Tính toán:
    - Tổng số đánh giá hợp lệ: totalReviews = COUNT(reviews)
    - Điểm trung bình cộng: ratingAvg = ROUND(AVG(rating), 1)
And Cập nhật vào bản ghi Vendor:
    | Field | Value |
    | ratingAvg | 4.9 |
    | totalReviews | 129 |
    | badge | "TopRated" (Nếu ratingAvg >= 4.8 và totalReviews >= 50) |
And Đẩy dữ liệu cập nhật sang ElasticSearch / Caching Layer để tối ưu hóa tìm kiếm danh mục (UC-1.1)
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Làm tròn số học:** Điểm Rating được làm tròn đến 1 chữ số thập phân (Ví dụ: 4.85 -> 4.9).
- **Thực thể dữ liệu:** `Vendor`, `Review` (theo [[ba-data-model]]).
