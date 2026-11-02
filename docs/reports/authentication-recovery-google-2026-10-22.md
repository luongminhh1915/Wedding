# Authentication: password recovery and Google — 22/10/2026

## Scope and outcome

User request: finish password recovery and Google sign-in for Wedding Match. Scope remained on authentication; no unrelated backlog, deployment, or financial-policy work was started.

Password recovery is implemented across API, SQL schema mapping/migration, SMTP delivery, and UI. Google Customer sign-in was already implemented in the source; this pass filled its account-linking gap by adding a Customer-only link-status endpoint and an authenticated account-security UI for existing customers.

## Behavior now present

- Password reset request returns one generic `202 Accepted` response for valid, unknown, malformed, throttled, disabled, or failed-delivery requests; rate limits are per-IP through the existing auth limiter and per-account at one request/minute and five/hour.
- Reset tokens contain 256 bits of randomness, persist only as SHA-256 hashes, expire after 30 minutes, are single-use, and travel in the URL fragment (`/reset-password#token=...`). A failed resend does not invalidate a previously delivered token.
- Confirming reset changes the BCrypt password, increments `SecurityVersion` to revoke old JWT sessions, writes an audit event, and revokes sibling reset tokens in one transaction. It does not auto-login the user.
- Google login uses Google Identity Services; API verifies signature, issuer, audience, expiry and verified email. It creates/signs in Customer accounts only and does not automatically attach a Google identity to an existing email account.
- Existing Customer can sign in with their normal account, then use **Tài khoản → Mật khẩu & bảo mật → Đăng nhập bằng Google**. Added `GET /api/auth/google/link-status` and UI; `POST /api/auth/google/link` remains protected and verifies the Google ID token, exact normalized account email, and unique Google subject. The frontend binds linking to the bearer token/session revision active when the page opened. Non-Customer UI does not render this action.
- The account page explicitly states when Google is not configured instead of presenting a nonfunctional button.

## Files in this authentication scope

- Backend: `AuthController.cs`, `IdentityProviders.cs`, auth provider interfaces/registration, password-recovery commands, `PasswordResetToken` entity/configuration, `IApplicationDbContext`, `ApplicationDbContext`, and migration `20261022124728_PasswordRecoveryTokens` plus model snapshot.
- Frontend: `LoginForm.tsx`, `PasswordResetPage.tsx`, `QuickLogin.tsx`, `AppRoutes.tsx`, journey auth API, `AccountPage.tsx`, new `GoogleAccountLink.tsx`, account styles, reset-page styles.
- Tests/docs/config: `AccountAuthTests.cs`, new `google-account-link.test.mjs`, `authentication.md`, `deploy.env.example`, checklist and handoff.

The worktree was already broadly dirty/untracked before this request. The list above identifies auth-related files only; it does not claim all pre-existing worktree changes were authored for this feature.

## Verification performed

Commands executed on 22/10/2026 in local development, using isolated build outputs and in-memory SQLite for auth unit tests:

- `dotnet build src/Wedding.WebApi/Wedding.WebApi.csproj --configuration Phase5 --artifacts-path artifacts/auth-recovery-google-20261022/build --no-restore` — PASS, 0 warnings/errors.
- `WEDDING_TEST_SQLSERVER='' dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --configuration Phase5 --artifacts-path artifacts/auth-recovery-google-20261022/tests --filter 'FullyQualifiedName~Wedding.Tests.AccountAuthTests' --logger 'console;verbosity=minimal'` — PASS, 28 passed, 1 SQL-only test skipped; 4 existing xUnit2031 warnings in `VendorBusinessGroupTests.cs`. The SQL-only test is unrelated concurrent signup and was skipped deliberately; no application SQL Server was contacted.
- `npm.cmd test` — PASS, 405/405, including four Google link UI regression cases (Customer-only, provider-disabled, already linked, and configured/unlinked).
- `npm.cmd run lint` — exit 0, 0 errors, 13 existing warnings outside the new auth files.
- `npm.cmd run build` — PASS. Vite reports the existing ineffective dynamic import warning for `LoginForm.tsx` also imported by `AuthModal.tsx`.
- `dotnet ef migrations has-pending-model-changes --configuration Phase5 --project src/Wedding.Infrastructure/Wedding.Infrastructure.csproj --startup-project src/Wedding.WebApi/Wedding.WebApi.csproj --context Wedding.Infrastructure.Persistence.ApplicationDbContext` — PASS, no pending model changes.
- `git diff --check` — exit 0; Git reports pre-existing LF→CRLF conversion notices across the broadly dirty worktree, not whitespace errors.

The migration was generated and reviewed offline. It adds `PasswordResetTokens`, indexes and FK; `Down` refuses to drop reset-token rows. Migration is **not applied**. The API was not started because this repository's development startup can apply migrations; no SQL application database was accessed or changed.

## Unverified / operator setup required

Development config has no Google Client ID, SMTP host/from, or reset base URL. Therefore live Google popup/sign-in/link, actual SMTP receipt, and browser E2E are **UNVERIFIED**, not passed. Set public `Google__ClientId` for the Web OAuth client and register the exact frontend origin in Google Cloud. Configure SMTP credentials only in private server-side environment and set `Auth__PasswordResetBaseUrl` to the deployed HTTPS origin. Never put SMTP credentials in `VITE_*`, chat, or Git.

Before deploying the backend version, review and apply the additive migration through the normal controlled deployment process. No production deployment, account creation, email send, live Google auth, migration, commit, or push was performed in this task.

## Handoff

Code readiness is implemented and local/automated gates pass. Remaining work within auth is provider configuration followed by real browser/provider QA and applying the reviewed migration at deployment. Do not mark provider integration as live until these are verified.
