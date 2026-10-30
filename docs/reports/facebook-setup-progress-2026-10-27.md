# Facebook setup and safe schema preflight — 27/10/2026

## Scope / current result

The user asked to continue Facebook after Google was enabled on the existing hosted release. The owner completed Developer registration, entered the app contact email directly in Meta, and clicked final Create App themselves. Public app ID is `1163881512741877` (Wedding Match). The owner entered App Secret into a local ignored private config through a hidden terminal prompt. **Facebook configuration and the matching release are now deployed after explicit approval; real customer OAuth remains unverified. No secret value was printed in chat/logs/browser capture in this Facebook task.**

Google remains enabled in the new release through persistent private configuration. A non-destructive host DB backup, private configuration backup and old-site archive were created before release. The approved additive schema/provider/binary changes are recorded at the end. No financial policy, original financial snapshot, password or account role was rewritten.

## Meta setup

- Verified Meta Developer apps list had no applications, then opened Create App.
- Prepared app name `Wedding Match`; the owner supplied/corrected their contact email directly in the form. No contact address, password, OTP, or secret is recorded here.
- Selected the visible use case “Xác thực và yêu cầu dữ liệu từ người dùng qua phương thức Đăng nhập bằng Facebook”. No Page/Ads/WhatsApp use case or Page-management scope was selected.
- The account had no available business portfolio. Selected the explicit option to connect a business later; no business/entity or portfolio was created.
- The current Requirements/Overview page explicitly lists **Business Verification** and **App Review**, each required for this use case's public release/data access. Do not promise public availability or fabricate business information to bypass those gates. A fanpage does not itself replace a Meta Developer account/app or a verified business portfolio.
- Final Create App acknowledges Meta terms and creates persistent app credentials. The owner completed this action/password/OTP themselves and confirmed the dashboard; agent independently verified the app dashboard path. Current app settings resume at `https://developers.facebook.com/apps/1163881512741877/use_cases/customize/settings/?use_case_enum=FB_LOGIN&selected_tab=settings&product_route=fb-login`.
- Added the required basic `email` permission in the selected login use case; UI now shows both `email` and automatically granted `public_profile` as ready for testing. No other user/Page permissions were added.
- Enabled JavaScript SDK login and saved exact allowed origin `https://weddingmatch.website`; Meta normalized it to a trailing slash on persisted reload. Compared toggles before saving: only JS SDK changed; HTTPS and strict redirect protections remained enabled, embedded-webview/device flows stayed off, no invented redirect URI was added.
- Basic App Domain `weddingmatch.website` and Website platform URL `https://weddingmatch.website/` were saved and independently re-read after navigating away/back. No fake policy/terms/deletion URL was supplied. Advanced settings show API version v26.0.
- At the owner's explicit request, opened the App Secret reveal control without returning its value. Only boolean masked/verification state was checked; no full AX/screenshot was taken afterward. Basic secret is currently visible to the owner; do not capture that page until hidden/left.
- Owner-facing screenshot, with contact-email area excluded from the final saved image: ignored `artifacts/facebook-setup-20261027/meta-create-requirements.png`. Temporary earlier screenshot owned by this task was removed; no user artifact was deleted.

Primary compatibility reference: Meta's [official FedCM sample](https://github.com/fbsamples/fedcm-example-app) still includes FB.init/FB.login with public_profile/email fallback. No rewrite to the new beta was made. Meta documentation pages returned429 during research; the actual wizard above is the evidence for current requirements.

## Backend / database preparation

### Private input helper and credential verification

New `tools/Set-StudySocialConfiguration.ps1` creates a new config copy only under repo `.deployment/private`, preserving original/SQL/JWT/uploads/unknown keys and raw numeric JSON. It rejects overwrites, ambiguous keys, same/unsafe/reparse paths and unsafe SQL startup flags. Public App ID/version are CLI parameters; App Secret is exclusively a hidden SecureString prompt. Version validation was aligned with the backend's lowercase vN.0 requirement.

Synthetic helper smoke **17/17 PASS**, including unsupported minor/uppercase versions rejected before secret prompt, preservation, path/overwrite and sanitized-error checks. Evidence: ignored `artifacts/facebook-setup-20261027/social-config-smoke-7f12563213bc4144bccf3f34a0d8b8f1/result.json`.

Owner ran the helper successfully. Only boolean/public checks were returned for ignored `.deployment/private/facebook-login-20261027/wedding.production.json`: expected App ID/version, secret present, Google preserved, SqlServer and ApplyMigrations/SeedDemoData false. Its SQL/JWT origin is the previous local config; it must **not** overwrite current remote private config without comparison/merge.

Actual Meta credential check: GET Graph v26.0 app identity using app access token only in Authorization header returned expected public app ID/name. No secret in URL or output, no user access token/account-data request. This verifies app credentials, **not** customer OAuth or host integration.

### Full release gate — initial failed attempt, retained history

`tools/Test-ReadyToDeploy.ps1` ran with local test-only SQL opt-in. Frontend suite reached426PASS and lint retained13 existing warnings. Backend full actual-SQL suite: **1177 passed / 3 failed / 0 skipped / 1180 total**; TRX `artifacts/readiness/20261027-084704-c9a3fef6/test-results/backend.trx`. Gate stopped before producing a ready release.

All three failures were `Invalid column name FacebookSubject` while historical-schema fixtures seeded Users using the newest EF mapping: IndependentReviewSqlVerificationTests.Phase4CUpgrade..., CommissionPhase5SqlVerificationTests.UpgradeIsAdditive..., ContractCategorySnapshotTests.SqlMigration.... This failed run is historical; the successful repair/rerun is recorded below, without replacing its evidence.

### Fixture repairs and exact release gate — PASS

Only historical User fixture inserts changed in `IndependentReviewSqlVerificationTests.cs`, `CommissionPhase5SqlVerificationTests.cs`, and `ContractCategorySnapshotTests.cs`. They use parameterized original-schema columns without tracking those legacy users through the newest EF mapping. Current-schema seeding remains intact. No early Facebook column is injected, migration boundaries and financial/review preservation assertions are retained, and no production financial/contract/review source was changed. All three targeted actual-SQL regressions passed individually,0skip. Initial isolated agent commands with `--no-restore` failed at missing assets before running tests; restore-enabled reruns passed.

Root reran `tools/Test-ReadyToDeploy.ps1`, whose test SQL targets are unique owned localhost databases. `artifacts/readiness/20261027-091303-40c9d470/ready.json` reports LOCAL_GATES_PASSED: frontend426PASS, backend1180PASS/0FAIL/0SKIP with SQL enabled; lint exit0 with13 existing warnings, four existing xUnit2031 warnings in VendorBusinessGroupTests. FE/API build,6 packaging rejection checks, real local HTTPS Production account/role/password revocation/chat/session isolation, migration replay, restart and private image persistence all PASS. Smoke working set reached200.6MB; this is not a load/concurrency or real-host capacity result.

Exact release root: `artifacts/readiness/20261027-091303-40c9d470/deploy/wedding-match-20261027-091605-90e6654b`.

- Site ZIP SHA256: `68A820C6E1809451F3DE8D8274A689C8B7FED5706A173A3383D5A767AF6073D6`.
- Source SHA256: `163480EC6F762F9E09F90FE7F4E48CCDB93D565E907BC85FB190E4B3917C49B8`.
- Independent read-only audit confirmed both hashes,242 ZIP entries, no private/data/uploads/cert/env/SQL entries. Packaged web.config contains Production only: preserve Google via merged private config before replacing it.
- No public deployment was performed; rerun if source/config packaging inputs change.

Added only `tests/Wedding.Tests/FacebookIdentityMigrationSqlTests.cs`, a regression for the existing additive migration18→19. It hardcodes localhost and Integrated Security, uses a fresh GUID-owned `WeddingPlanTest_FacebookIdentity_*` database, refuses an existing target, ignores environment connection-string values, and drops only after its successful owned create and context disposal. No Down is run.

It verifies:

- Existing synthetic User fields/password hash/security version/GoogleSubject and WeddingProfile amounts/dates are unchanged by upgrade.
- New FacebookSubject is nullable nvarchar255, with a unique filtered index; multiple nulls coexist and duplicate nonnull identities are rejected.
- Idempotent replay preserves the linked Facebook identity, old data, and one migration-history row.
- The owned database is removed and DB_ID is null after cleanup.

### Fresh checks

- Read-only localhost/master SQL reachability: **PASS**. No application private configuration was loaded.
- Scoped SQL regression final run: **1 passed / 0 failed / 0 skipped**; TRX `artifacts/facebook-setup-20261027/test-results/facebook-migration19-rerun.trx`.
- Initial test run failed at a brittle SQL LIKE matcher for index metadata, not at migration/data preservation. Replaced the test matcher with direct normalized filter_definition verification; retained the failed TRX `facebook-migration19.trx`. Both initial/final owned DBs were dropped; root independently checked their absence via localhost/master metadata.
- Root integration run with process `WEDDING_TEST_SQLSERVER=1`:

```powershell
dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --no-restore --artifacts-path artifacts/facebook-setup-20261027/test-build --results-directory artifacts/facebook-setup-20261027/test-results --filter 'FullyQualifiedName~AccountAuthTests|FullyQualifiedName~IdentityTests|FullyQualifiedName~FacebookIdentityMigrationSqlTests' --logger 'trx;LogFileName=facebook-auth-readiness.trx' --logger 'console;verbosity=minimal'
```

Result: **39 passed / 0 failed / 0 skipped**. The opt-in environment value was restored afterward. Initial compilation reported four existing xUnit2031 warnings in VendorBusinessGroupTests; no warning was introduced in the new file. No frontend source changed; no new frontend suite/build result is claimed.

### Scoped offline migration artifact

Generated migration19 only, from `20261022124728_PasswordRecoveryTokens` to `20261026083936_FacebookCustomerIdentity`, with EF `migrations script --idempotent`, isolated `FacebookRelease` build, and explicit non-routable design-time connection. Build/generation **PASS**; no API startup or database connection by EF.

Artifact: ignored `artifacts/facebook-setup-20261027/offline/migration19.sql`, UTF16LE BOM with SQLCMD/filtered-index session preamble. SHA256 `180E127811DCE06E4CBFA7D39B49B4F57B3B9AABDBDC12C9FEADE078A51CA82F`.

Reviewed SQL contains only nullable column addition, unique filtered index, and migration-history insertion inside transaction/idempotency guards; no DROP/UPDATE/DELETE/TRUNCATE. The exact SQLCMD file has subsequently passed the private backup rehearsal below; **it has not been applied to production.**

## Host backup, exact target and private configuration preflight

- SmarterASP created an8.1MB `.bak` in its private `/db` folder. Download completed to the owner's Downloads; copied to ignored `.deployment/private/facebook-login-20261027/host-backup/database-before-facebook.bak`. No backup/real user data is included in Git, release ZIP or shared log. Host screenshot evidence: ignored `artifacts/facebook-setup-20261027/host-sql-backup-ready.png`.
- Backup SHA256 `2C96D737348BDE325D1B39D14C87011D2B14787E9D6448BB87529E8D0FC53FF5`.
- An ignored rehearsal script validates backup DB identity/type, runs VERIFYONLY, restores into an absent `WeddingPlanTest_FacebookRestore_<GUID>` on localhost with unique absent files and no WITH REPLACE, runs CHECKDB, verifies schema18, applies the exact hash-checked scoped migration19 twice, compares all non-history table row counts and checks new column/index/history. The owned DB is dropped only in cleanup; no real DB restore/Down occurs.
- Final rehearsal result **Restore/Migration/Replay/RowsPreserved/Cleanup all true**,62 tables. Evidence `artifacts/facebook-setup-20261027/restore-rehearsal-result.json`. First rehearsal restored successfully but failed at an unquoted reserved alias in a metadata SELECT; cleanup passed. The alias was corrected and the complete rerun passed.
- Direct hosted SQLCMD metadata read failed with network/timeout classification, not LoginFailed; captured diagnostics intentionally exclude credentials/raw error output. No TLS/network/service settings were weakened. Built-in SQL Studio Portal provides an authenticated read-only fallback and independently confirmed exact target `sql1004.site4now.net / db_ad0009_weddingmatch`,18 migrations, latest `20261022124728_PasswordRecoveryTokens`, Facebook column absent. Initial metadata SELECT had a reserved alias, then SET NOCOUNT was rejected by portal policy; a plain SELECT succeeded. Do not infer the portal accepts SQLCMD batches/DDL until checked within authorized release scope.
- Active remote `/private/wedding.production.json` was backed up directly to ignored `host-backup/wedding.production.before.json`, never printed. SQL/JWT and all settings outside Google/Facebook match the owner candidate. Ignored `wedding.production.host-merged.json` uses the active remote base with only Google/Facebook sections added; flags remain SqlServer/ApplyMigrations=false/SeedDemoData=false. At preflight it was not uploaded; approved Save/re-read completed later below.
- Current live site binary archive completed49MB and was moved with overwrite disabled to `/private/deploy/site-before-facebook-20261027.zip`; destination file/size independently confirmed. Download action started but no completed local ZIP was found yet; do not claim local archive integrity verification.
- Shared Git state after code repairs: branch weddy-journey / HEAD3596ae8,161 tracked changed paths,177 untracked entries,0 staged,1 worktree. These include extensive pre-existing work; only the named fixture/helper/SQL-test/docs are attributed here. No commit/push/reset/clean.

## Authorization and approved production execution

### Explicit authorization received

The owner approved the exact named real DB additive FacebookSubject/index migration19 and deployment/configuration of the tested release, preserving existing accounts/data/contracts/finance/Google, with possible short interruption and Facebook testing/public-review limitations explained. This supersedes the earlier pending-approval checkpoints. No real mutation had happened at authorization checkpoint.

SQL Studio's plain SELECT succeeded, but SET is disallowed and its ARITHABORT is0. No writes were disguised or split to bypass that limitation. Direct System.Data.SqlClient also failed with sanitized error64. SmarterASP's official [connection troubleshooting KB](https://www.smarterasp.net/support/kb/a2152/a-network-related-or-instance-specific-error-occurred-while-establishing-a-connection-to-sql-server_.aspx) links `https://mssql.site4now.net/default.asp`; agent authenticated to this official legacy SQL manager using existing private credentials, verified sql1004/database, then its Tools → Run Query successfully executed a plain metadata SELECT with SET NOCOUNT. No password/connection string was printed or captured.

The host-adapted script `artifacts/facebook-setup-20261027/offline/migration19-host.sql` removes SQLCMD-only directives/GO, adds exact-target/predecessor/partial-state guards and explicit TRY/transaction/CATCH rollback, retains filtered-index session settings and the same three EF19 mutations, and checks exact column/index/history postconditions before commit. SHA256 `7F572C71BAE8F2CABEA0DA483AB70A35E616C6AAA20709320658C5552BB816D4`. Independent review found an index-postcondition hardening opportunity; root added exact sole key/filter/enabled/ignore-duplicate checks before use. Final exact script (only DB-name guard changed to the owned GUID) passed backup restore, CHECKDB, apply, replay, record counts, injected failure after ALTER proving full rollback, and cleanup. Evidence `artifacts/facebook-setup-20261027/host-script-rehearsal-result.json`.

After authorization, the guarded script was filled/read back in the official manager and submitted to the exact real DB. Result:19 migration rows, latest `20261026083936_FacebookCustomerIdentity`, column510bytes/nullable nvarchar255, unique filtered index1. Independent SQL Studio metadata counted62 tables; only history18→19 and AuditLogs131→132 differ from the preflight baseline, all other table counts unchanged. The reason for the concurrent audit append was not established and is not attributed to migration. No real Down/restore/reseed or business-row update occurred.

Private config was re-read immediately before Save and still exactly matched its backup. Saved merged candidate uses the active remote base, preserves SQL/JWT/unrelated values and disables migration/demo seed. Reopened remote JSON matched the candidate exactly after normalizing textarea CRLF→LF; raw newline-only inequality was investigated, not mistaken for lost settings. Secret values were never printed/captured.

Exact verified ZIP was uploaded to `/private/deploy/facebook-login-20261027/WeddingMatch-site.zip`. Created an owned `/site1/app_offline.htm` for a short maintenance window; HTTP503 confirmed. Hosting work queue then reported DECOMPRESS Success from that archive to `/site1/`, not a nested site directory. The marker was moved, with overwrite disabled, to the private release folder to resume service. No private/uploads folder was deleted or included in the release ZIP. Old binary archive/private config/SQL backup remain available; rollback must keep additive schema19 and must never restore over later live user data.

## Live verification and remaining owner steps

- Public HTTPS `/health/ready`, `/api/auth/providers` and `/` each returned200; homepage references exact new `index-BgeoNPK6.css` and module script.
- Provider API returns the existing Google Client ID and Facebook public ID1163881512741877/versionv26.0. No App Secret is exposed. Email OTP/passwordRecovery remainfalse because SMTP is still unconfigured.
- POST invalid synthetic Facebook access token to `/api/auth/facebook` returns400JSON; invalid synthetic Google ID token to `/api/auth/google` returns400JSON. These are denial/smoke tests and do not establish a successful real account login.
- Actual live `/login` renders Google Identity Services and an enabled Facebook button. A click in IAB entered a pending state but no Facebook popup or completed consent was visible; no SDK error appeared in the captured filtered browser warnings/errors. Reload restored the ready button. **OAuth sign-in/link/collision E2E remains UNVERIFIED**, not failed/passed based solely on this browser limitation. Owner should test in Chrome using their app-role/test account and perform their own login/consent.
- Proof cropped to Facebook section only (no personalized Google email): ignored `artifacts/facebook-setup-20261027/facebook-button-live.png`; full screenshot is private/ignored. No real test customer/staff was created for this live smoke.
- Meta app remains in testing. Public release requires the actual Meta review/business verification and truthful privacy/deletion/contact handling; do not promise all Facebook customers can sign in yet or fabricate policy/legal details. SMTP/password recovery, real upload persistence/load capacity and www binding are not revalidated by this release.
- Full local gate426FE/1180BE,0skip; existing13lint/4analyzer warnings. No source changed after exact release verification; docs/artifacts were updated without rerunning the full suite. No unfinished migration/upload/maintenance command remains, no commit/push/reset/clean.

Next: owner Chrome OAuth consent/login, then existing-Customer link/email-collision/role QA; complete confirmed Meta public requirements. Schema19 and release are already applied: inspect live state before retrying anything; do not repeat migration or deployment from historical pending checkpoints.
