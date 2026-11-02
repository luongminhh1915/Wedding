# Cải thiện trải nghiệm khách hàng

## Đối chiếu trước khi sửa

Đọc Master PRD, business rules, UC-3.1 (NCC), UC-7.4/7.5 (ngân sách/checklist), UC-4.3/4.4/4.6 (khách xác nhận hợp đồng), tài liệu đăng nhập và hành trình hiện tại. Các thay đổi UI không thay thế các quy tắc một NCC/combo, xác thực hợp đồng hai chiều, phân quyền hoặc vòng đời thay đổi/hủy.

Những khoảng trống thực tế:

- `/register` đã tồn tại nhưng popup đăng nhập không đóng khi chuyển sang đăng ký.
- Thông báo đã lưu DB nhưng chỉ lấy 50 dòng, chưa phân trang/đọc từng mục; badge chỉ đếm 50 dòng đó.
- Bảo mật/liên hệ nằm trong hộp thoại, chưa có trang tài khoản theo nhóm chức năng.
- Navbar chưa sticky; danh mục combo cần khoảng cách với navbar và giải pháp gọn trên điện thoại.
- Combo tải toàn bộ catalog; bộ lọc hồ sơ khách hàng trước đây chỉ xét một trang dữ liệu đã tải.
- Dashboard trộn công cụ chính với tiện ích và lặp nhiều thống kê, chưa giải thích bước tiếp theo.

## Điều hướng và nghiệp vụ

Lộ trình chính: **Tổng quan → Công việc → Ngân sách → Khám phá dịch vụ/combo → Lựa chọn & yêu cầu → Tư vấn, báo giá và hợp đồng**.

Chỉ onboarding 10 câu hỏi là bắt buộc để mở các chức năng kế hoạch. Checklist, ngân sách, khám phá, thiệp/RSVP, khách/xếp bàn và lịch hẹn có thể thực hiện song song. Không tạo thêm wizard khóa các bước. Gợi ý trên tổng quan lấy số công việc/yêu cầu/hợp đồng thật, không tự khẳng định một hợp đồng đồng nghĩa đám cưới đã hoàn thành.

Các tiện ích song song vẫn truy cập trực tiếp. Thành viên/chia sẻ được thu gọn trên tổng quan; lời mời đang chờ tự mở phần này. Các URL cũ vẫn được hỗ trợ, nhưng đổi tab dẫn về `/customer/dashboard` để tránh kẹt ở tab mặc định của alias. Dashboard NCC/admin đọc tab hợp lệ từ URL và vẫn giới hạn tab theo quyền.

`/account` và `/notifications` dành cho mọi actor đã xác thực, kể cả khách chưa hoàn thành thiết lập. Đây là các khu vực quản lý tài khoản, không phải cách bỏ qua guard của kế hoạch.

## Tài khoản

Trang `/account` chia bốn mục: thông tin cá nhân, chỉnh sửa, mật khẩu/bảo mật và cài đặt. Chỉ một phần được mở tại một thời điểm; trên mobile menu chuyển thành lưới nhỏ.

- `PATCH /api/auth/profile` chỉ nhận `fullName` cho chính tài khoản đang hoạt động. Kiểm tra tối đa 150 ký tự và ký tự điều khiển, giao dịch + audit không chứa dữ liệu cá nhân. Không nhận ID, role, email, avatar, số điện thoại hoặc câu trả lời thiết lập để sửa thay tài khoản khác.
- Khách sửa số liên hệ qua API `/api/auth/contact` hiện có. Không tự xác minh OTP; thay số thu hồi token cũ, cấp token mới cho phiên hiện tại. Phản hồi thuộc phiên cũ không ghi đè hoặc chuyển trang phiên mới.
- Đổi mật khẩu tái sử dụng `/api/auth/change-password`: xác minh mật khẩu hiện tại, tăng phiên bản bảo mật, đăng xuất và yêu cầu đăng nhập lại. Không đổi quy tắc mật khẩu hoặc giảm bảo mật.
- Email và quyền chỉ xem; không tạo nút xóa tài khoản, bật SMS/email hoặc tính năng giả khi backend chưa hỗ trợ. Tên thương hiệu/hotline NCC vẫn thuộc hồ sơ doanh nghiệp, không bị ghi đè bằng tên tài khoản.
- Không mở lại biểu mẫu hồ sơ cưới hoặc tạo lại kế hoạch khi sửa thông tin tài khoản.

## Module chính

| Phần | Module |
| --- | --- |
| Đăng ký, 34 địa phương | `features/auth/components`, `data/vietnamProvinces.ts`, `VietnamProvinces.cs`; xem [chi tiết](registration-page.md) |
| Tài khoản | `features/account`, `UpdateAccountProfileCommand.cs` |
| Hành trình, menu và gợi ý | `CustomerDashboard.tsx`, `CustomerJourney.tsx`, `journeyPolicy.ts`, `WeddingMatchLayout.tsx` |
| Thông báo | `features/notifications`, `NotificationBell.tsx`, `NotificationsController.cs`, `NotificationsFeature.cs` |
| Phân trang | `components/Pagination.tsx`, catalog/đơn combo và hồ sơ tư vấn/báo giá/hợp đồng |

Navbar sticky dùng `--wm-header-offset` theo breakpoint. Sidebar combo tính offset từ biến này, giới hạn theo vùng chứa; mobile dùng danh mục ngang, không chiếm cột bên cạnh. Phân trang có trạng thái chờ/lỗi/trống, trước/sau/trang hiện tại, giữ bộ lọc và cuộn về đầu vùng danh sách. Giỏ lưu riêng tài khoản không bị cắt theo trang catalog.

## API và dữ liệu

- Thông báo: `GET /api/notifications?page=&pageSize=&unreadOnly=`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all`. Trả count chưa đọc trên toàn bộ dữ liệu của người dùng, không chỉ trang hiện tại. API chỉ trả thông báo của actor; liên kết ngữ cảnh được dựng từ loại tài nguyên cho phép và kiểm tra quyền hiện tại, không lưu URL điều hướng tùy ý. Thông báo cũ thiếu ngữ cảnh vẫn đọc được, không đoán liên kết từ nội dung.
- Catalog: `GET /api/combos/catalog`, có phân trang/lọc/sắp xếp và hydrate riêng dịch vụ đang có trong giỏ. Endpoint builder/chi tiết cũ được giữ tương thích.
- Combo đã lưu: `GET /api/combos/orders/my/page`. Tư vấn/báo giá/hợp đồng: `GET /api/customer-orders/page`, phân trang theo nhóm NCC/combo để không tách một gói thành nhiều thẻ thiếu dữ liệu.

Migration `20261018022533_NotificationCenter` **chỉ thêm** `Notifications.ContextKind`, `ContextId` nullable và index người dùng/thời gian/ID. Không xóa/seed hoặc sửa nội dung thông báo cũ. Account và danh mục địa phương không thêm bảng/cột.

Trước deploy cần backup SQL Server và thư mục ảnh, áp dụng migration đã review bằng quy trình deploy hiện có; production không tự chạy migration/seed. Frontend và API mới phải triển khai cùng nhau vì DTO danh sách thông báo đã chuyển sang dạng phân trang. Không nhập package hoặc fixture thử nghiệm vào DB người dùng.

## Kiểm chứng

Chạy `npm.cmd test`, `npm.cmd run lint`, `npm.cmd run build` trong `client`; backend test với `--artifacts-path` riêng để không ghi đè DLL của API đang chạy. SQL tests chỉ dùng database thử nghiệm tên riêng. `tools/Test-ReadyToDeploy.ps1` chạy toàn bộ gate, đóng gói, migration lặp lại và smoke HTTPS/SQL/auth, không deploy lên host.

## Nghiệm thu thực tế 18/10/2026

- **342/342 backend tests đạt, 0 skip**, bật SQL Server thật; **86/86 frontend tests đạt**. Build frontend/backend đạt. Lint không có lỗi, còn 11 cảnh báo: 9 ở các phần hiện có và 2 cảnh báo Fast Refresh do component phân trang xuất chung hook/helper; không phải lỗi build/runtime.
- Gói cuối: `artifacts/readiness/20261018-023924-78b639d4/deploy/wedding-match-20261018-023953-ee889746`. Kết quả máy đọc được: `artifacts/readiness/20261018-023924-78b639d4/ready.json`, trạng thái `LOCAL_GATES_PASSED`.
- Sáu ca package bị sửa sai được chặn. Migration chạy lặp trên SQL Server QA, giữ user/hash và ảnh riêng sau API restart; Production SPA, API 404 isolation, HTTPS redirect và readiness đạt. Smoke HTTPS kiểm tra đăng ký/phân quyền, mật khẩu sai không làm mất phiên, đổi mật khẩu thu hồi hai JWT, đổi liên hệ cấp phiên thay thế và WebSocket bị thu hồi không nhận tin riêng tiếp theo.
- Kiểm tra trình duyệt bằng tài khoản giả, API cổng 5013 và DB tên riêng: liên kết đăng ký đóng popup; dropdown tìm `da nang` chọn Đà Nẵng bằng Enter, form trống báo lỗi từng trường. Không tạo tài khoản hoặc đổi mật khẩu qua UI; các thao tác này được kiểm thử bằng API trên fixture riêng.
- Hộp thông báo có 16 bản ghi thực (12 fixture và 4 nhắc checklist do tác vụ nền tạo), không lẫn 2 thông báo của người khác; trang 2 đúng, đọc một mục làm badge 13 → 12, đọc tất cả về 0 và bộ lọc chưa đọc hiển thị trạng thái trống. Liên kết checklist mở đúng ID, cuộn và focus dòng công việc.
- Sửa tên khách QA qua trang Tài khoản, lưu và tải lại vẫn còn tên mới. Phần mật khẩu mở riêng, không hiện lẫn form khác. Khách QA mới đăng nhập tới bước 1/10; mở trực tiếp `/combo` vẫn trở về setup. Giỏ của khách khác không bị kế thừa.
- Catalog có 12 combo một NCC và 12 dịch vụ thật trong DB QA: kiểm tra trang 2, thứ tự giá giảm dần, tìm kiếm reset trang hợp lý và vẫn giữ dịch vụ đã chọn ngoài kết quả/trang hiện tại. Tư vấn/báo giá/hợp đồng chuyển đúng tab, trạng thái trống lấy từ API thật, không tạo hoặc xác nhận giao dịch tài chính.
- Kiểm tra desktop 1280px, tablet 768px, điện thoại 390px: không tràn ngang; desktop 6 và mobile 4 dịch vụ/trang; navbar/sidebar không chồng nhau. Đã sửa thêm việc danh mục đang chọn bị khuất khi đổi desktop sang mobile; bản cuối kiểm tra mục đang chọn nằm trọn trong thanh ngang sau resize. Tablet: đáy navbar 71px, đầu thanh danh mục 83px; mobile không vượt chiều ngang trang.
- Tất cả phiên/tab QA đã đăng xuất và đóng; DB thử nghiệm cùng API riêng được dọn dẹp. Chỉ fixture hư cấu bị xóa, không xóa DB/ảnh hoặc dừng API chính của người dùng. File ảnh nghiệm thu giữ ở thư mục visualizations của chat.

## Handoff và giới hạn

Khởi động lại backend/frontend đang chạy để nhận code mới; không chạy reset hoặc seed demo. Nếu đang chạy API trong terminal, dừng bằng Ctrl+C tại terminal đó trước khi chạy lại. Development áp dụng migration cộng thêm khi `Database:ApplyMigrations=true`; production luôn áp dụng SQL đã review trước khi khởi động.

Terminal backend, tại thư mục repo:

```powershell
dotnet run --project .\src\Wedding.WebApi --launch-profile http
```

Terminal frontend:

```powershell
cd D:\Gitgithubhub\Wedding\client
npm.cmd run dev
```

Không deploy công khai hoặc thay DB thật trong đợt này. Provider Google/SMTP/ZNS chưa cấu hình không được coi là đã kiểm thử tích hợp thật; thay đổi UX không tự cấu hình các dịch vụ đó. Chưa chạy kiểm thử tải đồng thời hay xác nhận giới hạn tài nguyên của host thực. Trang cài đặt không cung cấp đổi email, xóa tài khoản hoặc công tắc SMS/email giả; các chức năng đó cần luồng xác minh/chính sách riêng nếu phát triển tiếp.
