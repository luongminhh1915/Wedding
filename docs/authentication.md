# Đăng ký, đăng nhập và phân hệ tài khoản

## Luồng theo actor

Mọi actor dùng chung `/login`; không cho người đăng nhập tự chọn quyền. Vai trò lấy từ DB và API trả về sau xác thực.

| Actor | Cách có tài khoản | Sau đăng nhập |
| --- | --- | --- |
| Customer | Đăng ký khách hàng, OTP email hoặc Google khi đã cấu hình | Khách mới: 10 câu hỏi `/planner/setup`; đã thiết lập: `/customer/dashboard` hoặc trang khách hợp lệ đang xem |
| VendorOwner | Đăng ký đối tác | `/vendor/dashboard` |
| Moderator | SuperAdmin cấp trong Người dùng & đối tác | `/admin/dashboard`, các tab kiểm duyệt được phép |
| Finance | SuperAdmin cấp trong Người dùng & đối tác | `/admin/dashboard`, các tab tài chính được phép |
| CustomerCare | SuperAdmin cấp trong Người dùng & đối tác | `/admin/dashboard`, các tab hỗ trợ được phép |
| SuperAdmin | Tài khoản quản trị được cấp riêng, không đăng ký công khai | `/admin/dashboard` |

Đăng ký công khai chỉ có Customer/NCC. Nhân sự nội bộ sử dụng mật khẩu, không được tạo tài khoản nhân sự qua Google/OTP. Tạo nhân sự không tạo thêm SuperAdmin và không đăng nhập thay nhân sự vừa tạo.

## Lỗi chuyển phân hệ đã sửa

Trước đây `state.from`/intent chỉ kiểm tra đường dẫn nội bộ, nên khách đã thiết lập có thể quay lại `/vendor/dashboard` được nhớ từ tài khoản NCC. Guard chỉ hiện lỗi 403 tại URL đó. Ngoài ra, interceptor 401 chỉ xóa localStorage, không xóa user/token trong Zustand đang hiển thị.

Hiện tại:

- Password, đăng ký, OTP và Google đều qua `/auth/continue`. Đích quay lại được lọc theo actor; chặn auth-loop, URL ngoài site, đường dẫn mã hóa/sai kiểu. Khách không thể nhận intent của NCC/admin, kể cả URL viết hoa hoặc encoded.
- Đi trực tiếp vào phân hệ khác quyền sẽ chuyển về không gian của actor hiện tại, không để kẹt tại 403 hoặc mở nhầm dữ liệu. API vẫn kiểm tra quyền, không chỉ dựa vào giao diện.
- Đăng xuất xóa token/user, hủy query, xóa cache, xóa intent cũ và dùng replace về trang đăng nhập. Giỏ riêng từng khách/bản nháp DB không bị xóa.
- Phiên khôi phục từ trình duyệt phải được `/api/auth/me` xác nhận trước khi mở dữ liệu actor. JSON hỏng, token lỗi/hết hạn hoặc role không hợp lệ không làm crash ứng dụng. Khi chưa xác minh được do mất mạng, có nút thử lại/đăng xuất, không đoán vai trò.
- Khi đổi tài khoản, cây giao diện được mount lại theo session revision để bỏ state tạm và đóng kết nối/chat cũ. Các tab cùng origin đồng bộ đăng xuất/đổi tài khoản qua storage event. `localhost`, IP và subdomain là các origin riêng, không dùng chung phiên.
- 401 chỉ kết thúc phiên nếu phản hồi thuộc token hiện tại; 401 từ lần đăng nhập sai hoặc request của tài khoản trước không xóa phiên mới. `/me` cũ cũng không ghi đè tài khoản mới. Mỗi JWT mới có `jti` riêng, kể cả đăng nhập lại cùng tài khoản trong cùng giây.
- Menu tài khoản/điều hướng di động đi theo actor. Finance/CSKH không gọi query bài chờ duyệt dành cho Moderator/SuperAdmin.

JWT là bearer token có thời hạn, chưa có refresh token hoặc danh sách thu hồi riêng cho từng lần logout. Logout kết thúc phiên trên trình duyệt; không khẳng định bản sao token ở thiết bị khác đã bị thu hồi. Server kiểm tra tài khoản hoạt động, role và `SecurityVersion` trong DB trên mỗi request. Đổi mật khẩu hoặc thay đổi trạng thái khóa/kích hoạt tăng phiên bản này, nên mở khóa không làm token cũ sống lại. Production phải dùng HTTPS, JWT secret riêng và không bật seed demo.

## Đổi mật khẩu và bảo vệ phiên

- Nút chìa khóa “Đổi mật khẩu” trên thanh tài khoản dùng được cho cả 6 actor, không mở lại biểu mẫu hồ sơ cưới hoặc thay đổi onboarding.
- `POST /api/auth/change-password` nhận `currentPassword`, `newPassword`, `confirmPassword`; không nhận ID/role để đổi thay người khác. Phải xác minh mật khẩu hiện tại, mật khẩu mới khác mật khẩu cũ và đáp ứng giới hạn 8 ký tự / 72 byte UTF-8.
- Cập nhật hash, tăng `SecurityVersion` và audit trong cùng transaction; audit/response không chứa mật khẩu hoặc hash. Thành công trả 204, không phát JWT mới; giao diện xóa phiên/cache và yêu cầu đăng nhập lại.
- Request đổi mật khẩu được gắn với token lúc mở hộp thoại. Đổi tài khoản ở tab khác không được làm request chuyển sang actor mới; phản hồi cũ không đăng xuất phiên mới.
- JWT thiếu, sai hoặc trùng claim `security_version` bị từ chối. Sau khi cập nhật bản này, người dùng có token phát hành trước đó cần đăng nhập lại; tài khoản và dữ liệu vẫn giữ nguyên.
- Migration `ChangePasswordSecurityVersion` chỉ thêm `Users.SecurityVersion` mặc định 0. Áp dụng cùng các migration đã review trước khi chạy bản API mới; không reset schema hoặc dữ liệu.
- Chat kiểm tra quyền sở hữu phòng và phiên hiện tại cho mỗi lần tham gia/rời/gửi; chỉ người tham gia được gửi, SuperAdmin/Moderator giữ quyền đọc hỗ trợ như HTTP. Broadcast dùng nhóm theo lead + người dùng + role + phiên bản hiện tại, không gửi vào phòng chung chỉ biết GUID. Socket/token cũ không nhận broadcast mới sau khi phiên bản/role/quyền thay đổi; kết nối đóng khi JWT hết hạn.

Khôi phục mật khẩu qua email, đặt mật khẩu cho tài khoản chỉ có Google/OTP và ép đổi mật khẩu nhân sự lần đầu vẫn là backlog. Không giả lập gửi email hoặc bỏ bước xác minh danh tính khi chưa có SMTP/provider phù hợp.

## Đăng ký và cấp nhân sự

- Frontend có label/autocomplete, nhập lại mật khẩu, hiện/ẩn, báo lỗi từng trường, chặn gửi hai lần. API thực sự chạy validators trong handler; không phụ thuộc vào pipeline validation toàn ứng dụng đang chưa được nối.
- Email được trim/lowercase; họ tên/liên hệ được trim theo domain. Không trim mật khẩu có khoảng trắng có chủ đích. Mật khẩu mới ít nhất 8 ký tự, tối đa 72 byte UTF-8 theo BCrypt, không nhận toàn khoảng trắng. Tài khoản cũ mật khẩu ngắn vẫn đăng nhập được, không tự đổi mật khẩu cũ.
- Tên tối đa 150 ký tự, email 150, điện thoại Việt Nam 10 chữ số bắt đầu bằng 0; thương hiệu NCC 200, tỉnh/thành 100. Mức hoa hồng đăng ký NCC giữ các lựa chọn hiện có 3/8/10/12%, không sửa tài khoản cũ.
- Tạo tài khoản/kiểm tra email trùng trong transaction serializable; có test SQL đồng thời chỉ tạo một email. Slug NCC có xử lý dấu tiếng Việt và tránh trùng. Public DTO không cho truyền role để nâng quyền.
- `POST /api/users/staff`: chỉ SuperAdmin đang hoạt động, body `fullName,email,phoneNumber,password,role`; role chỉ `Moderator|Finance|CustomerCare`. API trả `UserManagementDto`, không trả token/hash/mật khẩu. Có audit, không log mật khẩu. Nhân sự được bàn giao mật khẩu ban đầu qua kênh riêng an toàn; chưa tự gửi email.
- Khóa/kích hoạt: Moderator không quản lý tài khoản nhân sự; SuperAdmin không bị khóa từ thao tác này. Backend kiểm tra lại actor trong DB, không chỉ ẩn nút trên UI.

OTP email/Google chỉ hiển thị và chạy khi provider được cấu hình. Lượt QA này không gửi OTP/email thật hoặc đăng nhập tài khoản Google ngoài hệ thống; không coi provider chưa cấu hình là đã nghiệm thu tích hợp thực.

## Nghiệm thu local 17/10/2026

- 63/63 test frontend; 134/134 backend với `WEDDING_TEST_SQLSERVER=1`, không skip. Frontend/API build thành công; lint 0 lỗi, 10 cảnh báo cũ ngoài phần auth.
- Kiểm tra trình duyệt trên origin QA riêng `auth-qa.localhost:5176`: vào URL NCC khi chưa đăng nhập; đăng nhập NCC đúng portal; logout → mở lại URL NCC → đăng nhập Customer đã thiết lập về dashboard khách; Customer mở trực tiếp URL NCC được chuyển lại dashboard khách. Tab thứ hai khôi phục phiên qua `/me`, nhận logout và chuyển actor sang SuperAdmin mà không giữ nội dung khách.
- SuperAdmin thấy biểu mẫu cấp nhân sự; form trống bị chặn/báo lỗi phía client. Tạo 3 nhân sự hư cấu qua API QA và xác nhận phiên SuperAdmin không đổi. Không xác nhận hợp đồng, thay mật khẩu, gửi email hay thực hiện thanh toán.
- Form đăng ký Customer/NCC hiển thị đúng các trường và lỗi bắt buộc, không có quyền nội bộ để tự chọn. Khách QA mới đăng ký qua API đăng nhập trên trình duyệt được chuyển thẳng tới bước 1/10; không hoàn tất wizard hoặc tạo kế hoạch trong lượt này. Finance đăng nhập thấy đúng các tab tài chính, không thấy quản lý người dùng/cấp nhân sự hoặc kiểm duyệt; tổng quan tải thành công. Các tab trình duyệt QA tạm đã đăng xuất và đóng, không thay phiên ở origin `localhost` của người dùng.
- `tools/Verify-AuthQa.ps1` mặc định chỉ kiểm tra provider/read-only. `-ApplyFixtures` chỉ append 5 tài khoản hư cấu có tiền tố `[QA AUTH]` vào DB duy nhất `WeddingPlanUi_0161333`, yêu cầu API 5009 đúng binary auth QA. Utility không tạo SuperAdmin, không reset/ghi đè tài khoản, kiểm tra đủ 6 role qua login + `/me` và API khác quyền trả 403. Không log thông tin đăng nhập/token và không được dùng trong startup/deploy.

Các kết quả ở mục nghiệm thu trên là vòng auth trước phần đổi mật khẩu. Bản mới có migration cộng thêm `ChangePasswordSecurityVersion`; cần cập nhật backend, frontend và schema cùng nhau. Kiểm thử mới dùng DB hư cấu tên riêng, không reset/seed lại DB người dùng. Xem thêm [kiểm chứng gói deploy](deployment-verification.md).
