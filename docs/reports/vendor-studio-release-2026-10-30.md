# Vendor Studio and Admin Vendor provisioning release — 30/10/2026

Status: **DEPLOYED; hosted integration verified.** Production authenticated Studio mutation remains **UNVERIFIED**; the complete authoring workflow was exercised against an owned local SQL fixture with the same release.

## Scope and authorization

Owner requested deployment of the full-screen Vendor Service Authoring Studio and explicitly approved additive migration21 plus deployment to `weddingmatch.website`. The resumed request was “tiếp tục deploy cho t”. No new business or financial policy was introduced.

Branch `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`. The worktree includes extensive prior uncommitted work. Counts and historical checkpoints do not attribute all changes to this release. No commit, push, reset, clean, application DB restore/reseed, or real QA account/content creation was performed during this continuation.

Final Git inspection:189 tracked paths changed,236 untracked files,0 staged,1 worktree; `git diff --check` exit0. The new release report and feature/test files listed below are among the untracked files and must be preserved.

## Shipped behavior

- Full-screen Studio with independently scrolling form/preview columns, collapse/reopen form, and mobile form/preview tabs. Final mobile width uses the containing viewport and locks background scrolling while the editor is open.
- Collections and latest update are persisted in the existing `Listing.ServiceDetailsJson`, hydrated when reopening, included in moderation revisions, and rendered from approved public content. A new draft does not replace the approved public version.
- Device image uploads use the existing multipart `/api/upload/images` API. Upload errors no longer masquerade as successful blob URLs. Save is blocked during uploads; session/actor changes and editor teardown cancel stale work. Temporary preview URLs cannot be persisted.
- Backend validates album IDs/names/counts, update text, and image references. No new Studio table or additional Studio migration was required.
- Admin-created Vendors may omit email/phone, receive a stable unique login alias, and specify multiple operating provinces or nationwide coverage. Email sign-in is retained. Arbitrary display-name sign-in is rejected; display names are not unique credentials. Public registration and other actors retain their contact requirements.
- Coverage is included in management/public combo Vendor DTOs. Notification delivery can address an in-app Vendor recipient without inventing an email. Account provisioning does not verify a shop or accept financial/legal terms.

## Principal implementation files

- `client/src/features/vendor-portal/components/ListingForm.tsx`, `ListingLivePreview.tsx`, **new** `listingStudioUpload.ts`; `hooks/useListings.ts`.
- `client/src/features/listings/serviceDetailsModel.ts`, `client/src/types/listing.types.ts`, `client/src/features/customer/components/ServiceDetails.tsx`.
- `src/Wedding.Application/Features/Listings/ListingServiceDetails.cs`, **new** `ListingStudioContent.cs`.
- `src/Wedding.Application/Features/Auth/Commands/LoginCommand.cs`; `client/src/features/auth/components/LoginForm.tsx`.
- User creation/query/management DTOs, Combo public Vendor DTOs/query projections, appointment/combo/background notification recipient call sites, and migration21 guard.
- **New untracked tests** include `ListingStudioContentTests.cs`, `AdminVendorMigrationSqlTests.cs`, and `client/tests/listing-studio-persistence.test.mjs`. Historical SQL fixture repairs in the review/commission/contract/Facebook tests preserve their earlier-schema assertions instead of applying the new schema early.

Many of these files already contained earlier changes. Keep the full worktree, including untracked feature files, when handing off.

## Exact release and deployment

Release directory: `artifacts/vendor-studio-release-20261030/wedding-match-20261029-193643-936106ca`.

- `WeddingMatch-site.zip`: **253 entries**, SHA256 `AE3C254DC15C8C18737E53FB9B2B0D29ECE6B4D72A0B4B06C6FE0C96B37C450F`.
- Uploaded under `/private/deploy/vendor-studio-20261030/`. A browser download of the uploaded ZIP matched this SHA before extraction.
- Hosting queue reports **DECOMPRESS Success** at approximately **07:48 VN**, destination `/site1/`.
- Task-owned `app_offline.htm` produced HTTP503 with the expected marker during replacement. It was moved to the private release directory with overwrite disabled; it is no longer in the app directory.
- `/health/ready` returned HTTP200 after reopening. Production startup migrations and seeding remain disabled.
- Fresh private configuration downloaded before/after deployment is byte-identical. The candidate IIS configuration also matched the fresh host backup. SQL/JWT, Gmail, Google/Facebook and private upload storage configuration were preserved.
- Existing static files were not recursively cleaned. No real uploads were copied into the site ZIP.

The earlier `wedding-match-20261029-190809-ab7ebdcb` candidate was superseded by the final mobile layout correction; do not deploy it.

## Database evidence and limitations

Migration `20261029152219_AdminVendorContactAndOperatingAreas` was already applied at the preceding checkpoint, around **03:05 VN**. The resumed continuation independently checked the live database using SELECT statements and **did not rerun migration21 or apply the full history script**.

Production is at21 migrations. `Users.LoginName` is nullable nvarchar80 with an enabled filtered unique index. `Vendors.OperatingRegions` is nullable nvarchar1000. The email constraint is enabled/trusted and supports the scoped Vendor exception. Both new columns initially contain no non-null values.

Important historical correction: the preceding checkpoint’s “Zero Data Loss” statement was based only on six table counts. It did not prove every original field was unchanged. `live-schema-before.json` was overwritten with schema21; it is not evidence of an original schema20 inspection. Preserve this limitation rather than presenting a stronger retrospective guarantee.

For the actual application replacement, a new read-only private hash snapshot was taken while offline. The post-deployment comparison verified **62 tables /539 existing rows**, with **no differences** in the scoped fields and one allowed appended audit/outbox row. Deliberately excluded mutable fields were `Users.LastLoginAt`, notification delivery/read timestamps/attempts/errors, and SQL rowversion. This evidence covers the deployment interval, not the earlier migration interval. No financial snapshot or contract row was rewritten by this continuation.

## Backups and restore rehearsal

- Existing `/site1-before-studio-20261030.zip`: hosting COMPRESS Success verified.
- Fresh host SQL backup: MSSQL BACKUP Success, created approximately **07:44 VN**. Downloaded into the private deployment directory; no backup/config contents are in this report or Git.
- Fresh backup SHA256 `4EFA3DA6F37D029C9D96748B407BB92FE03A23CC49572AC6915828439FA715DF`.
- Backup identity/full type, `RESTORE VERIFYONLY`, restore into a new owned localhost GUID database, `DBCC CHECKDB`, and schema21/index/constraint checks **PASS**. No production restore and no migration replay were performed in this rehearsal.
- Restored DB had62 application tables/538 rows (26 users,2 vendors,0 listings,0 booking contracts). Later audit activity accounts for the different total at the deployment snapshot.
- The owned restore database was dropped after ownership checks and its absence verified. The earlier Studio QA fixture is already Closed/database absent/no5029 listener; its retained `runtime.json` Ready value is stale.

## Verification

These full source gates were executed for the final release before deployment and their logs were checked during continuation; they were not represented as new reruns:

| Gate | Command / result |
|---|---|
| Frontend | `npm.cmd test --prefix client -- --run`: **521/521 PASS**,0 fail/skip; final mobile source included. |
| Backend including owned SQL | `WEDDING_TEST_SQLSERVER=1 dotnet test tests/Wedding.Tests/Wedding.Tests.csproj -p:ArtifactsPath=D:/Gitgithubhub/Wedding/artifacts/vendor-studio-fullgate-final-20261030/build -p:UseSharedCompilation=false --logger 'trx;LogFileName=full-backend-final.trx' --results-directory artifacts/vendor-studio-fullgate-final-20261030/results`: **1273/1273 PASS**,0 fail/skip. PowerShell sets the opt-in environment variable before the command. |
| Build/package | `tools/Publish-StudySite.ps1 -OutputRoot artifacts/vendor-studio-release-20261030`: TypeScript/Vite/.NET Release and package verifier PASS,253 entries. Vite reports the isolated output directory is outside the client root and will not be emptied. |
| Migration | Two actual SQL tests for20→21, replay/preservation/index/check/Down guards PASS on owned unique databases. |
| Owned API/browser |19 API checks PASS; actual desktop/mobile upload/save-block/save/reopen/collapse, approval and public draft privacy checked. Browser album count2, update persisted,0 temporary refs. |

Fresh continuation checks:

- `npm.cmd run lint --prefix client`: exit0, **13 existing warnings**. Backend compilation historically reports4 existing xUnit2031 warnings.
- `node artifacts/vendor-studio-deploy-20261030/verify-live-assets.mjs`: **129/129 hosted frontend files match SHA256** of the final package, including HTML/CSS/JS/media.
- Twelve HTTP checks PASS: home/login/browse/combo/cam-nang/health/readiness/providers200; anonymous users/admin articles401; private config and nonexistent managed image404. An initial check used the nonexistent `/api/governance/articles` route and returned404; after reading the actual controller/caller the canonical `/api/admin/articles` correctly returned401. The initial failed diagnostic is retained separately.
- Provider responses before/after match. Clarity project/tag and body masking remain in live HTML; no new Clarity ingestion claim.
- Actual hosted browser home loads categories normally; login renders the Vendor alias identifier, recovery button, Google area and Facebook button. No real sign-in or real Vendor mutation was performed.
- Uploaded ZIP checksum and extraction success are verified. A supplementary direct DLL download/checksum attempt remained incomplete in the browser; **individual hosted backend DLL hashes are UNVERIFIED**. No downloaded executable was run.
- `git diff --check`: exit0. No staged changes or extra worktree introduced.

Ignored evidence: `artifacts/vendor-studio-deploy-20261030/{live-assets.json,http-after.json,live-schema-independent-20261030.json,deployment-preservation-result.json,restore-schema21-result.json,api-results.json,browser-api-checks.json}` and screenshots. Private files stay under `.deployment/private/`; never add them to Git.

## Remaining work / exact next step

Deployment is complete. To use the Studio, sign into a real Vendor account → Vendor dashboard → Dịch vụ → create/edit. If an old browser tab still uses the prior bundle, refresh it.

Hosted authenticated Studio authoring/upload/moderation with owner-controlled real actors, full OAuth login, and full password-reset token consumption remain UNVERIFIED; this release did not create production test data or send another test email. No new load/concurrency claim, www alias/SEO indexing claim, or completion of Phase5 financial backlog is implied.

If the owner reports a specific defect, reproduce it in an owned fixture first and inspect this release evidence before editing. Do not reapply migration21, restore a backup onto the app DB, restart development with automatic migrations, or repeat completed QA cleanup.
