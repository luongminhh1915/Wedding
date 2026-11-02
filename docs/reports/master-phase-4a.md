# Phase 4A — bảo vệ luồng kiểm duyệt ServiceItem

Ngày: 20/10/2026 (UTC+7), tiếp tục công việc tối 19/10 trên branch `weddy-journey`. Phạm vi là quyền truy cập, validation, chống duyệt nội dung cũ và tính nguyên tử của ServiceItem/variant/add-on/consent. Giữ các thay đổi hợp lệ chưa commit của dự án.

## Trạng thái tại thời điểm lập báo cáo

**Phase4A đã đạt gate trong phạm vi báo cáo:**651/651 backend gồm SQL,146/146 frontend, build/lint/package/HTTPS/security/restart và browser/API QA. API local chính đã cập nhật;55 bảng dữ liệu khớp chính xác trước–sau restart. Lỗi lưu variant/add-on thực tế đã sửa và kiểm chứng, không bỏ assertions. Không lấy kết quả Phase3C/3D làm bằng chứng cho source này; chưa hoàn thành toàn bộ Phase4/Master Prompt và chưa public deploy.

| Hạng mục | Trạng thái |
|---|---|
| Frontend unit | **PASS 146/146**, gồm 4 trường hợp mới cho payload kiểm duyệt ServiceItem. |
| Frontend production build | **PASS** theo lượt kiểm tra riêng của root. |
| Frontend lint | **PASS**, 0 error; 10 warning hiện hữu, không tắt analyzer để giấu. |
| Backend Release build riêng | **PASS**, 0 warning, 0 error. |
| Backend targeted lần đầu | **FAIL: 82/86 đạt, 4 lỗi**, không skip/timeout. Lỗi EF replacement được mô tả riêng bên dưới. |
| Backend targeted sau sửa cuối | **PASS 86/86**, không skip; gồm SQL Server roundtrip/replacement, TRX `artifacts/master-upgrade/phase4a-tests/final-results/service-item-moderation.trx`. |
| Browser/API QA Phase 4A | **PASS** trong phạm vi thực tế ghi bên dưới; SQL/actor/financial test không được thay bằng quan sát UI. |
| Full readiness, package/security/SQL regression | **PASS651/651 BE+SQL,146/146 FE**,0 skipped; Release/prod build,225-entry ZIP,6 package rejection checks và ProductionHTTPS/account/security/restart/persistence smoke đạt. |
| Áp dụng runtime/deploy cho source mới | **PASS local5008**, Debug0warning0error, health/catalog/Swagger200 và review request schema mới. **Chưa public deploy**, cần kiểm host/HTTPS/storage/backup/resource/integration thực. |

Báo cáo không khẳng định chịu tải đồng thời100 khách. Working set162.9→191.0MB chỉ là functional smoke nhỏ, không phải capacity/load test.

## Backend đã thay đổi

### Actor và ownership kiểm tra lại trong DB

- `GetServiceItemsQuery` ở chế độ moderation yêu cầu tài khoản Moderator/SuperAdmin đang hoạt động, UserId tồn tại và role trong DB khớp role đang dùng. Không chỉ dựa vào `[Authorize]` ở controller.
- Đường đọc/sửa ServiceItem và đọc/lưu consent riêng của shop yêu cầu VendorOwner hợp lệ, account/shop hoạt động và map được Vendor của chính actor. Customer, Finance, CustomerCare, staff dùng nhầm đường Vendor, role giả/stale hoặc identity không tồn tại bị từ chối.
- Danh sách riêng lọc bằng VendorId đã xác thực; item ID/bài đăng của shop khác không được dùng để sửa dữ liệu. ServiceItem đã tạo không được chuyển sang Listing khác, và ngành của item phải khớp Listing.
- Approval kiểm tra lại Listing đúng shop/ngành, `Status=Active`, `IsActive=true`; owner/shop hoạt động và owner còn role VendorOwner; Vendor verified, hồ sơ pháp lý/ngân hàng hiện hữu Approved và category đang hoạt động.
- Combo participation consent không bị coi là điều kiện duyệt một ServiceItem độc lập. Nó vẫn là điều kiện riêng của public combo. Duyệt item đang tắt không tự bật `IsActive`; khi dependencies mất hiệu lực, staff vẫn có thể từ chối Pending bằng lý do hợp lệ.

### Validate trước khi thay tracked state

- Chuẩn hóa và giới hạn tên, khu vực, chính sách hủy, tên variant/add-on theo giới hạn lưu trữ; tối đa 20 variant và 30 add-on. Chặn phần tử null; collections null được chuẩn hóa thành danh sách rỗng để giữ hành vi tương thích.
- Dùng domain hiện hữu để kiểm giá đồng nguyên, biên giá, đơn vị Package/Table/Guest/Hour, sức chứa, daily capacity và giá sau một variant không âm trước khi sửa scalar hoặc xóa children cũ.
- Đây không phải công thức tài chính mới. Check `BasePrice + PriceDelta >= 0` đã tồn tại trước Phase 4A; chỉ chuyển từ vòng thay children sang bước validation trước mutation. Không đổi giới hạn, tỷ lệ hoặc cách tính giá.
- Save, review và lưu consent chạy qua transaction serializable hiện hữu. Audit actor/role thật được ghi cùng transaction; failure không được để lại scalar, children, consent, audit hoặc notification nửa chừng.

### ReviewVersion bắt buộc, không có bypass legacy

- `ServiceItemDto` thêm `reviewVersion` dạng SHA-256 opaque được server tính từ các ID liên quan, nội dung/giá/đơn vị/giới hạn, trạng thái/ghi chú, activity/capacity, ticks tạo/cập nhật và toàn bộ variant/add-on.
- Children được sắp theo ID trước khi hash. Các khoản tiền vốn là đồng nguyên được serialize bằng invariant format `0`, tránh `1000.00m` và giá trị SQL `decimal(18,0)` đọc lại tạo token khác nhau; DateTime dùng ticks để không phụ thuộc `Kind` bị SQL trả về Unspecified.
- `POST /api/combos/items/{id}/review` nhận `approve`, `note`, `reviewVersion`. Controller truyền token vào handler, handler so sánh chính xác token hiện tại trước khi quyết định; item vẫn phải Pending theo domain.
- Token thiếu/trống/cũ/sai bị từ chối cả approve và reject. Vendor sửa và gửi lại không thể bị staff quyết định bằng token đã tải trước đó. Review trùng không tạo thêm audit/notification thành công.
- Từ chối bắt buộc lý do sau trim từ 1–2.000 ký tự; approve có thể không có note, nhưng note nếu có cũng tối đa 2.000 ký tự.
- DTO response thêm field là additive, nhưng request review mới **bắt buộc có token hợp lệ trong mọi trường hợp**. Cần phát hành frontend và backend cùng phiên bản. Client cũ thiếu token phải nhận lỗi và tải bản mới; không thêm fallback, legacy bypass hoặc tự đoán token để duyệt cho qua.
- Token phục vụ optimistic concurrency, không phải chữ ký phê duyệt và không thay thế kiểm tra quyền/dependencies. Phase này chưa tạo immutable ServiceItem revisions hay lịch sử nội dung đầy đủ tương tự Listing/Combo.

### Notification và audit

- Quyết định hợp lệ tạo một notification InApp cho UserId owner thật, cùng audit `ServiceItem.Review` chứa decision, note, version và actor/role. Các bản lưu/cập nhật và consent có audit riêng.
- DeduplicationKey có cấu trúc `service-item-review:{itemId}:{reviewVersion}:{approved|rejected}`, dùng index dedup hiện hữu; không thêm schema mới.
- `ContextKind` và `ContextId` được chủ ý để null: allowlist notification hiện chưa có resource ServiceItem. Không giả thành lead/combo/contract để gắn deep link sai hoặc mở rộng quyền.
- Typed ServiceItem notification deep link vẫn **TODO**. Nội dung được lưu thật nhưng báo cáo không tuyên bố đã có điều hướng tới đúng item từ notification, email/SMS mới hay outbound delivery mới.

## Frontend đã thay đổi

- Staff card trong `ComboAdministration.tsx` hiển thị thông tin ServiceItem, variant/add-on, phản hồi kiểm duyệt và link private Listing preview đang có. Thay prompt từ chối bằng textarea có giới hạn/lý do bắt buộc; nút được khóa trong lúc gửi.
- `serviceItemReviewPayload` chỉ gửi decision, note đã trim và token server; không cho payload kéo theo giá, commission hoặc snapshot khác từ object đang hiển thị.
- Chặn quyết định trên item không Pending hoặc thiếu/malformed token. Server vẫn kiểm authoritative version/quyền. Có thông báo lỗi và thao tác tải lại; key card gồm item ID và reviewVersion để không giữ note cũ khi nội dung đã đổi.
- Nhãn Pending của ServiceItem trên Vendor/staff là “Chờ kiểm duyệt”, không còn dùng nhầm “Chờ NCC duyệt” của luồng đặt lịch/báo giá. StatusPill nhận label override tùy chọn; các luồng khác giữ nhãn hiện hữu.
- Giữ editor/lifecycle Combo, pricing math, consent và panel quy tắc giảm hiện hữu. Chưa biến màn này thành queue thống nhất có server paging/filter cho cả Listing/ServiceItem/Combo.

## Lỗi thật được regression tests phát hiện và cách sửa

Lượt targeted đầu thực thi đủ 86 trường hợp: 82 đạt, 4 lỗi `DbUpdateConcurrencyException` với thông báo EF dự kiến tác động 1 dòng nhưng thực tế 0 dòng. Các trường hợp lỗi:

1. Sửa/gửi lại item sau khi staff đã tải version cũ.
2. Lưu Draft thay scalar và children, rồi kiểm null collections.
3. Sửa item có giao dịch lịch sử rồi approve.
4. Sửa item có giao dịch lịch sử rồi reject.

Nguyên nhân là EF state khi thay children của parent đã được tracked: variant/add-on mới đã có GUID sinh sẵn; chỉ đưa chúng vào navigation collection có thể khiến EF suy ra UPDATE cho GUID chưa có trong DB thay vì INSERT. Đây không phải bằng chứng có người khác sửa DB đồng thời, không phải lý do bỏ assertion hoặc tăng timeout.

Sửa tại save handler: nối mỗi object child mới vào navigation **trước**, rồi gọi tường minh `context.ItemVariants.Add(variant)` hoặc `context.ItemAddOns.Add(addOn)` trên **chính object/GUID đó**. EF nhận Added chính xác, relationship fix-up không tạo reference trùng trong collection. Lượt sửa trung gian82/86 đã phát hiện việc track trước rồi manual-add khiến một child xuất hiện hai lần và token sai; đã sửa thứ tự, giữ nguyên assertions và thêm SQL replacement regression. Xóa children cũ và INSERT children mới vẫn nằm trong một transaction; không sửa ID/snapshot giao dịch đã xác nhận để né lỗi.

Lượt cuối sau sửa thực thi **86/86 PASS**, không skip,10 giây test. Giữ cả TRX lỗi trung gian để truy vết, không ghi đè chúng bằng kết quả thành công.

## Phạm vi test mới và chứng cứ

`tests/Wedding.Tests/ServiceItemModerationTests.cs` có 86 trường hợp targeted theo fixture hiện tại, gồm:

- Actor/role/DB identity thực, disabled actor/shop, cross-shop item/Listing denial và own read scope.
- Validation và normalization trước mutation; create/replacement lỗi giữ nguyên scalar, children, audit/notification; null collections tương thích.
- Approve revalidate đầy đủ dependencies; reject có lý do khi dependencies bị tắt; approval không tự bật activity/consent.
- Missing/stale version, chỉnh rồi gửi lại, quyết định trùng, owner notification và audit actor thật.
- Fault injection sau SaveChanges nhưng trước commit để kiểm rollback đồng thời data/audit/notification.
- Consent chỉ đổi shop mình, không đổi rate hoặc giao dịch cũ; sửa/duyệt item không tính lại order totals/discount/selection/cancellation, confirmed contract hoặc commission đã trả.
- SQLite regression cho luồng thay children gây 4 lỗi ở lượt đầu. SQL Server opt-in test dùng DB tổng hợp `WeddingPlanTest_ServiceModeration_*`, kiểm canonical version qua decimal-scale round-trip rồi tiếp tục thay children trên parent hiện hữu: ID mới được INSERT/persist/read lại đúng, token cũ bị từ chối, token mới duyệt được, audit/notification tách đúng phiên bản. Test kiểm không pending migrations/model changes và chỉ dọn DB do fixture sở hữu.
- Frontend thêm 4 test payload: whitelist/token/note, bắt buộc lý do, malformed/missing token fail closed và cấm quyết định ngoài Pending.

Artifact hiện có / chỗ root sẽ cập nhật:

- Lượt đầu **FAIL 82/86**: `artifacts/master-upgrade/phase4a-tests/results/service-item-moderation.trx`.
- Rerun trung gian **FAIL82/86** do duplicate child reference/token: `artifacts/master-upgrade/phase4a-tests/verified-results/service-item-moderation.trx`.
- Targeted cuối **PASS86/86**: `artifacts/master-upgrade/phase4a-tests/final-results/service-item-moderation.trx`.
- Full readiness: `artifacts/readiness/20261019-170757-20a732e7/ready.json`; `VerifiedUtc=2026-10-19T17:10:14.7172763Z` (**00:10:14 ngày20/10/2026, UTC+7**).
- Backend TRX tổng: `artifacts/readiness/20261019-170757-20a732e7/test-results/backend.trx`,651 executed/passed,failed/skipped0.
- Release: `artifacts/readiness/20261019-170757-20a732e7/deploy/wedding-match-20261019-170907-edbf50b6`; log `artifacts/master-upgrade/phase4a-ready-final.log`.
- Source SHA256: `3483214121889A45A4257D332F81257082BDD312E8A56404DE455D78926F3246`.
- ZIP SHA256: `38D0D1D3E75E66AF162726BE3B38B1026AE96A58A3B3B3CFE6C2DA58EA02780E`.
- Local runtime refresh: kiểm đúng process/project/port, dừng một API cũ và build Debug0warning0error rồi mở API5008 mới. ApplyMigrations=false/SeedDemoData=false chỉ trong process. User Vite5173 giữ nguyên. Health/catalog/Swagger200; xác minh request schema qua `$ref` của endpoint review có `approve,note,reviewVersion` (Swagger không tự công bố DTO response IActionResult, không coi thiếu schema tên DTO là API cũ).
- SHA256 ordered all-column JSON và row count của **55 bảng dữ liệu hiện có khớp chính xác trước–sau**, không migration/seed/data rewrite ở4A; main6 Users/1 Vendor/2 Listings,contract/commission0,migrations12. Chứng cứ không PII: `artifacts/master-upgrade/phase4a-main-preservation.txt`. Không sửa trạng thái tài chính để làm smoke pass.
- Cảnh báo runtime hiện hữu được giữ, không tắt để giấu: MediatR thiếu license key (cần kiểm điều kiện sử dụng trước public deploy); EF multiple-collection single-query có thể cần tối ưu trong module paging/performance. Bốn xUnit2031 warning cũ và mười lint warning cũ không chặn build/test, chưa tự sửa ngoài phạm vi.

## Browser/API QA thực tế

- Chỉ DB tổng hợp riêng `WeddingPlanTest_ServiceReviewUi_20261019_97c61e`, API5014/Vite5179; bootstrap Vendor/Moderator và Listing đã duyệt qua API thật. Không dùng dữ liệu/tài khoản khách hàng.
- Moderator UI mở Pending version cũ13.500.000đ. Vendor API sửa cùng ID, đổi tên/giá14.500.000đ/variant/add-on rồi gửi lại; mỗi collection chỉ1 child. UI cũ vẫn nội dung cũ; ấn Duyệt bị server chặn, có lỗi và nút Tải lại dịch vụ. Không đổi trạng thái, không có review audit/notification thừa.
- Tải lại hiển thị nội dung/giá/children mới. Reject bị khóa khi thiếu lý do; nhập lý do, UI từ chối thành công. Owner API đọc đúng ReviewNote. Vendor sửa điều khoản/gửi lại; Moderator tải lại và duyệt bằng UI, statusApproved cùng nội dung mới.
- Vendor HTTP GET hàng chờ trả403; Moderator POST review thiếu token trả400. Các matrix actor giả/disabled/IDOR được kiểm thử ở86 bài backend.
- Kiểm DB sau luồng: Approved,1 variant,1 add-on, **2 review audit +2 service-review notification**, contract/commission0. Notification đúng owner, không có outbound email/SMS. SQL test riêng kiểm confirmed order/contract/paid commission không thay đổi.
- Viewport390×844: document clientWidth375/scrollWidth375, không tràn ngang. Đã reset viewport. Full-page screenshot lỗi công cụ, đã lưu screenshot viewport: `C:/Users/Admin/.codex/visualizations/2026/10/16/01a14397-369e-7862-9140-f563154bb4f2/phase4a-service-review-compact.png`; ảnh desktop trạng tháiApproved `phase4a-service-review-final.png` cùng thư mục.
- Đã đóng các tab, dừng đúng2 process QA và xóa đúng DB tổng hợp nêu trên. Không dừng SQL/user Vite5173 hoặc xóa DB thật. Không có upload mới ở4A.

## Database, tài chính và phần việc còn lại

- Phase 4A không thêm migration hoặc cột ReviewVersion: token được tính khi trả DTO; notification/audit dùng schema đã có. Không reset/reseed DB, không chạy Down, không backfill rate/historical snapshot hoặc tự bật auto-migrate Production.
- Test SQLite là fixture độc lập, không phải đổi project từ SQL Server sang SQLite. SQL integration vẫn dùng DB SQL Server tổng hợp riêng; không dùng dữ liệu khách hàng làm fixture.
- Không đổi commission basis/rate/rule resolution, K1/K2, escrow/ledger/payment/refund/cancellation/discount allocation hoặc policy activation. Các quyết định đã chốt ở `docs/decisions/2026-10-19-commission-basis-and-activation.md` vẫn là ranh giới cho các phase sau, chưa được coi đã triển khai bởi content approval hay token này.
- **Phase 4 chưa hoàn tất:** queue thống nhất và server paging/filter Listing/ServiceItem/Combo; customer combo price/region/service-count/savings/rating filters và URL/back persistence; review Shop/Service/Combo, eligibility/verified weighting/rating aggregation cần tiếp tục theo quyết định nghiệp vụ. Không tự đổi review/rating policy trong Phase 4A.
- **Phase 5–7 chưa được tuyên bố hoàn thành:** scoped/effective commission rules và snapshots; negotiation/counteroffer/chat; partnership files/versioning/activation và adjustment lịch sử vẫn là module phụ thuộc riêng.
- Gate4A đã đóng; tiếp tục queue paging/filter và URL/back persistence theo checklist. Không public deploy hoặc thay SQL/mạng/power/security của máy. Những nghiệp vụ review weighting/settlement chưa rõ được ghi nhận, không tự quyết định để gọi toàn dự án đã xong.

## Các file chính

- Backend: `src/Wedding.Application/Features/Combos/ComboCatalogFeature.cs`; `src/Wedding.WebApi/Controllers/CombosController.cs`.
- Helpers dùng lại: `ComboTemplateLifecycle.ActorAsync`, `ListingPublication.CheckSubmission`, `ApplicationDbContext.ExecuteSerializableAsync`, domain ServiceItem/variant/add-on và notification hiện hữu.
- Frontend: `client/src/features/journey/admin/ComboAdministration.tsx`, `admin/serviceItemReview.ts`, `client/src/features/journey/comboApi.ts`, label override trong `journey/ui.tsx` và `vendor/ServiceItemManager.tsx`.
- Tests: `tests/Wedding.Tests/ServiceItemModerationTests.cs`, `client/tests/service-item-review.test.mjs`.
