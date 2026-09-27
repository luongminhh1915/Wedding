---
id: "US-5.1-5.2-5.6"
title: "User Story: Hệ Thống Tiến Trình Nền Tính Toán, Tổng Hợp Đối Soát Hoa Hồng 2 Kỳ & Quyết Toán Sau Cưới"
epic: "Epic 5: Đối Soát Hoa Hồng Môi Giới 2 Kỳ & Thu Phí"
use_cases: ["UC-5.1", "UC-5.2", "UC-5.6"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-05-06-Commission-And-Review.html#tab-settlement"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-5.1, US-5.2 & US-5.6: CÁC TIẾN TRÌNH NỀN TÍNH TOÁN VÀ ĐỐI SOÁT HOA HỒNG 2 KỲ (KỲ 1 & KỲ 2)

---

## 1. Tuyên Bố User Story (Story Statements)

### Cho UC-5.1 (Tính hoa hồng Kỳ 1 - 50%):
> **Là** Hệ thống Nền tảng (Platform Backend Engine),  
> **Tôi muốn** tự động lắng nghe sự kiện Hợp đồng dịch vụ cưới được khách hàng xác nhận 2 chiều thành công (`BookingContract.status = 'Confirmed'`), tính toán tổng hoa hồng sàn theo tỷ lệ cam kết và tách 50% ghi nhận vào sổ cái hoa hồng Kỳ 1,  
> **Để** làm căn cứ đưa vào kỳ đối soát gần nhất mà không cần kế toán tính thủ công.

### Cho UC-5.2 (Tổng hợp bảng kê ngày 25 hàng tháng):
> **Là** Hệ thống Nền tảng (Platform Scheduled Job),  
> **Tôi muốn** tự động quét toàn bộ các khoản hoa hồng chưa đối soát vào 00:00 ngày 25 hàng tháng, gom nhóm theo từng Nhà Cung Cấp, tính tổng công nợ, sinh mã đối soát duy nhất và tạo mã VietQR động,  
> **Để** phát hành Bảng kê đối soát và gửi thông báo thanh toán (hạn 5 ngày đến ngày 30) cho NCC qua Zalo ZNS và Email.

### Cho UC-5.6 (Quyết toán hoa hồng Kỳ 2 sau đám cưới):
> **Là** Hệ thống Nền tảng (Platform Daily Event/Schedule Job),  
> **Tôi muốn** tự động quét các Hợp đồng đã diễn ra đám cưới thành công (`EventDate <= CURRENT_DATE` và đã báo cáo hoàn tất dịch vụ), giải phóng khoản hoa hồng 50% Kỳ 2 từ trạng thái đóng băng (`EscrowLocked`) sang trạng thái sẵn sàng đối soát (`Unsettled`),  
> **Để** đưa vào bảng kê đối soát ngày 25 của tháng tiếp theo.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1 (UC-5.1): Tính toán hoa hồng Kỳ 1 khi Hợp đồng đạt Confirmed
```gherkin
Given Hợp đồng "CTR-2026-0081" vừa được Khách hàng bấm Xác nhận 2 chiều thành công
And Giá trị hợp đồng TotalContractValue = 50.000.000đ, Tỷ lệ hoa hồng CommissionRate = 10%
When Hệ thống xử lý sự kiện Domain "ContractConfirmedEvent"
Then Tính Tổng hoa hồng: 50.000.000đ * 10% = 5.000.000đ
And Tạo bản ghi CommissionTransaction cho Kỳ 1:
    | Field | Value |
    | contractId | CTR-2026-0081 |
    | period | "Period1" |
    | percentage | 50% |
    | amount | 2.500.000đ |
    | status | "Unsettled" |
And Tạo bản ghi CommissionTransaction cho Kỳ 2:
    | Field | Value |
    | contractId | CTR-2026-0081 |
    | period | "Period2" |
    | percentage | 50% |
    | amount | 2.500.000đ |
    | status | "EscrowLocked" |
And Ghi nhận log kế toán kiểm toán vào bảng AuditLog
```

### Kịch bản 2 (UC-5.2): Chạy Job đối soát định kỳ ngày 25 hàng tháng
```gherkin
Given Đồng hồ hệ thống chạm mốc 00:00:00 ngày 25 hàng tháng
When Job "MonthlyCommissionSettlementJob" kích hoạt
Then Hệ thống quét các bản ghi CommissionTransaction có status = "Unsettled"
And Gom nhóm theo từng vendorId (Ví dụ: Vendor "White Peony Studio")
And Tính Tổng số tiền phải thanh toán = Tổng (amount) các giao dịch hợp lệ
And Sinh bản ghi CommissionSettlement:
    | Field | Value |
    | settlementId | "SETTLE-202609-V001" |
    | vendorId | "V001" |
    | monthYear | "2026-09" |
    | totalAmount | 12.500.000đ |
    | dueDate | "2026-09-30 23:59:59" |
    | paymentSyntax | "EXEHOAHONG SETTLE-202609-V001" |
    | status | "PendingPayment" |
And Tạo mã VietQR động theo chuẩn NAPAS với đúng số tiền và nội dung chuyển khoản
And Kích hoạt gửi thông báo Zalo ZNS và Email kèm bảng kê PDF tới Chủ cơ sở NCC
```

### Kịch bản 3 (UC-5.6): Giải phóng hoa hồng Kỳ 2 sau ngày tổ chức lễ cưới
```gherkin
Given Hợp đồng "CTR-2026-0043" có Ngày tổ chức đám cưới EventDate = "2026-09-12"
And Đã có báo cáo hoàn tất dịch vụ ServiceCompleted từ khách hoặc quá 3 ngày sau cưới không có khiếu nại
When Job "ReleasePeriod2CommissionJob" chạy lúc 01:00 hàng ngày
Then Hệ thống tìm thấy giao dịch hoa hồng Kỳ 2 của Hợp đồng đang ở status = "EscrowLocked"
And Cập nhật status chuyển sang "Unsettled"
And Gắn cờ eligibleForSettlement = true
And Khoản tiền này sẽ được gom vào bảng kê ngày 25/09 của kỳ đối soát hiện tại
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-006`:** Tỷ lệ phân bổ hoa hồng Sàn là 50% Kỳ 1 (ngay sau khi xác nhận hợp đồng) và 50% Kỳ 2 (sau khi tổ chức đám cưới thành công).
- **`BR-007`:** Chu kỳ đối soát cố định chốt vào ngày 25 hàng tháng, hạn thanh toán là ngày 30.
- **`FM-003` & `FM-004`:** Công thức tính hoa hồng và phân chia tỷ lệ 2 kỳ đối soát.
- **Thực thể dữ liệu:** `CommissionTransaction`, `CommissionSettlement`, `BookingContract`, `Vendor` (theo [[ba-data-model]]).
