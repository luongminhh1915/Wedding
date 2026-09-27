---
id: "US-5.4"
title: "User Story: Hệ Thống Tiếp Nhận Webhook Xác Nhận Thanh Toán Hoa Hồng Từ Ngân Hàng / VietQR"
epic: "Epic 5: Đối Soát Hoa Hồng Môi Giới 2 Kỳ & Thu Phí"
use_cases: ["UC-5.4"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-05-06-Commission-And-Review.html#tab-settlement"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-5.4: TIẾP NHẬN WEBHOOK XÁC NHẬN THANH TOÁN HOA HỒNG TỰ ĐỘNG

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** Hệ thống Nền tảng (Platform Integration Gateway),  
> **Tôi muốn** cung cấp API Endpoint an toàn nhận Webhook giao dịch biến động số dư từ Đối tác Ngân hàng / Cổng VietQR theo thời gian thực (realtime),  
> **Để** tự động bóc tách cú pháp chuyển khoản, khớp mã kỳ đối soát, gạch nợ tự động và cập nhật trạng thái đã thanh toán cho Nhà Cung Cấp trong vòng 3 giây.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Nhận Webhook hợp lệ và gạch nợ thành công
```gherkin
Given Đối tác Ngân hàng gửi HTTP POST request tới "/api/v1/webhooks/bank-payment"
And Header chứa chữ ký số X-Signature hợp lệ (HMAC-SHA256)
And Body chứa thông tin giao dịch:
    | Field | Value |
    | transactionId | "VCB2026092700918" |
    | amount | 12500000 |
    | description | "EXEHOAHONG SETTLE-202609-V001" |
    | transactionTime | "2026-09-27T10:15:30Z" |
When Hệ thống tiếp nhận và xác thực chữ ký số thành công
Then Phân tích chuỗi description để trích xuất settlementId: "SETTLE-202609-V001"
And Tìm bản ghi CommissionSettlement tương ứng:
    - Kiểm tra số tiền nhận 12.500.000đ khớp 100% với totalAmount của bảng kê
    - Cập nhật CommissionSettlement.status = "Paid"
    - Cập nhật CommissionSettlement.paidAt = "2026-09-27T10:15:30Z"
    - Cập nhật toàn bộ các CommissionTransaction thành phần sang status = "Settled"
And Bắn thông báo realtime qua WebSocket tới Vendor Portal để đóng Modal VietQR và cập nhật giao diện
And Phản hồi HTTP 200 OK cho Ngân hàng kèm body: `{"code": "00", "message": "Reconciled Successfully"}`
```

### Kịch bản 2: Giao dịch sai số tiền hoặc sai cú pháp
```gherkin
Given Ngân hàng gửi webhook với description: "White Peony nop tien" (không trích xuất được settlementId hợp lệ)
When Hệ thống phân tích cú pháp thất bại
Then Lưu giao dịch vào bảng UnrecognizedBankTransactions với status = "PendingManualReview"
And Gửi cảnh báo Slack/Telegram nội bộ cho Đội Kế toán Sàn để đối soát thủ công
And Trả về HTTP 200 OK cho Ngân hàng (để tránh ngân hàng retry lặp lại)
```

### Kịch bản 3: Chống tấn công giả mạo Webhook (Replay / Tampering Attack)
```gherkin
Given Request gửi tới Endpoint webhook nhưng chữ ký X-Signature không khớp với Secret Key của Sàn
When Hệ thống kiểm tra tính toàn vẹn
Then Từ chối xử lý ngay lập tức
And Trả về HTTP 401 Unauthorized kèm body: `{"error": "Invalid Signature"}`
And Ghi log cảnh báo an ninh bảo mật vào AuditLog
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Idempotency:** Mỗi `transactionId` từ ngân hàng chỉ được xử lý đúng 1 lần duy nhất, tránh việc nạp tiền trùng lặp khi webhook bị retry.
- **Thực thể dữ liệu:** `CommissionSettlement`, `PaymentTransaction`, `AuditLog` (theo [[ba-data-model]]).
