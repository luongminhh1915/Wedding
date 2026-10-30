# Phase 4C — Repair follow-up20/10, chốt bàn giao21/10/2026

## Phạm vi và điểm xuất phát

Theo yêu cầu tiếp tục song song và lưu handoff, chỉ sửa findings của [verification trước](phase4c-verification-2026-10-20.md); không bắt đầu Phase5–7, không deploy/commit/push/reset/clean, không migrate/reseed DB ứng dụng hoặc sửa lịch sử tài chính.

Git đầu lượt được kiểm độc lập: `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`,60 tracked modified/43 expanded untracked/0 staged/1 worktree. `git diff --check` exit0, có warnings LF→CRLF. Đây là worktree nhiều thay đổi sẵn có, không quy tất cả cho lượt repair.

## Phân việc không chồng lấn

- Backend: `IndependentReviewFeature.cs`, `ReviewDtos.cs` và regression file mới.
- Frontend presentation: CustomerReviewCenter, ReviewModerationQueue, review types/presentation model/tests.
- Root: hooks cache invalidation, ReviewList own-Vendor affordance, cache/transport tests, integration và toàn bộ handoff/checklist/report.
- QA: chuẩn bị harness ignored riêng; root kiểm target trước khi chạy. Không agent nào tự khởi động API trỏ cấu hình mặc định.

## Sửa lỗi và compatibility

- Edit optional fields null/omitted giữ photos và criteria cũ. `photosJson: "[]"` vẫn là cách chủ động xóa photo references đã có; không thêm null-clear criteria. Tiêu chí numeric được gửi thay tương ứng, bộ kết quả vẫn qua domain validation. Không thay weighting/average hoặc xóa file ảnh vật lý.
- Pending-edit DTO phải có đủ before/after photos và bốn criteria, Moderator so sánh được toàn bộ.
- Custom Completed ComboOrder phải xuất hiện Shop opportunity, không có Combo review nếu thiếu public template/version proof. Partial order vẫn bị chặn; stable order key và historical snapshot rules giữ nguyên.
- Attempt response phải khớp số lần persisted, không cộng hai lần do EF relationship fixup.
- Tên target chọn chuỗi không rỗng đúng loại; cache invalidation bao phủ independent panels, filter/page/session và legacy lists. Chỉ Vendor sở hữu review Approved thấy CTA trả lời; backend ownership vẫn authoritative.
- Hai câu hỏi policy đã được user xác nhận21/10: ẩn Combo reviews cùng Combo availability; giữ Pending edit khi có dispute. Dùng lại `ComboTemplateAvailability.ComponentsEligibleAsync` và `ReviewRules.HasDisputedEvidenceAsync`, không nhân đôi policy. Reject vẫn được phép, bản Approved/history không bị ghi đè khi hold. Xem [decision CONFIRMED](../decisions/2026-10-21-review-visibility-and-dispute-holds.md).

## Kết quả kiểm chứng cuối

**Các lỗi repair đã IMPLEMENTED và được kiểm chứng automated + targeted browser. Không chứng nhận toàn bộ Phase4/ứng dụng/deployment đã hoàn tất.**

| Gate | Kết quả mới, không thay bằng counts lịch sử |
|---|---|
| Frontend |266/266 PASS,0fail/skip; `npm.cmd test`20/10; lint exit0/13warnings cũ, TS+Vite build PASS. Không có FE edit sau gate đó. |
| Backend full cuối |**1004/1004 PASS,0fail/skip**, actual SQL;21/10 01:16:00–01:17:27 UTC+7. TRX `artifacts/review4c-repairs/final-test-results/backend.trx`. |
| Backend repair tests |14 cases trong IndependentReviewRepairTests;39 cases trong IndependentReviewPolicyGuardTests. SQL cases dùng fixedlocalhost+IntegratedSecurity, prefix+GUID mới, kiểm DB chưa tồn tại và chỉ cleanup owned target. |
| Migration |Existing IndependentReviewSqlVerificationTests và full schema tests đều PASS trong full run: migration15→16, idempotency, legacy preservation, guards. Không schema/migration mới trong repair. |
| API build |21/10 API build PASS0warning/0error trong `artifacts/review4c-repairs/api-build`. |
| HTTP repair |`artifacts/review4c-repair-qa/Verify-Review4CRepairApi.ps1` tất cả assertions PASS trên API5023. Eligibility/custom/partial/legacy, role+ownership, reply3targets, preservation/reject/approve/null/explicitclear/criteria/count/aggregates/duplicates. Đây không phải production API. |
| Browser |Customer custom-Shop + Combo submissions/target names, text/rating edit; native modal initial focus, Escape và return focus; Moderator exact before/after photo+4criteria và approve; public Service/Shop data; owning Vendor reply xuất hiện ngay không reload; foreign Vendor có0reply/0edit-reply CTAs. |
| Mobile targeted |Viewport390x844: comparison ảnh tải thành công (naturalWidth96), documentWidth375 không tràn viewport390. Không thay thế full mobile/a11y/browser suite. Override đã reset. |

Lệnh mới chính xác:

```powershell
# client/
npm.cmd test
npm.cmd run lint
npm.cmd run build

# repo root, SQL switch chỉ =1, không dùng app connection
$env:WEDDING_TEST_SQLSERVER='1'
dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --artifacts-path artifacts/review4c-repairs/final-test-build --results-directory artifacts/review4c-repairs/final-test-results --logger 'trx;LogFileName=backend.trx' --verbosity minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1 RunConfiguration.MaxCpuCount=1
dotnet build src/Wedding.WebApi/Wedding.WebApi.csproj --artifacts-path artifacts/review4c-repairs/api-build --verbosity minimal
```

Full run trước hai policy guards:965/965 PASS ngày20/10 21:01–21:02. Sau đó sửa một xUnit2031 trong test mới và thêm guards/tests; **1004 cuối là kết quả thay thế**. Final còn4 xUnit2031 cũ trong VendorBusinessGroupTests; lint13warnings cũ, Vite `INEFFECTIVE_DYNAMIC_IMPORT` của LoginForm; runtime EF20601 TargetType sentinel warning chưa gây insert sai trong coverage đã chạy.

Browser edit approved thật: Rating2, photos giữ1ảnh, criteria5/4/3/4, AverageRating vẫn4, attempt1/Approved/canEdit=false. Không “sửa” AverageRating thành2 vì đó là weighting hiện hữu. Bản so sánh Moderator có ảnh thật tải được ở cả hai phía, không chỉ URL mẫu hỏng.

Screenshot API trả `Unable to capture screenshot` ở cả native/tab capture, desktop/mobile; **không có screenshot mới hợp lệ**, không dùng ảnh lượt cũ làm proof mới. Evidence UI dựa AX/DOM read-only và API trên fixture thật; foreign CTA snapshot text giữ ở `artifacts/review4c-repairs/foreign-vendor-ui.txt`.

## Isolation, cleanup và bảo toàn

QA harness ignored: `artifacts/review4c-repair-qa/{Review4CRepairQa.csproj,Program.cs,Verify-Review4CRepairApi.ps1}`;6actors/2shops hư cấu, DB `WeddingPlanTest_Review4CRepair_9c5e7f3a640b40cca55913c203fa42d0`. Fixedlocalhost, GUID, DBnonexist và ownership-marker+identity guards trước cleanup. API explicit SqlServer/ApplyMigrations=false/SeedDemoData=false; Uploads roots chỉ artifacts. Script harness được sửa trước chạy: component contract phải bị deny như source đã chốt, Shop submit dùng ComboOrderId; không nới API để làm harness pass.

API5023(session51122)/Vite5183(session89495) đã Ctrl-C dừng đúng scope (exit1 do shutdown), không còn listener; tab QA đã đóng và viewport reset. CLI drop với guard thành công01:23:11VN21/10; chỉ dữ liệu test hư cấu bị xóa, media/manifest-closed/harness vẫn trong artifacts và có thể tạo lại fixture mới. SQL tests tự cleanup DB do test sở hữu. Không có command còn chạy từ lượt này.

DB ứng dụng không kết nối; migration16 “chưa áp” là trạng thái bàn giao, không phải truy vấn DB ứng dụng trong lượt này.

Final Git: branch/HEAD không đổi,60tracked modified/51expanded untracked/0staged/1worktree. Tám untracked mới của repair: hai models FE, hai tests FE, hai test files BE, report này và decision21/10. Các untracked/dirty khác đều được giữ; không quy toàn bộ cho repair. Source fingerprint từ đúng hàm Get-SourceFingerprint trong readiness script (chỉ invoke hàm, không chạy packaging script): `919B93B49AED954A3CC5722F67253B4F8F7B3198F750056A54FE76A3AB3EC0A1`. Hash không bao gồm docs/artifacts/private/data; không cấp chứng nhận package cũ cho source mới.

## File của lượt này

- BE: `src/Wedding.Application/Features/Reviews/IndependentReviewFeature.cs`, `DTOs/ReviewDtos.cs`; mới `tests/Wedding.Tests/IndependentReviewRepairTests.cs`, `IndependentReviewPolicyGuardTests.cs`.
- FE: CustomerReviewCenter, admin/ReviewModerationQueue, types/review.types, customer/hooks/useReviews, components/reviews/ReviewList; mới customer/reviewCacheModel, reviewPresentationModel và hai tests tương ứng; bổ sung review-transport tests hiện hữu.
- Docs: AGENT_HANDOFF, COPILOT_START_PROMPT, master checklist, independent-reviews decision (giữ history), decision21/10 và report này. Không sửa AGENTS/Copilot adapter/production configuration/schema/domain/contracts/finance của worktree có sẵn.

## Điểm tiếp cho Copilot

Không làm lại bốn findings đã sửa. Còn QA mở rộng: dataset paging/sort lớn, exact7-day boundary, simultaneous decisions, full keyboard/mobile/cross-origin; snapshot matching hardening audit chưa chứng minh bypass hiện hành. Catalog cards dùng keys khác chưa được invalidated ngay sau Shop rating change; không tuyên bố toàn bộ catalog refresh tức thời (review panels đã kiểm).

Hai policy guards có handler+actualSQL tests, chưa có browser tình huống đang tranh chấp/Combo bị tắt. Không lấy targeted browser làm full actor E2E/release acceptance. Chỉ làm tiếp phần được user giao; chưa áp migration DB ứng dụng, chưa package/release/deploy hoặc Phase5–7. Bản [handoff sống](../AGENT_HANDOFF.md) và [start prompt](../COPILOT_START_PROMPT.md) là điểm tiếp quản.
