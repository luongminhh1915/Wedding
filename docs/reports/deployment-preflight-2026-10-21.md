# Deployment preflight — weddingmatch.vn —21/10/2026

> **Cập nhật sau báo cáo này:** user đã đồng ý phương án trả phí, nhưng SmarterASP checkout Advance3tháng thực **25,50 USD**, không phải ước tính14,85 USD bên dưới (4,95/tháng chỉ khi36tháng). Đã hỏi duyệt lại mức mới; chưa tạo account/mua/DNS/upload/remoteSQL. [Báo cáo tiếp quản mới](paid-deployment-preparation-2026-10-21.md) và [decision](../decisions/2026-10-21-paid-deployment.md) supersede trạng thái ngân sách/checkout ở phần lịch sử này. Các kết quả local gate bên dưới giữ nguyên, không được coi là đã public.

## Trạng thái/phạm vi

**Chưa public deploy, chưa đăng ký/mua domain hoặc host.** User đổi ưu tiên từ budget UX sang “deploy lên trước”, mong muốn `weddingmatch.vn`, và xác nhận domain chưa đăng ký. Chờ user chọn/duyệt chi phí và tài khoản host; không suy quyền mua/accept terms/payment từ yêu cầu deploy chung. Không hỏi secrets qua chat.

Giữ kiến trúc hiện tại: React build cùng ASP.NET Core10/IIS, SQL Server riêng, private config/uploads nằm ngoài public document root; không chuyển SQLite, tách frontend/backend sang provider khác hoặc mở Phase5–7 để deploy. Không đưa DB local, user files, tokens, secrets hay demo accounts vào ZIP. Việc chuyển dữ liệu thật từ local lên host cần xác định đích, backup và phạm vi riêng; chưa thực hiện.

## Domain và host — thông tin kiểm tra mới, không phải đơn hàng

- **VNNIC WHOIS** tại [trang chính thức](https://vnnic.vn/whois-information/) đã hiển thị `weddingmatch.vn` / **“chưa cấp phát”** trong browser21/10. Kết quả là ảnh chụp thời điểm, không giữ chỗ/đảm bảo vẫn mua được. Không giải CAPTCHA xuất hiện sau kết quả. Ảnh chỉ vùng tên/trạng thái (không IP) giữ trong ignored `artifacts/deploy-preflight-20261021/weddingmatch-vn-whois.png`.
- [P.A bảng giá](https://www.pavietnam.vn/vn/bang-gia-ten-mien.html) đang công bố .vn năm đầu450.000đ (100.000 đăng ký +350.000 duy trì); gia hạn tổng512.000đ gồm dịch vụ/thuế trong bảng hiện tại. Chưa kiểm checkout/promo eligibility, chưa mua. [VNNIC](https://www.vnnic.vn/vi/ten-mien-vn/ho-tro/cau-hoi-thuong-gap/cau-hoi-ve-dang-ky-ten-mien) nêu duy trì tối thiểu1năm; không mua .vn chỉ1tháng.
- **MonsterASP Free không phù hợp thu dữ liệu khách thật**: không custom domain, [Terms3.2](https://www.monsterasp.net/Terms/) chỉ cho dev/testing/learning/evaluation và cấm production/personal-data production; không dùng gói đó cho khảo sát người dùng thật. Cập nhật `study-deployment.md` để ngừng khuyến nghị Free cũ cho dữ liệu thật.
- [Monster Premium Single](https://www.monsterasp.net/) hiển thị $1.95/tháng năm đầu nhưng thu theo năm: **$23.40 upfront**, trướcVAT; renewal$30/năm. Không coi là giá mua1tháng.
- **Lịch sử ước tính, KHÔNG dùng để mua:** [SmarterASP plans](https://www.smarterasp.net/hosting_plans) quảng cáo Advance4.95USD/tháng/512MB. Phép nhân ~~14.85USD/3tháng~~ từ giá này sai điều kiện kỳ hạn; [checkout Advance](https://www.smarterasp.net/signup?plantype=449) sau đó xác minh **25.50USD/3tháng**, 4.95 chỉ36tháng. Basic8.85USD/3tháng cũng chỉ là ước tính chưa kiểm checkout, không khuyến nghị mua theo số đó. Thông tin localRAM không là chứng nhận100concurrent.
- Smarter60daytrial có domain/SSL theo bảng, nhưng chưa xác minh điều kiện production personal-data/retention đủ cho khảo sát; chỉ coi là lựa chọn thử dữ liệu giả tới khi điều kiện rõ. Không chọn trial để lách hạn chế dữ liệu thật.

Đã hỏi user duyệt phương án domain .vn + Advance3tháng; chưa có câu trả lời khi bắt đầu local gate. Chủ dự án phải tự hoàn thành thông tin chủ thể, CAPTCHA/điều khoản/thanh toán theo quy trình tài khoản; không tự gửi thông tin cá nhân cho registrar/host.

## Source được bảo toàn tại điểm đổi ưu tiên

Baseline đầu lượt budget: `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`,98tracked modified/82expandeduntracked/0staged/1worktree. Giữ toàn bộ thay đổi có trước. Không commit/push/reset/clean hoặc đổi main SQL service/process.

Budget personal UX đã thực hiện trước khi user đổi ưu tiên:

- `client/src/features/customer/components/tools/WeddingBudgetPlanner.tsx` thêm row/live form remaining vàexcess, nhãn tổng đã chi/đã cọc, hướng dẫn cumulative không cộng cọc lần hai; edit payload allowlist6fields, không gửi contractId/isPaid từ DTO.
- Mới `personalBudgetModel.ts` cùng thư mục: chỉ derived planning status, không đổi persisted IsPaid hoặc tài chính.
- Mới `client/tests/personal-budget.test.mjs`: **14/14 PASS**, actual SSR/callback/model; fullFE lúc đó345/345, lint exit0/13warnings cũ, buildPASS. Không gọi là browser persistence proof.
- [Decision CONFIRMED](../decisions/2026-10-21-personal-customer-budget.md). API/schema/ledger/contract sync giữ nguyên; cờ IsPaid còn có provenance guards nên không sửa toàn cục.
- Browser budget chưa chạy do user chuyển deploy; chỉ đã tạo rồi guarded drop đúng testDB `WeddingPlanTest_Review4CRepair_131251b77bac424cbaf9a6277f67dabe`, không API/Vite/tab budget nào được mở. Media/closedmanifest giữ lại; không xóa dữ liệu thật.

## Sửa release smoke trước khi chạy

Audit phát hiện `tools/Verify-AccountSecurity.ps1` còn assertion signup bắt buộc1membership/.08 cũ, trái decision open-category21/10. Đã sửa **test helper**, không thay app hoặc financial policy:

- Normal signup fixtures không gửi BusinessGroupIds; exact generic acknowledgement version đọc canonical `BusinessGroupPolicy.ServiceCategoryRegistrationVersion`.
- Policy zero groups/nonlegacy/null legacy, category API đủ8types/5groups và match public canonicalmetadata; không nới inactive/role/own-shop guards.
- Giữ localhost/uniqueDB/process ownership/private-draft/account/SignalR kiểm thực; thêm category401/403.
- Mới `client/tests/deploy-smoke-category.test.mjs`: **3/3 PASS** static regression, PowerShell parsePASS. Không gọi static test là SQL/HTTPS proof.

## Local release gate

Đã chạy thành công:

```powershell
pwsh -NoProfile -File tools/Test-ReadyToDeploy.ps1
```

Run: `artifacts/readiness/20261021-112442-19349708`, command session28435 đã kết thúc exit0, **VerifiedUtc2026-10-21T11:27:52.2046772Z (18:27:52VN)**. Script đã được đọc/audit: testSQL fixedlocalhost uniqueGUID, migration chỉ trên disposableDB, packageSQL tạo offline, production smoke loopback5011/5012 với QAconfig riêng; không appDB/startup main hoặc upload host. Không chỉnh source trong lúc fingerprint gate chạy.

Kết quả mới trên source/package tích hợp:

- FE **348/348 PASS,0fail/skip** (gồm14budget+3smoke static mới); lint exit0/13warnings cũ, TS/Vite buildPASS. Warning mixed LoginForm import và outDir mới ngoài project không bị xóa vẫn còn, không phải lỗi.
- BE **1073/1073 PASS,0fail/skip**, actual SQL;4xUnit2031warnings cũ. TRX tại `test-results/backend.trx` của run trên. Không lấy kết quả13:45 làm gate mới.
- ZIP **231entries**, khoảng52.032.808bytes; noSQLite/userdata/uploads/productionsecrets.6negativeguardrailsPASS.
- SQL release script import/replay trên fresh ownedtestDB;8categories/5groups/16templates, không demo user/vendor. Production SPA/routes/API404isolation/readiness/catalog trước-sau restart, private image vàsentineluser/hash persistencePASS.
- HTTPS account+open-category thựcPASS: genericack,0membership,8canonicaltypes/5groups,401/403; own-shop paging/filter/privatepreview; password/session/contact isolation vàSignalR canonicalroom/revokedalreadyopensocket privacyPASS. Không phải browser fullE2E/realhostQA.
- Working set localsmoke165.2MB; sauaccount/WebSocket202.1MB, privatebytes76.8MB. Cówarning >200MB: **không chứng minh 256MB fit hoặc100concurrent**; đề xuất512MB không là performance guarantee.

Artifact:

- `ready.json`: **LOCAL_GATES_PASSED**, **PublishedRemotely=false**.
- ReleaseRoot `artifacts/readiness/20261021-112442-19349708/deploy/wedding-match-20261021-112642-d49c4be6`.
- ZIP `WeddingMatch-site.zip`, SHA256 `5F867CCEE46F860C58BAEE8D8448344DFEC904E7756E79F991E3CFA71BB0742A`.
- OfflineSQL `sql/migrate.sql`, SHA256 `5625A627EED9D2F04B1E24877D3FC3E9A7DCF2B11853284C872B3688ED8B5CE4`.
- SourceSHA256 `86C5401571FE81AFDE035EF2B3A7C1CB782D432B2335B854B28CBF8B4395835A`; script xác minh source không đổi trong gate. Docs/artifacts excluded. Chỉ artifact này được local-verified; sửa source phải gate lại.

Cleanup: script đã dừng đúng processloopback và drop ownedDB `WeddingPlanTest_Study_d1a5d547c084462195a31a7d7d90aa77`. QA-onlycert/config/log giữ tại uniqueTemp `WeddingStudyDeployment_703a8f62bab14ab58d8d3d22ddcad58c`; không credentials khách thật. Tab WHOIS đã đóng, croppedproof giữ ignoredartifacts. Không listener5011/5012/5023/5183 hoặc build/test/server command của lượt này còn chạy. Chỉ disposablefixturedata bị xóa, không useruploads/untracked/DBmain.

**Chưa public**: không DNS/domainregistration/hostingaccount/upload/remoteSQLwrite; không apply migration DB ứng dụng, bootstrapadmin hoặc chuyển dữ liệu thật. RealhostHTTPS/SQLpermissions/persistentuploads/backup-restore/provider/load chưaverified. Chờ user quyết định, không lập automation lách blocker.

## Việc cần có để public

1. User duyệt ngân sách/host; tự mua domain và dịch vụ, báo đã có account (không gửi mật khẩu/key).
2. Lấy đúng hostname/IP/DNS, .NET10/IIS setting, SQL Server đích và private storage root từ Control Panel; secrets nhập trong cấu hình riêng trên máy/host, không trong chat/Git/ZIP.
3. Xác minh DB đích trống hoặc có dữ liệu và backup; review SQL migration riêng, không overwrite/reset DB. Production giữ ApplyMigrations=false/SeedDemoData=false. Admin bootstrap cần user nhập mật khẩu tại máy, không bật public seed.
4. Upload đúng verified artifact, giữ private/uploads/config khi redeploy; gắn domain bằng DNS thật, cấp HTTPS hợp lệ trước mở login/đăng ký dữ liệu thật. Không DNS/SSL changes trước khi domain thuộc user.
5. Host smoke/schema/persistent uploads/restart/downloaded backup/restore riêng và provider/config limits; không coi local gates thay realhost QA hoặc load100.

Advanced finance vẫn chưa hoàn tất; study mode không giao dịch tiền thật. Không tự chọn demo vendor seeds, di chuyển user records hoặc kích hoạt policy mới để có nội dung demo.

Git cuối: branch/HEAD giữ nguyên;101tracked modified/87expandeduntracked/0staged/1worktree, diffcheck0; kiểm6docs liên quan không missing relative links. So với đầu budget98/82,3tracked mới là budgetUI/studydeploymentguide/securitysmoke;5untracked mới là budgetmodel/test/decision, smoketest và báo cáo này. Những file đã dirty/untracked trước vẫn được bảo toàn; không quy toàn bộ101/87 cho lượt này. Không commit/push/reset/clean.
