# Google and Facebook customer sign-in — 26/10/2026

## Scope

User requested Google and Facebook sign-in, then continuation of other project work. This checkpoint completes the social-auth code and verification only. Google sign-in was already present in the source; this pass made its unavailable state explicit and completed the Facebook path end to end. No unrelated backlog, deployment, production configuration, or database application was performed.

## Implemented

- Google remains GIS ID-token sign-in to `POST /api/auth/google`. The login page now keeps a visible disabled action when `Google:ClientId` is absent, surfaces provider status failures, and reports a failed GIS script load. Existing Customer-only login and explicit account linking behavior is preserved.
- Facebook login uses the JS SDK with `public_profile,email`, then posts the access token to `POST /api/auth/facebook`. The API verifies the token server-side through Graph `debug_token`, checks the expected App ID and token validity/expiry, then checks that `/me` returns the same subject and a usable email. Provider request timeouts/errors fail with safe messages.
- Google and Facebook social login/create are Customer-only. An existing email is never silently linked. Customers with an existing account must first sign in by their current method and link the provider from **Tài khoản → Mật khẩu & bảo mật**; the verified provider email must match the account email. Vendor and internal accounts continue using password login.
- Facebook account-link status and mutation are Customer-authorized endpoints. The UI pins the initiating account session and checks exact account email. The integration-status endpoint reports whether Facebook server configuration is complete.
- Added `Users.FacebookSubject` mapping, maximum length 255, and a filtered unique index. Migration `20261026083936_FacebookCustomerIdentity` is additive. Rollback refuses to remove existing provider links.
- Added server-side configuration placeholders to `deploy.env.example`; `Facebook:AppSecret` must remain private and must never be put in a `VITE_*` setting.

## Verification

- `dotnet build src/Wedding.WebApi/Wedding.WebApi.csproj --configuration AuthSocial --no-restore` — **PASS**, 0 warnings, 0 errors. The separate `AuthSocial` output avoids the running `Debug` WebApi process.
- `dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --configuration AuthSocial --no-restore --filter "FullyQualifiedName~AccountAuthTests|FullyQualifiedName~IdentityTests" --logger "console;verbosity=minimal"` — **37 passed, 1 skipped, 0 failed**. The skip is SQL Server concurrent-registration verification because `WEDDING_TEST_SQLSERVER` was not enabled; it is unrelated to social-provider HTTP mocks.
- `npm.cmd test` — **426/426 passed**.
- `npm.cmd run build` — **PASS** (`tsc -b` + Vite).
- `npm.cmd run lint` — exit 0, **13 warnings** in existing files outside this social-auth change.
- `dotnet ef migrations has-pending-model-changes ... --configuration AuthSocial` — **No changes since the latest migration**.
- Offline SQL generated from migration `20261022124728_PasswordRecoveryTokens` to `20261026083936_FacebookCustomerIdentity` contains only `ALTER TABLE [Users] ADD [FacebookSubject] nvarchar(255) NULL`, creation of the unique filtered index, and insertion into migration history. **No application database was connected or migrated.**
- `git diff --check` — pending final checkpoint command; worktree has existing CRLF normalization warnings.

## Not verified / required before real use

- Real Google/Facebook OAuth was not run in a browser because provider credentials and app-side domain/origin configuration were not supplied/verified. Automated verifier tests use fake provider responses; they do not prove external provider access.
- Migration19 has **not** been applied to local or hosted SQL Server. Until it is applied through the reviewed deployment process, the Facebook column is absent from the deployed database.
- This source has **not** been deployed. The currently published website is not updated by this task.
- Configure Google `Google:ClientId`; configure Facebook `Facebook:AppId`, `Facebook:AppSecret`, and `Facebook:GraphApiVersion` privately on each environment. Register the exact web origin/domain with both providers, use provider test users or an approved app mode, then smoke-test new Customer login, existing-email collision/link, internal-role denial, disabled-provider state, and account switching. Never paste client secrets, tokens, passwords, or connection strings into chat or Git.

## Files in this scope

- API/application/domain: `AuthController.cs`, `IntegrationStatusController.cs`, `OtpAndSocialLoginCommands.cs`, `IIdentityProviders.cs`, `IdentityProviders.cs`, `DependencyInjection.cs`, `User.cs`, `UserConfiguration.cs`.
- SQL Server: `20261026083936_FacebookCustomerIdentity.cs`, its designer, and `ApplicationDbContextModelSnapshot.cs`.
- Frontend: `QuickLogin.tsx`, `facebookSdk.ts`, `FacebookAccountLink.tsx`, `AccountPage.tsx`, `api.ts`, `sessionPolicy.ts`.
- Tests/config/docs: `AccountAuthTests.cs`, `IdentityTests.cs`, `social-login.test.mjs`, `auth-session.test.mjs`, `deploy.env.example`, `authentication.md`, `master-upgrade-checklist.md`, `AGENT_HANDOFF.md`.

No commit, push, reset, clean, API restart, migration application, or deployment was performed. Preserve the broad pre-existing dirty/untracked worktree.
