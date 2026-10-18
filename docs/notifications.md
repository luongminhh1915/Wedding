# Trung tâm thông báo

`/notifications` là trang riêng dùng chung cho các actor đã đăng nhập. Biểu tượng chuông mở trang này, không tạo thêm popup. Bộ lọc Tất cả/Chưa đọc và trang hiện tại được lưu trong URL; desktop 8 mục, điện thoại 5 mục. Số chưa đọc trên navbar tính trên **toàn bộ** thông báo của tài khoản, không chỉ trang đang xem hoặc 50 bản ghi gần nhất. Các truy vấn dùng chung nhóm cache `notifications` và làm mới sau thao tác đọc; polling mỗi 60 giây cập nhật những sự kiện phát sinh khi đang mở web.

## API

- `GET /api/notifications?page=1&pageSize=8&unreadOnly=false`: `{ items, page, pageSize, totalCount, totalPages, unreadCount }`. Mỗi item chỉ chứa `id`, `message`, `createdAt`, `readAt`, `targetPath`; không trả địa chỉ email đích hoặc lỗi vận chuyển riêng tư. Giới hạn pageSize 1–20; page từ 1; trang vượt giới hạn được đưa về trang cuối hợp lệ. Sắp xếp `CreatedAt DESC, Id DESC` ổn định khi các bản ghi có cùng thời gian.
- `POST /api/notifications/{id}/read`: đánh dấu một thông báo của chính người đăng nhập; không tìm thấy hoặc thuộc người khác đều trả 404. Thao tác lặp lại không đổi thời điểm đã đọc.
- `POST /api/notifications/read-all`: cập nhật các bản ghi chưa đọc của chính người đăng nhập bằng một câu lệnh DB; không tải toàn bộ danh sách vào bộ nhớ. Hai lệnh đọc trả 204.

GET trước đây trả mảng 50 mục; consumer duy nhất trong repo là NotificationBell đã chuyển sang contract phân trang. Frontend/backend cần triển khai cùng phiên bản. JWT/role/tình trạng tài khoản tiếp tục do cơ chế xác thực hiện có kiểm tra; các endpoint không nhận UserId từ phía khách để chọn hộp thư khác.

## Liên kết nghiệp vụ

Migration cộng thêm `20261018022533_NotificationCenter` bổ sung `ContextKind` (nullable), `ContextId` (nullable) và index `(UserId, CreatedAt, Id)`, không xóa/seed lại hoặc cập nhật nội dung cũ. Thông báo cũ chưa có ngữ cảnh vẫn xem và đánh dấu đọc bình thường, không đoán liên kết từ câu chữ.

Producer lưu loại tài nguyên và GUID, **không lưu URL tùy ý**. API chỉ tạo `targetPath` sau khi kiểm tra lại quyền sở hữu hiện tại đối với hợp đồng, báo giá, tư vấn, lịch hẹn, combo, công việc hoặc đề nghị thay đổi/hủy. Tài chính chỉ nhận liên kết đề nghị cần Finance; Moderator/CSKH không được mở một phân hệ không thuộc quyền từ thông báo. Tài nguyên đã mất/không còn quyền có `targetPath=null`. Frontend kiểm tra lại allowlist đường dẫn/role và chống URL ngoài site, mã hóa bất thường, tham số lạ/trùng.

Các producer hiện có được gắn ngữ cảnh: gửi báo giá, gửi/chỉnh/rút combo/tư vấn, đề nghị thay đổi/hủy và phản hồi, đặt/cập nhật lịch hẹn, nhắc công việc và nhắc hợp đồng chờ xác nhận. Cơ chế email/outbox/SMTP và thời điểm nghiệp vụ không bị thay bằng dữ liệu giả. Dữ liệu ZNS/SMS chưa cấu hình không được coi là đã gửi thành công.

URL chứa `tab`, `section` khi cần, `focus`, `focusKind`; các màn hình đích chọn đúng khu vực, đưa mục liên quan vào danh sách hiện có và đánh dấu để dễ nhận biết. Công việc/lịch hẹn đã mất có thông báo không còn khả dụng; không tự tạo lại hoặc lấy dữ liệu của người khác để bù vào.

Hộp thư tư vấn/hợp đồng NCC có `focusId` tùy chọn trên API danh sách hiện có, luôn lọc theo NCC đang đăng nhập trước khi tìm ID. Nhờ vậy thông báo cũ mở đúng bản ghi dù nằm ngoài 20 mục mặc định hoặc bộ lọc hiện tại. Chuyển bộ lọc/xem lại danh sách sẽ xóa ngữ cảnh focus. Đề nghị thay đổi đã xử lý vẫn hiện khi mở từ thông báo, kể cả bộ lọc mặc định là “Cần phản hồi”; không tự gửi/chấp nhận đề nghị khi mở liên kết.

## Kiểm thử

`NotificationCenterTests` kiểm tra 63 thông báo/đếm chưa đọc vượt 50, phân trang ổn định, lọc chưa đọc, trang vượt giới hạn, đọc lặp lại, cách ly dữ liệu/mark-all, kiểm tra role/quyền tài nguyên, focus tư vấn/hợp đồng cũ ngoài trang đầu và chặn ID của NCC khác, URL không lấy từ nội dung, sender cũ/mới và migration thực trên SQL Server riêng tên `WeddingPlanTest_Notifications_<GUID>`. DB kiểm thử được kiểm tra tiền tố trước dọn dẹp; không dùng DB khách hàng. `client/tests/notifications.test.mjs` kiểm tra liên kết theo role/chống redirect, typed focus và UTC datetime2.

Trước khi chạy production, áp dụng migration đã review theo quy trình deploy hiện có; không bật seed hoặc startup migrations trên production. Không có thao tác gửi email thật trong các test trên.
