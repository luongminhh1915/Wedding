# Trang đăng ký độc lập

## Đối chiếu hiện trạng

`/register` đã có trang riêng, các trường và API đăng ký thật cho Customer và VendorOwner. Vấn đề thực tế là liên kết **Đăng ký ngay** trong hộp thoại đăng nhập chuyển URL nhưng không đóng hộp thoại, nên che trang mới. Không cần tạo lại hệ thống xác thực hoặc thêm bảng tài khoản.

Tham chiếu nghiệp vụ: Master PRD, BR-001 và UC-3.1 giữ một tài khoản chủ cho mỗi NCC; tài khoản nội bộ không được đăng ký công khai. Thông tin ngân hàng/MST được hoàn thiện trong hồ sơ đối tác, không thêm một bước không cần thiết trước khi tạo tài khoản.

## Luồng đã điều chỉnh

- Mọi liên kết đăng ký tiếp tục điều hướng `/register`; liên kết trong đăng nhập đóng hộp thoại. Hộp thoại đăng nhập cũng tự đóng khi đổi route.
- `/register?role=vendor` mở biểu mẫu NCC; các nút chọn vai trò đồng bộ query string và giữ thông tin đang nhập riêng cho từng loại tài khoản.
- Customer nhập họ tên, email, điện thoại, mật khẩu và xác nhận mật khẩu. Sau thành công vẫn qua `/auth/continue`, bắt buộc onboarding 10 câu hỏi nếu chưa hoàn thành.
- NCC thêm thương hiệu, địa phương và nhóm dịch vụ/hoa hồng hiện có; sau thành công đi theo định tuyến actor hiện có. Không cho người dùng tự chọn quyền nhân sự/quản trị.
- Không tự điền mặc định địa phương. Combobox tìm kiếm không dấu, điều khiển bằng bàn phím, hiện trạng thái không tìm thấy và lỗi cụ thể. Phải chọn một mục thật; văn bản tìm kiếm tùy ý không trở thành giá trị đăng ký.

## Danh sách địa phương và lưu trữ

Danh sách gồm **34 đơn vị cấp tỉnh hiện hành**, đối chiếu [cổng thông tin Chính phủ](https://chinhphu.vn/p) ngày 18/10/2026. Dùng tên riêng không kèm tiền tố “Tỉnh/Thành phố” để không gắn dữ liệu vào một phân loại hành chính có thể thay đổi. Có Huế, Đồng Nai, Bắc Ninh và Quảng Ninh với tên hiện hành; không dùng tên Thừa Thiên Huế hoặc các địa phương cũ đã hợp nhất trong lựa chọn mới.

Frontend và backend có danh mục tương ứng, kèm automated test đối chiếu đủ 34 tên và thứ tự. Backend đăng ký NCC chấp nhận một số cách nhập phổ biến như HCM, HN hoặc tiền tố Tỉnh/Thành phố rồi lưu tên chuẩn. Không tự đổi các giá trị địa phương đã lưu trong tài khoản, NCC, bài đăng hoặc vùng phục vụ trước đó.

Không thay schema, không cần migration và không reset/seed dữ liệu. Validation mật khẩu, xác nhận mật khẩu, giới hạn UTF-8/BCrypt, transaction tạo User/Vendor, quyền truy cập và quản lý phiên vẫn được sử dụng lại.
