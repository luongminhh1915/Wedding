# Google sign-in — live host configuration, 27/10/2026

## Result and scope

Google sign-in is now configured on the existing release at `https://weddingmatch.website`. The live login page renders the Google Identity Services button using the newly created Web OAuth client. This is **configuration/UI verification**, not a completed Google account login or public-app approval.

The user requested that the agent continue configuring the provider and hosted website. No backend/frontend release, application SQL migration, data edit, demo seed, financial change, commit, or push was performed in this configuration task.

## Why no application deployment was needed

The live `/api/auth/providers` response already included `googleClientId`, initially empty. Google login, linking, and `Users.GoogleSubject` existed before the deployed 18-migration release. Setting its existing configuration key enables that release directly.

Do not deploy the current backend merely to enable Google: the newer EF User mapping includes `FacebookSubject`, and the newer startup checks require migration19. The hosted release still has the older provider response shape with no Facebook fields. A full release requires its own reviewed migration/deployment process.

## Changes made

- Added exactly one public environment variable, `Google__ClientId`, inside the existing `/site1/web.config` `<aspNetCore><environmentVariables>` block using the hosting file editor. Preserved every existing XML node and `ASPNETCORE_ENVIRONMENT=Production`.
- This IIS config save restarted the existing app so the setting was loaded. Private SQL/JWT settings were not edited; no Google Client Secret is used by the ID-token implementation.
- Before saving, backed up the original web.config and prepared the exact patched file under ignored `.deployment/private/google-login-20261027/` as `web.config.before` and `web.config.google`.
- Prepared ignored `wedding.production.with-google.json` in that directory from the existing local `wedding.production-rotated.json`, adding only `Google.ClientId`. Deep comparison confirmed all other configuration values unchanged and SQL Server migration/seeding flags false. The source local config was left untouched. This prepared copy has **not** been uploaded or compared with the current remote private JSON; never overwrite later host edits using it without checking.
- Confirmed the Google OAuth client's allowed JavaScript origin is exactly `https://weddingmatch.website`. Google Audience is External/Testing with one test user. No extra test user, app publication, or new credential was created by this task.

## Fresh verification

Checkpoint time: **27/10/2026 10:49 VN**. These are fresh host/config checks in this task.

- Parsed original and patched web.config with `System.Xml`: **PASS**. Exactly one Google entry; removing it produces the original XML; Production preserved.
- `Invoke-RestMethod https://weddingmatch.website/api/auth/providers`: **PASS**, Google configured and exact expected public Client ID returned. No Facebook fields were introduced.
- `Invoke-WebRequest https://weddingmatch.website/health/ready`: **HTTP 200**, `{"status":"ready"}`, normal TLS certificate validation. This is a connection/readiness check, not a database migration or data audit.
- `POST /api/auth/google` with a deliberately invalid synthetic ID token: **HTTP 400**. No real account/token was submitted and no Customer was created by this check.
- Actual browser `https://weddingmatch.website/login`: **Google button rendered**, Google's button iframe uses the expected client. Screenshot: ignored `artifacts/google-login-20261027/google-enabled-live.png`.
- Attempted the Google button in the in-app browser. No account chooser/new tab/navigation became observable and no captured warning/error explained it. This is **not** evidence of a completed login, nor proof of a specific provider failure. Left the live login page open for the owner's own test in regular Chrome. No Google account was selected or consent submitted by the agent.
- No source code changed, so broad build/lint/unit suites were not rerun. Historical automated results remain in the preceding reports and are not claimed as new results here.

## Worktree handoff

Branch `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`; one worktree, zero staged. Final snapshot has 161 tracked paths modified and 174 untracked status entries (180 expanded files). The extensive pre-existing source changes remain intact and are not attributed to this task. Documentation delta: this new report plus updates to `AGENT_HANDOFF.md`, `authentication.md`, and `master-upgrade-checklist.md`. Host change and local ignored backups/config copy are described above. `git diff --check` exit0; no source/build/test process remains running from this task.

## Remaining / owner handoff

- Actual sign-in requires the owner to choose a designated test-user Google account and complete Google's account/consent screen. Google sign-in is Customer-only; internal roles and Vendor accounts retain password login. A collision with an existing account must follow the implemented explicit-link process; do not change roles or auto-link emails to make a test pass.
- Google still restricts this app to Testing. Branding/public privacy and data-handling details are incomplete. No fabricated privacy/terms/deletion URL was supplied, and no public publication was attempted. Public customer availability is **UNVERIFIED**.
- A Client Secret was inadvertently included in a browser accessibility response during an earlier client-creation inspection. Its value is not copied into source, reports, config, or this task's files, and the app does not use it. The owner was told to rotate it; rotation remains **UNVERIFIED**. Do not print the secret or downloaded credential JSON.
- Facebook and live email recovery remain unconfigured; this task only enabled Google on the existing release.

## Preserve on future deploy / rollback

`Publish-StudySite.ps1` generates a fresh web.config, so a future upload can remove this host environment setting. Preserve the Google entry or deliberately move `Google.ClientId` into the existing persistent private configuration before replacing web.config. Do not replace SQL/JWT settings or enable startup migrations/seeding. Current private config loading has `reloadOnChange=false`, so changing only the private JSON requires an app recycle.

To undo this change, restore the exact ignored `web.config.before` to `/site1/web.config` through the host editor; this disables the added Google setting and restarts the same app. Do not run SQL or restore a database for a provider-config rollback.
