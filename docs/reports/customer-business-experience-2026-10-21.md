# Customer experience và business flow — 21/10/2026

## Phạm vi và điểm tiếp nhận

Nguồn: người dùng yêu cầu ưu tiên trải nghiệm khách hàng, chức năng business và cập nhật handoff cho Copilot. Tiếp tục luồng đang có: chọn dịch vụ/combo → yêu cầu tư vấn/chat → báo giá → hợp đồng → theo dõi. Không mở Phase5–7, không đổi công thức hoặc kích hoạt chính sách tài chính.

Đã đọc hướng dẫn, handoff/checklist/decision và đối chiếu diff/source/caller/tests. Git đầu lượt: `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`, **90 tracked modified / 75 expanded untracked / 0 staged / 1 worktree**. Đây là worktree nhiều thay đổi sẵn có; không quy toàn bộ diff cho lượt này. Không reset/clean/commit/push/deploy.

Báo cáo này tiếp nối, không viết lại kết quả lỗi trong [checkpoint bị ngắt](vendor-open-category-2026-10-21.md). Quyết định [open-category](../decisions/2026-10-21-service-category-without-vendor-membership.md) và [FixedPrice/private content](../decisions/2026-10-20-fixed-combos-and-private-service-changes.md) vẫn giữ nguyên.

## Đã sửa thực tế

### 1. Khách không bị hiểu nhầm mất hồ sơ khi API lỗi

- Leads, Quotes, Contracts, MyCombos phân biệt lỗi đọc với loading và thành công không có dữ liệu. Lỗi initial/refetch không còn hiện empty-success, stale actionable cards hoặc pagination giả.
- Thông báo có nút thử lại, khóa khi đang tải và giải thích lỗi đọc không có nghĩa dữ liệu đã bị xóa. Retry chỉ đọc lại dữ liệu, không tạo đơn hoặc chạy mutation.
- Khi chỉ metadata combo lỗi, vẫn xem được snapshot đơn đã lưu nhưng không có quyền sửa/rút/lưu trữ chưa được xác minh.
- Dialog đang mở giữ input, dùng capabilities mới nhất; mất kết nối hoặc quyền thay đổi thì không gửi thao tác bằng metadata cũ. Không dựa riêng vào UI để thay thế backend guard.
- Loại bỏ refetch trùng cùng query ở Leads/Contracts. Copy hành trình/empty-state không còn hứa được tùy chỉnh trực tiếp nguyên gói FixedPrice: chọn dịch vụ lẻ hoặc combo, muốn đổi gói thì trao đổi với NCC.

### 2. Thỏa thuận riêng và bước báo giá/hợp đồng nhất quán

- Sau gửi/rút/đồng ý/từ chối đề xuất riêng, invalidation chính xác các lifecycle reads và sibling workspace cùng actor/target. Query đang active refetch thật; workspace vừa nhận response không bị refetch ghi đè form.
- Không xóa giỏ, sửa public catalog, snapshot, giá/cọc/payment/commission hoặc budget cache.
- Projection dùng cùng hàm kiểm tra với command guard hiện có. PendingCustomer chặn Accept/Confirm; accepted nội dung mới hơn tài liệu yêu cầu NCC phát hành lại hồ sơ.
- API thêm nullable `canAcceptQuote`, không đổi ý nghĩa `canRespondQuote`: **khách vẫn được từ chối báo giá** trong thời gian chờ nội dung. Với hợp đồng, yêu cầu chỉnh sửa/dừng/đề nghị phối hợp vẫn theo quyền cũ.
- Thời điểm phát hành hợp đồng đối chiếu đúng linked Quote.CreatedAt hoặc verification window hiện có, không dùng CreatedAt của draft tái sử dụng. Không tự suy đoán snapshot/revision lịch sử.
- Batch read đề xuất theo order/customer/vendor, không query mỗi card; missing linked quote fail closed ở quyền Confirm.

### 3. Hoàn thiện dependency open-category bị ngắt

- VendorPartnershipPolicy xem mọi nhóm active như thông tin tham khảo từ API, tách rate cũ và acknowledgement cũ. Membership không còn bị trình bày như quyền tạo dịch vụ hoặc cần Admin cấp nhóm.
- Giữ loading/error/retry/empty riêng, không dựng rate hay giả bằng chứng Vendor đã duyệt/chấp nhận mọi nhóm.
- Thay hai expected-throw membership tests đã superseded bằng bốn positive regression (missing/unrelated membership × inherited/legacy rate). Vẫn kiểm canonical category/group/rate/provenance, idempotency, planner và bất biến hợp đồng/payment/snapshot cũ; giữ mọi guard inactive/ownership/A–Z.
- Không sửa command tài chính để làm test xanh; không schema hoặc migration mới.

## File thay đổi của lượt này

Frontend:

- `client/src/features/journey/customerOrders/OrderReadError.tsx` **mới**.
- `client/src/features/customer/components/CustomerLeads.tsx`, `MyContracts.tsx`, `CustomerOrderActionDialog.tsx`.
- `client/src/features/journey/quotes/MyQuotes.tsx`, `journey/combo/MyCombos.tsx`, `journey/customerOrders/customerOrderApi.ts`.
- `client/src/features/private-agreements/privateAgreementApi.ts` (đã untracked từ trước), `privateAgreementCacheModel.ts` **mới**.
- `client/src/features/customer/journeyPolicy.ts`, `client/tests/account-journey.test.mjs`.
- `client/src/features/vendor-portal/components/VendorPartnershipPolicy.tsx`, `vendorJourneyModel.ts` (copy giữ chính sách cũ).
- **Mới:** `client/tests/customer-order-read-state.test.mjs` (20 SSR cases), `service-category-policy.test.mjs` (9 SSR cases), `private-agreement-cache.test.mjs` (QueryClient/QueryObserver thực). Tái sử dụng private-service-agreement tests cũ.

Backend/tests:

- `src/Wedding.Application/Features/CustomerOrders/CustomerOrderJourney.cs`.
- `src/Wedding.Application/Features/PrivateServiceAgreements/PrivateAgreementContractHandoff.cs` (đã untracked từ trước).
- `tests/Wedding.Tests/CustomerPrivateAgreementCapabilityTests.cs` **mới**, có SQL test riêng.
- `tests/Wedding.Tests/ContractCategorySnapshotTests.cs`.

Docs: báo cáo mới này, living handoff, master/module checklist, Copilot start prompt. Không sửa AGENTS/Copilot instruction rules hoặc decision history. Sáu source/test files mới phải đi cùng worktree; còn rất nhiều untracked có trước, không chỉ copy sáu file này.

## Kiểm chứng mới trên source tích hợp

### Frontend

Chạy trong `client`:

```powershell
npm.cmd test
npm.cmd run lint
npm.cmd run build
```

**331/331 PASS, 0 fail/skip**; lint exit0, **13 warnings hiện hữu**; TS/Vite build PASS. Build vẫn có warning LoginForm được import cả static/dynamic. Rerun sau sửa copy journey cũng PASS. Full run đầu có một assert copy journey cũ; đã sửa copy giữ đúng quyết định legacy, không bỏ/weaken test.

SSR render actual pages/OrderUi/Card/Row (chỉ stub data hooks và closed leaf dialogs) kiểm initial/stale error, loading, true-empty, success, readonly combo khi metadata fail, retry, Accept vs Reject capability. QueryObserver tests kiểm actual invalidation/refetch và preservation. Không coi SSR/QueryObserver là browser E2E.

### Backend/API build và SQL

```powershell
$env:WEDDING_TEST_SQLSERVER='1'
dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --artifacts-path artifacts/customer-business-20261021/test-build --results-directory artifacts/customer-business-20261021/test-results --logger 'trx;LogFileName=backend.trx' --verbosity minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1 RunConfiguration.MaxCpuCount=1
dotnet build src/Wedding.WebApi/Wedding.WebApi.csproj --artifacts-path artifacts/customer-business-20261021/api-build --verbosity minimal
```

Full BE **1073/1073 PASS, 0 fail/skip**, actual SQL; TRX start **13:28:46**, finish **13:30:27 VN 21/10/2026**. API build **0 warnings / 0 errors**. Test compilation có **4 xUnit2031 warnings cũ** ở VendorBusinessGroupTests. Targeted trước full: capability/private/lifecycle **56 PASS + 3 SQL SKIP**, category contracts **50 PASS + 1 SQL SKIP**; các skip targeted đã được full SQL run bao phủ, không dùng targeted làm SQL proof.

SQL test mới fixed localhost/integrated, GUID `WeddingPlanTest_PrivateCapabilities_*`, pre-existence check và guarded owned-only cleanup; không nhận connection string ứng dụng. Kiểm pending→rejected, accepted→reissued quote, confirm hold, scope isolation và không ghi tài chính/snapshots. Test DB unique mới là nơi migrate; **không kết nối/migrate DB ứng dụng**.

Source fingerprint theo `Get-SourceFingerprint` trong `tools/Test-ReadyToDeploy.ps1` (chỉ lấy hàm hash, không chạy release/deploy script): `03D12651678FE93DEC14687600E66963C71F215DBC7A66AD45CFFCBDCDA531C1`. Docs/artifacts/private/uploads không nằm trong hash. Không có readiness/release certification mới.

### Targeted browser QA

Computer-use skill được dùng để kiểm trực tiếp browser, không thay bằng DOM mock. Actual SQL fixture GUID riêng `WeddingPlanTest_Review4CRepair_077b61c4a1c846d5ac4b33b704679223`, 6 actors hư cấu, không dùng tài khoản/dữ liệu thật. API5023/Vite5183, explicit SqlServer test connection, migrate/seed=false, process override `VITE_API_URL`/proxy để không dùng `.env` API5008.

Đã xác minh trực tiếp:

- Customer login đúng portal, mở nhóm hợp đồng đã lưu (4 contracts thuộc2 orders).
- Dừng đúng API QA, bấm Làm mới: sau retry thất bại, trang hiện thông báo lỗi + Thử lại, không false empty/deleted/cards/pagination. Khởi động lại đúng test API với cùng signing key, bấm Thử lại: busy disabled rồi khôi phục đúng codes/value/status cũ, không login lại trong lần thử này.
- Lần khởi động QA đầu vô tình đổi ephemeral signing key, session cũ bị từ chối và redirect login đúng guard. Đã đăng nhập QA lại và lặp toàn bộ offline→retry→restore bằng key QA ổn định để có bằng chứng hợp lệ; không sửa session/auth code hay dùng lần key-rotation làm recovery proof.
- Logout Customer/login Vendor vào đúng vendor dashboard; tạo form mới hiện đủ8 category trong5 nhóm. Shop fixture chỉ có membership Photo/Video vẫn chọn được Venue3%, A–Z12%; có cảnh báo legacy8% chưa tự chuyển, A–Z chỉ approved management fee. **Không submit/lưu dịch vụ**, không tạo account, ký hoặc xác nhận hợp đồng.
- Vendor policy view tải đủ5 nhóm global, phân biệt legacy/acknowledgement; signup Vendor không có group selector, giữ generic acknowledgement.

Ảnh thật trong ignored artifacts (không chụp dữ liệu thật):

- [Lỗi đọc và thử lại](../../artifacts/customer-business-20261021/browser/customer-contract-read-error.png).
- [Khôi phục hợp đồng](../../artifacts/customer-business-20261021/browser/customer-contract-read-recovered.png).
- [Category/group và bảo toàn legacy](../../artifacts/customer-business-20261021/browser/vendor-category-legacy-policy.png).

Đây là **targeted browser QA**, không phải full E2E: chưa browser-test submission/moderation open-category, pending/accepted/reissued private agreements, race/dialog refresh hoặc mobile toàn luồng. Các phần đó chỉ có automated/API-handler/SQL proof tương ứng trong lượt này. Runtime hiện warning EF ReviewTargetType default/sentinel và MediatR development license; không coi là build errors hoặc tự đổi schema/licensing.

## Giới hạn và ưu tiên tiếp theo

1. Full browser của luồng riêng nhiều Lead, chat reconnect/rejoin, pending/accepted/reissue và dialog đang mở gặp refetch vẫn cần extended QA; unit/SQL/SSR không được đổi nhãn thành full E2E.
2. Có **inconsistency cần audit và người dùng xác nhận trước sửa**: `BudgetItem.Create(actualCost > 0)` đang đánh dấu IsPaid, còn nhánh cập nhật `SynchronizeContract` chỉ khi paid >= total. Có thể hiển thị “đã thanh toán” sau mới cọc ở nhánh tạo mới. Lượt này **không thay financial/planner rules**, không tính lại dữ liệu cũ. Đã gửi câu hỏi user về tách “đã chi” với “trả đủ”; chưa có trả lời lúc chốt. Sau xác nhận cần regression deposit/partial/full/cancel, giữ original ledger.
3. Music upload/save/play/mobile/restart, auth recovery/provider-dependent flows, extended reviews/keyboard/mobile và MoneyInput vẫn theo checklist, chưa nghiệm thu hết.
4. Advanced Commission/Negotiation/Partnership Contract/contract-specific commission adjustments và settlement differences **chưa hoàn thành**; không mở trong lượt này, giữ các quyết định tài chính đã chốt. Không coi private Customer–Vendor content agreement là commission adjustment.
5. Main migration16 chưa áp là thông tin handoff trước, không phải query main mới. Host/provider/backup-restore/load100/cross-origin/release package chưa kiểm lại; không báo sẵn sàng deploy.

## Git và cleanup cuối

13:45 VN21/10: branch/HEAD giữ nguyên; **98 tracked modified / 82 expanded untracked / 0 staged / 1 worktree**, diff-check exit0 (LF→CRLF warnings). So với đầu90/75, có8 tracked paths mới dirty, sáu source/test mới untracked và báo cáo mới; nhiều file chỉnh sửa đã dirty/untracked từ trước. Không coi tất cả98/82 là code của lượt này.

Đã đóng tab QA riêng, không đổi viewport hoặc đóng tab người dùng. Dừng đúng test API sessions75690/89626/69563/64899 và Vite85443 (Ctrl-C + confirm terminate batch); không kill process khác. Guarded harness drop **đúng owned DB** nói trên lúc13:45:03VN sau xác minh name/GUID/marker/6 actors; không active fixture manifest hoặc listener5023/5183. Chỉ dữ liệu giả lập disposable bị xóa; closed manifest, ảnh/media và test artifacts giữ lại. Không xóa user data, untracked hoặc DB thật. Không command build/test/server của lượt này còn chạy.

Không process main, cấu hình SQL, hệ thống hay production nào bị thay đổi để làm test xanh. Chưa restart API main nên giao diện frontend mới không tự chứng nhận BE binary main đã cập nhật; không bare startup vì Development có ApplyMigrations=true.
