# CONFIRMED — xác nhận trước khi duyệt dịch vụ/combo public

Ngày:21/10/2026, Asia/Ho_Chi_Minh. Nguồn: user yêu cầu “tạm thời bỏ qua1bên cái host website”, trong chức năng Admin cần thông báo “are you sure” khi duyệt combo/dịch vụ.

## Phạm vi

- Tạm gác việc mua/cấu hình/deploy hosting, giữ quyết định tên miền/chi phí trước đó như lịch sử; không tự tạo account, mua hoặc deploy trong lượt này.
- Thêm bước xác nhận trước gửi lệnh duyệt **public Listing, ServiceItem legacy, Combo** ở các màn hiện có. Admin/SuperAdmin và Moderator đang dùng cùng màn được cùng UX, không được cấp quyền mới.
- Hộp thoại chỉ rõ loại, tên, NCC và phiên bản đang xem, nút Hủy/Xác nhận duyệt. Mở hoặc Hủy không ghi DB/gọi mutation; chỉ xác nhận mới gửi đúng request đã xem.
- Giữ note khi hủy hoặc API lỗi; chặn thao tác trùng/busy và dữ liệu đổi trong khi xác nhận. Không tự chuyển sang revision mới, không đoán revision legacy.
- Không thay payload hợp lệ/endpoints/reject reasons/quyền backend/activity/public eligibility, không đụng hợp đồng, commission hoặc snapshot. Canonical pricing ServiceItem vẫn duyệt cùng Listing; không tạo một lượt duyệt riêng.

## Không suy rộng

Không mở Phase5–7, thêm phê duyệt tài chính/settlement, đổi luật moderation hoặc làm mọi nút Admin không liên quan trong cùng nhiệm vụ. Trạng thái IMPLEMENTED/VERIFIED và giới hạn browser/SQL phải dựa vào [báo cáo](../reports/admin-approval-confirmation-2026-10-21.md), không chỉ quyết định này.
