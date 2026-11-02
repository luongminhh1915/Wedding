# Continuation verification — 27/10/2026

## Scope

Resumed from the 26/10 Google/Facebook sign-in checkpoint. Revalidated existing social-login and personal-budget code without changing application source, database configuration, provider credentials, or deployment state. The next customer-experience checkpoint remains budget persistence/mobile browser QA, which needs an isolated synthetic account and an application fixture.

## Fresh verification

- `npm.cmd test` in `client` — **426 passed, 0 failed, 0 skipped**.
- `npm.cmd run build` in `client` — **PASS** (`tsc -b` and Vite production build).
- `node --test tests/personal-budget.test.mjs tests/social-login.test.mjs` in `client` — **17 passed, 0 failed** (14 budget, 3 social-login checks).
- `dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --no-restore --filter FullyQualifiedName~AccountAuthTests` — **30 passed, 1 skipped, 0 failed**. The skipped case is SQL Server concurrent-registration verification because `WEDDING_TEST_SQLSERVER` was not enabled. Build emitted four existing xUnit2031 analyzer warnings in `VendorBusinessGroupTests.cs`, outside the auth tests.
- `git diff --check` — exit **0**; Git printed existing LF/CRLF normalization warnings.
- Git before this report was added: branch `weddy-journey`, HEAD `3596ae8`; 161 tracked paths modified, 172 untracked paths, 0 staged; one worktree. After adding this report, untracked count is 173. The shared worktree has extensive pre-existing changes; this continuation added only this report and a handoff checkpoint entry, and does not claim ownership of the other changes.

## Not verified

- No local development listener was running on the expected Vite/API ports. No app was started because Development API startup may apply migrations, and there was no verified isolated SQL test database plus synthetic browser actor available.
- Customer budget save/reload, mobile layout, keyboard/caret/IME behavior, and true OAuth were **not browser-tested**.
- Google/Facebook credentials and registered origins remain owner-configured. The Facebook migration is additive and still unapplied; no SQL Server was contacted.
- No deployment, account creation, migration, or production/host change occurred.

## Next safe step

Continue with disposable browser QA once an isolated test database and synthetic Customer/Vendor fixture are available. Verify budget save/reload and responsive/money-input behavior first; do not use main or hosted accounts for those writes. For social login, run provider smoke only after the owner privately configures Google/Facebook applications and the reviewed migration is applied through the deployment process.
