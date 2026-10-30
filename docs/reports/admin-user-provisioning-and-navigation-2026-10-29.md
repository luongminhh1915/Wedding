# Admin account creation and navigation cleanup — 29/10/2026

## Current scope / initial checkpoint

User accepts hiding the optional email-OTP login section, requests moving service discovery out of the navbar into homepage content, and adds Admin provisioning of customers/vendors/all actors. User explicitly confirms this includes another SuperAdmin. [Decision](../decisions/2026-10-29-admin-create-all-user-roles.md).

Initial UI edits: remove desktop/mobile discovery entry, reuse existing search/CTA directly below homepage intro with a clear discovery title/anchor, remove unused featured-pill CSS, hide OTP UI/state while preserving password login, Google/Facebook and email recovery/SMTP/API. Existing role-based internal navigation remains.

On the initial UI snapshot before Admin form/backend edits: full frontend486/486PASS,0fail/skip; lint exit0 with13existingwarnings; isolated TypeScript/Vite same-origin buildPASS. Logs/artifact in ignored `artifacts/nav-home-login-20261029/`. These are not the final integration gate for the new Admin capability.

Admin implementation is in progress in parallel, with stable canonical POST `/api/users` contract and existing UserManagementDto response; legacy `/api/users/staff` remains unchanged. No JWT/session replacement, no real production-account creation, no schema migration, no vendor acknowledgement/approval or financial policy activation. Root handles owned local SQL/API/browser verification and final integration gate after source changes complete.

No new frontend/backend host release has been applied for these changes at this checkpoint. The prior SMTP correction is live and must not be overwritten with an older private config. Existing broad worktree changes/untracked files remain preserved.

## Integration verification — 29/10, supersedes implementation-in-progress above

Implemented canonical `POST /api/users` for exactly Customer, VendorOwner, Moderator, Finance, CustomerCare and SuperAdmin. Both route and handler require an active SuperAdmin; the handler rechecks the actor inside its serializable User/Vendor/audit transaction. Duplicate email cannot replace/promote an existing account. SuperAdmin requires explicit confirmation and a password of at least12characters, at most72UTF8bytes. Other roles retain shared8character validation. Existing `/api/users/staff` remains restricted to its original3roles.

Admin form supports all6roles, Vendor brand/province and explicit SuperAdmin checkbox plus confirmation dialog. Pending requests are single-flight and bound to creator ID/token/session revision. Success clears password state and refreshes users without replacing the Admin session. Failure keeps editable input. Vendor creation produces its linked, unverified shop without recording vendor acceptance of partnership terms or approving a financial exception. No model/schema migration was needed.

Changed source/test paths for this task:

- `client/src/components/WeddingMatchLayout.tsx`, `client/src/features/home/HomePage.tsx`, `client/src/styles/marketplace-experience.css`, `client/tests/actor-navigation.test.mjs`: discovery navbar pill removed; existing search reused below introduction.
- `client/src/features/journey/planner/QuickLogin.tsx`: optional email-OTP UI hidden; password, social sign-in and recovery retained; server/API/SMTP flags unchanged.
- `client/src/features/admin/UserManagement.tsx`, new `adminCreateUserModel.ts`, new `client/tests/admin-user-creation.test.mjs`: all-role provisioning UI and session/payload/confirmation tests.
- New `src/Wedding.Application/Features/Users/Commands/CreateUser/CreateUserCommand.cs`; `src/Wedding.WebApi/Controllers/UsersController.cs`; `src/Wedding.Domain/Entities/Vendor.cs`.
- New shared `src/Wedding.Application/Features/Auth/Commands/VendorRegistrationRules.cs` and existing `RegisterVendorCommand.cs` reuse slug generation while public-signup consent is preserved.
- New `tests/Wedding.Tests/AdminUserCreationTests.cs`; decision record and living handoff/auth/product/checklist documentation.

Final source gate:

- `npm.cmd test`:499/499PASS,0fail/skip. New Admin tests13/13PASS; first test-adapter run had a dataURI import-rewrite defect, fixed in test harness before final gate.
- `npm.cmd run lint`:exit0,13existingwarnings.
- `WEDDING_TEST_SQLSERVER=1 dotnet test tests/Wedding.Tests/Wedding.Tests.csproj -p:ArtifactsPath=<repo>/artifacts/admin-users-fullgate-20261029/build --logger trx;LogFileName=full-backend.trx --results-directory artifacts/admin-users-fullgate-20261029/results`:1205/1205PASS,0fail/skip, including actual owned localhost SQL.4existing xUnit2031 analyzer warnings. Shell logger value was quoted in actual invocation.
- `tools/Publish-StudySite.ps1 -OutputRoot artifacts/admin-users-release-20261029`:TypeScript/Vite and Release API publish PASS; offline SQL generated only.
- `tools/Verify-StudyPackage.ps1 -ReleaseRoot artifacts/admin-users-release-20261029/wedding-match-20261029-020914-41110170`:PASS,242ZIPentries, SQLServer/net10/production startup migrate/seed false, no private config/userdata.
- Independent static second-agent review found no concrete authorization/provenance blocker; this is bounded review, not a substitute for runtime checks.

Runtime QA used only GUID-owned `WeddingPlanTest_AdminUsers_27c45c34773441eaa41c589234ca6d8d`, a matching persistent ownership marker, fake `.invalid` accounts, disabled external providers, and the actual published site/API on loopback5029. Never pointed the fixture at application/hosted DB. Actual HTTP30checksPASS: anonymous401; Customer/VendorOwner/Moderator403; create and password-login all6roles; duplicate rejection; missing SuperAdmin confirmation rejected; response privacy; linked Vendor; creator still authorized. Actual browserPASS: Customer and Vendor creation, canonical province selection, SuperAdmin missing-confirmation block, dialog cancel preserves draft, confirmed SuperAdmin creation, list refresh and original Admin session retained. No real email/account/password/financial writes.

Homepage browserPASS at1440/1024/900/390px: direct discovery navbar links0, search below introduction and no document horizontal overflow. Search submits to `/browse?keyword=QA+bouquet&location=H%C3%A0+N%E1%BB%99i&sortBy=popular`. Login OTP inputs absent. Admin form itself fits390px (339pxwide); existing user-list/header page has441px document width with long synthetic emails, so broad Admin mobile layout is NOT claimed fully verified. QA runtime logged existing EF ReviewTargetType default-sentinel, MediatR license notice and local-only no-HTTPS-port warning. The local HTTP fixture does not change hosted TLS.

Owned cleanup succeeded: verified marker/seed identities, stopped exact API PID, nonforced DROP of this owned GUID database, independently verified `DB_ID` absent. No recursive deletion; no listeners5029/5193 remain. Ignored evidence is under `artifacts/admin-users-qa-20261029/` and build logs under `artifacts/nav-home-login-20261029/`. Real host release remains pending at this checkpoint; working SMTP private config must be retained. Full owner reset consumption, real social OAuth and unrelated business E2E remain outside these checks.

## Hosted rollout checkpoint

Fresh private config downloaded privately and equals working corrected SMTP baseline; exact fresh IIS config equals release web.config. Host COMPRESS of current site succeeded to `/site1-before-admin-20261029.zip`53MB outside public root at09:41VN. A local download attempt timed out in browser tooling; server backup itself is verified in the queue, local full-archive copy is NOT claimed. Exact gated package8C1A75…C001EC uploaded to `/private/deploy/admin-users-20261029/WeddingMatch-site.zip`53MB. Created only this task's `/site1/app_offline.htm`, saved uniquely tagged maintenance content; actual readiness503 verified at2026-10-29T02:45:54Z. Queued extraction to exact `/site1` after folder-picker verification. No SQL connection/migration or private-config write. No claim of completion until extraction success, maintenance removal and live checks below.

## FINAL — hosted29/10/2026, Asia/Ho_Chi_Minh

Hosting DECOMPRESS reports Success at09:50VN for the staged package into `/site1/`. Moved only the task-owned maintenance file into `/private/deploy/admin-users-20261029/`, overwriteoff. Website reopened: readiness200 at09:54VN. Fresh public checks PASS: home/login/readiness/providers200, private-config404, anonymous POST `/api/users`401. All84current frontend HTML/JS/CSS files downloaded over valid HTTPS and SHA256 compared to exact gated release:84/84MATCH. Public provider response exactly unchanged (Google/Facebook/recovery/SMTP configuration availability retained). Actual public browser verifies direct discovery navbar links0, homepage discovery section below introduction, forgot-password button visible, Google GIS iframe and enabled Facebook button, email-OTP input/title absent. An initial immediate-login DOM observation was taken before render and reported false flags; replaced by ready-state checks. Existing real SMTP delivery remains the owner-confirmed preceding result; no new email or password reset sent for this release.

No private configuration was uploaded/edited; no direct hosted SQL connection, migration, reseed, account creation or financial write by this task. Existing application background jobs may perform their normal work after restart; there is no claim of a byte-for-byte production DB audit. Offline migration SQL equals previous live release exactly, so it was not applied. Package is gated/selected/extracted and live API route exists; separate direct remote DLL hash comparison remains UNVERIFIED because the browser download workflow timed out; no DLL hash equality is claimed. Real authenticated hosted Admin creation was deliberately not used as a test: role creation/login proved against the same published build on the owned SQL fixture. Actual owner social OAuth and full recovery-token-consumption E2E remain UNVERIFIED.

Server backup remains `/site1-before-admin-20261029.zip` outside public root; staged new package and consumed maintenance marker remain in private deployment folder. No extraction/maintenance job pending. Restore/replay was not repeated for this schema-unchanged rollout; no production DB restore. Prior backup restore evidence is historical. Broader Admin mobile header/user-list overflow with long emails remains a recorded UI limitation, not a blocker to the new form fitting the screen.

Final code tests were not rerun after docs-only updates. Current full gate remains499FE/1205BE,0fail/skip;13existinglintwarnings/4existingtestanalyzerwarnings. Git broad worktree preserved, no commit/push/reset/clean; new commands/model/test/decision/report files remain untracked and must travel with the worktree. Live proof and hashes are in ignored `artifacts/admin-users-qa-20261029/live-after.json`, `live-browser-checks.json`, `live-home.png`; local dialog screenshot `superadmin-confirmation.png` uses only disposable fictional data.
