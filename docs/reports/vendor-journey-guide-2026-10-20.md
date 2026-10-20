# Hướng dẫn sử dụng và hành trình nhà cung cấp — 20/10/2026

## Phạm vi theo yêu cầu mới nhất

Thêm hướng dẫn chi tiết ngay trong web và hành trình cho NCC tương tự CustomerJourney, trước khi tiếp tục các module nâng cấp còn lại. Không bắt đầu lại Master Prompt, không thay công thức tài chính, không dựng wizard bắt buộc cho NCC.

Đã kiểm tra Git status/diff trên `weddy-journey`; bảo toàn các thay đổi chưa commit từ những phiên trước. Chỉ bổ sung phần hướng dẫn, API tổng quan chỉ đọc và sửa một câu hướng dẫn xác minh không có căn cứ bảo đảm.

## Đã triển khai

- `/vendor/dashboard` mặc định mở **Tổng quan**, có tình trạng shop thật, gợi ý việc cần xử lý và bản đồ 10 bước.
- `/vendor/dashboard?tab=guide&guideStep=verification` mở **Hướng dẫn từng bước**; mỗi bước có thao tác cụ thể, kết quả cần thấy, người xử lý tiếp, lưu ý và liên kết tới màn hình thật.
- 10 bước: hồ sơ/xác minh → chính sách → bài dịch vụ → combo riêng (tùy chọn) → lịch hẹn (tùy chọn) → yêu cầu tư vấn → báo giá → hợp đồng với cặp đôi → thay đổi/hủy → đối soát.
- Mỗi tab nghiệp vụ có hướng dẫn theo ngữ cảnh, nút xem cách thực hiện và mở mục tiếp theo. Điều hướng không tự lưu, gửi, duyệt hoặc xác nhận dữ liệu.
- 7 FAQ giải thích bài dịch vụ/món/combo, điều kiện hiển thị, báo giá khác hợp đồng, giới hạn tài chính và cách hiểu số liệu.
- Trạng thái lấy từ DB; không có phần trăm hoàn tất giả hoặc đánh dấu hoàn thành chỉ vì đã mở trang. Khi chưa tải được dữ liệu, không biến lỗi thành số 0; hướng dẫn và liên kết vẫn dùng được.
- Ưu tiên đề nghị thay đổi/hủy và khách đang chờ phản hồi. Không khóa các công cụ khác khi chưa hoàn thiện hồ sơ/chưa có dấu xác nhận chính sách nhóm mới của tài khoản cũ.
- Giữ deep link/tab cũ, các tham số focus của thông báo và bookmark bộ lọc dịch vụ cũ không có `tab`. Bước hướng dẫn dùng URL nên Back/reload giữ đúng mục.
- CSS navy/cream, vàng bổ trợ; desktop có sidebar hướng dẫn, mobile chuyển danh sách bước thành thanh ngang cuộn riêng.
- Bỏ câu cũ “duyệt trong 24 giờ”/cam kết ưu tiên không được bảo đảm trong phần xác minh; không đổi quy trình xét duyệt.

## Backend/database/phân quyền

`GET /api/vendor-journey/me` dùng MediatR và một truy vấn tổng hợp SQL, chỉ đếm dữ liệu của shop đang đăng nhập. Kiểm tra role claim **và** User/Vendor đang hoạt động, role thực tế trong DB; không nhận vendorId để chọn shop khác.

Không trả chứng từ, thông tin ngân hàng, dữ liệu riêng của khách hoặc tỷ lệ hoa hồng. Đếm bài/bản sửa chờ duyệt theo phiên bản mới nhất; không cộng số dòng revision thành số bài. `publicListingCount` là bài `Active && IsActive`, được ghi rõ trên UI là **Bài đã duyệt · đang bật**, không khẳng định toàn bộ điều kiện công khai đã đạt. Combo/service item được duyệt cũng không đồng nghĩa đang công khai.

Không thêm migration, bảng, seed hay thay cấu trúc SQL Server. Không thay snapshot hoa hồng, số tiền hợp đồng, nghĩa vụ thanh toán hoặc cơ chế đối soát. Hướng dẫn nêu rõ các luồng thương lượng/ngoại lệ hoa hồng nâng cấp chưa hoàn thiện; không dẫn tới nút chức năng chưa tồn tại. Nhập mô tả phí A–Z không phải Admin duyệt cơ sở tài chính.

## File thay đổi của riêng đợt này

Mới:

- `client/src/features/vendor-portal/VendorJourney.tsx`
- `client/src/features/vendor-portal/vendor-journey.css`
- `client/src/features/vendor-portal/vendorJourneyModel.ts`
- `client/src/features/vendor-portal/hooks/useVendorJourney.ts`
- `client/tests/vendor-journey.test.mjs`
- `src/Wedding.Application/Features/VendorJourney/GetVendorJourneyQuery.cs`
- `src/Wedding.WebApi/Controllers/VendorJourneyController.cs`
- `tests/Wedding.Tests/VendorJourneyTests.cs`
- Báo cáo này.

Cập nhật có bảo toàn thay đổi trước đó:

- `client/src/features/vendor-portal/VendorDashboard.tsx`
- `client/src/features/journey/vendor/VendorVerificationPanel.tsx`
- `docs/master-upgrade-checklist.md`

## Kiểm thử và nghiệm thu

- Backend mới: **37/37**, gồm SQLite và SQL Server thật trên DB kiểm thử dùng một lần; phân quyền các actor, role DB không khớp claim, tài khoản/shop bị tắt, dữ liệu shop khác, phiên bản mới nhất, chỉ một SELECT/không tracking/không ghi.
- Backend đầy đủ: **688/688, 0 fail, 0 skip**, bật `WEDDING_TEST_SQLSERVER=1`, output riêng `artifacts/vendor-journey-tests` để không ghi đè DLL của API đang chạy. Lần build test có 4 cảnh báo xUnit2031 cũ.
- Frontend: **155/155**, gồm 9 test hành trình mới (URL safelist, bookmark cũ, trạng thái thiếu dữ liệu, thứ tự gợi ý, legacy và không suy đoán đã hoàn tất thanh toán).
- `npm.cmd run build` đạt. Lint 0 lỗi; 10 cảnh báo cũ ở các module khác. Vite còn cảnh báo LoginForm vừa import tĩnh vừa import động.
- `dotnet build src/Wedding.WebApi/Wedding.WebApi.csproj --no-restore --nologo` đạt, 0 warning/0 error.
- HTTP thật trên API QA: NCC đăng ký/đăng nhập và nhận tổng quan đúng shop mới; chưa đăng nhập **401**, Customer **403**.
- Trình duyệt QA: đăng nhập NCC vào Tổng quan, mở hướng dẫn báo giá, CTA tới tab báo giá thật, Back/reload giữ `guideStep=quotes`, mở FAQ và hướng dẫn hợp đồng. Không gửi báo giá, ký hợp đồng hoặc chuyển tiền.
- Desktop 1280px: 10 thẻ bước, số liệu đúng; `scrollWidth == clientWidth`. Mobile 390px: document `clientWidth=375`, `scrollWidth=375`; thanh bước cuộn bên trong (338px/1609px), không kéo rộng toàn trang.
- Ảnh nghiệm thu: `vendor-journey-overview.png` và `vendor-journey-mobile.png` trong thư mục visualization của chat.

## Runtime và bảo toàn dữ liệu

Đã cập nhật API local `http://localhost:5008`, PID 9900 tại thời điểm nghiệm thu; Vite `http://localhost:5173` PID 5612 được giữ nguyên. API readiness trả `ready`, Swagger có endpoint mới, anonymous401.

Trước/sau restart: so SHA-256 JSON toàn bộ cột theo thứ tự khóa chính và số dòng của **55 bảng nghiệp vụ + `__EFMigrationsHistory`**, tất cả giống nhau. Main DB vẫn `WeddingMatch_Dev`, 12 migration. Startup đặt `ApplyMigrations=false`, `SeedDemoData=false`.

API QA5014, Vite QA5179 và DB `WeddingPlanTest_VendorGuideUi_20261020_c721a4` chỉ dùng cho đợt kiểm thử này đã dọn; hai tài khoản hư cấu không được giữ lại. Có thể tái tạo dữ liệu test bằng test/fixture; không đụng tài khoản hoặc DB thật. Đã reset viewport và đóng tab QA.

## Phần còn lại / không tuyên bố hoàn thành

Đợt này hoàn thành **hướng dẫn và hành trình NCC**, không hoàn tất Phase4B/5/6/7/8/9 hoặc toàn bộ Master Prompt. Engine tài chính mới, thương lượng, hợp đồng đối tác/phụ lục và ngoại lệ từng hợp đồng vẫn theo checklist cũ. Không push/deploy; chưa tạo lại gói readiness/production cho source mới và không dùng artifact cũ để chứng nhận phiên bản này.
