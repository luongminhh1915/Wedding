---
title: "UI Specification: Hàng Đợi Lead & Phòng Chat Báo Giá (SCR-VENDOR-LEAD-CHAT)"
epic: "Epic 2: Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead"
use_cases: ["UC-2.3", "UC-2.4"]
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-02-Lead-And-Vendor-Portal.html"
---

# ĐẶC TẢ GIAO DIỆN NGƯỜI DÙNG (UI SPECIFICATION)
## MÀN HÌNH: HÀNG ĐỢI LEAD & PHÒNG CHAT BÁO GIÁ CỦA NCC (SCR-VENDOR-LEAD-CHAT)

---

## 1. Thông Tin Chung Màn Hình

- **Mã màn hình:** `SCR-VENDOR-LEAD-CHAT`
- **Tên màn hình:** Quản Lý Hàng Đợi Lead & Phòng Chat Trực Tiếp
- **Ứng dụng:** Vendor Portal (Web Portal Tinh Gọn dành cho Chủ Nhà Cung Cấp)
- **Đường dẫn (URL):** `/vendor/leads` và `/vendor/chat/[lead_id]`
- **Đối tượng sử dụng:** Nhà Cung Cấp (Vendor Owner)
- **File Prototype tham chiếu:** [SCR-02-Lead-And-Vendor-Portal.html](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Prototype/SCR-02-Lead-And-Vendor-Portal.html)

---

## 2. Ma Trận Điều Khiển Giao Diện (UI Control Matrix)

| STT | Mã Control | Tên Phần Tử | Loại Control | Mô Tả & Ràng Buộc | Hành Vi Khi Tương Tác |
|:---|:---|:---|:---|:---|:---|
| 1 | `LBL_SLA_TIMER` | Đồng hồ đếm ngược SLA | Text Badge | Format: `Còn X giờ Y phút` | Đổi màu đỏ khi thời gian còn dưới 30 phút; quá 24h tự chuyển Cancelled |
| 2 | `BTN_ACCEPT_LEAD`| Nút Tiếp nhận Lead | Button Primary | Text: "✓ Tiếp Nhận Lead" | Bấm nút chuyển trạng thái Lead từ `New` sang `Accepted`, ghi nhận `accepted_at` |
| 3 | `BTN_OPEN_CHAT` | Nút Mở phòng chat | Button Accent | Text: "💬 Mở Phòng Chat" | Mở khung chat In-App thời gian thực (UC 2.4), chuyển trạng thái sang `InConsultation` |
| 4 | `CHAT_WINDOW` | Cửa sổ tin nhắn | Chat Container | Danh sách tin nhắn hiển thị bong bóng chat | Tự động cuộn xuống tin nhắn mới nhất |
| 5 | `BTN_ATTACH_QUOTE`| Nút đính kèm báo giá | Button Icon | Icon kẹp giấy | Cho phép tải lên file PDF/hình ảnh bảng báo giá chi tiết |
| 6 | `BTN_CREATE_DEAL` | Nút Tạo Hợp Đồng | Button Gradient | Text: "📝 Tạo Hợp Đồng & Nhập Voucher" | Mở Modal tạo hợp đồng và xác thực 2 chiều (UC 4.1) |

---

## 3. Các Trạng Thái Thẻ Lead Trên Giao Diện

1. **Thẻ Lead Mới (`New`):** Viền đỏ nhạt, có nhãn "Lead Mới (Chưa nhận)", đồng hồ đếm ngược SLA 2h nhấp nháy, số điện thoại bị che `0987***123`.
2. **Thẻ Đang Tư Vấn (`InConsultation`):** Viền xám chuẩn, nhãn "Đang Tư Vấn", hiển thị thời gian tiếp nhận và nút mở phòng chat.
3. **Thẻ Quá Hạn Bị Hủy (`Cancelled`):** Làm mờ (Opacity 60%), hiển thị nhãn "Quá hạn SLA", không thể bấm tiếp nhận.
