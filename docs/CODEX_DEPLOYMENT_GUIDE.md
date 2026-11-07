# Wedding Match — Hướng dẫn Deploy cho Codex / Coding Agents

Tài liệu này hướng dẫn chi tiết quy trình deploy ứng dụng Wedding Match lên môi trường hosting production (`https://weddingmatch.website/`) theo đúng các quy tắc bất biến trong [`AGENTS.md`](file:///d:/Gitgithubhub/Wedding/AGENTS.md) và [`docs/AGENT_HANDOFF.md`](file:///d:/Gitgithubhub/Wedding/docs/AGENT_HANDOFF.md).

---

## 1. Kiến trúc & Môi trường Hosting Production

- **Tên miền website**: `https://weddingmatch.website/`
- **Hosting Provider**: SmarterASP (IIS, Windows Server, .NET 10 runtime).
- **Cấu trúc thư mục trên Host**:
  - `/site1`: Thư mục Web Root chạy ứng dụng (.NET 10 `Wedding.WebApi.dll`, `web.config`, và thư mục con `wwwroot/` chứa React 19 SPA bundle).
  - `/private`: Thư mục riêng tư nằm ngoài Web Root (chứa `appsettings.Production.json`, cấu hình SMTP, JWT secrets, upload file riêng tư, bản backup, và staging `/private/deploy/`).
- **Cơ sở dữ liệu Production**:
  - SQL Server `db_ad0009_weddingmatch` tại host `sql1004.site4now.net`.
  - Mức migration hiện tại: **Migration 22** (`20261101152242_CommissionNegotiationFoundation`).
  - Cấu hình production: `ApplyMigrations=false`, `SeedDemoData=false` (Startup không bao giờ tự ý chạy migration hay seed dữ liệu mẫu).

---

## 2. Quy tắc an toàn tối thượng khi Deploy (Bắt buộc theo AGENTS.md)

1. **Tuyệt đối không tự ý chạy Migration hay lệnh SQL phá hủy trên DB production**:
   - Không chạy `Down` migration, không drop/reset/reseed database.
   - Nếu có migration mới: phải là additive (chỉ thêm bảng/cột mới, không phá vỡ schema cũ) và phải được người dùng phê duyệt trước.
2. **Bảo vệ thư mục `/private`**:
   - Khi giải nén bản cập nhật vào `/site1`, tuyệt đối không ghi đè hoặc làm mất các file trong `/private`.
3. **Luôn Backup trước khi cập nhật**:
   - Luôn nén backup `/site1` thành file zip có gắn timestamp (ví dụ: `site1-before-update-[YYYYMMDD-HHmm].zip`) trước khi ghi đè bất kỳ file nào.
4. **Không commit/push Git trừ khi có chỉ thị rõ ràng từ người dùng**.

---

## 3. Quy trình Deploy chi tiết

### Phương án A: Deploy Cập Nhật Giao Diện (Frontend UI Patch)
> **Khi nào áp dụng**: Khi chỉ thay đổi giao diện React, CSS, trình soạn thảo Blog Google Docs, trang đọc báo, cánh hoa, nhạc... mà không thay đổi code C# hay DB schema.

1. **Build gói frontend trên máy local**:
   ```powershell
   # Chạy test kiểm tra:
   npm.cmd test --prefix client -- --run
   # Build production bundle:
   npm.cmd run build --prefix client
   ```
2. **Lấy file zip cập nhật giao diện**:
   - File zip đã được đóng gói sẵn tại:
     [`artifacts/ui-patch-20261103/Frontend-Romantic-Appearance-Music.zip`](file:///d:/Gitgithubhub/Wedding/artifacts/ui-patch-20261103/Frontend-Romantic-Appearance-Music.zip)
   - Hoặc nén toàn bộ nội dung thư mục `client/dist/` thành file zip (trong đó `dist/` chứa `index.html` và thư mục `assets/`).
3. **Thao tác trên SmarterASP Control Panel (File Manager)**:
   - **Bước 1**: Đăng nhập SmarterASP Control Panel $\rightarrow$ **Files** / **File Manager**.
   - **Bước 2**: Vào `/site1`, chọn thư mục `wwwroot` $\rightarrow$ bấm **Zip/Compress** lưu thành `wwwroot-backup-[timestamp].zip`.
   - **Bước 3**: Upload file `Frontend-Romantic-Appearance-Music.zip` vào `/site1`.
   - **Bước 4**: Bấm chuột phải vào file zip $\rightarrow$ chọn **Unzip** (Extract) đè vào `/site1` (file zip sẽ giải nén cập nhật đè thư mục `wwwroot`).
   - **Bước 5**: Kiểm tra website `https://weddingmatch.website/` trên trình duyệt.

---

### Phương án B: Deploy Toàn Diện Cả Hệ Thống (Full Backend + Frontend Release)
> **Khi nào áp dụng**: Khi có thay đổi code backend .NET (API Controllers, Application Features, DTOs, Services) hoặc đóng gói phiên bản lớn.

1. **Kiểm tra Gate kiểm thử trên máy local trước khi đóng gói**:
   ```powershell
   # 1. Frontend tests (543 tests pass 100%):
   npm.cmd test --prefix client -- --run

   # 2. Frontend production build:
   npm.cmd run build --prefix client

   # 3. Backend targeted tests:
   dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --filter "FullyQualifiedName!~Sql"
   ```
2. **Chạy script đóng gói tự động**:
   ```powershell
   # Chạy từ repo root:
   pwsh -File tools/Publish-StudySite.ps1
   ```
   - Script này sẽ:
     - Chạy `dotnet publish` chế độ Release cho `Wedding.WebApi`.
     - Build `client/` và copy bundle vào `site/wwwroot/`.
     - Tạo gói `WeddingMatch-site.zip` và file script migration `sql/migrate.sql` trong thư mục `artifacts/study-deploy/wedding-match-[timestamp]/`.
3. **Thao tác Deploy trên SmarterASP Hosting**:
   - **Bước 1 (Backup)**: Vào File Manager, nén toàn bộ thư mục `/site1` thành `/site1-backup-[timestamp].zip`.
   - **Bước 2 (Chế độ bảo trì)**: Upload file `app_offline.htm` vào `/site1` để IIS tạm ngưng nhận request và giải phóng khóa file DLL.
   - **Bước 3 (Upload)**: Upload file `WeddingMatch-site.zip` lên thư mục `/private/deploy/`.
   - **Bước 4 (Giải nén)**: Giải nén nội dung file zip đè vào `/site1`.
   - **Bước 5 (Mở lại web)**: Đổi tên hoặc xóa file `app_offline.htm` trong `/site1`.

---

## 4. Các bước Smoke Test nghiệm thu sau Deploy (Verification Checklist)

Sau khi deploy, Codex phải kiểm tra các tiêu chí sau và báo cáo kết quả:
1. **Liveness Check**:
   - Truy cập: `https://weddingmatch.website/health` $\rightarrow$ Trả về `200 OK` ("Healthy").
2. **Database Readiness Check**:
   - Truy cập: `https://weddingmatch.website/health/ready` $\rightarrow$ Trả về `200 OK` (Xác nhận kết nối SQL Server thành công).
3. **Public APIs**:
   - Truy cập: `https://weddingmatch.website/api/articles` $\rightarrow$ Trả về danh sách bài viết cẩm nang cưới công khai.
   - Truy cập: `https://weddingmatch.website/api/listings` $\rightarrow$ Trả về danh sách dịch vụ đang hoạt động.
4. **Giao diện người dùng (Browser Smoke Test)**:
   - Trang chủ (`/`): Tiêu đề, slogan, hiệu ứng cánh hoa bay, menu phát nhạc trên navbar.
   - Trang cẩm nang / Blog (`/cam-nang` hoặc `/wiki`): Danh sách bài viết dạng tạp chí, chi tiết bài viết phong cách báo chí cao cấp.
   - Khám phá dịch vụ (`/browse`): Bộ lọc danh mục, bộ lọc gói combo NCC.
   - Quản trị CMS (`/admin/dashboard?tab=cms`): Đăng nhập SuperAdmin/Moderator, kiểm tra trình soạn thảo Google Docs và nút xuất bản bài viết.

