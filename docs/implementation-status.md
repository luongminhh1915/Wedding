# Tiến độ và lỗi còn lại

Trạng thái ngày 17/10/2026, nhánh `weddy-journey`. Đối chiếu `Wedding-plan.md` với code hiện tại; bảng “hiện trạng” trong bản kế hoạch cũ không còn mô tả đầy đủ code mới. Chỉ chuẩn bị deploy, không tự đăng ký host, phát hành Internet, gửi email thật hoặc tạo giao dịch tiền thật.

## Đã bổ sung ở vòng hoàn thiện gần nhất

- SQL Server là mặc định; Production không xóa/tạo lại DB, không seed demo hoặc tự chạy migration. Bản SQLite cũ và ảnh đã có snapshot riêng, không tự import/ghi đè DB mới.
- .NET 10 LTS, gói IIS cùng origin frontend/API, cấu hình riêng và ảnh nằm ngoài thư mục publish. Script SQL chạy lại được, có kiểm tra integrity, encoding và session options phù hợp sqlcmd.
- Quy trình deploy/backup, bootstrap quản trị đầu tiên không có mật khẩu mặc định, kiểm thử Production trên SQL Server riêng. Xem [hướng dẫn deploy](study-deployment.md).
- Đổi mật khẩu cho các actor, thu hồi token cũ theo phiên bản, chống request của tài khoản trước ghi nhầm tài khoản mới.
- Bảo vệ phòng chat theo quyền thực và phiên hiện tại, không cho đăng ký phòng bằng GUID của người khác hoặc tiếp tục nhận tin mới bằng phiên đã thu hồi.
- Chi tiết bài đăng công khai chỉ hiện bài Active của NCC/tài khoản còn hoạt động. Chủ NCC và Moderator/SuperAdmin được preview đúng quyền; không lộ lý do từ chối cho người ngoài.
- Review công khai không chứa ID tài khoản khách, ID/mã hợp đồng hay dữ liệu kiểm duyệt; upload ảnh listing chỉ dành cho NCC hoạt động có hồ sơ hợp lệ.

## Ưu tiên tiếp theo (chưa coi là đã hoàn thành)

1. Tài khoản tạo bằng Google/OTP có SĐT giữ chỗ `chua-cap-nhat` nhưng chưa có luồng cập nhật liên hệ, nên có thể bị chặn đặt lịch hẹn. Cần form liên hệ riêng, xác thực dữ liệu và không tự đánh dấu đã OTP SĐT. Không khôi phục phần sửa hồ sơ cưới đã được yêu cầu bỏ.
2. Sửa listing của NCC: form đang khởi tạo mô tả rỗng/thiếu album và payload update chưa lưu thay đổi ảnh. Cần tải detail đúng quyền và cập nhật media trong transaction, tránh mất mô tả/ảnh khi chỉ sửa giá.
3. Quên mật khẩu, đặt mật khẩu sau Google/OTP và bàn giao mật khẩu nhân sự an toàn cần thiết kế xác minh lại danh tính. Không dùng endpoint công khai tự đặt/reset mật khẩu theo email.
4. SMTP, Google OAuth, Zalo, webhook ngân hàng và hóa đơn điện tử phải được chủ dự án cấu hình và nghiệm thu với provider thật. Có lớp code tích hợp không đồng nghĩa đã tích hợp thành công.
5. Hoàn tất đo tải trên host được chọn trước khi cam kết 100 khách đồng thời. Số RAM/bài smoke một người không thay thế load test hoặc cam kết SLA.

Các yêu cầu cập nhật của chủ dự án luôn ưu tiên hơn bản kế hoạch cũ: combo chỉ thuộc **một NCC**, khách mới hoàn thành 10 câu hỏi trước dashboard, không mở lại sửa hồ sơ cưới, ảnh thiệp upload trực tiếp và tự xếp khách lấp chỗ bàn còn trống.
