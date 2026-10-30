# Phase 4C — Kiểm chứng SQL, API và browser ngày 20/10/2026

## Kết luận và phạm vi

**IMPLEMENTED; SQL migration verification đạt trong DB test; targeted browser QA đã thực hiện nhưng có lỗi, chưa nghiệm thu toàn Phase 4C.** Không bắt đầu Phase 5–7. Lượt này không sửa production code, không thay quyết định nghiệp vụ, không kết nối/migrate DB ứng dụng hoặc production, không sửa hợp đồng/thanh toán thật, không commit/push/reset/clean.

Trạng thái migration DB ứng dụng “chưa áp” là thông tin bàn giao của người dùng; lượt này không truy vấn DB đó để xác minh độc lập hoặc phát hiện thao tác của tác nhân khác. Bằng chứng SQL mới chỉ chứng nhận các DB test nêu bên dưới.

Đã đọc AGENTS, Copilot adapter, handoff, master checklist và decision `2026-10-20-independent-reviews.md`; đối chiếu source/diff/untracked, chia audit backend/frontend/migration độc lập. Số liệu bàn giao không được coi là kết quả chạy mới.

## Git và bảo toàn source

- Đầu lượt: `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`; 60 tracked paths thay đổi, 41 untracked files, 0 staged, 1 worktree. Các thay đổi này gồm công việc có sẵn, không quy hết cho Phase 4C.
- Thêm duy nhất một file kiểm thử source: `tests/Wedding.Tests/IndependentReviewSqlVerificationTests.cs`. Các thay đổi khác của lượt này là tài liệu; harness/build/TRX/screenshots nằm trong `artifacts/` ignored.
- Fingerprint đầu lượt: `BCF223C9D6A99F99B2FA9267BF4A2FB1E37E5A6D1C78A7A575A43AC53ABC699C`.
- Fingerprint sau thêm test: `DAB981696337E3E0B33B205FDEFD514EADC96098432DB12B33FC682268FEBF4C`. Tính lại cùng thuật toán nhưng loại đúng file test mới cho kết quả bằng fingerprint đầu lượt: toàn bộ source/config/test có sẵn được giữ nguyên. Docs/artifacts/data không nằm trong fingerprint này.
- Kiểm cuối lúc20:29:30 UTC+7: 60 tracked modified/43 untracked/0 staged/1worktree, branch/HEAD không đổi. `git diff --check` exit0; còn28 cảnh báo LF→CRLF từ working tree, không có lỗi whitespace/encoding hay missing relative links trong tài liệu đã kiểm. API5021/Vite5181 không còn listener. Phải kiểm lại khi tiếp quản, không ép worktree về số liệu cũ.

## Kết quả kiểm chứng mới

| Phạm vi | Kết quả và giới hạn |
|---|---|
| API build | PASS, 0 warning/0 error; output riêng `artifacts/review4c-verification/api-build`, không ghi đè API đang chạy của người dùng. |
| Backend targeted | **83/83 PASS, 0 fail, 0 skip**, có actual SQL Server; 20:10:41–20:11:20 UTC+7. Gồm ReviewTests, CompletedReviewPolicyTests, ComboTemplateLifecycleTests và SQL verification mới. Không phải full suite 950. |
| Migration trên DB test | PASS fresh install, 15→16, script idempotent chạy hai lần, Migrate replay, model drift, preservation, constraints và guarded Down trên fixture riêng. Không áp migration lên DB ứng dụng. |
| HTTP API scripted | **18/18 checks PASS** trên API QA: actor/ownership/foreign denial, Completed hai component, partial/legacy exclusion, stable ComboOrder Shop key, duplicate target, pending-public isolation và validation. |
| Browser Customer | Đăng nhập hư cấu, eligibility thực, gửi Service/Shop/Combo qua UI; edit request qua UI, bản Approved giữ trong khi Pending. Shop/Combo thiếu tên được tái hiện. |
| Browser Moderator | Duyệt ba review mới qua UI; từ chối edit qua UI giữ bản cũ; duyệt edit thứ hai qua UI xuất bản nội dung mới. Edit thứ hai được tạo qua API để kiểm response count, không gọi toàn bộ chuỗi này là E2E thuần UI. |
| Public browser/API | Service, Shop, Combo panels/list/summary thực; trước sửa lần hai điểm độc lập Service4/Shop3/Combo5; sau edit Service2, Shop/Combo không đổi. Bộ lọc 5 sao trên Service2 trả empty hợp lệ trong khi summary vẫn1. Chưa dataset đủ lớn để nghiệm thu page/sort boundary. |
| Full FE/BE trước đó | FE252/build/lint, BE924 pass +26 SQL skips/950 là **lịch sử**, không chạy lại full suite chỉ để sửa tài liệu. Không nhận full suite mới xanh. |

Lệnh mới:

```powershell
dotnet build src/Wedding.WebApi/Wedding.WebApi.csproj --artifacts-path artifacts/review4c-verification/api-build --verbosity minimal

$env:WEDDING_TEST_SQLSERVER = '1'
dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --artifacts-path artifacts/review4c-verification/test-build --results-directory artifacts/review4c-verification/test-results --logger 'trx;LogFileName=targeted.trx' --filter 'FullyQualifiedName~IndependentReviewSqlVerificationTests|FullyQualifiedName~ReviewTests|FullyQualifiedName~CompletedReviewPolicyTests|FullyQualifiedName~ComboTemplateLifecycleTests' --verbosity minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1 RunConfiguration.MaxCpuCount=1

& artifacts/review4c-qa/Verify-Review4CApi.ps1
```

TRX: `artifacts/review4c-verification/test-results/targeted.trx`. API script là harness một lượt, dùng ID fixture đã tạo; muốn chạy lại phải tạo fixture mới và cập nhật ID tương ứng, không trỏ sang API ứng dụng.

Warnings: 4 xUnit2031 hiện hữu khi build test; API runtime QA có EF20601 về enum TargetType/default/sentinel. Đây chưa phải lỗi insert đã tái hiện. HTTP harness ban đầu kỳ vọng 400 cho foreign order nhưng API trả 404 an toàn; sửa assertion thành đúng 404 rồi 18/18 đạt, không sửa handler. Fixture ban đầu có lỗi kiểu IEnumerable/IReadOnlyList và thiếu fixedPackage flag cho hai nguồn cùng category; chỉ sửa harness, không source sản phẩm.

## SQL isolation và migration

SQL test chỉ coi `WEDDING_TEST_SQLSERVER` là opt-in, không nhận connection string tùy ý. Kết nối cố định localhost/Integrated Security, tên GUID mới `WeddingPlanTest_Review4C_*`, kiểm prefix/InitialCatalog và DB chưa tồn tại trước create/migrate/drop. Cleanup chỉ DB do chính test tạo.

Test mới thực hiện:

- Migrate tới15, tạo legacy Service/consultation review, ComboOrder cũ không revision, synthetic contract snapshots/partial commission/payment event; chụp mọi bảng/cột cũ theo PK.
- Chạy script SQL Server idempotent 15→16 hai lần; Migrate replay; không pending model changes. So sánh toàn bộ dữ liệu cũ, chỉ loại ba cột Vendor rating cache đã được quyết định đổi sang Shop-only và EF history; kiểm riêng cache reset.
- Giữ nguyên ID/content/photos/status/verified flags/reply/timestamps cũ, không tự điền revision/snapshot cho legacy order.
- Thực thi năm uniqueness keys, shape check/FK, edit sequence/Pending/Approved uniqueness; kiểm EF từ chối sửa/xóa history và sửa snapshot.
- Bốn unsafe Down guards phải từ chối và giữ migration/data; safe Down/Up chỉ trên fixture test sau khi gỡ đúng các rows/sentinels mới do test tự tạo. Không chạy Down trên DB ứng dụng.

Browser dùng DB mới `WeddingPlanTest_Review4CUi_20261020_5e92d2a1488d4accbb0df255a18d1d21`, chỉ bốn actor hư cấu, API5021/Vite5181. Fixture có order đủ hai Completed components, order thiếu snapshot, order còn một component Confirmed, và custom order không template. Revision fixture mới được biết chính xác khi tạo; không backfill đơn lịch sử của ứng dụng. Khi kết thúc đã dừng đúng hai process, đóng tab QA và xóa đúng DB fixture có identity guard; SQL test tự cleanup DB riêng. Không đụng tab/process/DB của người dùng. Dữ liệu test bị xóa không cần phục hồi; source harness, TRX và ảnh QA vẫn giữ.

## Lỗi đã tái hiện — chưa sửa trong phạm vi kiểm chứng

### P1 — Sửa nội dung/điểm chung làm mất ảnh và điểm chi tiết

`CustomerReviewCenter.tsx:44` chỉ gửi rating/content; optional photos/criteria thành null trong edit request và `Review.ApplyApprovedEdit` thay toàn bộ After fields. Với review fixture có ảnh + criteria5/4/3/4: Pending và Rejected giữ đúng bản cũ, nhưng khi approve bản sửa chỉ text/rating thì PhotosJson và cả bốn criteria thành **null**. Moderator chỉ thấy before/after text/rating, không được xem proposed photos/criteria; DTO pending edits cũng thiếu After fields đó.

Phạm vi file: `client/src/features/customer/CustomerReviewCenter.tsx`, `client/src/features/admin/ReviewModerationQueue.tsx`, `src/Wedding.Application/Features/Reviews/DTOs/ReviewDtos.cs`, `IndependentReviewFeature.cs:239`, `src/Wedding.Domain/Entities/Review.cs:143`. Cần regression cho semantics omitted/preserve vs explicit clear, đầy đủ comparison và user controls; không âm thầm đổi aggregation/weighting để chữa.

### P2 — Custom ComboOrder không xuất hiện cơ hội đánh giá Shop

Fixture custom order có Completed contract, không public template: GET eligible-targets trả **0 Shop opportunity**, trong khi POST shop cùng ComboOrderId trả **200** hợp lệ; POST Combo vẫn từ chối400. Query tại `IndependentReviewFeature.cs:188` lọc ComboTemplateId.HasValue, còn standalone Shop branch loại mọi Combo component. Đây là thiếu discovery/UI opportunity, không phải quyền review vào template khác.

### P2 — Tên Shop/Combo trống trong danh sách của khách

API mine trả listingTitle rỗng dù comboTemplateName/vendorBrandName có giá trị. `listingTitle ?? comboTemplateName ?? vendorBrandName` chọn chuỗi rỗng; browser card Shop/Combo không có tên. Tương tự edit queue theo source. Không phải thiếu dữ liệu target trong DB.

### P2 — Response bộ đếm lần sửa cao hơn thực tế

Lần gửi2 trả `editAttemptCount=3`, GET mine/pending queue và DB vẫn đúng2. EF relationship fixup đã đưa edit mới vào collection; `IndependentReviewFeature.cs:244` lại cộng1. Submission gate hiện vẫn dùng count thực, không có bằng chứng vượt giới hạn. Cần regression assertion response vs persisted count.

## Static findings / chưa tái hiện

- Approve/reply mới vẫn invalidates legacy listing/vendor query keys; public panels dùng target-reviews nên có thể không refresh ngay. Chưa browser-test own Vendor reply hoặc cache propagation.
- Reply controls hiện cho mọi VendorOwner; backend còn kiểm ownership. Đây là UX misleading, chưa thấy bypass quyền.
- Public Combo review query kiểm template activity/approval/dates nhưng không cùng dependency guard với catalog; cần chốt historical-review visibility trước khi thay chính sách, không tự ẩn/xóa review cũ.
- Edit approval chưa gọi dispute-evidence guard giống approve review gốc; chưa mô phỏng dispute trên browser, không sửa trạng thái hợp đồng để vá.
- Một số loading states/modal focus/Escape/target context trong queue còn thiếu theo source; chưa nghiệm thu mobile/keyboard/a11y, expiry đúng biên7 ngày, dataset pagination lớn hoặc separate-origin deployment.

## File thay đổi của lượt verification

- Mới: `tests/Wedding.Tests/IndependentReviewSqlVerificationTests.cs` và báo cáo này.
- Cập nhật trạng thái/evidence: `docs/AGENT_HANDOFF.md`, `docs/master-upgrade-checklist.md`, `docs/decisions/2026-10-20-independent-reviews.md`, `docs/COPILOT_START_PROMPT.md`.
- Không sửa `src/` hoặc `client/`; mọi thay đổi production đang dirty từ trước được bảo toàn.

Ảnh QA trong `artifacts/review4c-verification/screenshots/`: customer-submissions, moderator-edit-comparison, public-target-filter, public-combo-after-approval. Không chứa tài khoản/dữ liệu thật.

## Điểm tiếp tục và dừng

**Dừng sau kiểm chứng Phase4C.** Nếu người dùng giao sửa tiếp: ưu tiên P1 edit data loss/projection, custom Shop opportunity, tên target/count/cache; thêm regression tương ứng, rerun targeted SQL/API/browser rồi mới nghiệm thu. Cần phân biệt yêu cầu sửa bug với các câu hỏi policy về historical public visibility/dispute. Không dùng báo cáo này cấp quyền bắt đầu Commission, Negotiation, Contracts, deploy hoặc migration DB ứng dụng.
