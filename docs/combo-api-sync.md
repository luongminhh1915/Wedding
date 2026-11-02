# Đồng bộ giao diện và API catalog

Ngày 18/10/2026, màn hình combo báo không mở được danh mục vì frontend mới gọi `GET /api/combos/catalog?page=1&pageSize=6` nhưng API local vẫn chạy DLL cũ. Gọi trực tiếp cổng 5008 và qua proxy 5173 đều trả 404 cho endpoint này; `/api/combos/builder` cũ vẫn trả 200 JSON. Đây không phải bằng chứng mất DB hoặc mất giỏ.

DLL trong `src/Wedding.WebApi/bin/Debug/net10.0` được build lúc 15:53:51 UTC ngày 17/10, trước source route catalog (17:50:12 UTC) và handler phân trang mới. Vite cập nhật frontend trực tiếp; API chạy bằng `dotnet run` thông thường không tự nạp lại source mới.

## Cách xử lý

Dừng đúng API đang chạy bằng Ctrl+C ở terminal backend, sau đó tại thư mục repo chạy:

```powershell
dotnet run --project .\src\Wedding.WebApi --launch-profile http
```

Không chạy seed/reset, không tạo DB mới và không thay giỏ bằng dữ liệu giả. Nếu Development bật migration thì chỉ áp dụng migration cộng thêm đã review; production áp dụng SQL migration đã review trước khi chạy bản mới. Khi deploy, phát hành frontend và API cùng một package.

## Ngăn tái diễn

- Màn hình lỗi 404 trong Development nhắc API chưa đồng bộ và cần khởi động lại backend. Production chỉ hiển thị hướng dẫn thử lại ngắn gọn; không lộ stack trace, URL riêng hoặc nội dung lỗi của server.
- Smoke deploy kiểm tra endpoint catalog hiện tại trả HTTP 200 JSON, trường phân trang và các mảng builder/giỏ; kiểm tra lại sau API restart. Endpoint builder cũ không được dùng làm fallback tải toàn bộ dữ liệu hoặc thay thế việc kiểm tra contract mới.
- Test frontend kiểm tra thông báo Development/Production, lỗi mạng/lỗi khác và không lộ dữ liệu lỗi thô. Đây là bổ sung cho test phân trang/SQL Server hiện có, không phải kiểm thử tải.

## Trạng thái kiểm tra local

- Ba tài khoản staff (`SuperAdmin`, `Finance`, `Moderator`) đã được bổ sung vào DB hiện có và kiểm tra đăng nhập trước khi dừng API cũ; không reset mật khẩu/tài khoản khách hàng. Mật khẩu nằm trong file private bị Git bỏ qua, không nằm trong tài liệu hoặc package deploy.
- Đã tạo và `RESTORE VERIFYONLY ... WITH CHECKSUM` thành công cho bản sao lưu SQL trước khi đồng bộ. Bản build Debug mới thành công; frontend có 89 test đạt, lint không có lỗi và build production thành công.
- Lần chạy lại tiếp theo phát hiện dịch vụ SQL Server `MSSQLSERVER` đang `Stopped`. Terminal không có quyền bật dịch vụ (lệnh `Start-Service MSSQLSERVER` bị từ chối). Sau khi quan sát dịch vụ chuyển về `Running`, API mới đã khởi động trên cổng 5008, áp dụng thành công migration cộng thêm `NotificationCenter`, và giữ seed tắt.
- Đối chiếu ngay sau migration: 5 user, 1 hồ sơ cưới, 35 checklist, 14 mục ngân sách, 1 vendor. SHA-256 của snapshot các trường định danh/bảo mật user không đổi so với trước restart (không bao gồm thời điểm login/update). Không tạo DB mới, không reset hoặc thay dữ liệu khách hàng.
- Sau đó dịch vụ SQL lại chuyển về `Stopped`. Request catalog đã vào handler mới (không còn route 404), nhưng trả 500 do không kết nối được SQL Server; gate deploy cũng dừng ngay ở kiểm tra kết nối SQL. Chưa xác nhận catalog HTTP 200 qua proxy hay đăng nhập lại sau restart. Không xem build/test frontend hoặc package offline là bằng chứng đã hoàn tất kiểm tra runtime.

Nếu dịch vụ đang tắt, người vận hành mở PowerShell **Run as administrator** rồi chạy `Start-Service MSSQLSERVER`. Sau khi SQL chạy, dùng lệnh backend ở trên và kiểm tra catalog, schema, tài khoản cùng các số đếm dữ liệu trước/sau. Không thay sang SQLite hay seed để né lỗi kết nối.

## Kiểm tra lại sau khi máy khởi động lại (18/10/2026)

- Windows System/Service Control Manager ghi nhận event 7034 lúc 15:26:08 (giờ Việt Nam): `SQL Server (MSSQLSERVER)` terminated unexpectedly. Máy khởi động lại lúc 15:28; SQL khởi động lại lúc 15:30:45. Đây là lỗi dịch vụ Database Engine, khác với lỗi lệch phiên bản API ở trên.
- SQL log trước đó có lỗi 701 thiếu bộ nhớ lúc 02:26 và 08:30, cùng lỗi 17300 lúc 08:30. Không tìm thấy log chứng minh lỗi bộ nhớ gây đúng lần dừng 15:26; không kết luận nguyên nhân gốc hoặc gán cho việc hết token.
- Máy khoảng 16 GB RAM. `max server memory (MB)` vẫn là mặc định 2147483647; SQL hiện không báo thiếu bộ nhớ. Instance chứa cả DB khác, nên chưa tự đổi cấu hình RAM, recovery, quyền hoặc cài lại SQL. Không chạy thêm tải lớn để cố tái hiện crash trên instance đang chứa dữ liệu thật.
- Kết nối Windows Authentication tới `localhost`, DB `WeddingMatch_Dev`, hiện thành công; DB `ONLINE`, đủ 9 migration. Snapshot định danh/bảo mật user vẫn khớp trước restart; số đếm vẫn là 5 user, 1 hồ sơ, 35 checklist, 14 ngân sách, 1 vendor.
- API đã chạy bằng code mới. `GET /api/combos/catalog?page=1&pageSize=6&guests=200` trả **200 JSON** ở cả cổng 5008 và qua proxy 5173. Cả ba tài khoản staff đăng nhập đúng role và đọc notifications được 200; không ghi mật khẩu/token vào tài liệu.
- Kiểm tra trình duyệt thật: trang combo tải được, hiện trạng thái chưa có combo được duyệt (DB hiện có 0 listing, 0 combo template), không phải lỗi kết nối. Không nạp dữ liệu mẫu để thay thế danh mục thật.

Kết nối đã phục hồi tại thời điểm kiểm tra, nhưng chưa thể tuyên bố đã xử lý dứt điểm nguyên nhân SQL dừng đột ngột. Nếu tái diễn, lưu mốc thời gian và kiểm tra Windows event + SQL error log; chỉ tinh chỉnh bộ nhớ hoặc sửa/cập nhật cài đặt SQL sau khi xác định phạm vi ảnh hưởng.

Tài liệu Microsoft: [cấu hình bộ nhớ SQL Server](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/server-memory-server-configuration-options?view=sql-server-ver17), [chẩn đoán thiếu bộ nhớ](https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/performance/troubleshoot-memory-issues).
