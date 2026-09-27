---
id: "US-5.5"
title: "User Story: Kế Toán Sàn Đối Soát Thủ Công & Xuất Hóa Đơn Điện Tử VAT Cho Nhà Cung Cấp"
epic: "Epic 5: Đối Soát Hoa Hồng Môi Giới 2 Kỳ & Thu Phí"
use_cases: ["UC-5.5"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-05-06-Commission-And-Review.html#tab-settlement"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-5.5: KẾ TOÁN DUYỆT ĐỐI SOÁT THỦ CÔNG & XUẤT HÓA ĐƠN VAT (E-INVOICE)

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Kế toán viên Sàn (Finance / Accountant),  
> **Tôi muốn** truy cập màn hình Quản lý đối soát trên Admin Back-office để tra cứu các giao dịch chuyển khoản bị lỗi cú pháp, khớp nối thủ công với mã kỳ đối soát của Vendor và bấm lệnh xuất Hóa đơn điện tử VAT (e-Invoice) gửi thẳng qua email của đối tác,  
> **Để** hoàn tất nghĩa vụ tài chính, thuế và giải tỏa trạng thái nợ hoa hồng cho Nhà Cung Cấp một cách minh bạch.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Đối soát thủ công giao dịch chuyển khoản sai cú pháp
```gherkin
Given Kế toán mở danh sách "Giao dịch ngân hàng chưa khớp"
And Thấy khoản tiền 12.500.000đ từ tài khoản công ty "White Peony Studio" nhưng nội dung ghi "White Peony nop tien thang 9"
When Kế toán tìm kiếm mã kỳ đối soát "SETTLE-202609-V001" của NCC White Peony
And Bấm nút "Khớp nối thủ công & Gạch nợ"
And Nhập ghi chú: "Đã kiểm tra khớp số tiền và sao kê ngân hàng MBBank ngày 27/09"
Then Hệ thống cập nhật bảng kê SETTLE-202609-V001 sang trạng thái "Paid"
And Lưu thông tin người duyệt: FinanceAdminID và Thời điểm duyệt
And Gửi thông báo xác nhận thanh toán thành công tới Vendor Portal
```

### Kịch bản 2: Bấm lệnh xuất Hóa đơn điện tử VAT (VNPT/MISA e-Invoice)
```gherkin
Given Kỳ đối soát SETTLE-202609-V001 đã ở trạng thái "Paid"
When Kế toán bấm nút "🧾 Xuất Hóa Đơn Điện Tử VAT"
Then Hệ thống tự động tổng hợp thông tin pháp nhân của Vendor:
    - Tên công ty / Hộ kinh doanh: CÔNG TY TNHH WHITE PEONY WEDDING
    - Mã số thuế (MST): 0317894561
    - Địa chỉ: 123 Pasteur, P. Võ Thị Sáu, Q.3, TP.HCM
    - Dịch vụ: Phí môi giới dịch vụ cưới tháng 09/2026
    - Doanh thu chưa thuế: 11.574.074 đ
    - Thuế suất VAT: 8% (925.926 đ)
    - Tổng thanh toán: 12.500.000 đ
And Gửi request tới API của nhà cung cấp hóa đơn điện tử (VNPT-Invoice / MISA meInvoice)
When Nhận phản hồi thành công kèm Số hóa đơn (VD: HD-2026-09001) và Mã tra cứu CQT
Then Lưu link file PDF hóa đơn vào hệ thống
And Tự động gửi email đính kèm file Hóa đơn VAT tới email kế toán của NCC
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **Thuế & Pháp lý:** Mọi khoản thu hoa hồng môi giới của Sàn đều phải xuất hóa đơn điện tử có mã của Cơ quan Thuế theo đúng Nghị định 123/2020/NĐ-CP.
- **Thực thể dữ liệu:** `CommissionSettlement`, `VendorTaxProfile`, `EInvoiceRecord`, `AuditLog` (theo [[ba-data-model]]).
