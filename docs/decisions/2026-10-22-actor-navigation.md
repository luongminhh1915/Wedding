# CONFIRMED — thông báo căn giữa và navbar theo actor

Ngày22/10/2026, Asia/Ho_Chi_Minh. Nguồn: user yêu cầu sau bàn giao tài liệu, kèm ảnh trang thông báo bị lệch trái: “trang thông báo ... nằm ở giữa” và navbar mỗi actor “chỉ xuất hiện những mục thuộc chức năng của nó”.

- Căn giữa toàn bộ khung nội dung thông báo, có gutter khi màn hình nhỏ; không căn giữa từng dòng nội dung hoặc sửa global container của mọi trang.
- Navbar desktop và di động chỉ hiện lối tắt thuộc phân hệ/vai trò hiện tại. Customer và khách chưa đăng nhập giữ menu khám phá/planner; Vendor/staff không hiện menu bán hàng dành cho Customer.
- Account, thông báo và logout là chức năng chung theo phiên. Các chức năng còn lại vẫn trong dashboard được lọc role; navbar không cần chứa mọi tab cùng lúc.
- Đây là thay đổi presentation/navigation, **không cấp thêm/bớt quyền API**, không đổi role/tab permissions, onboarding, notification ownership, rate/finance/data hay deployment.
- Nhóm shortcut cụ thể được chọn từ tabs hiện hữu và có regression đối chiếu. Query `tab` phải xác định active state, không active tất cả link có cùng pathname dashboard.

Implementation/test/giới hạn xem [report](../reports/notifications-actor-navbar-2026-10-22.md); CONFIRMED không tự chứng nhận browser authenticated E2E.
