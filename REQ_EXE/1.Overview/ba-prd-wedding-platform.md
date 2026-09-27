---
title: "Tài Liệu Đặc Tả Yêu Cầu Sản Phẩm (Master PRD): Nền Tảng Dịch Vụ Cưới (Wedding Service Platform)"
project: "Wedding Service Platform"
code: "WEDDING-PLATFORM"
version: "1.0.0"
author: "Lead Business Analyst & Domain Expert"
created_at: "2026-09-26"
status: "Hoàn thiện - Chờ phê duyệt Gate S5"
type: "Product Requirement Document (PRD)"
---

# TÀI LIỆU ĐẶC TẢ YÊU CẦU SẢN PHẨM (MASTER PRD)
## DỰ ÁN: NỀN TẢNG MÔI GIỚI DỊCH VỤ CƯỚI (WEDDING SERVICE PLATFORM)

---

## Change Log

| Version | Ngày | Người thực hiện | Nội dung chi tiết |
|:---|:---|:---|:---|
| 1.0.0 | 2026-09-26 | Lead BA & Domain Expert | Khởi tạo tài liệu Master PRD tổng thể sau khi hoàn tất Overview, Master ERD, 42 Use Cases và Bộ Quy định nghiệp vụ BRL |

---

## 1. Bối Cảnh & Mục Tiêu Dự Án

### 1.1. Bối Cảnh Thị Trường (Market Context)
Thị trường dịch vụ cưới tại Việt Nam có quy mô ước tính hàng tỷ USD mỗi năm nhưng đang tồn tại sự phân mảnh và thiếu minh bạch nghiêm trọng:
- **Phía Cô dâu / Chú rể (Bên Cầu):** Tốn 3–6 tháng tìm kiếm thông tin phân tán trên mạng xã hội, lo ngại tình trạng "loạn giá", phát sinh chi phí ẩn và thiếu các đánh giá xác thực từ những người đã thực sự sử dụng dịch vụ.
- **Phía Nhà Cung Cấp - NCC (Bên Cung):** Đối mặt với chi phí quảng cáo (Facebook/TikTok Ads) ngày càng đắt đỏ (150.000đ - 300.000đ/lead), chất lượng lead thấp (tỷ lệ chốt < 5%) và không có công cụ số hóa danh mục portfolio hiệu quả.

### 1.2. Mục Tiêu Sản Phẩm MVP (Lean MVP Objective)
Xây dựng một **Nền tảng Môi giới Dịch vụ Cưới (Wedding Service Marketplace)** tinh gọn, tập trung 100% vào giá trị kết nối và sinh lời:
1. **AI Stylist Matching:** Ứng dụng bài trắc nghiệm hình ảnh (Visual Moodboard Quiz) và trợ lý ảo AI để phân tích gu thẩm mỹ, tự động gợi ý 3–5 NCC tối ưu về phong cách và ngân sách.
2. **Lean Vendor Portal:** Cổng quản trị dành riêng cho Chủ NCC (1 NCC = 1 tài khoản) để quản lý bài đăng, tiếp nhận lead tức thì qua Zalo (<10s), chat báo giá và xác nhận hợp đồng.
3. **Mô Hình Đối Soát Hoa Hồng 2 Kỳ (3%–12%):** Cơ chế xác thực giao dịch 2 chiều minh bạch, chia đôi kỳ thu hoa hồng (50% lúc cọc, 50% sau ngày cưới) giúp NCC an tâm dòng tiền.
4. **Bộ Tiện Ích Cưới Miễn Phí (Freemium):** Tạo thiệp cưới online tương tác, thu thập phản hồi điểm danh RSVP 1 chạm, dự toán ngân sách và bảng checklist 12 tháng.

---

## 2. Tiêu Chí Thành Công Của Dự Án (Success Metrics / KPIs)

| Chỉ Số Đo Lường (KPI) | Mục Tiêu Giai Đoạn MVP (3–6 Tháng Đầu) | Phương Pháp Đo Lường |
|:---|:---|:---|
| **Số lượng Đối tác NCC (Active Vendors)** | $\ge 200$ Nhà cung cấp tại TP.HCM & Hà Nội (phủ đủ 7 ngành hàng) | Đếm số Vendor có $\ge 1$ bài đăng `Active` |
| **Số lượng Yêu cầu Tư vấn (Monthly Leads)** | $\ge 1.500$ Leads phát sinh / tháng | Đếm tổng số `Lead` tạo mới trên hệ thống |
| **Thời gian phản hồi Lead (Vendor SLA)** | $80\%$ Lead được tiếp nhận trong vòng $\le 2\text{ giờ}$ | Đo khoảng thời gian từ `created_at` đến `accepted_at` |
| **Tỷ lệ Chuyển đổi Hợp đồng (Conversion Rate)** | $\ge 12\% - 15\%$ Lead chuyển đổi thành Hợp đồng ký kết | $\frac{\text{BookingContract Confirmed}}{\text{Total Leads}}$ |
| **Tổng Giá Trị Giao Dịch Sàn (GMV)** | $\ge 15\text{ – }20\text{ Tỷ VNĐ}$ | Tổng giá trị hợp đồng được xác thực `Confirmed` |
| **Tỷ lệ Thu Hồi Hoa Hồng (Commission Collection)** | $\ge 95\%$ hoa hồng được thanh toán đúng hạn ngày 25 | $\frac{\text{Commission Paid}}{\text{Total Commission Due}}$ |

---

## 3. Tác Nhân & Ứng Dụng Tương Tác (Actors & Applications)

> Chi tiết xem tại: [[ba-product-overview]]

| Tác Nhân (Actor) | Phân Loại | Kênh / Ứng Dụng Tương Tác | Trách Nhiệm & Chức Năng Chính |
|:---|:---|:---|:---|
| **Cô dâu / Chú rể (Khách hàng)** | End-User (Cầu) | **Customer Web Portal** *(Responsive)* | Lướt xem 7 ngành dịch vụ, làm Quiz AI Moodboard, gửi lead nhận mã ưu đãi, chat báo giá, tạo thiệp RSVP, duyệt HĐ 2 chiều. |
| **Khách mời (Wedding Guests)** | End-User (Khách) | **Customer Web Portal - RSVP Page** | Xem link thiệp cưới, bản đồ chỉ đường và bấm xác nhận tham dự (RSVP) 1 chạm không cần tài khoản. |
| **Nhà Cung Cấp (Vendor Owner)** | Partner B2B (Cung) | **Vendor Portal** *(Web Tinh Gọn)* | Đăng gói dịch vụ/portfolio, nhận thông báo Zalo (<10s), chat tư vấn, nhập mã ưu đãi ký HĐ, thanh toán hoa hồng VietQR ngày 25. |
| **Kiểm duyệt viên (Moderator)** | Internal Ops | **Admin Back-office Portal** *(RBAC)* | Thẩm định hồ sơ NCC, duyệt bài đăng dịch vụ (SLA 24h), duyệt đánh giá review và biên tập cẩm nang SEO. |
| **Kế toán / Đối soát (Finance)** | Internal Ops | **Admin Back-office Portal** *(RBAC)* | Chốt bảng kê hoa hồng ngày 25 hàng tháng, đối soát thanh toán VietQR và xuất hóa đơn điện tử VAT. |
| **Chăm sóc Khách hàng (CSKH)** | Internal Ops | **Admin Back-office Portal** *(RBAC)* | Hỗ trợ giải quyết tranh chấp, audit chất lượng lead và kích hoạt khớp nối khẩn cấp (Emergency Matching). |
| **Quản trị viên Cấp cao (Super Admin)** | Internal Ops | **Admin Back-office Portal** *(RBAC)* | Quản lý danh mục 7 ngành hàng, cấu hình tỷ lệ hoa hồng, phân quyền tài khoản và theo dõi dashboard toàn sàn. |

---

## 4. Tóm Tắt Quy Trình Nghiệp Vụ Cốt Lõi (Core Business Processes)

Quy trình vận hành khép kín gồm 5 luồng chính:
1. **[BP-01] Khám Phá & Khớp Nối AI:** Khách làm trắc nghiệm Moodboard → OpenAI/Qdrant bóc tách vector → Gợi ý 3-5 bài đăng tối ưu.
2. **[BP-02] Tiếp Nhận & Điều Phối Lead:** Khách gửi form (OTP SĐT) → Hệ thống cấp mã Voucher 8 ký tự → Bắn thông báo Zalo ZNS cho NCC (<10s) → NCC tiếp nhận (<2h) & mở phòng chat In-App.
3. **[BP-03] Đăng Bài & Kiểm Duyệt:** NCC nộp gói dịch vụ/ảnh portfolio → Moderator kiểm duyệt trong 24h → Bài đăng hiển thị `Active`.
4. **[BP-04] Ký Hợp Đồng & Xác Thực 2 Chiều:** NCC nhập mã Voucher + giá trị HĐ + ảnh phiếu thu cọc → Khách nhận thông báo và bấm duyệt trên App/Zalo trong 72h → Hợp đồng chuyển `Confirmed` (Khách nhận quà mừng cưới).
5. **[BP-05] Đối Soát & Thu Hoa Hồng 2 Kỳ:** Kích hoạt hoa hồng K1 (50%) → Hệ thống chốt bảng kê vào ngày 25 hàng tháng → NCC quét VietQR thanh toán → Webhook gạch nợ tự động → Sau ngày cưới thu nốt K2 (50%).

---

## 5. Phạm Vi Dự Án (Scope of Work)

### 5.1. Thuộc Phạm Vi Triển Khai (In-Scope - Lean MVP)
- Hệ thống Portal đa người dùng: Customer Web Portal, Vendor Portal, Admin Back-office Portal.
- Trợ lý AI Stylist & Trắc nghiệm Visual Moodboard Quiz.
- Quản lý danh mục 7 ngành dịch vụ cưới, bài đăng và album portfolio.
- Quản lý Lead, tích hợp gửi thông báo Zalo ZNS realtime và phòng chat trực tiếp In-App.
- Quy trình xác thực hợp đồng 2 chiều và cơ chế bảo vệ quyền riêng tư (Smart Privacy ẩn SĐT).
- Tính toán hoa hồng 3%–12% tự động, chia 2 kỳ thanh toán 50/50, đối soát ngày 25 và gạch nợ VietQR.
- Hệ thống đánh giá uy tín có kiểm duyệt (Verified Buyer Review).
- Bộ công cụ cưới miễn phí: Thiệp cưới online, RSVP điểm danh, dự toán ngân sách, checklist 12 tháng.
- Quản trị nội dung CMS Cẩm nang cưới SEO và Dashboard báo cáo doanh thu/lead.

### 5.2. Ngoài Phạm Vi (Out-of-Scope - Dành Cho Phase 2)
- Các tính năng quản trị nhân sự nội bộ của NCC (tạo tài khoản nhân viên, phân công việc, chấm công).
- Tính năng theo dõi timeline đám cưới siêu nhỏ từng phút của NCC.
- Cổng thanh toán giữ tiền đặt cọc (Escrow Payment trực tiếp qua Sàn).
- Ứng dụng Native Mobile App (iOS/Android) — Giai đoạn MVP sử dụng Web Responsive / PWA.
- Tích hợp kết nối API trực tiếp vào phần mềm kế toán ERP của các tập đoàn khách sạn 5 sao.

---

## 6. Danh Mục Các Epics Lớn (Epic Summary)

> Chi tiết danh sách 42 Use Cases xem tại: [[ba-epics-and-user-stories]]

| Epic | Tên Epic | Mục Tiêu Nghiệp Vụ Cốt Lõi | Actor Chính | Số UC | Mức Ưu Tiên (MoSCoW) |
|:---|:---|:---|:---|:---|:---|
| **Epic 1** | Khám Phá Dịch Vụ Cưới & AI Stylist Matching | Duyệt 7 ngành hàng, làm Moodboard Quiz & chat AI nhận gợi ý 3-5 NCC chuẩn gu/ngân sách. | Cô dâu / Chú rể, Khách vãng lai | 5 | **Must-have** |
| **Epic 2** | Quản Lý Yêu Cầu Tư Vấn & Điều Phối Lead | Gửi lead OTP, cấp mã Voucher 8 ký tự, bắn Zalo ZNS <10s, chat báo giá và SLA 2h. | Khách hàng, NCC, CSKH, Backend | 6 | **Must-have** |
| **Epic 3** | Quản Lý Hồ Sơ & Bài Đăng Gói Dịch Vụ NCC | Đăng ký NCC, đăng gói dịch vụ/portfolio, kiểm duyệt bài đăng trong 24h. | NCC, Moderator, Super Admin | 5 | **Must-have** |
| **Epic 4** | Quản Lý Hợp Đồng & Xác Thực Giao Dịch 2 Chiều | Nhập mã Voucher, upload ảnh HĐ, khách hàng duyệt xác nhận 2 chiều trong 72h. | Khách hàng, NCC, Backend | 6 | **Must-have** |
| **Epic 5** | Đối Soát Hoa Hồng Môi Giới 2 Kỳ & Thu Phí | Tính hoa hồng 3%-12%, chia đôi 50/50, chốt bảng kê ngày 25, gạch nợ VietQR. | NCC, Finance, Backend, Ngân hàng | 6 | **Must-have** |
| **Epic 6** | Đánh Giá Trải Nghiệm & Uy Tín Đối Tác | Đánh giá Verified Buyer sau hoàn tất dịch vụ, duyệt review, cập nhật điểm Rating Avg. | Khách hàng, Moderator, NCC, Backend | 4 | **Should-have** |
| **Epic 7** | Tiện Ích Chuẩn Bị Cưới & Thiệp Mời Online | Tạo thiệp cưới online, khách điểm danh RSVP 1 chạm, quản lý ngân sách & checklist 12 tháng. | Khách hàng, Khách mời | 5 | **Must-have** (Growth Hook) |
| **Epic 8** | Quản Trị Hệ Thống, Vận Hành & Báo Cáo Toàn Sàn | Phân quyền RBAC, CMS Cẩm nang SEO, Dashboard GMV/Hoa hồng, Audit Log. | Super Admin, Moderator, Finance, CSKH | 5 | **Must-have** |

---

## 7. Quy Định Nghiệp Vụ & Công Thức Trọng Yếu (Business Rules Summary)

> Chi tiết xem tại: [[ba-business-rules]]

- **`BR-001` (Lean Vendor RBAC):** 1 Nhà cung cấp = 1 Tài khoản Vendor Owner duy nhất (cắt bỏ phân quyền nhân viên).
- **`BR-002` (Smart Privacy):** Số điện thoại khách hàng ban đầu được mã hóa/ẩn (`0987***123`), chỉ mở khóa khi khách hàng chấp thuận.
- **`BR-003` (SLA Lead 2h):** NCC phải tiếp nhận lead trong vòng 2h; tự động hủy và cảnh báo nếu quá 24h.
- **`BR-004` (Voucher 8 ký tự):** Mỗi lead cấp 1 mã độc nhất có hiệu lực 30 ngày để nhận ưu đãi và định danh giao dịch.
- **`BR-005` (Xác thực 2 chiều):** Hợp đồng chỉ chuyển `Confirmed` khi khách hàng bấm xác nhận duyệt trên App/Zalo trong 72h.
- **`BR-006` (Hoa hồng chia 2 kỳ 50/50):** Thu 50% sau khi cọc hợp đồng, thu 50% còn lại sau ngày cưới hoàn tất.
- **`BR-007` (Đối soát ngày 25):** Tự động chốt bảng kê vào ngày 25 hàng tháng; thanh toán qua VietQR trong 5 ngày.
- **Khung tỷ lệ hoa hồng (`FM-001`):** Tiệc cưới (`3%-5%`), Trang trí / Quay chụp / Váy cưới / Makeup / Thiệp cưới (`8%-10%`), Wedding Planner trọn gói (`10%-12%`).

---

## 8. Mô Hình Dữ Liệu Tham Chiếu (Data Model Reference)

> Chi tiết xem tại: [[ba-data-model]] và [[ba-data-model-index]]

Hệ thống bao gồm **15 thực thể dữ liệu chuẩn hóa**:
1. `User`, `Vendor` (Tài khoản & Hồ sơ đối tác)
2. `Category`, `Listing`, `ListingMedia` (Danh mục 7 ngành & Portfolio gói dịch vụ)
3. `Lead`, `Voucher` (Yêu cầu tư vấn & Mã ưu đãi độc nhất)
4. `BookingContract`, `Commission` (Giao dịch hợp đồng 2 chiều & Hoa hồng 2 kỳ)
5. `Review`, `Article` (Đánh giá kiểm duyệt & Cẩm nang SEO)
6. `WeddingInvitation`, `GuestRSVP`, `BudgetItem`, `ChecklistTask` (Bộ tiện ích cưới & Điểm danh)

**6 Sơ đồ trạng thái vòng đời đã hoàn thiện:**
- `SD-Lead` (6 trạng thái) · `SD-Listing` (5 trạng thái) · `SD-BookingContract` (5 trạng thái)
- `SD-Commission` (4 trạng thái) · `SD-Voucher` (4 trạng thái) · `SD-Review` (3 trạng thái)

---

## 9. Yêu Cầu Phi Chức Năng (Non-Functional Requirements - NFR)

| Nhóm Yêu Cầu | Tiêu Chuẩn Cam Kết | Giải Pháp Kỹ Thuật |
|:---|:---|:---|
| **Hiệu năng & Tốc độ (Performance)** | • Tốc độ tải trang chi tiết dịch vụ: $\le 1.5\text{ giây}$.<br>• Tốc độ gợi ý AI Stylist: $\le 3.0\text{ giây}$.<br>• Độ trễ gửi thông báo Zalo ZNS: $\le 10\text{ giây}$. | CDN Cloudflare caching, tối ưu hóa kích thước ảnh qua Cloudinary WebP, Vector DB Qdrant indexing. |
| **Bảo mật & Quyền riêng tư (Security & Privacy)** | • Mã hóa SĐT khách hàng (Smart Privacy).<br>• Mã hóa mật khẩu chuẩn `Argon2` / `Bcrypt`.<br>• Bảo mật kết nối toàn diện qua `HTTPS / SSL`. | Data masking tại tầng API; phân quyền chặt chẽ theo vai trò (RBAC) trên JWT Token. |
| **Độ sẵn sàng & Chịu tải (Availability & Scalability)** | • Khả năng chịu tải: $\ge 1.000$ người dùng đồng thời (CCU).<br>• Uptime hệ thống: $\ge 99.5\%$. | Triển khai kiến trúc Containerization (Docker), Autoscaling Backend và PostgreSQL connection pool. |
| **Tính khả dụng (Usability & Responsiveness)** | • Tối ưu 100% trên thiết bị di động (Mobile-first).<br>• Giao diện sang trọng, hiện đại, chuẩn cảm xúc đám cưới (Wedding Emotional Design). | UI Design System đồng bộ, tone màu Pastel/Rose Gold thanh lịch, thân thiện với người dùng không rành công nghệ. |

---

## 10. Danh Mục Tài Liệu Nguồn Của Dự Án (Source Artifacts Traceability)

| Phân Loại Tài Liệu | Đường Dẫn Lưu Trữ (Path) | Mục Đích Sử Dụng |
|:---|:---|:---|
| **Tóm tắt Khơi gợi (Elicitation Summary)** | [elicitation_summary_Wedding-Service-Platform.md](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Elicitation/elicitation_summary_Wedding-Service-Platform.md) | Nguồn sự thật về yêu cầu nghiệp vụ & thỏa thuận thống nhất |
| **Báo cáo Xử lý Mâu thuẫn (Conflict Report)**| [ba-conflict-report-wedding-platform-20260926.md](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Elicitation/ba-conflict-report-wedding-platform-20260926.md) | Biên bản giải quyết 8 chiều mâu thuẫn & chốt Lean MVP |
| **Tổng quan Sản phẩm (Product Overview)** | [ba-product-overview.md](file:///c:/Users/Admin/Desktop/EXE/docs-BA/ba-product-overview.md) | Mô hình Actor, App và Sơ đồ System Context Diagram |
| **Mô hình Dữ liệu (Master Data Model & ERD)** | [ba-data-model.md](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Data%20Model/ba-data-model.md) | Sơ đồ ERD, Data Dictionary 15 thực thể & 6 State Diagrams |
| **Danh sách Master Epics & 42 Use Cases** | [ba-epics-and-user-stories.md](file:///c:/Users/Admin/Desktop/EXE/docs-BA/epics-US/ba-epics-and-user-stories.md) | Danh mục chi tiết 42 Use Cases của hệ thống |
| **Bảng ánh xạ Use Case ↔ Thực thể** | [ba-uc-entity-map.md](file:///c:/Users/Admin/Desktop/EXE/docs-BA/epics-US/ba-uc-entity-map.md) | Đối chiếu 2 chiều UC ↔ Data Model (Khớp 100%) |
| **Quy định Nghiệp vụ & Công thức** | [ba-business-rules.md](file:///c:/Users/Admin/Desktop/EXE/docs-BA/Business%20Rules/ba-business-rules.md) | 10 Business Rules & 6 Calculation Formulas |
| **Bảng theo dõi Tiến độ (Command Center)** | [ba-development-tracking.md](file:///c:/Users/Admin/Desktop/EXE/docs-BA/ba-development-tracking.md) | Bảng theo dõi tiến độ sản xuất UI, Spec, Coding, QA |
