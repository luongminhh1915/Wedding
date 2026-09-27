---
id: "US-4.1"
title: "User Story: Nhà Cung Cấp Nhập Mã Ưu Đãi & Tạo Giao Dịch Hợp Đồng Dịch Vụ Cưới"
epic: "Epic 4: Quản Lý Hợp Đồng & Xác Thực Giao Dịch 2 Chiều"
use_cases: ["UC-4.1"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-03-04-Vendor-Listing-And-Booking.html#modalContract"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-4.1: NCC NHẬP MÃ ƯU ĐÃI & TẠO GIAO DỊCH HỢP ĐỒNG 2 CHIỀU

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Chủ Nhà Cung Cấp (Vendor Owner),  
> **Tôi muốn** nhập mã Voucher ưu đãi của khách hàng, nhập tổng giá trị hợp đồng, số tiền cọc thực tế và tải lên ảnh chụp hợp đồng/phiếu thu,  
> **Để** gửi yêu cầu xác thực 2 chiều cho khách hàng và kích hoạt gói quà tặng của Sàn.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Nhập mã Voucher hợp lệ và tạo hợp đồng
```gherkin
Given NCC mở Modal "Tạo Hợp Đồng Dịch Vụ Cưới" trên Vendor Portal
When NCC nhập mã Voucher "WVIP8899"
Then Hệ thống tự động kiểm tra tính hợp lệ của Voucher (trạng thái "Active", còn hạn 30 ngày)
And Tự động hiển thị tên Cặp đôi khách hàng và Lead liên quan
When NCC nhập: Tổng giá trị = 180.000.000đ, Số tiền cọc = 50.000.000đ, tải ảnh hợp đồng và bấm "Gửi Yêu Cầu Xác Thực"
Then Bản ghi BookingContract được tạo ở trạng thái "PendingVerification"
And Kích hoạt gửi thông báo xác thực tới Khách hàng qua Zalo ZNS và App
```

### Kịch bản 2: Xử lý nhập mã Voucher không tồn tại hoặc đã hết hạn
```gherkin
Given NCC nhập mã Voucher "WVIP0000" (không tồn tại trong hệ thống)
When Bấm kiểm tra
Then Hệ thống hiển thị thông báo lỗi: "Mã ưu đãi không hợp lệ hoặc đã hết hạn. Vui lòng kiểm tra lại với khách hàng!"
And Khóa nút bấm tạo hợp đồng cho đến khi nhập mã hợp lệ
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-004` & `BR-005`:** Hợp đồng chỉ được tạo khi gắn với mã Voucher hợp lệ và phải trải qua quy trình xác thực 2 chiều.
- **Thực thể dữ liệu:** `BookingContract`, `Voucher`, `Lead`, `Vendor` (theo [[ba-data-model]]).
