---
id: "US-7.1-7.2-7.3"
title: "User Story: Cặp Đôi Thiết Kế Thiệp Cưới Online, Khách Mời Xác Nhận Tham Dự (RSVP 1-Chạm) & Quản Lý Khách Mời"
epic: "Epic 7: Tiện Ích Chuẩn Bị Cưới & Thiệp Mời Online"
use_cases: ["UC-7.1", "UC-7.2", "UC-7.3"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-07-Wedding-Invitation-RSVP.html#tab-invitation"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-7.1, US-7.2 & US-7.3: THIỆP MỜI CƯỚI ONLINE, FORM RSVP 1-CHẠM & QUẢN LÝ DANH SÁCH KHÁCH MỜI

---

## 1. Tuyên Bố User Story (Story Statements)

### Cho UC-7.1 (Tạo và tùy chỉnh thiệp cưới Online):
> **Là** Cặp đôi Cô dâu / Chú rể,  
> **Tôi muốn** chọn các mẫu giao diện thiệp cưới điện tử (E-Invitation Landing Page), tùy chỉnh thông tin ngày cưới, địa điểm, album ảnh tình yêu và mã QR mừng cưới VietQR,  
> **Để** tạo ra trang thiệp cưới trực tuyến lãng mạn, độc đáo mang dấu ấn cá nhân và xuất bản đường dẫn link chia sẻ cho bạn bè.

### Cho UC-7.2 (Mở link thiệp cưới và xác nhận tham dự - RSVP 1-chạm):
> **Là** một Khách mời dự tiệc cưới (Wedding Guest),  
> **Tôi muốn** mở link thiệp cưới trên điện thoại thông minh, xem thiệp động kèm nhạc nền lãng mạn, bản đồ chỉ đường Google Maps và bấm xác nhận tham dự tiệc (RSVP 1-chạm),  
> **Để** báo trước cho dâu rể biết mình có tham dự hay không, đi bao nhiêu người và gửi gắm lời chúc phúc thân tình.

### Cho UC-7.3 (Quản lý danh sách khách mời và thống kê RSVP):
> **Là** Cặp đôi Cô dâu / Chú rể,  
> **Tôi muốn** xem dashboard thống kê trực quan số lượng khách đã xác nhận tham dự (Yes/No), tự động tính số lượng bàn tiệc cần đặt và xuất file danh sách khách mời ra Excel,  
> **Để** chủ động điều phối chỗ ngồi và chốt số mâm cỗ chuẩn xác với nhà hàng tiệc cưới mà không bị lãng phí.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1 (UC-7.1): Tạo và xuất bản thiệp cưới online
```gherkin
Given Cặp đôi mở công cụ E-Invitation Builder
When Chọn mẫu theme "Rustic Greenery"
And Nhập thông tin:
    - Chú rể: Hoàng Long, Cô dâu: Minh Thư
    - Thời gian: 18:00 Chủ Nhật, 15/11/2026
    - Địa điểm: White Palace Grand Ballroom, 194 Hoàng Văn Thụ, TP.HCM
    - Tải lên 5 ảnh cưới album chất lượng cao
    - Cấu hình số TK mừng cưới VietQR: MBBank 0987654321
And Bấm "Lưu & Xuất Bản"
Then Hệ thống sinh đường link truy cập duy nhất: `https://exe.wedding/inv/hoang-long-minh-thu-2026`
And Kích hoạt chế độ SEO OpenGraph để khi gửi qua Zalo/Facebook Messenger sẽ hiển thị preview ảnh bìa thiệp cưới lộng lẫy
```

### Kịch bản 2 (UC-7.2): Khách mời thực hiện RSVP 1-chạm
```gherkin
Given Khách mời mở đường link thiệp cưới trên điện thoại
When Khách cuộn xuống khu vực "Xác Nhận Tham Dự (RSVP)"
And Nhập tên: "Nguyễn Văn Tuấn", chọn số người đi cùng: "+1 (Đi 2 người)"
And Nhập lời chúc: "Chúc 2 bạn trăm năm hạnh phúc, sớm có quý tử!"
And Bấm nút "✓ Tôi Sẽ Tham Dự"
Then Hệ thống lưu bản ghi RSVPGuest:
    | Field | Value |
    | invitationId | "INV-2026-LONGTHU" |
    | guestName | "Nguyễn Văn Tuấn" |
    | status | "Attending" |
    | partySize | 2 |
    | wishes | "Chúc 2 bạn trăm năm hạnh phúc..." |
And Màn hình khách mời hiển thị hiệu ứng pháo hoa chúc mừng lung linh
And Gửi thông báo thông báo đẩy tức thời (Push Notification) tới điện thoại của Cô dâu / Chú rể
```

### Kịch bản 3 (UC-7.3): Thống kê danh sách khách mời và dự tính số bàn tiệc
```gherkin
Given Đã có 320 khách được gửi thiệp
When Cặp đôi truy cập tab "Thống kê Khách mời & RSVP"
Then Hệ thống hiển thị tổng quan:
    - 265 khách xác nhận "Sẽ tham dự"
    - 25 khách phản hồi "Bận việc không đi được"
    - 30 khách chưa phản hồi (có nút bấm "Gửi tin nhắn Zalo nhắc nhẹ")
And Tự động tính toán số bàn tiệc quy đổi: `CEIL(265 / 10) = 27 bàn tiệc`
When Cặp đôi bấm "📥 Xuất File Excel"
Then Hệ thống tải về file `Danh_Sach_Khach_Moi_Long_Thu_20261115.xlsx` phân nhóm theo cột: Bạn Chú Rể, Bạn Cô Dâu, Họ Hàng, Đồng Nghiệp
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Thực thể dữ liệu:** `WeddingInvitation`, `RSVPGuest`, `CoupleProfile` (theo [[ba-data-model]]).
