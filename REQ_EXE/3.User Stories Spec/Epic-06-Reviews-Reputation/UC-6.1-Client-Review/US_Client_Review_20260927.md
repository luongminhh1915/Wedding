---
id: "US-6.1"
title: "User Story: Khách Hàng Gửi Đánh Giá Dịch Vụ Cưới & Chấm Điểm Số Sao (Verified Buyer Review)"
epic: "Epic 6: Đánh Giá Trải Nghiệm & Uy Tín Đối Tác"
use_cases: ["UC-6.1"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-05-06-Commission-And-Review.html#reviewModal"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-6.1: KHÁCH HÀNG GỬI ĐÁNH GIÁ DỊCH VỤ CƯỚI & CHẤM ĐIỂM SỐ SAO (1 - 5 SAO)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Cặp đôi Cô dâu / Chú rể đã tổ chức đám cưới qua Sàn (Verified Buyer),  
> **Tôi muốn** mở form đánh giá để chấm điểm số sao theo 4 tiêu chí cốt lõi, viết nhận xét trải nghiệm thực tế và tải lên tối đa 5 hình ảnh đám cưới thật,  
> **Để** chia sẻ cảm nhận chân thực, giúp các cặp đôi đi sau có nguồn tham khảo uy tín và tri ân Nhà Cung Cấp đã phục vụ chu đáo.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Khách hàng gửi đánh giá đầy đủ hợp lệ
```gherkin
Given Khách hàng có Hợp đồng "CTR-2026-0043" đã hoàn tất đám cưới (ServiceCompleted)
When Khách hàng mở Modal Đánh giá và thực hiện:
    - Chấm điểm 4 tiêu chí: Thẩm mỹ (5*), Phục vụ (5*), Đúng hẹn (5*), Chi phí (5*) -> Điểm TB Overall = 5.0
    - Nhập nội dung nhận xét: "Team White Peony làm việc trên cả tuyệt vời..." (tối thiểu 30 ký tự)
    - Tải lên 3 ảnh chụp thực tế tiệc cưới (JPG/PNG < 5MB/ảnh)
And Bấm nút "Gửi Đánh Giá Ngay"
Then Bản ghi Review được tạo với:
    | Field | Value |
    | contractId | "CTR-2026-0043" |
    | vendorId | "V001" |
    | userId | "USR-LONGTHU" |
    | rating | 5.0 |
    | isVerifiedBuyer | true |
    | status | "PendingModeration" |
And Hiển thị thông báo cảm ơn và thông tin: "Đánh giá của bạn đang được kiểm duyệt (SLA < 24h)"
And Gửi bản ghi vào hàng đợi ReviewModerationQueue cho Đội ngũ Kiểm duyệt viên
```

### Kịch bản 2: Chặn tài khoản ảo hoặc người chưa từng ký hợp đồng đánh giá
```gherkin
Given Một tài khoản khách vãng lai hoặc chưa từng có hợp đồng xác thực với NCC "White Peony"
When Cố gắng truy cập form đánh giá trực tiếp qua URL hoặc gọi API POST /api/v1/reviews
Then Hệ thống kiểm tra điều kiện tiên quyết: Không tìm thấy Hợp đồng ở trạng thái Confirmed/ServiceCompleted
And Chặn gửi đánh giá, hiển thị thông báo: "Chỉ các cặp đôi đã xác thực hợp đồng qua Sàn mới được quyền gửi đánh giá uy tín!"
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-008`:** Đánh giá 100% người dùng thật (Verified Buyer Only). Chỉ cho phép đánh giá khi Hợp đồng có mã Voucher và đã diễn ra hoặc xác thực thành công.
- **Thực thể dữ liệu:** `Review`, `ReviewMedia`, `BookingContract`, `Vendor` (theo [[ba-data-model]]).
