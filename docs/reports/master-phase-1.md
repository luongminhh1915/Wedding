# Phase 1 — Audit, readiness và bản vá bảo mật nền

Ngày: 18/10/2026. Baseline source: `468e507`, branch `weddy-journey`, worktree sạch lúc bắt đầu. Checklist/acceptance/dependencies: [master-upgrade-checklist](../master-upgrade-checklist.md).

## Phạm vi đã kiểm tra

Đọc toàn bộ 1.482 dòng Master Prompt và hai ảnh tham chiếu. Quét cấu trúc repo (488 file source/test/tool/docs trong inventory sau khi loại output/assets); đọc các entry point, domain/schema, UI/API và test liên quan theo từng miền. Audit source không được coi là đã thực thi mọi luồng hoặc chứng minh không còn lỗi.

| Miền | Kết quả audit |
|---|---|
| Frontend | React/TS/Vite, feature modules, API clients, role routes, auth cache/session, onboarding, planner/nav, Vendor/Admin dashboards, listing/combo/review và invitation/music. Có chức năng thật; nhiều phần Master là mở rộng chứ không viết lại. |
| Backend | .NET/EF Core 10, Clean Architecture + CQRS, REST/SignalR, validation/domain rules, owner-scoped access, financial serializable transaction/app lock, append-only audit. |
| Database | SQL Server `localhost` / `WeddingMatch_Dev`, 52 bảng, 9 migration, seed tắt. Không reset/reseed hay đổi provider. |
| AuthN/AuthZ | Sáu actor giữ nguyên; signup chỉ Customer/Vendor, cấp staff SuperAdmin; JWT kiểm role/active/security version trong DB, đổi mật khẩu thu hồi phiên, cache không dùng chéo actor. Backend guards là bắt buộc, UI route guard không thay thế authorization. |
| Service & Combo | Listing + ServiceItem + ComboTemplate và các order snapshots phải tái sử dụng; thiếu business groups, revisions, một số preview/moderation UX; không tạo service thứ ba hoặc nhầm giỏ khách với combo NCC. |
| Financial | Một rate shop override category; thiếu scoped/effective rules và base snapshot; Confirm tính toàn ContractValue, completion có legacy fallback. Không sửa công thức trong Phase 1. |
| Negotiation & partnership | Chưa có nghiệp vụ này. Lead chat/Customer Quote/BookingContract không thể đổi tên thành negotiation/partnership vì khác participant, quyền, trạng thái và lưu trữ. |
| Deploy | Production cấm demo/autostart migration, private configuration/uploads, offline idempotent SQL, package guards/HTTPS/SQL/restart/account smoke. Chưa deploy công khai. |

## Dữ liệu thật trước thay đổi

Chỉ đọc metadata, số đếm và fingerprint SHA-256 của snapshot các trường user định danh/bảo mật; không ghi PII, mật khẩu hoặc JWT vào báo cáo. Không sử dụng số đếm cũ từ lượt trước vì người dùng đã thêm dữ liệu.

| Dữ liệu | Baseline Phase 1 |
|---|---:|
| Users | 6 |
| WeddingProfiles | 2 |
| ChecklistTasks | 70 |
| BudgetItems | 28 |
| Vendors | 1 |
| Listings | 2 |
| ComboTemplates | 0 |

Fingerprint user trước test: `62F0A041F07D3E2CDD33031DE63A87411B583BE5FE577D44E334B61BFBBC3B63`. Không bao gồm LastLoginAt/UpdatedAt; đây không phải hash backup toàn DB. Sẽ đối chiếu lại sau gate; nếu người dùng đang chỉnh dữ liệu đồng thời phải phân tích delta, không tự khôi phục đè.

## Bản vá Phase 1 (không thay quy tắc tài chính)

1. **Danh sách thông báo chuyển tiền / bank data**: endpoint chung từng chỉ yêu cầu đăng nhập và gọi GetAll không lọc. Allowlist `VendorOwner,Finance,SuperAdmin`; service xác thực actor DB còn active/đúng role; Vendor chỉ query shop mình, staff tài chính xem toàn bộ. Giữ signature để không phá caller đối soát hiện có; không đổi create/approve/reject/per-contract/payment.
2. **POST review bypass private listing**: dùng điều kiện public Listing trước khi đọc title/tạo review. Không tự đổi eligibility/weighted rating hiện hữu — review tư vấn là câu hỏi của Phase 4.
3. **Giỏ combo cũ bypass owner bị khóa**: tính giá/submit phải kiểm owner active nhất quán với public catalog. Không đổi giá, discount tiers, đơn vị tính, số item/category hoặc snapshot đơn cũ.
4. **Readiness gate resource hygiene**: chạy xUnit test collections tuần tự trên instance SQL dùng chung, giữ các bài test concurrency bên trong. Không đổi max SQL memory, service recovery, firewall hay quyền máy tính.
5. **README chính xác**: sửa thông tin .NET 8/SQLite mặc định cũ thành stack/DB thực; lệnh npm.cmd và backend rebuild; phân biệt cấu hình Development/Production, link checklist.
6. **Retire endpoint seed HTTP cũ**: `POST /api/devseed/seed` từng không yêu cầu quyền, gọi initializer và luôn quảng cáo tài khoản demo/mật khẩu 123456 dù seed đã tắt. Không có caller UI/tool hiện hành. Nay trả 410 JSON, không truy cập initializer/DB và không trả credentials. Startup policy giữ nguyên, không đổi dữ liệu hay mật khẩu đã có.

## File thay đổi

- `README.md`
- `docs/master-upgrade-checklist.md`
- `docs/reports/master-phase-1.md`
- `tools/Test-ReadyToDeploy.ps1`
- `tools/Verify-AccountSecurity.ps1`
- `tools/Verify-StudyDeployment.ps1`
- `src/Wedding.WebApi/Controllers/CommissionsController.cs`
- `src/Wedding.WebApi/Controllers/DevSeedController.cs`
- `src/Wedding.Application/Common/Interfaces/IAdvancePaymentService.cs`
- `src/Wedding.Infrastructure/Services/AdvancePaymentService.cs`
- `src/Wedding.Application/Features/Reviews/Commands/SubmitReview/SubmitReviewCommandHandler.cs`
- `src/Wedding.Application/Features/Combos/ComboPricingFeature.cs`
- `tests/Wedding.Tests/AdvancePaymentAccessTests.cs` (16 case, gồm caller đối soát thật)
- `tests/Wedding.Tests/MarketplacePublicationGuardsTests.cs` (10 case)
- `tests/Wedding.Tests/DevSeedEndpointTests.cs` (1 case)
- `tests/Wedding.Tests/ReviewTests.cs` (hai fixture chuyển sang Listing đã duyệt, giữ mục tiêu test cũ)

## API/database

- Không có API mới, không đổi DTO response hay route, không đổi schema/migration.
- Siết quyền GET `/api/commissions/advance-requests`; Vendor own-only, Finance/SuperAdmin all; actor khác bị từ chối. Endpoint admin vẫn Finance/SuperAdmin.
- Endpoint seed HTTP legacy trả 410 thay cho 200 seed/demo credentials; không có route seed mới. Khởi tạo database chỉ theo startup policy hiện hữu.
- POST review và combo price/submit từ chối các target không đủ điều kiện công khai/owner active.
- Không đổi policy/rate/base/rounding/discount/VAT, quy trình phê duyệt hay khoản tiền của giao dịch nào.

## Gate và bằng chứng thực thi

**Phase 1 đạt gate.** Bộ kiểm tra đã hoàn tất trong khi phiên chat bị gián đoạn; đã đọc lại `ready.json`, TRX và log khi tiếp tục ngày 19/10/2026, không chạy lại/mặc định hoàn thành theo việc file tồn tại.

| Gate | Kết quả |
|---|---|
| Frontend unit | PASS — 89/89, không skip. |
| Frontend lint | PASS — 0 lỗi; 11 warning hiện hữu (hook/render/HMR), không giấu warning. |
| Frontend production build | PASS — có warning dynamic/static import LoginForm; không chặn build. |
| Backend unit + SQL integration | PASS — **369/369**, failed/aborted/notExecuted = 0, SQL opt-in bật; test collections tuần tự. |
| Backend release build/package | PASS — package của đúng source Phase 1, không chứa DB/user files/production credentials. |
| Package guardrails/Production SQL/HTTPS/restart/account smoke | PASS — 6 rejection checks, schema idempotent trên QA SQL, catalog hiện tại, retired seed 410/no credentials/no data mutation, HTTP role isolation, password/contact/JWT/SignalR privacy và restart persistence. |
| Main DB sau gate | PASS — đối chiếu lại ngày 19/10: đủ số đếm baseline và fingerprint bên trên; vẫn 9 migration. Không ghi/apply migration lên DB thật trong Phase 1. |

Artifact chứng minh Phase 1:

- `artifacts/readiness/20261018-164017-c4316213/ready.json`
- `artifacts/readiness/20261018-164017-c4316213/test-results/backend.trx`
- Release: `artifacts/readiness/20261018-164017-c4316213/deploy/wedding-match-20261018-164131-d7b816b8`
- Source SHA-256: `0AD94C3D6F90EA7701DD9C2F45E87B686395C3D3D5E06790C2B2179BF56801CD`

Kết quả này chỉ xác nhận source/package Phase 1. Mọi sửa tiếp theo ở Phase 2 phải chạy gate riêng, không dùng artifact cũ để tuyên bố deploy-ready cho code mới.

Log FE baseline: `artifacts/master-upgrade/phase1-1792341113177/`. Mọi artifact test/package là local; không đưa secret hoặc dữ liệu khách vào package. SQL tests dùng unique QA DB, cleanup chỉ DB do chính test tạo; diagnostics QA giữ riêng để kiểm tra.

## Ranh giới, rủi ro và bước tiếp

- SQL từng dừng đột ngột/thiếu RAM ở log cũ; Phase 1 không chứng minh đã xử lý nguyên nhân crash và không phải kiểm thử tải. Chạy test tuần tự, không tự điều chỉnh instance chung.
- Các gap lớn Phase 2–9 và đủ AC1–47 đã map trong checklist; **chưa được đánh dấu hoàn thành**.
- Chưa sửa public Vendor signup nhận CommissionRate vì cần thay đồng bộ membership/default/legacy semantics, không vá UI rồi vẫn sai resolver. Đây là việc chặn của Phase 2, không phải tính năng đã đạt.
- Người dùng đã chốt ngày 19/10: legacy rate giữ cho giao dịch mới cho tới Admin xác nhận chuyển đổi; snapshot gốc lúc hợp đồng được xác nhận giữ nguyên, policy mới dùng ngày hiệu lực. Ngoại lệ điều chỉnh riêng hợp đồng phải là workflow/adjustment append-only, không sửa payment history. Chi tiết và câu hỏi còn mở ở `docs/decisions/2026-10-19-legacy-and-contract-commission-adjustments.md`.
- Các câu hỏi tiếp theo (trước phase tương ứng): fixed/dynamic combo + replacement pricing/cardinality; combo commission allocation/no double charge; consultation review và rating aggregation; contract prerequisite cho rule activation.
- Tiếp tục Phase 2 từ baseline này. VAT/base, định giá combo và settlement adjustment vẫn cần quyết định trước phase tài chính tương ứng; không gọi workflow tài chính chưa triển khai là đã hoạt động.
