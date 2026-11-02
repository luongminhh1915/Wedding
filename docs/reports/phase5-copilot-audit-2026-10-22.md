# Phase 5 Checkpoint 2 — audit Copilot, sửa lỗi và bàn giao

Ngày22/10/2026, Asia/Ho_Chi_Minh. Người dùng yêu cầu kiểm tra hướng Copilot và sửa phần chưa ổn; sau đó đổi yêu cầu sang hoàn thiện tài liệu cho agent khác. Source fixes bên dưới hoàn tất **trước** docs-only; không mở thêm tính năng sau đổi phạm vi.

## Kết luận

Hướng policy/basis/adjustment append-only, pin revision và không tác động ledger là nền tảng hợp lý. Chưa đủ điều kiện gọi Phase5 hoàn tất hoặc bật công thức mới. Đã sửa các gaps cụ thể, không rewrite resolver/contract/payment/settlement/legacy snapshots. Phase6/7/UI/deploy không được triển khai trong lượt này.

## Những lỗi/gaps đã sửa

1. **Rate null:** non-Combo domain cho phép null, trong khi SQL TargetScope cần Rate NOT NULL. Nay reject trước persistence, có domain/handler regression.
2. **Rate precision:** policy/basis/proposed adjustment rate có thể vượt decimal(5,4), bị SQL tự làm tròn. Nay reject giá trị không biểu diễn chính xác, không tự chọn rounding mới; bounds3–12% giữ nguyên.
3. **Pending không thể reject khi target stale:** policy/basis trước kiểm target active cho cả approve lẫn reject. Nay approval vẫn validate đầy đủ, rejection chỉ cần đúng Admin/version/state/reason và lưu history.
4. **Adjustment kẹt sau payment/hold/dispute:** approval vẫn kiểm eligibility và CommissionVersionSnapshot, nhưng authorized rejection được đóng stale request để có thể gửi lại khi đủ điều kiện. Không tự resolve hold hay sửa nghĩa vụ.
5. **Target drift và A–Z classification:** Service policy kiểm Vendor/account/role; basis kiểm VendorOwner thật. Category/group A–Z phải AzManagementFee; ordinary phải StandardService tại các scope biết category. Combo policy duyệt lại target/kind của từng covering Approved basis, không chỉ tin status cũ.
6. **Input exclusions:** null/blank/invalid JSON được DomainException; normalized/deduplicated/escaped JSON giới hạn12000 ký tự theo application mapping. Migration SQL thực tế dùng nvarchar(max), không tuyên bố đã chứng minh SQL truncates trường này.
7. **Foreign metadata:** Vendor request kiểm contract ownership trước khi đọc eligibility chi tiết; contract foreign/missing không lộ trạng thái tranh chấp/nghĩa vụ qua error message.

## Files thực sự sửa/thêm trong audit code

Sáu source files (đã untracked từ bàn giao Copilot, phải bảo toàn cùng worktree):

- `src/Wedding.Domain/Entities/CommissionPolicyRule.cs`
- `src/Wedding.Domain/Entities/CommissionComponentBasisApproval.cs`
- `src/Wedding.Domain/Entities/CommissionAdjustmentRequest.cs`
- `src/Wedding.Application/Features/CommissionPolicies/CommissionPolicyFeature.cs`
- `src/Wedding.Application/Features/CommissionPolicies/CommissionComponentBasisFeature.cs`
- `src/Wedding.Application/Features/CommissionPolicies/CommissionAdjustmentFeature.cs`

Tests: sửa `CommissionAdjustmentEligibilityTests.cs`; thêm `CommissionAdjustmentRatePrecisionTests.cs`, `CommissionComponentBasisDomainGuardTests.cs`, `CommissionComponentBasisHandlerGuardTests.cs`, `CommissionPolicyWorkflowGuardTests.cs`, `CommissionPhase5SqlVerificationTests.cs`, `CommissionSqlTestDatabase.cs` tại `tests/Wedding.Tests/`.

Không sửa frontend, ApplicationDbContext, mappings, migration17/designer/model snapshot hoặc current financial resolver trong audit này. Những diff của các file đó đã có trước, không quy toàn bộ worktree cho lượt này.

## Kiểm chứng đã hoàn thành

Build/test output riêng: `artifacts/phase5-copilot-audit-20261022/`. Test process đặt `WEDDING_TEST_SQLSERVER='1'` rồi restore giá trị trước đó; không nhận connection string từ biến này trong fixture mới.

```powershell
dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --artifacts-path artifacts/phase5-copilot-audit-20261022/build --filter 'FullyQualifiedName~CommissionPolicy|FullyQualifiedName~CommissionComponentBasis|FullyQualifiedName~CommissionAdjustment|FullyQualifiedName~CommissionPhase5' --results-directory artifacts/phase5-copilot-audit-20261022/tests --logger 'trx;LogFileName=phase5-targeted-final.trx'

dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --no-build --no-restore --artifacts-path artifacts/phase5-copilot-audit-20261022/build --results-directory artifacts/phase5-copilot-audit-20261022/tests --logger 'trx;LogFileName=backend-full.trx'
```

| Evidence | Kết quả mới |
|---|---|
| Targeted,00:28:29–00:28:51+07 | **62/62 pass,0fail,0skip**; gồm19 `[SqlServerFact]` methods (một số methods kiểm nhiều fixtures/holds) |
| Full backend,00:29:03–00:30:12+07 | **1135/1135 pass,0fail,0skip** |
| Domain/Application/Infrastructure/WebApi/test build | PASS;4 xUnit2031 warnings cũ ở VendorBusinessGroupTests; không còn4 warnings mới của test audit sau sửa assertions |
| SQL verification | Actual SQLServer16; freshlocalhostGUIDownedDB; upgrade16→17, idempotent script2lần, financial rows unchanged, constraints/uniqueness, guarded Down refusal, immutable records, concurrency/version/roles/ownership/holds/coverage |
| Model | `HasPendingModelChanges()` false trong SQL verification; không chạy lại EF CLI riêng hoặc áp model lên main |
| API permission coverage | Controller role metadata tất cả11actions + actual handlers/SQL guards. **Không** fresh HTTP server/JWT pipeline/browser E2E |
| Frontend | Không thay đổi; không rerun.392/392/lint/build21/10 là lịch sử, không phải result mới |

Lượt targeted đầu:56pass/6fail/62; lỗi testfixtures (ngày cưới tương lai và detached entities sau transaction rollback), đã sửa test setup/reload và giữ assertions. Test log đầu `phase5-targeted.trx` giữ nguyên. Lượt đầu có4 analyzer warnings mới, đã sửa trước build/finalrun. Không đổi quy tắc CompleteContract hoặc nới assertions để pass.

SHA256 TRX:

- targeted-final: `F4B0D31EE4C88AB3F57F31F460931BB07F1D7B77BE42101D957C3C1E25BB3F15`
- backend-full: `76B9F41BA1240085ADC22FB4180A3D5B344C671259FBBED763B1A9691F40628A`

Backend source fingerprint: **405 files**, `867D9D23374B8F9FF6D3130CDB97985C710FD5B356B06D0EA17A6D5D907B60BD`. Thuật toán: `git ls-files --cached --others --exclude-standard`, chọn `(src|tests|tools/Wedding.LocalDataBackup)/**/*.cs|*.csproj` và `global.json`, sort unique paths bằng PowerShell Sort-Object, mỗi dòng `<relative path> TAB <uppercase SHA256(file)>`, joinLF+trailingLF, UTF8, SHA256. Không gồm docs, frontend hoặc private config; đây không là release-package fingerprint. Source không đổi sau gate trong lượt docs-only.

## DB và an toàn

- Read-only probe chỉ localhost/master kiểm phiên bản/quyền tạo DB test. New fixture fixed localhost + IntegratedSecurity + GUID prefix `WeddingPlanTest_Commission5_`, kiểm target/preexistence và chỉ cleanup DB nó tạo. Test DB/data synthetic đã cleanup; không ảnh hưởng dữ liệu khách.
- Migration17 đã được chạy/kiểm **trên test-owned DB**, không còn đúng nếu viết “chưa từng áp bất kỳ DB nào”. Application/production migration state không query/verify; không chạy API main, apply/reseed/restore/sửa dữ liệu thật.
- Full suite có SQL fixtures hiện hữu của test sở hữu; không chạy legacy hardcoded UI QA scripts, không đổi service/RAM/TLS/ports, không kill processes không rõ chủ.
- Không commit/push/reset/clean/deploy, không mua host/DNS, không ghi secret/PII thật vào docs.

## Chưa xong và dependencies

Approved policy **chưa vào ConfirmContract resolver**; runtime vẫn legacy ResolveRate/Split trên ContractValue. Basis descriptions không phải verified numeric VAT/sponsor/pass-through/management-fee values. Điều kiện percomponent hiện là policy-approval guard, chưa là new-booking financialengine gate.

Thiếu numeric basis/evidence approval/new financial snapshots; scoped effective/expiry resolver; signed partnership linkage/activation; negotiation; private partnership docs; effective adjustment/delta/manualFinance evidence và UI. Adjustment request chưa có new amount/delta/effective scope; Vendor self-history read endpoint còn thiếu. No autooffset/refund.

Thứ tự hợp lý: Phase5 dormant foundation/calculation/snapshot design → Phase6 → Phase7 signed linkage → integrate/verify activation đủ ba điều kiện. Không bỏ signed gate để hoàn thành Phase5 trước Phase7. Các câu hỏi remaining trong [roadmap](../PRODUCT_COMPLETION_ROADMAP.md), không hỏi lại những quyết định đã confirmed.

## Đổi phạm vi sang bàn giao tài liệu

Sau user yêu cầu docs-only, tạo [START_HERE](../START_HERE.md), [functional spec](../PRODUCT_FUNCTIONAL_SPEC.md), [architecture/operations](../ARCHITECTURE_AND_OPERATIONS.md), [completion roadmap](../PRODUCT_COMPLETION_ROADMAP.md); cập nhật Living Checkpoint/master checklist/Copilot start/auth note để sửa trạng thái cũ, giữ history. Snapshot đường dẫn worktree tại [manifest](handoff-worktree-2026-10-22.json); source phải chuyển cả untracked, không chỉ HEAD.

Bộ docs phân biệt source behavior, confirmed rules, current/historical verification và gaps. Không sửa Master Prompt gốc hoặc tạo quyết định tài chính mới. Sau tài liệu dừng; không tự thực hiện next tasks.

Git cuối: `weddy-journey`/HEAD không đổi,105tracked modified/123expandeduntracked/0staged/1worktree; baseline104/111 gồm nhiều thay đổi có sẵn. Read-only master count xác nhận0 DB prefix Commission5 test còn lại, không tự cleanup DB lạ. Không command/agent phụ còn chạy. Trạng thái automation lịch sử chưa kiểm; không nói lịch đã tắt. Markdown/docs-only không làm phát sinh yêu cầu rerun BE/FE suites.

Docs verification:11 Markdown files mới/cập nhật,114 relative links,0missing; JSON manifest parse và path inventory khớp Git. `git diff --check` exit0,65 LF→CRLF notices hiện hữu, không lỗi whitespace. Backend405-file fingerprint sau docs vẫn khớp gate; không sửa source để làm tài liệu có vẻ hoàn tất.
