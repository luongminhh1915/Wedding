# Wedding Match - Hành trình phát triển sản phẩm

Bộ bàn giao ngày **05/11/2026**, gồm **265 ảnh gốc**: 98 ảnh lịch sử và 167 ảnh chụp mới của 35 màn/trạng thái. PDF **79 trang** kể lại 14 mốc phát triển và có chỉ mục ảnh.

## Mở theo thứ tự

1. `WeddingMatch_HanhTrinhSanPham_20261105.pdf`: xem câu chuyện phát triển, thay đổi giao diện và hiện trạng.
2. `01_Anh_san_pham_theo_moc/`: 70 ảnh sản phẩm lịch sử, chia theo ngày.
3. `05_Web_hien_tai_2026-11-05/`: ảnh web thật mới chụp, chia theo trang. `part-01`, `part-02`... là các đoạn cuộn liên tiếp có vùng chồng lặp; xem đủ các đoạn để đọc trang dài.
4. `02_Tu_lieu_tham_khao/`: 23 thiết kế, bảng màu và ảnh mẫu, tách khỏi ảnh sản phẩm.
5. `03_Thiet_lap_trien_khai_an_toan/`: 5 ảnh minh họa quá trình thiết lập/triển khai.
6. `04_Tong_quan_contact_sheets/`: bảng ảnh thu nhỏ để tra nhanh, không tính thêm vào số ảnh gốc.

Hai cặp catalog CSV/JSON chứa mã, chú thích, ngày, môi trường, đường dẫn và SHA-256. Ảnh gốc được sao chép nguyên byte. `Bia_PDF.jpg` là ảnh xem trước tài liệu, không phải ảnh chụp sản phẩm mới.

## Phạm vi và những mốc thiếu

- Ảnh phục hồi sớm nhất là **16/10/2026**. Git có lịch sử trước đó nhưng chưa tìm được ảnh tương ứng; không dựng lại ảnh quá khứ.
- Phần lớn ngày ảnh lịch sử dựa trên ngày lưu/sửa tệp, được đối chiếu tài liệu khi có. Không mặc định đó luôn là ngày chụp chính xác.
- Các màn Customer/NCC cũ dùng ảnh lịch sử. Ảnh nội bộ mới chụp bằng **SuperAdmin**; không nhận đây là phiên riêng của Moderator/Finance/NCC/Customer.
- Trang so sánh, xem trước thiệp và trang kỷ niệm chứa nội dung minh họa; đã ghi nhãn trong PDF/catalog.
- Khung ảnh bìa Blog chưa hiển thị ở ảnh danh sách mới chụp; giữ nguyên trạng thái quan sát được. Không lấy ảnh này làm bằng chứng tải ảnh thành công.
- Không đưa mật khẩu hiện rõ, cấu hình riêng, backup DB hoặc hội thoại riêng vào bộ bàn giao. Hai ảnh mới bị lệch tab đã được loại khỏi bản giao.
- Đây là bộ tư liệu giao diện, không phải chứng nhận mọi chức năng hoặc giao dịch đã được kiểm thử hoàn tất. Lượt này không sửa code, chạy SQL/migration, deploy hay tạo dữ liệu giao dịch.

Các mốc thiếu bằng chứng ảnh và giới hạn kiểm chứng được ghi trong PDF. Đọc `README-ANH-LICH-SU.md` nếu cần chi tiết về bộ ảnh phục hồi.
