# Danh mục dịch vụ xuyên suốt và Phase4B — 20/10/2026

## Kết quả và phạm vi

Tiếp tục branch `weddy-journey`, baseline `468e507`; kiểm tra worktree/diff/checklist trước chỉnh sửa, giữ toàn bộ thay đổi hợp lệ chưa commit. Ưu tiên mới của người dùng là tìm dịch vụ theo danh mục giống hướng ảnh Weddy, cùng taxonomy cho Vendor tạo dịch vụ, hợp đồng và nguồn hoa hồng. Không đổi branding Wedding Match, không sao chép thêm nhóm/rate chưa được thống nhất.

### Đã triển khai

- `/browse`: dropdown danh mục + keyword + nút tìm, icon/category rail từ DB, filter AND khu vực/khoảng giá/đánh giá shop, sort và SQL paging. URL/chips/reset/back/reload lưu điều kiện; mức đánh giá chỉ dùng shop có ít nhất3 review theo chính sách hiện hữu.
- Tám category canonical thuộc năm business group hiện tại dùng chung API, không tạo bộ enum FE riêng. MC/ban nhạc trong ảnh chỉ là ví dụ, chưa tự gán nhóm hoa hồng mới.
- `/api/categories/my`: active membership quyết định category được tạo mới; frontend fail-closed nếu không tải được quyền. Legacy có thể chỉnh dịch vụ cũ trong phạm vi grandfather đã kiểm tra; không tự tạo membership hoặc đổi category của bài đã tồn tại.
- Admin tại Người dùng & đối tác → Chi tiết NCC có editor bổ sung nhóm **add-only**, lý do bắt buộc, audit/actual SuperAdmin guard. Không thay scalar hoa hồng cũ hoặc policy acknowledgment/version, không coi thao tác này là phê duyệt tài chính.
- ServiceItem lấy category từ Listing của chính shop, kiểm cùng danh mục khi save/review/public pricing; khách không thể gửi category/rate giả.
- Confirm hợp đồng chốt category/group ID+name, nguồn tỷ lệ và policy version. Legacy giữ tỷ lệ riêng, không gán giả version của group hiện tại. Snapshot cũ không được tự backfill/đổi sau khi xác nhận. Completion thiếu original rate bị từ chối trước mutation thay vì fallback mức hôm nay/8%. Xem báo cáo snapshot riêng.
- Hàng chờ thống nhất Listing/ServiceItem/Combo: metadata union/search/paging SQL, latest proposal và exact private detail; ảnh, mô tả, thành phần, private preview. Decision dùng handler có revision/version guard và required rejection reason; giữ published baseline, IsActive và notification/audit hiện có.
- Combo filters AND: estimated price theo engine hiện có, shop city, service region, min/max included-service count, savings, shop rating, category/vendor/keyword; sort giá/newest/rating/savings. Giá hiển thị theo số khách, theo giờ mặc định1 giờ và chưa gồm tùy chọn thêm, không gọi là giá hợp đồng. URL/filter chips/dialog/back giữ ngữ cảnh và không remount giỏ.
- Theo quyết định người dùng: review mới chỉ từ giao dịch Completed của đúng Customer, Listing và Vendor. Không dùng Confirmed quá3 ngày làm thay Completed. Chặn foreign/inactive/role spoof/dispute/duplicate/private listing. Modal có loading/error/retry/empty; bỏ lựa chọn tạo review tư vấn mới. Review tư vấn cũ giữ nguyên, có nhãn lịch sử, không đổi trọng số1/.5 hoặc tính lại điểm.

## Kiểm thử và artifact

- Full final **861/861 backend,0 skip**, `WEDDING_TEST_SQLSERVER=1`, gate tuần tự trên SQL Server thật và DB test dùng một lần. TRX: `artifacts/readiness/20261020-002151-460226f2/test-results/backend.trx`.
- Frontend **199/199**,0 skip; lint0error/10warning hiện hữu; TypeScript/Vite production build thành công. BE build thành công;4 warning xUnit2031 cũ trong VendorBusinessGroupTests. Vite còn warning LoginForm vừa static/dynamic import, không phải runtime error.
- Package ZIP/rejection6 guards, schema migration idempotency, realHTTPS signup/membership/paging/actor isolation, account/password/JWT/WebSocket boundaries, API/SPA isolation, private upload and SQL persistence qua restart đều đạt. Đây là local Production smoke, không phải100-user load test hoặc host thực.
- `ready.json` cuối: `artifacts/readiness/20261020-002151-460226f2/ready.json`, VerifiedUtc `2026-10-20T00:24:10.3086149Z` (07:24VN). Gate trước tại `20261020-001153-cd271577` cũng đạt nhưng chưa gồm sửa short label A–Z.
- Source SHA256 `096A0FEAF4A670E085C7B8440C40645F7AC0243F150A63C34013A8C21DFA5AA3`; Site ZIP SHA256 `9710FF84F504E98DE641A7564C1CC3A9A67EDE588D24A89AE61E5C8F3F3081E5`. Chỉ artifact chính xác này đã qua smoke; cần chạy lại nếu đổi source.
- Full gate đầu860/861: assertion so thứ tự GUID tie bằng .NET Guid.CompareTo không đúng thứ tự SQL uniqueidentifier. Đã sửa test kiểm thứ tự giá và repeated-query stability, không đổi production SQL ordering; gate cuối861/861 đạt. Giữ TRX lần đầu tại `artifacts/category-final-tests/results/category-final.trx`.

## Browser/API QA tích hợp

Môi trường riêng API5014/Vite5179, DB `WeddingPlanTest_CategoryUi_20261020_d87e19`, chỉ tài khoản `example.invalid` và nội dung ghi rõ `[QA]`, không ghi dữ liệu giả vào DB chính.

- API thật: NCC chỉ nhóm ảnh bị từ chối tạo Decor; Admin bổ sung Decor có lý do, policy/rate/ack giữ nguyên; tạo/submit/approve Listing; public category+keyword+Hà Nội+10–15triệu trả đúng1 trong3 dịch vụ giả lập. Hình fixture dùng URL ảnh đã có, không upload ảnh cá nhân.
- Browser `/browse` desktop1280 và mobile390: chọn category+keyword, area+price, result2→1; detail/back và reload giữ đúng URL/lựa chọn. Mobile document clientWidth375=scrollWidth375. Lưu `service-category-desktop.png`, `service-category-mobile.png` trong thư mục visualization của task.
- Vendor login đi đúng portal; form tạo dịch vụ chỉ có5 category của2 nhóm đã được Admin xác nhận, không có A–Z/venue ngoài phạm vi; chọn Decor hiển thị đúng group, default8% là tham chiếu, không phải customer discount.
- Admin chi tiết NCC render add-only editor, các nhóm cũ checked+disabled và nút submit disabled khi thiếu nhóm mới/lý do. Không dùng browser thay đổi policy tài chính.
- Hàng chờ filterApproved đọc đúng Listing revision/ảnh; pending combo fixture xuất hiện với2 dịch vụ và đúng revision; **browser phê duyệt thành công**, queue về0 và toast xác nhận notification, sau đó combo xuất hiện public18triệu theo dữ liệu giả lập. Không ký hợp đồng hoặc thanh toán.
- Combo browser: kết hợp10–20triệu+shop Hà Nội+phục vụ Hà Nội+category ảnh+ít nhất2 dịch vụ trả đúng1 combo; trang detail có breadcrumb/CTA quay lại giữ toàn bộ filter. Mobile375/375, dialog giữ lựa chọn; đổi giá30–50triệu trả0 với empty/reset rõ ràng, không hiển thị lại toàn catalog. Sửa short label cũ trên mobile từ Wedding Planner thành Tổ chức A–Z; sau đó chạy lại release gate.
- Một số screenshot fullPage sau modal/admin bị công cụ từ chối; AX state và kết quả API/browser vẫn đọc được. Không gọi đây là E2E thuần UI đầy đủ mọi actor/module; API fixtures tạo bằng script riêng, review eligibility và financial category flows còn được xác minh bằng SQL integration tests.

## Database và runtime local

- Backup `D:\sql\dev\MSSQL16.MSSQLSERVER\MSSQL\Backup\WeddingMatch_Dev_before_category_snapshots_20261020_0014.bak`, COPY_ONLY/CHECKSUM; **RESTORE VERIFYONLY WITH CHECKSUM thành công**. VERIFYONLY không thay thế restore drill.
- Migration `20261019174943_ContractCategorySnapshots` đã review Up chỉ thêm6 nullable columns, không UPDATE/DELETE/backfill/seed/đổi tiền. Áp dụng offline vào `WeddingMatch_Dev`:12→13.
- So sánh COUNT_BIG và SHA-256 JSON theo PK của toàn bộ cột cũ **55 bảng nghiệp vụ** trước/sau migration: tất cả khớp. Không đưa PII vào báo cáo; migration history chỉ thêm đúng dòng13.
- Main API5008 dùng binary BE đã kiểm thử trong `artifacts/category-final-tests/Wedding.WebApi.dll`, content root `src/Wedding.WebApi`, `ApplyMigrations=false`, `SeedDemoData=false`, SQL Server explicit `WeddingMatch_Dev`. Ban đầu khởi động Release, sau đó chuyển binary ngoài bin để không khóa DLL khi đóng gói Release; backend source không đổi. Health/Swagger/catalog/category trả200; private category/queue anonymous401. Vite người dùng5173 giữ nguyên, không deploy public/push/reset.
- Fingerprint55 bảng khớp lại sau restart, evidence tại `artifacts/master-upgrade/category-preservation.json`. Đã dừng đúng API5014/Vite5179 và xóa **chỉ DB giả lập do phiên này tạo** sau khi kiểm tra chính xác2 email example.invalid,3 Listing/2 ServiceItem/1 template,0 contract/payment. Dữ liệu QA không khôi phục bằng UI; có thể dựng lại bằng fixture scripts trong `artifacts/master-upgrade`, không ảnh hưởng DB chính. Tab/viewport kiểm thử đã đóng/reset.

## File thay đổi chính trong lần tích hợp

- FE public: `BrowsePage`, `SearchFilterBar`, `CategoryBar`, `ListingCard`, `ListingDetailPage`, `useBrowse`, `serviceFilterModel`, `service-search.css`.
- FE Vendor/Admin: `ListingForm`, `useVendorCategories`, `vendorCategoryModel`, `VendorBusinessGroupsEditor`, `vendorMembershipModel`, `UserManagement`, `ModerationQueue`, `useModerationQueue`, `moderationQueueModel`, `AdminDashboard`.
- FE combo/review/contract: `ComboBuilderPage`, `ComboCatalogFilters`, `comboFilterModel`, `comboApi`, `combo-discovery.css`; review modal/list/hook/policy model; shared contract category summary/types and Customer/Vendor contracts.
- BE taxonomy: public categories, `VendorCategoryEligibility`, `AdminVendorBusinessGroups`, `ListingCategoryAccess`, `ListingAccess`, create/submit/approve/save guards, public listing query/controllers.
- BE contract: Confirm/Complete handlers, ContractDto/three projections, BookingContract/config/DbContext immutable guard, migration13/Designer/ModelSnapshot.
- BE discovery/moderation/review: `ComboCatalogFilters`, pagination/public availability guards, `ModerationQueueFeature`/controller; SubmitReview/GetEligibleContracts/ReviewRules.
- New focused tests: `PublicServiceCategoryFilterTests`, `VendorCategoryEligibilityTests`, `ContractCategorySnapshotTests`, `ComboCatalogFilterTests`, `UnifiedModerationQueueTests`, `CompletedReviewPolicyTests`; FE service/vendor/combo/moderation/contract/review model tests. Valid pre-existing dirty changes remain unchanged outside integration needs.

## Chưa hoàn tất — không tuyên bố toàn Master Prompt xong

Independent Shop/Service/Combo rating targets/aggregation; Commission Management scoped/effective rule engine và immutable VAT/discount sponsor/approved pass-through/full basis snapshots; negotiation, partnership private documents/externally signed activation; contract-specific append-only adjustment và settlement differences. Các nguyên tắc tài chính đã chốt vẫn giữ, chi tiết unsettled allocation/offset/refund chưa được tự quyết định.

Music upload/YouTube validation, hierarchical customer/vendor/admin navigation và MoneyInput coverage còn lại; toàn bộ integration của các module mới, backup **restore drill**,100 concurrent load test và host thực. Không tạo lại module đã hoàn thành; tiếp tục từ các mục chưa đánh dấu trong checklist.
