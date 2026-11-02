# Quyết định: Danh mục thống nhất từ tìm dịch vụ đến hợp đồng

Nguồn: yêu cầu trực tiếp của người dùng ngày 20/10/2026, kèm ảnh bộ lọc dịch vụ Weddy. Áp dụng trên kiến trúc hiện có, không thay bảng giá/chính sách tài chính ngoài những quyết định đã chốt.

## Hướng triển khai

- Dùng **BusinessGroup → Category → Listing/ServiceItem** làm nguồn phân loại thống nhất. Một shop có nhiều nhóm; mỗi dịch vụ có một danh mục. Combo một NCC vẫn giữ danh mục của từng thành phần.
- Trang khám phá dịch vụ có danh mục + từ khóa tìm kiếm, thanh danh mục trực quan và bộ lọc kết hợp; trạng thái nằm trong URL để quay lại/tải lại không mất lựa chọn.
- Form NCC lấy danh mục được phép từ backend dựa trên nhóm đã đăng ký/xác nhận; không dựa vào danh sách cứng hoặc mức hoa hồng client gửi. Backend kiểm tra lại khi tạo/gửi duyệt.
- Shop cũ không được tự đoán/gán nhóm từ tỷ lệ hoa hồng. Tạo bài mới khi chưa có phạm vi đã xác nhận cần Admin bổ sung nhóm. Bài cũ không bị xóa/đổi danh mục; phạm vi hiện có của shop legacy được giữ để chỉnh sửa/duyệt nội dung hợp lệ.
- Admin chỉ bổ sung nhóm với lý do và audit. Việc này không thay tỷ lệ cũ, không giả lập xác nhận chính sách của NCC và không kích hoạt thỏa thuận tài chính mới.
- Danh mục của bài đã tạo được giữ cố định khi sửa. Cung cấp loại dịch vụ khác cần bài riêng; không sửa category làm lệch lịch sử giao dịch.
- Hợp đồng mới lưu snapshot danh mục/nhóm và nguồn tỷ lệ thực tế lúc xác nhận, cùng tỷ lệ gốc đã có. Không lấy danh mục/rate hiện hành để viết lại hợp đồng đã chốt.

## Giới hạn tài chính và dữ liệu cũ

- Vendor legacy tiếp tục giữ tỷ lệ shop đến khi có chuyển đổi hợp lệ do Admin xác nhận; Vendor mới kế thừa chính sách danh mục như quyết định trước.
- Snapshot mới là dữ liệu nullable bổ sung, không backfill hợp đồng cũ hoặc suy diễn người/phê duyệt/phiên bản chính sách. Với legacy không biết phiên bản nguồn, giữ NULL.
- A–Z vẫn không được tính mặc định 12% trên toàn ngân sách/hợp đồng; thiếu approved basis phải chặn như hiện tại. Metadata phân loại không phải engine VAT/chi hộ/ngoại lệ hoàn chỉnh.
- Không gán tỷ lệ cho MC, ban nhạc hoặc loại mới chỉ vì có trong ảnh tham khảo. Đợt này dùng tám Category đã có và các nhóm/chính sách đã thống nhất; danh mục được lấy từ DB, không tạo mặc định tài chính mới.
- Tỷ lệ hoa hồng nền tảng–Vendor và giảm giá Vendor–Customer là hai khái niệm riêng.

## Quyết định đánh giá đã nhận trong cùng phiên

Người dùng chọn: **chỉ giao dịch hoàn tất mới được tạo đánh giá mới; giữ đánh giá tư vấn cũ và gắn nhãn riêng**. Không xóa dữ liệu cũ hoặc tự thay trọng số rating. Triển khai/gate được báo cáo riêng, không suy ra hoàn tất chỉ vì đã ghi quyết định này.
