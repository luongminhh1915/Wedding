Version: 1.0.0
Author: M2MBA
Last Updated: 2026-09-26
Description: Master file Quy định nghiệp vụ & Công thức tính toán (Business Rules & Formulas) chuẩn Lean MVP dự án Wedding Service Platform.

# QUY ĐỊNH NGHIỆP VỤ & CÔNG THỨC TÍNH TOÁN (BUSINESS RULES & FORMULAS)
## DỰ ÁN: NỀN TẢNG MÔI GIỚI DỊCH VỤ CƯỚI (WEDDING SERVICE PLATFORM)

---

## Change Log

| Version | Ngày | Người thực hiện | Nội dung chi tiết |
|:---|:---|:---|:---|
| 1.0.0 | 2026-09-26 | Lead BA & Domain Expert | Khởi tạo 10 Quy định nghiệp vụ (BR-001 -> BR-010) và 6 Công thức tính toán (FM-001 -> FM-006) theo chuẩn Lean MVP |

---

## 1. Danh Mục Quy Định Nghiệp Vụ (Business Rules)

| ID | Tên Quy Định | Nội Dung Quy Định & Điều Kiện Áp Dụng | Trường Hợp Ngoại Lệ | Nguồn Tài Liệu | Mapping UC / Bước Quy Trình |
|:---|:---|:---|:---|:---|:---|
| **BR-001** | Tinh gọn tài khoản Nhà cung cấp (Lean Vendor RBAC) | Mỗi Nhà cung cấp (NCC) đối tác chỉ sở hữu **1 tài khoản duy nhất (Vendor Owner)**. Cắt bỏ toàn bộ phân quyền nhân viên nội bộ, phân bổ việc và theo dõi timeline đám cưới siêu nhỏ của NCC. | Không có ngoại lệ trong giai đoạn MVP | `elicitation_summary_Wedding-Service-Platform.md` Mục 3 | `UC-3.1` (Đăng ký NCC) |
| **BR-002** | Bảo mật số điện thoại khách hàng (Smart Privacy) | Số điện thoại của Cô dâu / Chú rể trên Lead gửi sang NCC ban đầu ở dạng mã hóa/ẩn (`0987***123`). NCC chỉ được mở khóa xem SĐT đầy đủ khi khách hàng bấm đồng ý nhận cuộc gọi hoặc xác nhận lịch hẹn tư vấn. | Khách hàng chủ động bấm [Hiện số điện thoại cho NCC] | `wedding_platform_domain_strategy.md` Mục 2 | `UC-2.1`, `UC-2.3` (`[STEP_BP-02_1]`, `[STEP_BP-02_3]`) |
| **BR-003** | Thời gian cam kết tiếp nhận Lead (SLA Tiếp Nhận) | NCC phải bấm nút [Tiếp nhận Lead] trên Vendor Portal trong vòng tối đa **2 giờ** kể từ khi nhận được thông báo Zalo ZNS. Nếu quá **24 giờ** NCC không phản hồi, hệ thống tự động hủy Lead (chuyển `Cancelled`) và kích hoạt cảnh báo vi phạm SLA. | Lead phát sinh ngoài giờ làm việc (22:00 - 07:00) được cộng dồn thời gian sang 08:00 sáng hôm sau | `elicitation_summary_Wedding-Service-Platform.md` Mục 2 | `UC-2.3`, `UC-2.5` (`[STEP_BP-02_3]`, `[STEP_BP-02_EX1]`) |
| **BR-004** | Cấp mã ưu đãi định danh Lead (Unique Voucher) | Mỗi yêu cầu tư vấn (Lead) hợp lệ được hệ thống tự động sinh 1 mã Voucher định danh độc nhất gồm **8 ký tự** (VD: `WVIP8899`), gắn với số điện thoại khách hàng, có thời hạn hiệu lực **30 ngày**. | Lead bị đánh dấu spam hoặc số điện thoại không xác thực OTP | `ba-product-overview.md` Mục 1 | `UC-2.1`, `UC-4.1` (`[STEP_BP-02_1]`, `[STEP_BP-04_1]`) |
| **BR-005** | Xác thực giao dịch hợp đồng 2 chiều (Two-way Confirmation) | Khi ký hợp đồng thực tế, NCC nhập mã Voucher + giá trị hợp đồng + tiền cọc và tải ảnh phiếu thu/hợp đồng. Hệ thống bắn thông báo xác thực tới Khách hàng. Khách hàng phải bấm [Xác nhận] trên App/Zalo trong vòng **72 giờ** thì giao dịch mới chuyển `Confirmed`. | Khách hàng từ chối do sai lệch thông tin (`PendingVerification` → `Draft`) hoặc quá hạn 72h tự động hủy | `SD-BookingContract.md` | `UC-4.1`, `UC-4.3`, `UC-4.5` (`[STEP_BP-04_1]`, `[STEP_BP-04_3]`) |
| **BR-006** | Thu hoa hồng môi giới chia 2 kỳ (50/50 Split Payment) | Phí hoa hồng môi giới của Sàn được chia làm 2 đợt thu từ NCC: **Đợt 1 (50%)** thu ngay sau khi khách hàng xác nhận cọc hợp đồng; **Đợt 2 (50%)** thu sau khi đám cưới đã diễn ra hoàn tất. | Hợp đồng thanh toán trọn gói 100% trước ngày cưới thì thu đủ 100% vào đợt đối soát gần nhất | `wedding_platform_domain_strategy.md` Mục 1 | `UC-5.1`, `UC-5.6` (`[STEP_BP-05_1]`, `[STEP_BP-05_6]`) |
| **BR-007** | Chu kỳ đối soát hoa hồng hàng tháng (Day 25 Settlement) | Hệ thống tự động tổng hợp bảng kê hoa hồng phải thu của tất cả NCC vào **00:00 ngày 25 hàng tháng**. NCC có thời hạn thanh toán trong vòng **5 ngày làm việc** (hạn chót ngày cuối tháng) qua cổng thanh toán VietQR động. | NCC thanh toán trễ hạn quá 7 ngày sẽ bị tạm ẩn các bài đăng dịch vụ (chuyển `Hidden`) | `wedding_platform_domain_strategy.md` Mục 1 | `UC-5.2`, `UC-5.3`, `UC-5.4` (`[STEP_BP-05_2]`, `[STEP_BP-05_3]`) |
| **BR-008** | Kiểm duyệt bài đăng & bảng giá dịch vụ (Moderation SLA 24h) | Mọi bài đăng gói dịch vụ mới hoặc cập nhật bảng giá của NCC phải trải qua kiểm duyệt của Moderator trong vòng tối đa **24 giờ**. Bài đăng vi phạm thuần phong mỹ tục, hình ảnh giả mạo hoặc phá giá thị trường sẽ bị từ chối kèm lý do. | NCC đối tác chiến lược (Tier Platinum) được áp dụng luồng phê duyệt nhanh (<4h) | `SD-Listing.md` | `UC-3.2`, `UC-3.4` (`[STEP_BP-03_1]`, `[STEP_BP-03_2]`) |
| **BR-009** | Tiêu chuẩn đánh giá xác thực (Verified Buyer Review) | Chỉ những khách hàng đã ký hợp đồng và hoàn tất dịch vụ cưới trên nền tảng (BookingContract ở trạng thái `Completed`) mới được gắn nhãn huy hiệu **[Verified Buyer]** và tính trọng số cao vào điểm Rating của NCC. | Khách hàng chỉ trải nghiệm tư vấn (chưa ký HĐ) chỉ được để lại nhận xét trải nghiệm phục vụ (không tính vào Rating sản phẩm) | `SD-Review.md` | `UC-6.1`, `UC-6.2`, `UC-6.3` |
| **BR-010** | Khớp nối khẩn cấp khi có sự cố (Emergency Matching) | Khi NCC chính thông báo hủy hợp đồng hoặc gặp sự cố bất khả kháng trước ngày cưới, đội ngũ CSKH có thẩm quyền kích hoạt luồng điều phối khẩn cấp, tự động tìm kiếm 2-3 NCC dự phòng cùng phân khúc và chuyển giao toàn bộ cọc/hợp đồng. | Khách hàng yêu cầu hoàn tiền 100% không nhận dịch vụ thay thế | `elicitation_summary_Wedding-Service-Platform.md` Mục 3 | `UC-2.6` (`[STEP_BP-02_EX2]`) |

---

## 2. Danh Mục Công Thức Tính Toán (Calculation Formulas)

### FM-001: Bảng Tỷ Lệ Hoa Hồng Môi Giới Theo 7 Ngành Hàng (Commission Rate Schedule)
- **Ý nghĩa:** Tỷ lệ hoa hồng phần trăm (%) áp dụng trên tổng giá trị hợp đồng dịch vụ cưới ký kết thành công giữa NCC và Khách hàng.

| STT | Ngành Hàng Dịch Vụ Cưới (Category) | Tỷ Lệ Hoa Hồng Mặc Định ($R$) | Khung Tỷ Lệ Đàm Phán (Min - Max) | Ghi Chú Nghiệp Vụ |
|:---|:---|:---|:---|:---|
| 1 | **Trung tâm Tiệc cưới (Venues & Sảnh tiệc)** | **3.0%** | `3.0% – 5.0%` | Giá trị hợp đồng lớn (100M - 500M+), tỷ lệ % thấp nhưng tuyệt đối cao |
| 2 | **Trang trí Tiệc cưới (Decor Concept)** | **8.0%** | `8.0% – 10.0%` | Hợp đồng trung bình (30M - 150M) |
| 3 | **Quay phim & Chụp ảnh (Photo / Video)** | **10.0%** | `8.0% – 10.0%` | Gói phóng sự / pre-wedding (15M - 50M) |
| 4 | **Váy cưới & Vest cưới (Bridal & Suits)** | **10.0%** | `8.0% – 10.0%` | Thuê / may váy cưới cao cấp |
| 5 | **Trang điểm Cô dâu (Bridal Makeup)** | **10.0%** | `8.0% – 10.0%` | Gói makeup ngày cưới & ăn hỏi |
| 6 | **Thiệp cưới & Quà cảm ơn (Invitations & Gifts)** | **8.0%** | `8.0% – 10.0%` | In ấn thiệp & quà tặng khách mời |
| 7 | **Wedding Planner trọn gói (Planning)** | **10.0%** | `10.0% – 12.0%` | Điều phối & tổ chức toàn diện |

---

### FM-002: Tổng Giá Trị Hoa Hồng Môi Giới Của Hợp Đồng (Total Commission)
- **Công thức:**
  $$\text{Total\_Commission} = \text{Contract\_Value} \times R$$
- **Trong đó:**
  - $\text{Total\_Commission}$: Tổng số tiền hoa hồng Sàn thu từ NCC (Đơn vị: VNĐ).
  - $\text{Contract\_Value}$: Tổng giá trị hợp đồng dịch vụ đã chốt thực tế (Đơn vị: VNĐ).
  - $R$: Tỷ lệ hoa hồng áp dụng cho ngành hàng dịch vụ tương ứng (theo bảng `FM-001`).
- **Điều kiện áp dụng:** Hợp đồng dịch vụ ở trạng thái `Confirmed`.

---

### FM-003: Hoa Hồng Thu Đợt 1 - Sau Khi Xác Nhận Cọc (Commission Kỳ 1)
- **Công thức:**
  $$\text{Commission\_K1} = \text{Total\_Commission} \times 50\% = \text{Contract\_Value} \times R \times 0.5$$
- **Trong đó:**
  - $\text{Commission\_K1}$: Số tiền hoa hồng NCC phải thanh toán ở kỳ đối soát ngày 25 ngay sau khi khách xác nhận cọc hợp đồng.
- **Điều kiện áp dụng:** Hợp đồng chuyển sang trạng thái `Confirmed` và số tiền cọc $\text{Deposit\_Amount} > 0$.

---

### FM-004: Hoa Hồng Thu Đợt 2 - Sau Khi Đám Cưới Hoàn Tất (Commission Kỳ 2)
- **Công thức:**
  $$\text{Commission\_K2} = \text{Total\_Commission} \times 50\% = \text{Total\_Commission} - \text{Commission\_K1}$$
- **Trong đó:**
  - $\text{Commission\_K2}$: Số tiền hoa hồng còn lại NCC phải thanh toán ở kỳ đối soát ngày 25 sau khi đám cưới diễn ra hoàn tất.
- **Điều kiện áp dụng:** Hợp đồng chuyển sang trạng thái `Completed`.

---

### FM-005: Điểm Uy Tín Trung Bình Của Nhà Cung Cấp (Vendor Rating Average)
- **Công thức:**
  $$\text{Rating\_Avg} = \frac{\sum_{i=1}^{n} \text{Rating}_i \times W_i}{\sum_{i=1}^{n} W_i}$$
- **Trong đó:**
  - $\text{Rating}_i$: Điểm đánh giá số sao của từng bài review (từ 1 đến 5 sao).
  - $W_i$: Trọng số bài đánh giá ($W_i = 1.0$ cho đánh giá có nhãn `[Verified Buyer]`; $W_i = 0.5$ cho đánh giá thông thường).
  - $n$: Tổng số bài review đã được Moderator duyệt (`Approved`).
  - Kết quả được làm tròn tới **1 chữ số thập phân** (VD: `4.8 / 5.0`).
- **Điều kiện áp dụng:** Tính lại mỗi khi có bài đánh giá mới được chuyển sang `Approved`.

---

### FM-006: Dự Toán Ngân Sách Cưới Còn Lại (Remaining Wedding Budget)
- **Công thức:**
  $$\text{Remaining\_Budget} = \text{Total\_Planned\_Budget} - \sum_{j=1}^{m} \text{Actual\_Spent}_j$$
- **Trong đó:**
  - $\text{Total\_Planned\_Budget}$: Tổng ngân sách cưới dự kiến ban đầu do Cô dâu / Chú rể thiết lập.
  - $\text{Actual\_Spent}_j$: Chi phí thực tế đã thanh toán/cọc cho từng hạng mục dịch vụ $j$.
  - $\text{Remaining\_Budget}$: Số tiền còn lại khả dụng cho các hạng mục cưới tiếp theo.
- **Điều kiện áp dụng:** Module Quản lý Ngân sách cưới trên Customer Web Portal (`UC-7.4`).

---

## 3. Danh Mục Vấn Đề Cần Làm Rõ (Open Q&A)

| STT | Câu hỏi / Vấn đề cần làm rõ | Phụ trách / Owner | Tình trạng |
|:---|:---|:---|:---|
| 1 | Chính sách chiết khấu giảm tỷ lệ hoa hồng cho NCC đạt doanh số lớn trong năm (Rebate policy) | Ban Giám Đốc / Finance | Dự kiến áp dụng ở Phase 2 |
| 2 | Hạn mức giá trị quà mừng cưới (Gift voucher) từ Sàn gửi tặng Cô dâu / Chú rể khi xác nhận hợp đồng thành công | Marketing / PO | Đã thống nhất: Quà tặng hiện vật hoặc voucher dịch vụ đối tác trị giá 200.000đ - 500.000đ |
