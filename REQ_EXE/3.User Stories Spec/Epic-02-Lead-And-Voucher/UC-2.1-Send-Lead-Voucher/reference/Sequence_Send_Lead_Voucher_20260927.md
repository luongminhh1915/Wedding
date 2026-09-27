---
title: "Sequence Diagram & API Specifications: Gửi Yêu Cầu Tư Vấn & Cấp Mã Ưu Đãi"
epic: "Epic 2: Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead"
use_cases: ["UC-2.1"]
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA & Solution Architect"
---

# SEQUENCE DIAGRAM & ĐẶC TẢ API (TECHNICAL SPECIFICATION)
## MODULE: GỬI YÊU CẦU TƯ VẤN & CẤP MÃ VOUCHER (UC 2.1)

---

## 1. Sơ Đồ Trình Tự Tương Tác (Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor Client as Cô dâu / Chú rể
    participant FE as Customer Web Portal
    participant API as Backend Core API
    participant ZNS as Zalo ZNS Service
    participant DB as PostgreSQL Database

    %% Bước 1: Khách hàng nhập OTP và Gửi Lead
    Client->>FE: 1. Điền Form, nhập SĐT, OTP 889900 & bấm "Gửi Lead"
    FE->>API: 2. POST /api/v1/leads { listing_id, customer_name, phone, otp, wedding_date, budget }
    API->>API: 3. Xác thực OTP & Sinh mã bảo mật: phone_masked = "0987***123"
    API->>API: 4. Sinh mã Voucher định danh độc nhất (8 ký tự, vd: "WVIP8899")
    API->>DB: 5. INSERT INTO leads (status='New', phone_masked=...) RETURNING lead_id
    API->>DB: 6. INSERT INTO vouchers (code='WVIP8899', lead_id=..., status='Active')
    
    %% Kích hoạt Event bắn Zalo ZNS (UC 2.2)
    API->>ZNS: 7. Bắn webhook gửi tin nhắn Zalo ZNS tới số Hotline NCC (<10s)
    ZNS-->>API: 8. Phản hồi Delivery Success (Message ID)

    API-->>FE: 9. HTTP 201 Created { lead_id, voucher_code: "WVIP8899", expires_at: "..." }
    FE-->>Client: 10. Hiển thị Popup chúc mừng kèm Mã Voucher & Kích hoạt đếm ngược SLA 2h
```

---

## 2. Đặc Tả Chi Tiết API: `POST /api/v1/leads`

#### Request Headers:
* `Content-Type`: `application/json`
* `X-Client-Session`: UUID định danh phiên trình duyệt (nếu khách vãng lai chưa đăng nhập).

#### Request Body (JSON):
```json
{
  "listing_id": "c1f7a28e-5b12-4c28-97e3-0d5b1234abcd",
  "customer_name": "Quốc Bảo & Mai Anh",
  "phone": "0988665544",
  "otp_code": "889900",
  "wedding_date": "2026-12-20",
  "budget_expected": 180000000,
  "guest_count_expected": 350,
  "note": "Cần tư vấn sảnh tiệc tối có không gian đón khách ngoài trời"
}
```

#### Response Success (`HTTP 201 Created`):
```json
{
  "success": true,
  "data": {
    "lead_id": "lead-9988-7766-5544",
    "status": "New",
    "phone_masked": "0988***544",
    "voucher": {
      "code": "WVIP8899",
      "status": "Active",
      "discount_description": "Ưu đãi giảm 5% trên tổng giá trị hợp đồng dịch vụ cưới",
      "expires_at": "2026-10-27T23:59:59Z"
    },
    "vendor_sla": {
      "max_response_time": "2 hours",
      "deadline": "2026-09-27T15:25:00Z"
    }
  }
}
```

#### Response Errors:
* `HTTP 400 Bad Request`: Mã OTP sai hoặc hết hạn (`ERR_INVALID_OTP`).
* `HTTP 422 Unprocessable Entity`: Ngày cưới trong quá khứ (`ERR_INVALID_WEDDING_DATE`).

---

## 3. Quy Tắc Xử Lý Backend & Activity Rules

| Mã Rule | Tên Quy Tắc | Nội Dung Chi Tiết |
|:---|:---|:---|
| **R-LEAD-01** | Mã hóa SĐT Smart Privacy | Backend bắt buộc phải tạo trường `phone_masked` dạng `0988***544` khi lưu vào database. Trong response gửi sang Vendor ban đầu, tuyệt đối không lộ SĐT đầy đủ cho đến khi khách đồng ý (`BR-002`). |
| **R-LEAD-02** | Khóa thời hạn Voucher 30 ngày | Thuộc tính `expires_at` của Voucher luôn được tính bằng `created_at + 30 days` (`BR-004`). |
