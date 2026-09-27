---
title: "Sequence Diagram & API Specifications: Khám Phá & Chi Tiết Gói Dịch Vụ Cưới"
epic: "Epic 1: Khám Phá Dịch Vụ Cưới & AI Stylist Matching"
use_cases: ["UC-1.1", "UC-1.2"]
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA & Solution Architect"
---

# SEQUENCE DIAGRAM & ĐẶC TẢ API (TECHNICAL SPECIFICATION)
## DỰ ÁN: NỀN TẢNG MÔI GIỚI DỊCH VỤ CƯỚI (WEDDING SERVICE PLATFORM)
### MODULE: KHÁM PHÁ DANH MỤC & XEM CHI TIẾT BÀI ĐĂNG (UC 1.1 & 1.2)

---

## 1. Sơ Đồ Trình Tự Tương Tác (Sequence Diagram)

```mermaid
sequenceDiagram
    autonumber
    actor User as Khách hàng / Dâu Rể
    participant FE as Customer Web Portal
    participant API as Backend Core API
    participant Cache as Redis Cache
    participant DB as PostgreSQL Database

    %% Luồng 1: Tải danh mục & Lọc bài đăng (UC 1.1)
    User->>FE: 1. Truy cập trang /danh-muc & chọn bộ lọc (Category, Tỉnh thành, Giá)
    FE->>API: 2. GET /api/v1/listings?category=venue&province=hcm&page=1&limit=12
    API->>Cache: 3. Kiểm tra cache danh sách (Cache Key: listings:filter:hash)
    alt Cache Hit
        Cache-->>API: 4a. Trả về danh sách Listings từ Redis
    else Cache Miss
        API->>DB: 4b. SELECT * FROM listings JOIN vendors WHERE status = 'Active' ...
        DB-->>API: 5. Trả về tập dữ liệu Listing + Media + Vendor
        API->>Cache: 6. Ghi nhớ cache (TTL: 15 phút)
    end
    API-->>FE: 7. HTTP 200 OK (Danh sách Listings + Pagination meta)
    FE-->>User: 8. Render lưới thẻ dịch vụ (Lọc kết quả realtime)

    %% Luồng 2: Xem chi tiết gói dịch vụ (UC 1.2)
    User->>FE: 9. Bấm vào Thẻ gói dịch vụ (Slug: crystal-grand-ballroom)
    FE->>API: 10. GET /api/v1/listings/{slug}
    API->>DB: 11. Query chi tiết Listing, Media Portfolio, Bảng giá, Reviews đã duyệt
    DB-->>API: 12. Trả về toàn bộ thông tin chi tiết
    API-->>FE: 13. HTTP 200 OK (Chi tiết Listing + Vendor + Reviews + Voucher Info)
    FE-->>User: 14. Hiển thị trang chi tiết, slider ảnh và nút "Nhận Voucher"
```

---

## 2. Bảng Danh Sách API Liên Quan

| STT | Mã BA ID | HTTP Method | Endpoint URL | Tên Nghiệp Vụ / Chức Năng | Màn Hình Gọi |
|:---|:---|:---|:---|:---|:---|
| 1 | `API-LISTING-01` | `GET` | `/api/v1/categories` | Lấy danh sách 7 danh mục dịch vụ cưới | `SCR-BROWSE-LISTING` |
| 2 | `API-LISTING-02` | `GET` | `/api/v1/listings` | Tìm kiếm, lọc và phân trang bài đăng dịch vụ | `SCR-BROWSE-LISTING` |
| 3 | `API-LISTING-03` | `GET` | `/api/v1/listings/{slug}` | Xem chi tiết gói dịch vụ và portfolio ảnh/video | `SCR-LISTING-DETAIL` |

---

## 3. Đặc Tả Chi Tiết Từng API

### 3.1. `API-LISTING-02`: Lấy danh sách bài đăng có bộ lọc (GET `/api/v1/listings`)

#### Request Parameters (Query String):
| Tham Số | Kiểu Dữ Liệu | Bắt Buộc | Mặc Định | Mô Tả |
|:---|:---|:---|:---|:---|
| `category` | String | Không | `null` | Mã code hoặc ID danh mục (vd: `venue`, `decor`, `photo`) |
| `province` | String | Không | `null` | Mã tỉnh thành (vd: `hcm`, `hn`, `dn`) |
| `price_min` | Number | Không | `0` | Giá tối thiểu (VNĐ) |
| `price_max` | Number | Không | `null` | Giá tối đa (VNĐ) |
| `rating_min`| Float | Không | `null` | Số sao tối thiểu (vd: `4.5`, `4.8`) |
| `style` | String | Không | `null` | Phong cách cưới (vd: `minimalist`, `vintage`) |
| `has_voucher`| Boolean | Không | `false` | Chỉ lấy bài đăng có ưu đãi Voucher |
| `sort_by` | String | Không | `recommended`| `recommended`, `price_asc`, `price_desc`, `rating` |
| `page` | Integer | Không | `1` | Số thứ tự trang |
| `limit` | Integer | Không | `12` | Số lượng bài đăng trên mỗi trang |

#### Response Success (`HTTP 200 OK`):
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "c1f7a28e-5b12-4c28-97e3-0d5b1234abcd",
        "slug": "crystal-grand-ballroom-reverie",
        "title": "Gói Sảnh Tiệc Pha Lê Hoàng Gia (Crystal Grand Ballroom)",
        "category": {
          "id": "cat-01",
          "code": "venue",
          "name": "Trung tâm Tiệc cưới"
        },
        "vendor": {
          "id": "ven-01",
          "brand_name": "The Reverie Saigon Hall",
          "is_verified": true,
          "province_code": "hcm",
          "district": "Quận 1"
        },
        "price_min": 12500000,
        "price_max": 25000000,
        "price_unit": "bàn",
        "rating_avg": 4.9,
        "review_count": 48,
        "cover_image_url": "https://images.unsplash.com/photo-1519167758481-83f550bb49b3",
        "voucher_tag": "Giảm 5% + Tặng Bàn Gallery",
        "style_tags": ["vintage", "luxury", "royal"]
      }
    ],
    "pagination": {
      "total_items": 42,
      "total_pages": 4,
      "current_page": 1,
      "limit": 12
    }
  }
}
```

#### Response Errors:
* `HTTP 400 Bad Request`: Tham số `page` hoặc `limit` không hợp lệ (`page < 1`).
* `HTTP 500 Internal Server Error`: Lỗi kết nối CSDL hoặc Redis.

---

### 3.2. `API-LISTING-03`: Xem chi tiết gói dịch vụ (GET `/api/v1/listings/{slug}`)

#### Response Success (`HTTP 200 OK`):
```json
{
  "success": true,
  "data": {
    "id": "c1f7a28e-5b12-4c28-97e3-0d5b1234abcd",
    "slug": "crystal-grand-ballroom-reverie",
    "title": "Gói Sảnh Tiệc Pha Lê Hoàng Gia (Crystal Grand Ballroom)",
    "description": "Sảnh tiệc pha lê phong cách hoàng gia Châu Âu với sức chứa lên tới 600 khách...",
    "category": { "code": "venue", "name": "Trung tâm Tiệc cưới" },
    "vendor": {
      "id": "ven-01",
      "brand_name": "The Reverie Saigon Hall",
      "hotline": "0909123456",
      "address_detail": "22-36 Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP.HCM",
      "rating_avg": 4.9,
      "is_verified": true
    },
    "price_min": 12500000,
    "price_max": 25000000,
    "media_portfolio": [
      { "media_url": "https://images.unsplash.com/photo-1519167758481-83f550bb49b3", "media_type": "IMAGE", "is_cover": true },
      { "media_url": "https://images.unsplash.com/photo-1511795409834-ef04bbd61622", "media_type": "IMAGE", "is_cover": false }
    ],
    "active_voucher": {
      "code_prefix": "WVIP",
      "discount_description": "Tặng voucher giảm 5% trên tổng giá trị hợp đồng khi đặt cọc qua Sàn"
    },
    "recent_reviews": [
      {
        "author_name": "Minh & Hằng",
        "rating": 5,
        "comment": "Sảnh đẹp lộng lẫy, âm thanh ánh sáng chuẩn 5 sao. Được Sàn tặng voucher tiết kiệm được gần 20 triệu!",
        "is_verified_buyer": true,
        "created_at": "2026-09-15"
      }
    ]
  }
}
```

---

## 4. Quy Tắc Xử Lý Backend & Activity Rules

### Luồng xử lý BE & Rule cho `GET /api/v1/listings`:

```mermaid
flowchart TD
    Start([Nhận Request GET /api/v1/listings]) --> ValidateInput{Validate params: page, limit}
    ValidateInput -- Không hợp lệ --> Return400[Trả về 400 Bad Request]
    ValidateInput -- Hợp lệ --> CheckCache{Check Redis Cache}
    
    CheckCache -- Có Cache --> ReturnCache[Trả về 200 OK từ Cache]
    CheckCache -- Không có Cache --> BuildQuery[Xây dựng SQL Query]
    
    BuildQuery --> FilterActive[Rule R-LIST-01: Chỉ lấy status = 'Active']
    FilterActive --> FilterCat[Lọc theo category_id nếu có]
    FilterCat --> FilterProv[Lọc theo province_code nếu có]
    FilterProv --> FilterPrice[Lọc theo khoảng giá min/max]
    
    FilterPrice --> ExecDB[(Thực thi Query Database)]
    ExecDB --> SetCache[Ghi Cache Redis 15 phút]
    SetCache --> Return200[Trả về 200 OK JSON]
```

| Mã Rule | Tên Quy Tắc | Nội Dung & Điều Kiện Thực Thi | Hành Động Khi Vi Phạm |
|:---|:---|:---|:---|
| **R-LIST-01** | Chỉ hiển thị bài đăng Active | Mọi truy vấn công khai của khách hàng chỉ được phép lấy các bài đăng có `Listing.status = 'Active'` và `Vendor.status = 'Active'`. | Bài đăng đang duyệt `PendingModeration` hoặc bị ẩn `Hidden` tuyệt đối không xuất hiện trong kết quả |
| **R-LIST-02** | Giới hạn phân trang an toàn | Tham số `limit` tối đa không vượt quá `50` bài đăng/request để bảo vệ tài nguyên server. | Nếu `limit > 50`, hệ thống tự ép về `limit = 50` |
