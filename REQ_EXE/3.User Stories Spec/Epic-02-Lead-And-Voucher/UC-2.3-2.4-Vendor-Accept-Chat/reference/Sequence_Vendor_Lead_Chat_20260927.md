---
title: "Sequence Diagram & API Specifications: Tiếp Nhận Lead & Phòng Chat Báo Giá"
epic: "Epic 2: Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead"
use_cases: ["UC-2.3", "UC-2.4"]
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA & Solution Architect"
---

# SEQUENCE DIAGRAM & ĐẶC TẢ API (TECHNICAL SPECIFICATION)
## MODULE: TIẾP NHẬN LEAD & CHAT BÁO GIÁ CỦA NCC (UC 2.3 & 2.4)

---

## 1. Sơ Đồ Trình Tự Tương Tác (Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor Vendor as Nhà Cung Cấp (Vendor Owner)
    participant VP as Vendor Portal Web
    participant API as Backend Core API
    participant WS as WebSocket / In-App Chat Server
    participant DB as PostgreSQL Database
    actor Client as Cô dâu / Chú rể

    %% Bước 1: Tiếp nhận Lead (UC 2.3)
    Vendor->>VP: 1. Xem danh sách Lead mới & bấm nút [Tiếp nhận Lead]
    VP->>API: 2. PATCH /api/v1/vendor/leads/{lead_id}/accept
    API->>DB: 3. UPDATE leads SET status='Accepted', accepted_at=NOW() WHERE id=lead_id
    DB-->>API: 4. Cập nhật thành công
    API-->>VP: 5. HTTP 200 OK { status: 'Accepted', accepted_at: '...' }
    VP-->>Vendor: 6. Cập nhật trạng thái sang 'Đang Tư Vấn' & Mở nút [Mở Phòng Chat]

    %% Bước 2: Chat trao đổi và gửi báo giá (UC 2.4)
    Vendor->>VP: 7. Bấm [Mở Phòng Chat] & nhập tin nhắn tư vấn + đính kèm file báo giá
    VP->>WS: 8. Gửi WebSocket event: `send_message` { lead_id, message, attachment_url }
    WS->>DB: 9. INSERT INTO chat_messages (lead_id, sender_id, content, ...)
    WS-->>Client: 10. Đẩy tin nhắn realtime tới Customer Portal của Dâu Rể (<1s)
    Client-->>WS: 11. Dâu rể phản hồi tin nhắn trong phòng chat
    WS-->>VP: 12. Cập nhật tin nhắn mới trên màn hình của NCC
```

---

## 2. Đặc Tả Chi Tiết API

### 2.1. `API-LEAD-03`: Tiếp nhận Lead (PATCH `/api/v1/vendor/leads/{lead_id}/accept`)
#### Request Headers:
* `Authorization`: `Bearer <vendor_jwt_token>`
#### Response Success (`HTTP 200 OK`):
```json
{
  "success": true,
  "data": {
    "lead_id": "lead-9988-7766-5544",
    "status": "Accepted",
    "accepted_at": "2026-09-27T13:30:00Z",
    "sla_performance": "Met SLA (Response time: 15 minutes)"
  }
}
```

### 2.2. `API-CHAT-01`: Lấy lịch sử chat (GET `/api/v1/leads/{lead_id}/messages`)
#### Response Success (`HTTP 200 OK`):
```json
{
  "success": true,
  "data": {
    "lead_id": "lead-9988-7766-5544",
    "messages": [
      {
        "id": "msg-001",
        "sender_role": "CLIENT",
        "sender_name": "Quốc Bảo & Mai Anh",
        "content": "Chào bên The Reverie Saigon, mình nhận được mã Voucher WVIP8899...",
        "created_at": "2026-09-27T13:28:00Z"
      },
      {
        "id": "msg-002",
        "sender_role": "VENDOR",
        "sender_name": "The Reverie Saigon",
        "content": "Dạ em chào anh chị! Em gửi anh chị file PDF thực đơn mẫu ạ...",
        "attachment_url": "https://cdn.weddingplat.vn/quotes/menu-reverie-2026.pdf",
        "created_at": "2026-09-27T13:32:00Z"
      }
    ]
  }
}
```

---

## 3. Quy Tắc Xử Lý Backend & Activity Rules

* **`R-LEAD-03` (Quyền truy cập phòng chat):** Chỉ có Chủ Nhà cung cấp được gán cho Lead đó (`vendor_id = current_vendor`) và chính Khách hàng gửi Lead mới có quyền kết nối WebSocket và xem nội dung phòng chat.
* **`R-LEAD-04` (Ghi nhận SLA):** Hệ thống tự động tính toán thời gian phản hồi: $\text{Response\_Time} = \text{accepted\_at} - \text{created\_at}$. Nếu $\le 2\text{ giờ}$ thì ghi nhận đạt chuẩn SLA của Sàn.
