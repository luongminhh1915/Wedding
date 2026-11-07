# Bộ ảnh hành trình Wedding Match - 05/11/2026

Phạm vi: phục hồi ảnh lịch sử, chụp hiện trạng website và tạo album/PDF theo yêu cầu chủ dự án. Không triển khai tính năng hoặc deploy.

- Branch `main`, HEAD `20b292b5384254c0d250766847570adbbe86bd48`; bảo toàn working tree sẵn có. Không commit/push/reset/clean.
- Đầu ra: `output/WeddingMatch_HanhTrinhSanPham_20261105.zip` (36.116.143 byte), PDF `output/pdf/WeddingMatch_HanhTrinhSanPham_20261105.pdf` (79 trang).
- 98 ảnh lịch sử: 70 sản phẩm, 23 tham khảo, 5 thiết lập. Mốc ảnh phục hồi sớm nhất16/10; đa số ngày dựa trên timestamp tệp, có đối chiếu report. Không dựng lại ảnh quá khứ.
- 167 ảnh mới của35màn/trạng thái trên web thật, gồm desktop/mobile và các mục Admin. Trang dài lưu chuỗi ảnh cuộn có chồng lặp. Phiên Admin do chủ dự án tự đăng nhập; không tạo tài khoản/giao dịch hay sửa nội dung/cấu hình. Chỉ mở form trống, không gửi.
- Chưa chụp lại phiên riêng Customer/VendorOwner/Moderator/Finance; dùng ảnh lịch sử khi có. Màn so sánh/thiệp/kỷ niệm chứa nội dung minh họa. Bloglist có ảnh bìa chưa hiển thị lúc chụp, đã ghi caveat. Hai ảnh lệch tab bị loại; không đưa mật khẩu hiện rõ, private config hoặc hội thoại riêng vào bản giao.
- Kiểm chứng artifact:265/265ảnh gốc giải mã được và SHA256 khớp; PDF79trang render bằng Poppler, kiểm phông tiếng Việt và xem bố cục; ZIP CRC PASS,287tệp. Không chạy build/test ứng dụng/SQL/migration trong lượt tài liệu này.
- Script/dữ liệu trung gian nằm `artifacts/product-history-20261105/`. Đây có cả inventory nội bộ không dành chia sẻ; **chỉ giao ZIP/output đã lọc**, không nén toàn bộ staging.
- Catalog và README nằm `output/product-history-20261105/`; checklist chức năng giữ nguyên vì ảnh UI không xác nhận toàn bộ nghiệp vụ hoàn tất.

Lệnh chính: bundled Python chạy `assemble_current.py`, `build_history_pdf.py --max-current 0`, `package_delivery.py`; Poppler `pdftoppm -scale-to 1200 -jpeg -r 90` render PDF. Đã đổi phông serif sang Times New Roman vì Georgia trên máy thiếu glyph tiếng Việt. Không tự thay đổi cài đặt hệ thống.
