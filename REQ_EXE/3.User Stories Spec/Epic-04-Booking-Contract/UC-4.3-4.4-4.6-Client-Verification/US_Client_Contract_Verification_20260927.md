---
id: "US-4.3-4.4-4.6"
title: "User Story: Khách Hàng Xác Nhận / Từ Chối Hợp Đồng 2 Chiều & Báo Hoàn Tất Sau Cưới"
epic: "Epic 4: Quản Lý Hợp Đồng & Xác Thực Giao Dịch 2 Chiều"
use_cases: ["UC-4.3", "UC-4.4", "UC-4.6"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-03-04-Vendor-Listing-And-Booking.html#viewClientView"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-4.3, US-4.4 & US-4.6: XÁC THỰC 2 CHIỀU & BÁO HOÀN TẤT DỊCH VỤ CƯỚI

---

## 1. Tuyên Bố User Story (Story Statement)

* **US-4.3 (Khách hàng xác nhận hợp đồng):**
  > **Là** một Cô dâu / Chú rể đã đặt cọc dịch vụ với NCC,  
  > **Tôi muốn** kiểm tra lại tổng giá trị hợp đồng, số tiền cọc thực tế trên thông báo của Sàn và bấm nút [Xác Nhận Hợp Đồng],  
  > **Để** tôi nhận được gói quà mừng cưới từ Sàn (Voucher 500k + Bộ thiệp online VIP) và đảm bảo quyền lợi khi xảy ra tranh chấp.

* **US-4.4 (Khách hàng từ chối xác nhận):**
  > **Là** một Cô dâu / Chú rể,  
  > **Tôi muốn** bấm từ chối và ghi rõ lý do khi phát hiện NCC nhập sai tổng giá trị hoặc số tiền cọc,  
  > **Để** hợp đồng được trả về cho NCC điều chỉnh lại chính xác trước khi ghi nhận hoa hồng.

* **US-4.6 (Báo hoàn tất sau đám cưới):**
  > **Là** một Cô dâu / Chú rể (hoặc Nhà Cung Cấp),  
  > **Tôi muốn** bấm xác nhận đám cưới đã diễn ra thành công và hoàn tất tất toán chi phí,  
  > **Để** hệ thống chuyển hợp đồng sang trạng thái `Completed`, mở khóa quyền đánh giá Review có kiểm duyệt và đối soát hoa hồng Kỳ 2.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria)

### Kịch bản 1: Khách hàng bấm xác nhận hợp đồng (UC 4.3)
```gherkin
Given Khách hàng mở màn hình thông báo xác nhận hợp đồng (qua App hoặc link Zalo)
When Khách hàng kiểm tra đúng: Gói sảnh tiệc, Tổng giá trị = 180M, Tiền cọc = 50M, Mã Voucher WVIP8899
And Bấm nút "✓ Bấm Xác Nhận Hợp Đồng"
Then Trạng thái BookingContract chuyển sang "Confirmed"
And Trạng thái Voucher chuyển sang "Redeemed"
And Trạng thái Lead chuyển sang "Converted"
And Hệ thống kích hoạt tính hoa hồng Kỳ 1 (Job 5.1) và gửi quà mừng cưới vào ví tài khoản khách hàng
```

### Kịch bản 2: Khách hàng từ chối xác nhận (UC 4.4)
```gherkin
Given Khách hàng phát hiện NCC nhập sai số tiền cọc (thực tế cọc 30M nhưng NCC nhập 50M)
When Khách hàng bấm "Sai thông tin? Từ chối" và nhập lý do: "Tôi chỉ mới đặt cọc 30 triệu, vui lòng sửa lại"
Then Trạng thái BookingContract trả về "Draft"
And Gửi thông báo yêu cầu NCC chỉnh sửa lại thông tin hợp đồng
```

### Kịch bản 3: Báo cáo hoàn tất dịch vụ cưới sau ngày diễn ra (UC 4.6)
```gherkin
Given Đám cưới đã diễn ra vào ngày 20/12/2026 và hợp đồng đang ở trạng thái "Confirmed"
When Khách hàng hoặc NCC bấm nút "Đám Cưới Đã Hoàn Tất Trọn Vẹn"
Then Trạng thái BookingContract chuyển sang "Completed"
And Hệ thống mở khóa quyền viết đánh giá kèm huy hiệu "[Verified Buyer]" cho Khách hàng
And Kích hoạt đưa hoa hồng Kỳ 2 (50% còn lại) vào kỳ đối soát ngày 25 tiếp theo (Job 5.6)
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-005` (Xác thực 2 chiều):** Phải có sự đồng thuận từ phía Khách hàng thì hợp đồng mới có giá trị pháp lý với Sàn.
- **`BR-006` (Hoa hồng 2 kỳ):** Đợt 1 thu sau khi `Confirmed`; Đợt 2 thu sau khi `Completed`.
- **Thực thể dữ liệu:** `BookingContract`, `Voucher`, `Lead`, `Review`, `Commission` (theo [[ba-data-model]]).
