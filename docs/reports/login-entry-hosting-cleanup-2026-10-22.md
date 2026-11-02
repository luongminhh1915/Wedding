# Login entry cleanup and hosting preflight note — 22/10/2026

## Request and outcome

The header login control previously opened a `LoginForm` modal even though `/login` is already a complete route. The Development-only login form also exposed preset demo identities/passwords. The header now navigates to `/login`, preserving the current path as the post-login destination; the modal component and test-account panel/styles were removed.

This changes the login presentation only. It does not delete or alter database users, seed data, auth APIs, Google sign-in, account linking, password recovery, or password-reset migration.

## Follow-up clarification — forgot-password must always be discoverable

After the login overlay was removed, the user clarified that Google login can wait but the forgot-password action must always be present. The action is now always visible. If provider status is loading, unavailable, or reports email recovery disabled, the recovery screen explains the state and the send button stays disabled. It becomes actionable only after `/api/auth/providers` confirms SMTP and a reset base URL are configured. This avoids hiding the entry point and avoids showing a false “email sent” acknowledgement when delivery is not enabled.

## Hosting safety checked

- `client/src/features/auth/components/LoginForm.tsx` contains no preset demo credentials/account section.
- `src/Wedding.WebApi/appsettings.json` has `ApplyMigrations=false`, `SeedDemoData=false`, and an empty application connection string.
- `deploy.env.example` keeps `Database__ApplyMigrations=false` and `Database__SeedDemoData=false`; credentials are placeholders.
- `DatabaseStartupPolicy` rejects demo seeding and automatic migrations outside Development. `Program.cs` rejects the repository's known default JWT signing secret in deployed environments.
- The local Development seeder remains available for explicit QA; production policy prevents enabling it. No database or seed records were changed.

## Verification

Executed 22/10/2026 in `client/` (latest follow-up verification):

- `node --test tests/login-entry-flow.test.mjs` — **3/3 passed**.
- `npm.cmd test` — **408/408 passed**, 0 failed/skipped.
- `npm.cmd run lint` — exit 0; existing warnings remain in unrelated components/tests.
- `npm.cmd run build` — **PASS** (`tsc -b` + Vite production build).
- `git diff --check` — **PASS**; Git printed pre-existing LF→CRLF notices across unrelated modified files.
- Source search confirms no login `AuthModal` caller/component or demo-account UI remains. The shared dialog CSS remains for the separate password-change dialog; it is not a login prompt.

## Not done / host still gated

No API was started, no SQL database was contacted/migrated, no browser/provider E2E was run, and no package was uploaded or deployed. The password-recovery migration `20261022124728_PasswordRecoveryTokens` is still unapplied. Google Client ID/origin and private SMTP/SQL/JWT settings must be configured out of Git; domain/DNS/HTTPS and fresh deployment-package verification remain. Do not use a previous package as proof for this changed source.

The repository had broad dirty and untracked changes before this task. Only login-header/form presentation and the documentation/test files listed by Git for this task were changed; all other work was preserved. No commit or push was made.
