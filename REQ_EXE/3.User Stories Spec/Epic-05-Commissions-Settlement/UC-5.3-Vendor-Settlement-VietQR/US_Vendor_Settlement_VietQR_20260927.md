---
id: "US-5.3"
title: "User Story: Nhà Cung Cấp Xem Bảng Kê Đối Soát & Thanh Toán Hoa Hồng Qua VietQR"
epic: "Epic 5: Đối Soát Hoa Hồng Môi Giới 2 Kỳ & Thu Phí"
use_cases: ["UC-5.3"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-05-06-Commission-And-Review.html#tab-settlement"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-5.3: NCC XEM BẢNG KÊ ĐỐI SOÁT & THANH TOÁN HOA HỒNG QUA VIETQR

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Chủ Nhà Cung Cấp (Vendor Owner),  
> **Tôi muốn** truy cập màn hình Đối soát trên Vendor Portal để xem chi tiết danh sách các hợp đồng dịch vụ cấu thành số tiền hoa hồng cần thanh toán trong kỳ ngày 25 và bấm mở mã VietQR động chuẩn NAPAS,  
> **Để** quét thanh toán hoa hồng cho Sàn một cách nhanh chóng, chính xác tuyệt đối mà không sợ sai số tiền hay sai cú pháp chuyển khoản.

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Xem bảng kê chi tiết kỳ đối soát hiện tại
```gherkin
Given Vendor đăng nhập vào Vendor Portal và bấm vào menu "Tài Chính & Đối Soát"
When Hệ thống tải kỳ đối soát mới nhất (Ví dụ: Tháng 09/2026, Mã: SETTLE-202609-V001)
Then Hiển thị tổng số tiền phải thanh toán: 12.500.000đ, hạn chót ngày 30/09/2026
And Hiển thị bảng chi tiết các hợp đồng:
    - 3 Hợp đồng thuộc Kỳ 1 (Tổng hoa hồng Kỳ 1: 7.500.000đ)
    - 2 Hợp đồng thuộc Kỳ 2 (Tổng hoa hồng Kỳ 2: 5.000.000đ)
And Mỗi dòng hiển thị đầy đủ: Mã HĐ, Tên Cặp đôi, Tên gói dịch vụ, Ngày cưới, Giá trị HĐ, Tỷ lệ hoa hồng (10%) và Số tiền tương ứng
```

### Kịch bản 2: Bấm nút thanh toán VietQR và quét mã thành công
```gherkin
Given Vendor đang xem bảng kê và bấm nút "Thanh Toán VietQR (12.500.000 đ)"
When Modal VietQR xuất hiện trên màn hình
Then Hiển thị hình ảnh mã QR chuẩn NAPAS 24/7 chứa sẵn thông tin:
    - Ngân hàng thụ hưởng: MBBank
    - Số tài khoản: 0987654321
    - Chủ tài khoản: CONG TY CO PHAN WEDDING EXE
    - Số tiền: 12.500.000 đ
    - Cú pháp nội dung: "EXEHOAHONG SETTLE-202609-V001"
And Hệ thống bật cơ chế WebSocket/Long-polling lắng nghe trạng thái thanh toán
When Vendor dùng App ngân hàng quét mã và xác nhận chuyển khoản thành công
Then Webhook cập nhật trạng thái bảng kê sang "Paid"
And Modal VietQR tự động đóng lại, hiển thị thông báo: "Quyết toán hoa hồng thành công!"
And Trạng thái trên giao diện chuyển sang badge xanh lá "Đã Thanh Toán"
```

### Kịch bản 3: Vendor khiếu nại sai lệch thông tin hợp đồng trong bảng kê
```gherkin
Given Vendor phát hiện 1 hợp đồng trong bảng kê bị sai lệch giá trị hoặc đã bị khách hủy
When Vendor bấm nút "Yêu cầu rà soát / Khiếu nại kỳ này"
Then Hệ thống mở form nhập lý do khiếu nại kèm mã HĐ liên quan
When Vendor gửi yêu cầu
Then Trạng thái bảng kê chuyển sang "InDispute" (Đang rà soát)
And Kích hoạt thông báo tới Kế toán Sàn để kiểm tra đối chiếu lại
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-007`:** Hạn chót thanh toán đối soát là 5 ngày kể từ ngày chốt (từ ngày 25 đến hết ngày 30 hàng tháng). Quá hạn sẽ bị cảnh báo và hạ thứ hạng hiển thị.
- **Thực thể dữ liệu:** `CommissionSettlement`, `CommissionTransaction`, `BookingContract` (theo [[ba-data-model]]).
