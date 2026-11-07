# Duyệt hàng loạt và tự động duyệt nội dung public — 01/11/2026

Status: **CONFIRMED** cho phạm vi xác nhận dưới đây. Chi tiết vòng lặp/batch là lựa chọn kỹ thuật, không phải chính sách hoa hồng.

## Nguồn và quyết định

Người dùng yêu cầu đối chiếu bản bàn giao mới, cập nhật tài liệu và deploy chức năng mới. Khi được hỏi có tự duyệt NCC chưa xác minh hay không, người dùng trả lời trực tiếp: **“Tự duyệt cả NCC chưa xác minh sau khi Admin xác nhận bật chế độ”.**

- Cho phép duyệt hàng loạt Listing, ServiceItem legacy và ComboTemplate đang chờ duyệt bằng các command kiểm duyệt chuẩn hiện có.
- Bật chế độ tự động phải có bước Admin xác nhận rõ phạm vi, bao gồm NCC chưa xác minh. Hủy không gửi lệnh và không bật chế độ.
- Việc duyệt nội dung không xác minh shop/giấy tờ, không thay thuế/ngân hàng/consent, không duyệt hay kích hoạt hoa hồng, không tác động hợp đồng/snapshot tài chính.
- Giữ quyền staff hiện có ở backend; Vendor hoặc thay localStorage không có quyền tự duyệt.

## Giới hạn triển khai trong bản release

- Chế độ tự động chạy khi staff mở màn hàng chờ, theo phiên/actor đã xác nhận. Không coi một checkbox lưu trong trình duyệt là chính sách tự động toàn máy chủ, toàn tài khoản hay giữa nhiều người dùng.
- Có giới hạn số lượng mỗi batch, khoảng nghỉ, khóa thao tác đang chạy, dừng khi lỗi hoặc đổi phiên. Hạng mục không đủ điều kiện được thông báo/bỏ qua; không chạy vòng lặp nóng vô hạn.
- Giữ command kiểm duyệt chuẩn, kiểm tra quyền đang hoạt động, phiên bản, trạng thái, audit và giao dịch. Lỗi hạ tầng/cancellation không được giấu thành thành công.
- Phê duyệt riêng từng nội dung vẫn dùng xác nhận của [quyết định21/10](2026-10-21-admin-approval-confirmation.md). Quyết định này bổ sung chế độ batch/auto có xác nhận, không xóa lịch sử hay thay các yêu cầu khác của quyết định cũ.

Chưa xác nhận hay triển khai một tác vụ tự duyệt chạy trên server khi không có staff mở màn. Muốn mở rộng sang tác vụ thường trực cần yêu cầu và thiết kế riêng.
