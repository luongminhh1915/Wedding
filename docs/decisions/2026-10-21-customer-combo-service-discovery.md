# Customer — chọn combo và dịch vụ cùng một nơi

Ngày21/10/2026. Trạng thái: **CONFIRMED**.

Nguồn trực tiếp: người dùng yêu cầu phần chọn combo của khách hàng phải hiển thị cả dịch vụ như trước, không chỉ combo.

- Trang `/combo` mặc định giữ gói combo của NCC và bổ sung khu dịch vụ lẻ thật có ảnh/giá/cấu hình/chọn vào cùng giỏ.
- Giữ tìm kiếm/danh mục/paging/Back/reload và một giỏ một NCC. Gói FixedPrice vẫn không tự thêm/bớt/đổi thành phần; thay đổi riêng theo quy trình đã chốt20/10.
- Không đổi điều kiện public/eligibility/consent/verification của nguồn, không tạo dịch vụ trùng hoặc dữ liệu mẫu trong DB ứng dụng để lấp danh sách trống.
- Preview dịch vụ tải có giới hạn từ catalog API hiện có; không dùng endpoint full builder để né paging. Không thay schema, giá/hoa hồng hoặc snapshot.

Implementation/verification: [report21/10](../reports/customer-combo-service-discovery-2026-10-21.md).
