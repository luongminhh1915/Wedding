# CONFIRMED — chọn loại dịch vụ tại thời điểm tạo, không chọn nhóm lúc đăng ký

Ngày:21/10/2026, Asia/Ho_Chi_Minh. Nguồn: người dùng yêu cầu trực tiếp “bỏ cái chọn nhóm triết khấu lúc chọn tk đi, sau này khi ncc tạo dịch vụ, có thể tự do chọn loại dịch vụ, nma sẽ hiển thị các nhóm triết khấu tương ứng cho từng loại”.

## Quyết định có hiệu lực cho luồng mới

1. Đăng ký NCC không chọn nhóm kinh doanh/hoa hồng và không phụ thuộc tải danh sách nhóm. Chỉ giữ thông tin tài khoản/shop và xác nhận điều khoản chung rằng hoa hồng xác định theo danh mục, có thể xem khi tạo dịch vụ; xác nhận này không là ký hợp đồng hay kích hoạt ngoại lệ tài chính.
2. Khi tạo dịch vụ, NCC tự chọn mọi danh mục đang hoạt động và có nhóm chính sách đang hoạt động được cấu hình. Không cần membership shop hoặc Admin bổ sung nhóm trước. Áp dụng cả shop cũ/chưa có VendorBusinessGroup.
3. Form hiển thị tên nhóm + tỷ lệ hoa hồng mặc định đúng theo Category từ server. NCC không tự nhập rate/group hoặc sửa liên kết chính sách qua payload. “Chiết khấu” trong yêu cầu là hoa hồng nền tảng, không phải giá giảm cho khách.
4. Dịch vụ vẫn cần quy trình Draft→submit→Moderator approval trước public. Không nới actor/ownership/shop/account activity, category/group inactive hoặc canonical validation.
5. Chọn danh mục không tự chuyển rate của Vendor cũ, không sửa original confirmed contract/payment/snapshot. A–Z basis approval, no doublecharge, ba điều kiện activation và các quyết định tài chính trước vẫn giữ nguyên. Đây không là mở Phase5–7.
6. Không xóa membership/acknowledgement/audit cũ. Legacy API fields giữ tương thích nhưng không còn là điều kiện cho phép tạo dịch vụ. Không tự gán toàn bộ nhóm hoặc tạo bằng chứng Vendor đã chấp thuận mọi rate. Dịch vụ đã tồn tại giữ CategoryId bất biến; hỗ trợ legacy existing-nullparent theo guard cũ, không mở danh mục chưa cấu hình cho bài mới.

## Supersedes có giới hạn

- Thay phần “đăng ký mới chọn một/nhiều BusinessGroup” của decision19/10 `2026-10-19-legacy-and-contract-commission-adjustments.md`.
- Thay membership-based permission/one-time Admin scope confirmation cho tạo dịch vụ từ Phase2/Phase4B. Admin bổ sung thông tin nhóm cũ nếu còn dùng chỉ là metadata/audit, không còn là cấp quyền tạo bài.
- Không supersede quyết định bảo toàn rate cũ/snapshot/basis/activation/settlement hoặc lịch sử kiểm chứng. Không sửa Master Prompt gốc.

## Giới hạn triển khai

- Sử dụng schema hiện tại, không backfill/reseed/migration DB thật. Generic acknowledgement dùng version riêng, không nhận version hoặc rate từ client.
- Nhóm/rate chính thức lấy DB; thiếu cấu hình hoặc API lỗi không dựng tỷ lệ giả. Nhóm A–Z có thể được chọn để soạn nội dung nhưng việc chốt commission vẫn tuân thủ guard tài chính hiện có.
- Build/test/API/browser được báo cáo riêng, CONFIRMED không đồng nghĩa đã VERIFIED.
