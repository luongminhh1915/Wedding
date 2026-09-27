---
id: "US-6.4"
title: "User Story: Nhà Cung Cấp Xem Và Phản Hồi Công Khai Đánh Giá Của Khách Hàng"
epic: "Epic 6: Đánh Giá Trải Nghiệm & Uy Tín Đối Tác"
use_cases: ["UC-6.4"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-05-06-Commission-And-Review.html#tab-reviews"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-6.4: NCC XEM VÀ PHẢN HỒI CÔNG KHAI ĐÁNH GIÁ CỦA KHÁCH HÀNG

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Chủ Nhà Cung Cấp (Vendor Owner),  
> **Tôi muốn** xem các bài đánh giá mà cặp đôi đã gửi cho dịch vụ của mình và viết lời phản hồi công khai ngay bên dưới bài đánh giá,  
> **Để** gửi lời cảm ơn tri ân tới cô dâu chú rể hoặc giải thích rõ ràng, thấu đáo nếu có sự cố xảy ra, thể hiện tinh thần trách nhiệm và thái độ dịch vụ chuyên nghiệp.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: NCC gửi phản hồi công khai
```gherkin
Given Vendor nhận được thông báo có đánh giá mới từ khách hàng "Hoàng Long & Minh Thư"
When Vendor mở chi tiết bài đánh giá trên Vendor Portal
And Nhập nội dung phản hồi: "Dạ cảm ơn anh Long và chị Thư rất nhiều ạ! Chúc hai anh chị trăm năm hạnh phúc..." (tối thiểu 10 ký tự, tối đa 1000 ký tự)
And Bấm nút "Gửi Phản Hồi Công Khai"
Then Bản ghi VendorReply được tạo và liên kết với ReviewID
And Phản hồi hiển thị ngay bên dưới bài đánh giá trên trang công khai của NCC
And Gửi thông báo tới Khách hàng: "Nhà Cung Cấp White Peony Studio vừa phản hồi đánh giá của bạn!"
```

### Kịch bản 2: Giới hạn số lần phản hồi
```gherkin
Given Vendor đã gửi 1 phản hồi công khai cho bài đánh giá REV-001
When Vendor muốn phản hồi thêm lần thứ 2
Then Hệ thống chỉ cho phép chỉnh sửa nội dung phản hồi hiện tại (ghi nhận lịch sử Edited), không cho phép spam nhiều phản hồi liên tiếp
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Thực thể dữ liệu:** `Review`, `VendorReply` (theo [[ba-data-model]]).
