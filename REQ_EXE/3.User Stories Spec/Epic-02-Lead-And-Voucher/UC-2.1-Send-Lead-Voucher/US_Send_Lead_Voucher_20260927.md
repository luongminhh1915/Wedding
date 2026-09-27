---
id: "US-2.1"
title: "User Story: Gửi Yêu Cầu Tư Vấn & Nhận Mã Ưu Đãi Độc Quyền (Smart Privacy)"
epic: "Epic 2: Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead"
use_cases: ["UC-2.1"]
status: "Ready"
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-02-Lead-And-Vendor-Portal.html#modalClientLead"
ui_spec: "docs-BA/User Stories Spec/Epic-02-Lead-And-Voucher/UC-2.1-Send-Lead-Voucher/reference/UI_Spec_Send_Lead_Voucher_20260927.md"
sequence_spec: "docs-BA/User Stories Spec/Epic-02-Lead-And-Voucher/UC-2.1-Send-Lead-Voucher/reference/Sequence_Send_Lead_Voucher_20260927.md"
---

# ĐẶC TẢ USER STORY (USER STORY SPECIFICATION)
## US-2.1: GỬI YÊU CẦU TƯ VẤN & NHẬN MÃ ƯU ĐÃI ĐỘC QUYỀN

---

## 1. Tuyên Bố User Story (Story Statement)

> **Là** một Cô dâu / Chú rể đang quan tâm đến một gói dịch vụ cưới trên sàn,  
> **Tôi muốn** gửi yêu cầu tư vấn nhanh với thông tin ngày cưới và ngân sách dự kiến (sau khi xác thực số điện thoại bằng OTP),  
> **Để** tôi nhận được mã ưu đãi độc quyền (Voucher 8 ký tự), đồng thời được Nhà cung cấp liên hệ tư vấn trong vòng 2 giờ mà không bị lộ số điện thoại cá nhân (chính sách Smart Privacy).

---

## 2. Tiêu Chí Nghiệm Thu (Acceptance Criteria - Given/When/Then)

### Kịch bản 1: Mở Modal và xác thực số điện thoại bằng OTP
```gherkin
Given Khách hàng đang ở trang chi tiết gói dịch vụ và bấm nút "Gửi Yêu Cầu Tư Vấn & Nhận Voucher"
When Pop-up Modal hiển thị
Then Khách hàng nhập họ tên, ngày cưới dự kiến, khoảng ngân sách và số điện thoại
When Khách hàng bấm "Gửi OTP"
Then Hệ thống gửi mã OTP 6 số qua tin nhắn SMS/Zalo trong vòng <= 10 giây
And Nút bấm chuyển sang trạng thái đếm ngược 60 giây trước khi được bấm gửi lại
```

### Kịch bản 2: Gửi yêu cầu tư vấn thành công và nhận mã Voucher độc nhất
```gherkin
Given Khách hàng nhập đúng mã OTP 6 số
When Khách hàng bấm nút "Gửi Lead & Lấy Mã Voucher"
Then Hệ thống tạo mới một bản ghi "Lead" ở trạng thái "New"
And Tự động sinh mã "Voucher" độc nhất 8 ký tự (vd: "WVIP8899") có hiệu lực 30 ngày
And Hiển thị Modal chúc mừng với mã Voucher to rõ kèm nút "Sao chép mã"
And Kích hoạt thông báo Zalo ZNS tới số điện thoại của Chủ Nhà cung cấp trong vòng <= 10 giây
```

### Kịch bản 3: Đảm bảo chính sách bảo mật số điện thoại (Smart Privacy)
```gherkin
Given Yêu cầu tư vấn đã được gửi thành công
When Dữ liệu được hiển thị trên Vendor Portal của Nhà cung cấp
Then Số điện thoại của khách hàng bắt buộc phải bị che ở dạng "0988***544"
And Chỉ khi khách hàng bấm đồng ý nhận cuộc gọi trong phòng Chat thì NCC mới được xem số điện thoại đầy đủ
```

### Kịch bản 4: Xử lý nhập mã OTP không chính xác
```gherkin
Given Khách hàng nhập mã OTP không trùng khớp với mã hệ thống đã gửi
When Bấm gửi yêu cầu
Then Hệ thống hiển thị cảnh báo đỏ: "Mã OTP không chính xác hoặc đã hết hạn. Vui lòng kiểm tra lại!"
And Không tạo bản ghi Lead và giữ nguyên dữ liệu form đã nhập
```

---

## 3. Quy Tắc Nghiệp Vụ & Dữ Liệu Liên Quan

- **`BR-002` (Smart Privacy):** Bảo mật số điện thoại ban đầu dưới dạng mã hóa.
- **`BR-003` (SLA Lead 2h):** Khởi tạo mốc đếm ngược thời gian cam kết phản hồi 2 giờ cho NCC.
- **`BR-004` (Voucher độc nhất):** Cấp mã 8 ký tự gắn liền với Lead để đối soát hoa hồng sau này.
- **Thực thể dữ liệu:** `Lead`, `Voucher`, `Listing`, `Vendor`, `User` (theo [[ba-data-model]]).
