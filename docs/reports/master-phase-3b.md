# Phase 3B — nội dung dịch vụ có cấu trúc, A–Z và tách module Combo

Ngày: 19/10/2026. Tiếp nối Phase 3A trên `weddy-journey`; không reset, commit hoặc push các thay đổi hiện có.

## Phạm vi đã triển khai

- Tái sử dụng `Listing`/`ListingMedia` và editor đầy đủ hiện hữu, không thêm mô hình dịch vụ thứ ba. Metadata descriptive được lưu trong một cột JSON nullable; bài cũ NULL vẫn đọc/sửa bình thường.
- Các nhóm thông tin: giới thiệu/đơn vị niêm yết/điểm nổi bật, bao gồm/không bao gồm/thời lượng/quy trình/tùy chọn, phụ phí/điều kiện/đổi lịch/yêu cầu/ghi chú, portfolio và video HTTP(S).
- Thông tin A–Z: phạm vi, quy mô, giai đoạn, thuê ngoài, mô tả phí quản lý/tổng gói/chi phí phát sinh/thanh toán. Category flag do API cấp; không hardcode ID ở frontend.
- Mô tả phí chỉ là thông tin đề xuất. Không duyệt commission basis từ text, không sửa giá/snapshot/ledger/payment hoặc cancellation policy.
- Create/update kiểm tra role và trạng thái actor thực trong DB, ownership và validation ngay trong handler, transaction serializable. Metadata null/omitted giữ nguyên; object mới thay thế toàn bộ metadata. Album omitted giữ nguyên tất cả ảnh/cover, không thay bằng preview rút gọn.
- Giới hạn: shortDescription500, priceUnit80, text khác4000/mục, portfolio10 URL/1000 ký tự mỗi URL, tổng JSON60000. Chặn executable/data/blob/credentials/control/backslash/traversal; encoded ordinary space được FE/BE chấp nhận nhất quán.
- Trang detail public và private preview dùng chung renderer read-only. Không inject HTML hoặc iframe từ Vendor; portfolio/video chỉ mở theo lựa chọn của khách. Hiển thị chính sách hủy đang lưu, không cho metadata ghi đè nó.
- Tab `Combo của tôi` độc lập tại `/vendor/dashboard?tab=combos`; giữ nguyên quản lý ServiceItem/participation/combo hiện hữu, bỏ khỏi `VendorAppointments`. `Dịch vụ của tôi` vẫn giữ paging/filter/preview Phase 3A. Đây mới là tách navigation, chưa phải hoàn tất Combo lifecycle.
- Thêm shared `MoneyInput`/model, áp dụng bước đầu cho min/max giá Listing, đơn giá/cọc báo giá và ba ô ngân sách. Format vi-VN khi nhập, caret logic kể cả chuỗi format không đổi, xử lý separator deletion/paste/bounds/IME guard, emit numeric/null, không format phần trăm. Chưa tuyên bố đã chuẩn hóa mọi ô tiền toàn website hoặc kiểm tra IME thực tế trên mọi thiết bị.
- Readiness fingerprint bổ sung `client/tests`, tránh source certificate bỏ sót thay đổi frontend tests.

## Database

Migration `20261019053813_ListingServiceDetails`: `Up` chỉ thêm `Listings.ServiceDetailsJson` nullable `nvarchar(max)` maxlength60000. Không SQL backfill, không cập nhật/xóa dòng legacy, không chạy `Down` trên DB thật.

SQL regression khởi tạo DB tạm riêng từ migration Phase2, chèn dữ liệu tổng hợp legacy, nâng lên latest hai lần rồi kiểm tra: tài khoản/hash/security version, scalar Vendor, Listing description/prices/cancellation/counters/media, confirmed contract giá/cọc/commission snapshot/version/receipt không đổi. Các DB test chỉ có tên `WeddingPlanTest_*` do test sở hữu.

## Các file chính

- Domain/EF: `Listing.cs`, `ListingConfiguration.cs`, migration + designer/model snapshot.
- Application: `ListingServiceDetails.cs` mới; Create/Update Listing validator+handler; ListingDto; detail/pending/admin DTO mappings; public CategoryDto flag.
- API: `ListingsController.cs` update request mapping.
- Frontend: `ListingForm.tsx`, `listingEditModel.ts`, `ServiceDetailsEditor.tsx` mới, `ServiceDetails.tsx` mới, `serviceDetailsModel.ts` mới, `ListingDetailPage.tsx`, listing types, VendorDashboard/VendorAppointments.
- Shared money: `MoneyInput.tsx`, `moneyInputModel.ts`, VendorQuotes, WeddingBudgetPlanner.
- Tests: `ListingServiceDetailsTests.cs` (55 cases), `service-details.test.mjs`, `money-input.test.mjs`; readiness fingerprint.

## Kiểm thử thực tế và các lần sửa trong gate

- Targeted backend ban đầu: 54/54 đạt gồm SQL. Sau bổ sung encoded-space URL case, tổng mới là55.
- Frontend cuối: 121/121, 0 fail/skip/cancel; log `artifacts/master-upgrade/phase3b-frontend-final.log`.
- Gate lần1: 490/491 backend đạt, một SQL migration-lock timeout; không sinh ready artifact.
- Gate lần2: 490/492 backend đạt, hai bài SQL create-DB timeout khi máy vào Modern Standby; không bỏ qua SQL hoặc tăng timeout để coi là đạt.
- System event xác minh: 12:45:47 Idle Timeout; 12:48:00 Lid vào Modern Standby; 13:11:04 Lid thoát Standby. Chỉ đọc event phục vụ chẩn đoán, không sửa power/security/SQL instance settings.
- Gate lần3: 492/492 backend đạt; production frontend build phát hiện `actualCost` optional chưa được coalesce cho MoneyInput. Đã sửa `??0`, build lại đạt. Artifact lần này không hoàn chỉnh, không dùng để deploy.
- Gate source cuối **PASS** lúc 13:16:01 (UTC+7): **492/492 backend**, SQL enabled, 0 skipped/notExecuted/fail; **121/121 frontend**, lint0error/11warning cũ; frontend production build, backend Release,6 package rejection checks, real HTTPS/SQL/account/SignalR/privacy/restart smoke đều đạt. Debug local build0warning/0error.
- `artifacts/readiness/20261019-061351-7d27c756/ready.json`; backend TRX trong `test-results/backend.trx`; release `deploy/wedding-match-20261019-061451-ee96d1d7` của cùng run.
- Source SHA256 `0D56A44CED83E7D2FEF4B560032E1DE518FB6AE92A6F4A2FA76D5369A9B92FC2`; ZIP SHA256 `E40C2A42732D2180E5AA3AA6D1F230CBD66FB332FCD0DE16B7F10B25ED0EE497`.
- Smoke working-set161.5→200.6MB chỉ workload nhỏ, không chứng minh concurrent capacity; host256MB cần kiểm tra resource thực tế. Bốn xUnit2031 warning test Phase2 và static/dynamic LoginForm import warning cũ không bị tắt đi.

### Áp dụng local sau gate

- COPY_ONLY+CHECKSUM backup mới và RESTORE VERIFYONLY đạt: `D:\sql\dev\MSSQL16.MSSQLSERVER\MSSQL\Backup\WeddingMatch_Dev_before_listing_details_20261019_3b6c02.bak`.
- Đọc lại migration history10/column chưa tồn tại trước khi áp dụng script EF idempotent **chỉ** migration mới. Script chỉ ALTER ADD nullable và INSERT migration history; không chạy full destructive/reset script.
- Trước/sau: Users6, Vendors1, Listings2, Media2, Profiles2, Checklist70, Budget28, Contracts0, Commissions0. Hash aggregate Users/Vendor/Listings/Media không đổi; 2Listing cũ metadataNULL, migration history11. Không seed/reset/backfill tài chính.
- Chỉ dừng đúng API5008 đã xác minh executable; rebuild Debug0warning0error, khởi động lại với ApplyMigrations=false/SeedDemoData=false. Health200, Swagger200/có ServiceDetailsDto; frontend5173 của user giữ nguyên. Không sửa SQL service/memory/host/network.

### Browser QA

- API5014 + Vite5179 riêng, DB `WeddingPlanTest_VendorUi_Phase3b_20261019` riêng; chỉ dữ liệu tổng hợp, không account/listing thử trong DB thật.
- Login Vendor đúng portal; mở form đã lưu với đầy đủ metadata/gallery, Category A–Z đúng flag. Sửa summary và giá qua UI, lưu thành công; API đọc lại `13500000.50` numeric, metadata mới và metadata khác/album giữ nguyên.
- Caret thực tế: leading-zero normalization vẫn vị trí1; chèn giữa `1.234` thành `19.234` caret2; Delete cạnh dấu nhóm thành `1.934` caret3. Paste `13.500.000,50 ₫` hợp lệ; paste `1e6` bị chặn, không đổi số cũ. Chưa giả lập IME OS thật.
- Private preview hiện đủ section/includes/excludes/process/A–Z/cancellation, liên kết `%20` không biến mất; anonymous direct GUID trả404; CTA giao dịch tắt.
- Mobile390×844: dialog clientWidth375/scrollWidth375, giá có format, sections mở/đóng dùng được; không tràn ngang. Tab Combo chỉ có quản lý combo; Lịch hẹn chỉ có booking/type/day exceptions, không mất chức năng.
- Đã đóng tab/reset viewport, dừng **chỉ** API5014/Vite5179 và xóa đúng DB tổng hợp do lượt này tạo. Dữ liệu thử không cần khôi phục; dữ liệu user và Vite5173/API5008 giữ nguyên tại thời điểm QA cleanup.
- Screenshot thật: `phase3b-service-details-desktop.png`, `phase3b-service-editor-mobile.png`, `phase3b-service-sections-mobile.png` trong thư mục visualizations của chat.

## Phần tiếp theo / chưa hoàn thành

- Combo lifecycle Draft/submit/reject-note/resubmit, private preview, gallery/terms/validity, separate activity và immutable published revisions chưa được triển khai trong Phase3B. Không đánh dấu hoàn tất vì đã tách tab.
- Listing published revisions/activity riêng; Phase4 moderation/history/notification/filter/review và Phase5–7 commission/negotiation/partnership/contract adjustment còn tiếp tục theo dependencies.
- Giữ quyết định legacy rate và original contract snapshot. Sau gate Phase3B, người dùng đã xác nhận ordinary/A–Z basis và ba điều kiện activation; xem `../decisions/2026-10-19-commission-basis-and-activation.md`. Cấu trúc và quy trình tài chính tương ứng vẫn phải triển khai Phase5–7; không coi descriptive metadata là phê duyệt. Unsettled allocation/offset/refund và cách phân bổ combo khi thiếu dữ liệu vẫn cần xác nhận phù hợp, không tự đặt số liệu.
- Không public deploy, đổi host/SQL/mạng, push/force-push hoặc reset dữ liệu.
