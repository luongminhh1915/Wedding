# NCC chọn loại dịch vụ trực tiếp — checkpoint bị ngắt21/10

## Scope / business decision

Người dùng yêu cầu bỏ chọn nhóm/chiết khấu khi đăng ký; lúc tạo dịch vụ được chọn mọi loại đang hoạt động, hiển thị nhóm hoa hồng tương ứng. [Decision CONFIRMED](../decisions/2026-10-21-service-category-without-vendor-membership.md) chỉ supersede signup/membershipgate; không tự chuyển legacyrate, sửa original contract/payment/snapshot hoặc mở Phase5–7.

## Source đã làm, chưa gate đầy đủ

- SignupFE bỏ group selector/rates/summary/businessgroupsfetch; payload không có group/rate/role/policyVersion; giữ genericack, account/password/province/Customer và authcontinuation.
- SignupBE giữ BusinessGroupIds optionaldeprecated nhưng bỏqua để tương thích, không tạo membership hoặc tự gán toàn bộ nhóm. Vẫn reject clientCommissionRate, atomic user/vendor và genericackversion `2026-10-21-service-category-v1`; rate0sentinel kế thừa Category như trước.
- Categories/my trả mọi activeconfiguredCategory có activeparent, không dùng historicalmembership làm quyền; legacyfields giữ. Create/Existing helper bỏ membershipgate, vẫn giữ activecategory/parent và nullparentlegacyexisting compatibility; không ghi membership/rate/finance.
- ListingForm bỏ thông báo buộc Admingánnhóm; chọn loại và `ServiceCategoryPolicySummary` read-only group/rate/backendmetadata, cảnh báo legacy/A–Z và chính sách chưa tải. Danh mục cũ immutable. vendorCategoryModel/journeycopy/Admin groupeditorcopy đã cập nhật ý nghĩa metadata, không cấp quyền.
- **Chưa sửa xong VendorPartnershipPolicy.tsx**: còn copy/nhóm theo membership cũ; nên chuyển hiển thị allactivegroups như chính sách tham khảo từ API global, tách legacyrate/ackhistory. Không tự giả nhóm hoặc policyapproval.

## Files thực tế

Backend: `RegisterVendorCommand.cs`, `Domain/Entities/BusinessGroup.cs` (staticconst, không schema), `VendorBusinessGroupTests.cs`, `ListingCategoryAccess.cs`, `Categories/VendorCategoryEligibility.cs`, `VendorCategoryEligibilityTests.cs`.

Frontend: `RegisterForm.tsx`, `registrationValidation.ts`, `types/auth.ts`, `registration-validation.test.mjs`, mới `vendor-signup-no-groups.test.mjs`; ListingForm, mới `ServiceCategoryPolicySummary.tsx`, vendorCategoryModel, vendorJourneyModel, Admin/VendorBusinessGroupsEditor. Root chưa thêm SSRtests cho policySummary hoặc full frontendgate.

Docs: decision mới, supersede note decision19/10, handoff/checklist/Copilot prompt và báo cáo này. Không đổi MasterPrompt hoặc xóa businesshistory.

## Test evidence mới, không che giấu lỗi

- FE targeted: `node --test tests/registration-validation.test.mjs tests/vendor-signup-no-groups.test.mjs` trongclient: **12/12PASS**,0skip. Signupagentlint3sourcefilesexit0. Chưa fullFEbuild/lint/test/browser.
- SignupBE: `dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --artifacts-path artifacts/vendor-open-category/signup-agent --filter 'FullyQualifiedName~VendorBusinessGroupTests|FullyQualifiedName~AuthInputTests|FullyQualifiedName~AuthActorTests|FullyQualifiedName~AuthTests' --results-directory artifacts/vendor-open-category/signup-agent-results --logger 'trx;LogFileName=signup-agent.trx' --verbosity minimal`: **48PASS/0FAIL/2SQLSKIP,50total**. OptinSQLblank. FourexistingxUnitwarnings. TRXđãđọc độc lập.
- Categoryagent targeted TRX `artifacts/vendor-open-category/categories-agent-results/categories-agent.trx`: **99PASS/2FAIL/2SQLSKIP,103total**. Không chạySQL. Hai fail trong `ContractCategorySnapshotTests.InvalidCanonicalCategoryCannotConfirmOrWriteFinancialPlannerOrSnapshotData`, case`foreign-membership` và`missing-membership`, tại202: test còn mong throw khi thiếu membership, trong khi newpolicy không còn gate này. Chưa sửa test sauinterruption; phải đối chiếu decision, đổi thành regressioncho phép đúngcategory+rate/snapshot và giữ các invalidcategory/ownership tests, không chỉ xóa financialassertions để xanh.
- FullBE1051/FE296 ở checkpoint trước là **historical, không chứng nhận source mới**. Không API/browserQA mới hoặc mainmigration.

## Điểm dừng và tiếp tục

Lượt bị ngắt, categoryagent interrupted. Root chỉ đọc lại trạng thái/lưu checkpoint, không triển khai module khác sau ngắt. Không process command chứa vendor-open-category còn chạy lúc13:06VN; không tạo DB/fixtureserver hoặc kết nốiSQLmain trong lượt này. Branch`weddy-journey`,HEAD`3596ae832faeac95fb15cc44bc0e8e62351c788b`; baseline75tracked/71untracked/0staged/1worktree, trước thêm report90tracked/74untracked/0staged/1worktree. Giữ toàn bộ source/untracked trước đó; diffcheck0.

Khi useryêu cầu tiếp:

1. VerifyGit/diffs/source, đọc decision mới; không khôi phục membershipgate cũ vì test lịch sử.
2. Kết thúc policyviewcopy/references + test ServiceCategoryPolicySummary (servergroup/rate,legacy/A–Z/error/noinputs), rà remainingjourney/signupcopy.
3. Sửa hai testsuperseded ở ContractCategorySnapshotTests thành positivepolicyregression với financialsnapshot assertions phù hợp; không sửa contract/finance implementation để làm test xanh.
4. FullFEtest/lint/build, APIbuild outputriêng, targeted+fullBE actualSQL trên DBunique được guard. Không appstartup/migrationmain. Historicaltests có thể còn membershipassertions: audittrước chỉnh.
5. Browsernếuavailable: signupkhônggroup→owner dashboard→create types ở nhiềugroup/legacyempty memberships→displaycanonicalrates→save/reload/submit/moderate; negativeinactivecategory/shop/role. Sauinterruption không dùng session cũ làm proof.
6. Cập nhật checklist/report mới theo evidence. MainAPI đang chạy chưarestart; cầnconfigsafe/migratefalse sauauthorizationscope riêng, không barestartup.

**Kết luận: IMPLEMENTED phần lớn, INCOMPLETE integration, có2targetedfail; không báo hoàn thành hoặc deployready.**
