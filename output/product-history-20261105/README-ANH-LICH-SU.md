# Ảnh lịch sử phát triển Wedding Match

Bộ này gồm **98 ảnh gốc đã chọn an toàn**, từ mốc lưu tệp **16/10/2026 đến 05/11/2026**. Ảnh hiện trạng website được nhóm tổng hợp bổ sung riêng; con số 98 chỉ tính ảnh lịch sử trong danh mục đi kèm.

## Cách xem

- `01_Anh_san_pham_theo_moc/`: 70 ảnh giao diện sản phẩm, thử nghiệm và kiểm chứng chức năng, chia theo ngày. Một số ảnh là bản local hoặc dữ liệu QA, không phải ảnh website production.
- `02_Tu_lieu_tham_khao/`: 23 ảnh mẫu, bảng màu, nhận diện, prototype và thiết kế tham khảo. Không xem nhóm này là bằng chứng chức năng đã triển khai.
- `03_Thiet_lap_trien_khai_an_toan/`: 5 ảnh thiết lập/kết nối/triển khai đã chọn để minh họa hành trình.
- `04_Tong_quan_contact_sheets/`: 8 bảng ảnh thu nhỏ có mã ảnh và chú thích để tra cứu nhanh. Đây là **contact sheet**, không phải ảnh chụp gốc và không tính thêm vào 98 ảnh.
- `historical-image-catalog.csv` và `.json`: mã ảnh, ngày, cơ sở xác định ngày, môi trường, chú thích, đường dẫn tương đối, kích thước và SHA256.

## Ngày và phạm vi

Ngày chủ yếu lấy từ thời điểm sửa/lưu tệp (`filesystem_mtime`); một số ngày được đối chiếu từ thư mục hoặc báo cáo. Tệp từng được sao chép có thể mang ngày lưu khác ngày chụp thực tế. Vì vậy, đây là các **mốc bằng chứng tìm lại được**, không khẳng định mọi ngày hoặc mọi màn hình từ lúc khởi đầu đều còn đầy đủ.

Đã kiểm kê 117 tệp ứng viên: bỏ 3 bản sao trùng hoàn toàn và 16 tệp khác không phù hợp để chia sẻ (thông tin riêng tư/nhạy cảm, ảnh cần che thông tin, ảnh trống hoặc thiết lập nội bộ). Không đưa ảnh chứa mật khẩu hay hội thoại riêng vào bộ này.

## Tính toàn vẹn

Ảnh gốc tại nguồn được giữ nguyên. Các bản sao trong bộ này khớp SHA256 với nguồn đã kiểm kê; không cắt, tô, sửa hay tạo lại ảnh. Một số tệp nguồn có đuôi `.png` nhưng chứa JPEG; đuôi bản sao được đặt theo định dạng thật mà không đổi byte ảnh.

Danh mục chia sẻ chỉ chứa đường dẫn tương đối; không kèm cấu hình riêng, mật khẩu, backup database hoặc đường dẫn nguồn riêng tư.
