---
title: "UI Specification: Trắc Nghiệm Visual Moodboard & Trợ Lý AI Stylist (SCR-AI-STYLIST)"
epic: "Epic 1: Khám Phá Dịch Vụ Cưới & AI Stylist Matching"
use_cases: ["UC-1.3", "UC-1.4"]
version: "1.0.0"
date: "2026-09-27"
author: "Lead BA"
prototype: "docs-BA/Prototype/SCR-01-Browse-And-Moodboard.html#moodboardModal"
---

# ĐẶC TẢ GIAO DIỆN NGƯỜI DÙNG (UI SPECIFICATION)
## MÀN HÌNH: TRẮC NGHIỆM VISUAL MOODBOARD & AI STYLIST CHAT (SCR-AI-STYLIST)

---

## 1. Thông Tin Chung Màn Hình

- **Mã màn hình:** `SCR-AI-STYLIST`
- **Tên màn hình:** Trắc Nghiệm Thị Giác Moodboard & Trò Chuyện Cùng AI Stylist
- **Ứng dụng:** Customer Web Portal (Modal Overlay / Dedicated AI Flow)
- **Đường dẫn (URL):** Modal kích hoạt tại `/danh-muc#ai-quiz` hoặc `/ai-stylist`
- **Đối tượng sử dụng:** Khách vãng lai, Cô dâu / Chú rể
- **File Prototype tham chiếu:** [SCR-01-Browse-And-Moodboard.html](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Prototype/SCR-01-Browse-And-Moodboard.html) (khối Modal Quiz)

---

## 2. Bố Cục Giao Diện 2 Bước (2-Step Flow)

### Bước 1: Bóc tách phong cách thẩm mỹ (Style Moodboard)
- Tiêu đề: "Chọn 3 bức ảnh chạm đến cảm xúc của bạn".
- Lưới 6–9 bức ảnh phong cách đại diện (Hoàng gia cổ điển, Vườn cổ tích, Tinh tế tối giản, Vintage mộc mạc, Hiện đại sang trọng, Bãi biển lãng mạn).
- Hiệu ứng chọn: Viền vàng đồng/hồng pastel nổi bật, hiển thị icon tick tròn `✓` góc trên bên phải.
- Bộ đếm thời gian thực: `Đã chọn: X/3 phong cách`. Nút "Tiếp tục" chỉ sáng khi đã chọn đủ 3 ảnh.

### Bước 2: Dự toán ngân sách & Vùng phục vụ
- Thanh trượt ngân sách (Budget Range Slider): Kéo từ 50 Triệu đến 600+ Triệu VNĐ, bước nhảy 10 Triệu, hiển thị số tiền sinh động.
- Dropdown chọn địa điểm tổ chức tiệc cưới: TP.HCM, Hà Nội, Đà Nẵng / Hội An, Khác.
- Nút "Bắt đầu Phân tích & Gợi ý AI": Hiệu ứng loading quay spinner trong tối đa 3 giây.
- Kết quả gợi ý (AI Match Output): Thẻ 3–5 Nhà cung cấp tối ưu nhất kèm tỷ lệ khớp nối (vd: `Khớp gu 98%`) và lý do vì sao AI đề xuất.

### Giao diện phụ: Khung Chat Trực Tiếp Với AI Stylist (UC 1.4)
- Cửa sổ chat dạng drawer trượt bên phải: Cho phép cô dâu/chú rể hỏi đáp tự nhiên ("Mình muốn đám cưới phong cách khu vườn cổ tích ngân sách 120 triệu ở TP.HCM thì nên chọn đơn vị nào?").

---

## 3. Ma Trận Điều Khiển Giao Diện (UI Control Matrix)

| STT | Mã Control | Tên Phần Tử | Loại Control | Giá Trị / Ràng Buộc | Bắt Buộc | Hành Vi Khi Tương Tác |
|:---|:---|:---|:---|:---|:---|:---|
| 1 | `GRID_MOOD_ITEMS` | Lưới ảnh Moodboard | Multi-select Cards | 6–9 hình ảnh chất lượng cao | Có (chọn 3) | Click để chọn/bỏ chọn; tối đa chọn đúng 3 ảnh |
| 2 | `TXT_SELECTED_COUNT` | Bộ đếm số ảnh đã chọn | Text Counter | Format: `X/3 phong cách` | — | Tự động tăng/giảm khi click ảnh; đủ 3 ảnh mở khóa nút Next |
| 3 | `BTN_QUIZ_NEXT` | Nút Tiếp tục bước 2 | Button Primary | Text: "Tiếp Tục: Chọn Ngân Sách →" | — | Chuyển sang `quizStep2` |
| 4 | `SLD_BUDGET` | Thanh trượt ngân sách | Range Slider | Min: 50, Max: 600, Step: 10 | Có | Hiển thị giá trị realtime tại `TXT_BUDGET_VAL` |
| 5 | `SEL_LOCATION` | Địa điểm tổ chức tiệc | Dropdown | TP.HCM, Hà Nội, Đà Nẵng... | Có | Mặc định: TP.HCM |
| 6 | `BTN_SUBMIT_QUIZ` | Nút kích hoạt AI | Button Gradient AI | Text: "✨ Bắt Đầu Phân Tích & Gợi Ý AI" | — | Gọi API phân tích Vector AI; hiện Spinner loading |
| 7 | `CHAT_AI_INPUT` | Ô nhập tin nhắn chat AI | Input Text | Placeholder: "Hỏi AI Stylist về phong cách, ngân sách..." | Không | Enter hoặc bấm nút Gửi để trò chuyện tự nhiên |
| 8 | `LIST_AI_RESULTS` | Thẻ kết quả gợi ý | Card List | 3–5 Cards Listing phù hợp | — | Hiển thị tag % khớp gu và nút "Nhận Voucher ngay" |
