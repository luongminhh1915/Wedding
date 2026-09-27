---
title: "UI Specification: Gửi Yêu Cầu Tư Vấn & Cấp Mã Ưu Đãi (SCR-SEND-LEAD-MODAL)"
epic: "Epic 2: Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead"
use_cases: ["UC-2.1"]
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-02-Lead-And-Vendor-Portal.html#modalClientLead"
---

# ĐẶC TẢ GIAO DIỆN NGƯỜI DÙNG (UI SPECIFICATION)
## MÀN HÌNH: GỬI YÊU CẦU TƯ VẤN & CẤP MÃ ƯU ĐÃI (SCR-SEND-LEAD-MODAL)

---

## 1. Thông Tin Chung Màn Hình

- **Mã màn hình:** `SCR-SEND-LEAD-MODAL`
- **Tên màn hình:** Pop-up Modal Gửi Yêu Cầu Tư Vấn & Nhận Voucher 8 Ký Tự
- **Ứng dụng:** Customer Web Portal (Modal Overlay tại trang chi tiết dịch vụ)
- **Đường dẫn (URL):** Modal xuất hiện tại `/dich-vu/[listing_slug]?action=consult`
- **Đối tượng sử dụng:** Cô dâu / Chú rể
- **File Prototype tham chiếu:** [SCR-02-Lead-And-Vendor-Portal.html](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Prototype/SCR-02-Lead-And-Vendor-Portal.html)

---

## 2. Ma Trận Điều Khiển Giao Diện (UI Control Matrix)

| STT | Mã Control | Tên Phần Tử | Loại Control | Kiểu Dữ Liệu | Bắt Buộc | Ràng Buộc & Validation | Hành Vi Tương Tác |
|:---|:---|:---|:---|:---|:---|:---|:---|
| 1 | `TXT_CUST_NAME` | Họ tên Dâu / Rể | Input Text | Chuỗi ký tự | Có | Độ dài 2 - 100 ký tự | Nhập tên để NCC xưng hô lịch sự |
| 2 | `DATE_WEDDING` | Ngày cưới dự kiến | Date Picker | Ngày tháng | Có | Ngày cưới phải lớn hơn hoặc bằng ngày hiện tại | Giúp NCC kiểm tra tình trạng trống lịch sảnh tiệc |
| 3 | `SEL_BUDGET` | Ngân sách dự kiến | Dropdown | Chọn khoảng giá | Có | Giá trị: 50M-100M, 100M-150M, 150M-200M... | Định hình quy mô tiệc để báo giá phù hợp |
| 4 | `TXT_PHONE` | Số điện thoại | Input Tel | 10 chữ số | Có | Regex: `^(0[3|5|7|8|9])+([0-9]{8})$` | Gắn nhãn: "Số điện thoại của bạn được ẩn an toàn (Smart Privacy)" |
| 5 | `BTN_SEND_OTP` | Nút gửi OTP | Button Outline | Text | Có | Đếm ngược 60 giây sau khi bấm | Gửi mã OTP 6 số qua tin nhắn SMS/Zalo |
| 6 | `TXT_OTP_CODE` | Ô nhập mã OTP | Input Number | 6 chữ số | Có | Định dạng 6 chữ số số học | Khớp mã xác thực |
| 7 | `TXT_NOTE` | Ghi chú yêu cầu | Textarea | Chuỗi ký tự | Không | Tối đa 500 ký tự | Ghi chú thêm về số lượng khách, tông màu... |
| 8 | `BTN_SUBMIT_LEAD`| Nút Gửi Lead | Button Primary | Text | — | Chỉ sáng khi đã nhập đủ thông tin và OTP hợp lệ | Gọi API `POST /api/v1/leads`, mở Popup mã Voucher |

---

## 3. Các Trạng Thái Giao Diện (UI States)

1. **Trạng thái khởi tạo (Initial Modal State):** Form trống, các trường yêu cầu có dấu `*` đỏ, nút "Gửi Lead" ở trạng thái disabled.
2. **Trạng thái gửi OTP thành công:** Hiện dòng thông báo xanh: "Mã OTP đã được gửi đến SĐT 098***. Vui lòng nhập trong 5 phút". Nút gửi lại OTP chuyển sang đếm ngược: "Gửi lại sau (59s)".
3. **Trạng thái hoàn thành (Success Voucher Modal State):**
   - Hiện icon pháo hoa chúc mừng.
   - Thẻ Voucher nổi bật với mã: `WVIP8899` (có nút 1 chạm để Copy mã).
   - Nội dung thông báo: "Yêu cầu của bạn đã được chuyển đến The Reverie Saigon. Nhà cung cấp sẽ phản hồi trong vòng 2 giờ. Bạn có thể mở phòng chat ngay bây giờ!".
