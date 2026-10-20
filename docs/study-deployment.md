# Deploy Wedding Match cho dự án môn học

Phương án được chuẩn bị: React + ASP.NET Core cùng một website IIS, SQL Server tách riêng trên MonsterASP.NET Free. Không cần Azure, email sinh viên, thẻ ngân hàng hay mua tên miền; dự kiến chi phí hosting **0 đồng** nếu chỉ dùng Free. Không có thao tác đăng ký tài khoản, thanh toán hoặc publish từ các script trong repo.

Free có 5 GB file, 256 MB RAM, SQL Server tối đa 1 GB, địa chỉ `*.runasp.net`. HTTPS cần yêu cầu hỗ trợ sau khi upload; không có backup tự động và không có AlwaysRunning. Đây là lựa chọn tiết kiệm cho khảo sát nhỏ, **không phải cam kết phục vụ 100 người đồng thời**. Thông số kiểm tra ngày 17/10/2026 tại [bảng Free/Premium chính thức](https://help.monsterasp.net/getting-started/first-steps/free-vs-premium).

Ứng dụng dùng .NET 10 LTS thay vì giữ .NET 8 sắp hết hỗ trợ trong thời gian chạy đề tài. Host hỗ trợ .NET hiện hành, IIS, SQL Server, SignalR; không chạy Docker trên gói này. Xem [công nghệ được hỗ trợ](https://help.monsterasp.net/getting-started/first-steps/supported-technologies).

## 1. Tạo gói deploy trên máy

Cần .NET SDK 10, Node.js đáp ứng `client/package-lock.json`, PowerShell 7. Không cần bật API hoặc kết nối database để tạo gói.

Để kiểm tra toàn bộ trước mỗi lần deploy trên máy có SQL Server local và `sqlcmd`, chạy một lệnh:

```powershell
pwsh -File .\tools\Test-ReadyToDeploy.ps1
```

Lệnh này chạy test/lint frontend, toàn bộ test backend có SQL Server (không chấp nhận skip), tạo gói mới rồi kiểm tra chính gói đó ở chế độ Production trên DB hư cấu và HTTPS loopback riêng, gồm đổi mật khẩu/thu hồi phiên và WebSocket chat thật. Kết quả thành công nằm trong `artifacts/readiness/<run>/ready.json`; chỉ artifact có hash ghi trong kết quả được nghiệm thu. Không upload host, không dùng DB người dùng, không restart API phát triển. SQL Server hoặc test không sẵn sàng thì dừng, không báo “ready” giả. Thêm `-InstallClientDependencies` nếu cần cài lại dependencies.

Kết quả local không thay thế xác minh HTTPS, quyền SQL, lưu trữ bền vững, backup/restore và giới hạn tài nguyên trên host thật. Sửa code xong cần chạy lại lệnh; không coi một gói cũ là bằng chứng cho code mới.

```powershell
pwsh -File .\tools\Publish-StudySite.ps1 -InstallClientDependencies
```

Khi đã có dependencies có thể bỏ `-InstallClientDependencies`. Mỗi lần tạo thư mục mới trong `artifacts/study-deploy/`, không ghi đè bản trước:

```text
wedding-match-<timestamp>-<id>/
  WeddingMatch-site.zip   <- chỉ upload gói website này
  sql/migrate.sql        <- schema SQL Server, thực hiện riêng
  release.json           <- SHA-256 và thông tin build
  site/                  <- bản xem trước nội dung ZIP
  build/                 <- trung gian local, KHÔNG upload hoặc chia sẻ
```

Script dừng khi frontend, API hoặc EF tool lỗi. `VITE_API_URL` được đặt rỗng riêng cho tiến trình build, ghi đè URL local mà không sửa `client/.env`; frontend gọi `/api`, `/uploads`, `/hubs` cùng domain. `VITE_STUDY_MODE=true` bật thông báo dự án học tập, không giao dịch tiền thật.

ZIP dùng danh sách cho phép: binary runtime + frontend vừa build + cấu hình rỗng an toàn. Không kèm SQLite, dữ liệu người dùng, ảnh upload, seed JSON, mật khẩu local, cấu hình Production hay publish credentials. Chỉ chia sẻ ZIP; không chia sẻ thư mục `build/` vì output trung gian của Web SDK có thể chứa nội dung local. `sql/migrate.sql` được tạo offline, chưa chạy lên bất kỳ DB nào.

Kiểm tra lại artifact trước khi upload:

```powershell
pwsh -File .\tools\Verify-StudyPackage.ps1 -ReleaseRoot 'D:\duong-dan\wedding-match-...'
```

Có thể kiểm thử các chốt từ chối trên bản sao hư cấu bằng `tools/Test-StudyPackageGuardrails.ps1 -ReleaseRoot <thu-muc-release>`: sai hash, lẫn DB, lẫn cấu hình Development, frontend trỏ API localhost, thiếu tùy chọn SQL cần thiết hoặc sai encoding SQL. Công cụ không sửa release gốc.

## 2. Tài khoản host và SQL Server

Người sở hữu dự án tự tạo tài khoản Free bằng email cá nhân, xác minh email và chọn điều khoản dịch vụ. Tạo một website Free, một database **MSSQL**, lưu lại domain, tên server/database và tài khoản SQL. Không chọn MySQL/PostgreSQL và không bật nâng cấp trả phí.

Tạo backup DB cũ trước khi chuyển dữ liệu. DB mới không có tài khoản `123456` hoặc dữ liệu demo vì production không seed. Nếu dữ liệu local đang nằm trong SQLite: giữ nguyên file gốc và backup nhất quán cả WAL, chuyển dữ liệu/đối soát riêng trước khi chuyển kết nối; chỉ đổi connection string **không** tự đưa dữ liệu cũ sang SQL Server. Không restore đè DB đang thu thập khách hàng.

Công cụ [Wedding.LocalDataBackup](../tools/Wedding.LocalDataBackup/README.md) tạo snapshot SQLite chỉ đọc, kiểm tra hash/số bản ghi và có tùy chọn sao lưu ảnh; không seed, chuyển DB hoặc xóa nguồn. Với ảnh đang được tải lên, tạm dừng nhận upload rồi lấy bản cuối nhất quán trước cutover.

Connection string phải đúng `DefaultConnection`. Giữ mật khẩu trong cấu hình riêng hoặc Control Panel, không commit, đưa vào ZIP, `VITE_*` hay gửi trong chat. Host hỗ trợ [biến môi trường riêng từng website](https://help.monsterasp.net/websites/configuration/environment-variables), cú pháp lồng nhau dùng `__` như `ConnectionStrings__DefaultConnection`.

Lưu ý riêng nhà cung cấp: tài liệu nêu chứng chỉ SQL tự ký; remote connection cần `Encrypt=True;TrustServerCertificate=True`. Không sửa về `Encrypt=False` cho kết nối qua Internet. Nếu phải chấp nhận chứng chỉ tự ký, dùng tùy chọn xác nhận rõ trong công cụ cấu hình và đúng hostname Control Panel, không áp dụng đại trà cho SQL provider khác. Đây là đánh đổi: có mã hóa nhưng không xác minh CA máy chủ. Xem [hướng dẫn TLS SQL Server](https://help.monsterasp.net/databases/mssql/secure-connection).

## 3. Cấu hình riêng và ảnh upload bền vững

Tạo file cấu hình bằng PowerShell 7, nhập secrets tại máy, rồi upload riêng qua SFTP/FTPS vào `/private/wedding.production.json`. Không upload file này vào `/wwwroot` hoặc đóng gói nó vào release.

```powershell
# Chỉ dùng switch này khi hostname SQL đúng của host và đã chấp nhận lưu ý TLS phía trên.
pwsh -File .\tools\New-StudyConfiguration.ps1 -TrustHostedSqlCertificate
```

Bỏ switch để giữ xác minh chứng chỉ CA mặc định với nhà cung cấp có chứng chỉ được tin cậy. File local được tạo mới tại `.deployment/private/wedding.production.json`; công cụ không ghi đè secrets đã tồn tại.

```text
website-root/
  private/
    wedding.production.json
    uploads/invitations/   <- ảnh thiệp của người dùng
    uploads/listings/      <- ảnh dịch vụ của NCC
  wwwroot/                 <- nội dung WeddingMatch-site.zip
    Wedding.WebApi.dll
    appsettings.json      <- mặc định an toàn, không secrets
    web.config
    wwwroot/              <- React, ảnh thương hiệu, CSS, JS
```

App production đọc `../private/wedding.production.json`; biến môi trường có thể ghi đè. Các root upload là `../private/uploads/invitations` và `../private/uploads/listings`, tính từ content root ứng dụng `/wwwroot`. Kiểm tra quyền tạo/ghi file cho app và dung lượng trước khi mở đăng ký. Nếu host cấp vị trí khác, chỉnh root tuyệt đối bằng cấu hình, không sửa code.

Thư mục `/private` ở đây do chủ dự án tạo, không phải thư mục tạm. Cấu trúc hai cấp `wwwroot/wwwroot` đúng với ASP.NET Core: [hướng dẫn SFTP/FTPS](https://help.monsterasp.net/websites/deploy/ftp-sftp). File nằm ngoài document root không được IIS phục vụ trực tiếp: [vị trí file riêng tư](https://help.monsterasp.net/websites/manage/backups). URL ảnh public chỉ được API phục vụ qua endpoint đã kiểm tra định dạng; không bật directory browsing cho thư mục riêng.

## 4. Schema và upload lần đầu

1. Xác nhận đúng database đích, backup trước nếu DB có dữ liệu. Đọc `sql/migrate.sql`, nhất là mọi thay đổi xóa/đổi cột ở các phiên bản sau. Không chạy `database drop`, `EnsureDeleted` hoặc reset seed.
2. Chạy script bằng **sqlcmd**, SSMS bật **SQLCMD Mode**, hoặc Control Panel → Databases → đúng DB → Import. `migrate.sql` dùng **UTF-16 LE có BOM** để sqlcmd tự nhận Unicode, không cần truyền `-f` và không bị đổi dấu tiếng Việt theo code page Windows; đừng lưu lại thành ANSI. JSON/React vẫn dùng UTF-8. Script có `:ON ERROR EXIT`, `XACT_ABORT ON` và các session SET cần thiết cho filtered index (đặc biệt `QUOTED_IDENTIFIER ON`), dừng khi gặp lỗi. Host dùng sqlcmd cho công cụ Import; xem [hướng dẫn import SQL](https://help.monsterasp.net/databases/mssql/import-sql-file) và [tài liệu encoding sqlcmd của Microsoft](https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-utility?view=sql-server-ver17). Kiểm tra log không lỗi, `__EFMigrationsHistory` đủ migration và tên danh mục tiếng Việt đúng trước khi chạy app. Script là idempotent, không phải backup dữ liệu.
3. Upload ZIP website bằng File Manager, giải nén nội dung trực tiếp vào `/wwwroot`, không thêm thư mục bọc. Sau này cập nhật chỉ overwrite file có trong ZIP, không chọn xóa toàn bộ đích; giữ nguyên `/private` và cấu hình riêng. Với app đang chạy, dừng pool hoặc dùng `app_offline.htm` trong lúc thay binary. Xem [deploy ZIP chính thức](https://help.monsterasp.net/websites/deploy/zip-file).
4. Production phải giữ `Database:Provider=SqlServer`, `SeedDemoData=false`, `ApplyMigrations=false`, JWT secret ngẫu nhiên riêng và hostname thật. Không bật Development trên Internet. Sai/missing schema phải sửa bằng migration có kiểm soát, không bật reset để chữa lỗi.
5. Yêu cầu support kích hoạt HTTPS và kiểm tra chứng chỉ đúng hostname. **Chưa có HTTPS hợp lệ thì chưa cho khách đăng ký/đăng nhập hoặc gửi dữ liệu thật.**

Sau khi schema hoàn tất, tạo quản trị viên đầu tiên bằng công cụ local, không mở endpoint bootstrap trên Internet:

```powershell
dotnet run --project tools/Wedding.DeployAdmin -- --config .deployment/private/wedding.production.json
```

Công cụ yêu cầu gõ lại đúng tên DB và nhập mật khẩu riêng, dừng nếu đã có SuperAdmin hoặc trùng email; không reset tài khoản cũ. Đọc [hướng dẫn bootstrap admin](../tools/Wedding.DeployAdmin/README.md) trước khi chạy. Công cụ này không có trong gói website.

Không đặt `sql/migrate.sql`, file `.bak`, secrets hoặc ZIP backup trong `/wwwroot`: khách có thể tải chúng. Hết deploy, tải/xác minh backup trước khi xóa ZIP release trên host để tiết kiệm chỗ; không xóa DB hay thư mục ảnh.

## 5. Kiểm thử trước khi gửi link khảo sát

- `/`, `/combo`, refresh một route con đều hiện React; API không tồn tại phải trả 404, không trả HTML 200.
- `/health` và readiness DB thành công; lỗi DB không được lộ connection string.
- Đăng ký khách thử, trả lời onboarding, lưu combo; đăng xuất/đăng nhập vẫn còn dữ liệu.
- Đăng ký NCC thử, đăng dịch vụ/ảnh, kiểm tra quyền duyệt và các actor đúng role.
- Upload ảnh thiệp/ảnh dịch vụ, restart app, deploy lại ZIP: dữ liệu và ảnh vẫn còn. Đối soát số bản ghi trước/sau.
- Test backup/restore trên **DB khác**, xác nhận liên kết ảnh/ID đúng. Không thử restore trên DB khảo sát đang dùng.
- Đo RAM và load test trên host bằng dữ liệu giả trước khi hứa tải đồng thời; 256 MB có thể thiếu. Nếu thường xuyên recycle/503, dừng thu thập và báo giới hạn; chưa tự mua Premium/VPS.

Giữ các tác vụ nền phục vụ chức năng, nhưng Free có idle shutdown nên nhắc lịch/xử lý nền không đảm bảo đúng phút khi không ai mở web. Không dùng ping liên tục để né giới hạn. Email/Google/thanh toán thật chỉ hoạt động sau khi cấu hình nhà cung cấp tương ứng; đề tài không nên yêu cầu chuyển tiền hoặc dữ liệu thanh toán thật.

## 6. Backup và kết thúc đề tài

Mỗi ngày có dữ liệu mới **và trước mỗi deploy/migration**:

1. Tạm dừng nhận ghi dữ liệu để DB và file ảnh cùng mốc nhất quán.
2. Control Panel → Databases → DB → Backups → **Create BAK file**, tải `.bak` về máy. Free phải tự tạo/tải; không có backup hằng ngày tự động. Xem [backup SQL Server của host](https://help.monsterasp.net/databases/mssql/backup).
3. Tải `/private/uploads` và file cấu hình riêng qua SFTP/FTPS; ghi cùng timestamp, phiên bản code, hash, số bản ghi chính và số ảnh. Giữ ít nhất hai bản ngoài host, thư mục mã hóa/giới hạn quyền truy cập. Không để backup trong Git public.
4. Bật app lại, kiểm tra lưu/đọc dữ liệu. Thử restore vào DB riêng ít nhất một lần; `.bak` của SQL Server mới không restore trực tiếp xuống SQL Server cũ hơn. Dùng máy cùng/mới hơn hoặc xuất BACPAC khi cần tính di động. Sau khôi phục DB cũ, thay JWT secret để token phát hành sau thời điểm backup không sống lại do `SecurityVersion` bị lùi; redeploy bình thường không cần đổi secret.

Khi kết thúc khảo sát: ngừng đăng ký mới, xuất backup cuối và kiểm tra phục hồi, lưu báo cáo ẩn danh. Chỉ xóa dữ liệu/hủy host theo phạm vi đã được người tham gia đồng ý và yêu cầu của chủ dự án; không coi Free là kho lưu trữ vĩnh viễn. Trước thu thập cần có thông báo mục đích môn học, người quản lý/liên hệ, trường dữ liệu thực sự cần và thời hạn lưu; không thu CCCD, thẻ ngân hàng hoặc mật khẩu của dịch vụ khác.
