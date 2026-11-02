# Deployment progress — 2026-10-22

## Current follow-up — 2026-10-22

The user confirmed using the 60-day trial and creating a fresh hosted SQL Server database without copying local customer/vendor data. Separate contact details for the three staff accounts were supplied in chat; they are not copied here. The Admin email correction was confirmed. The MSSQL 2022 creation form is open in the provider panel and still shows no existing databases; it has not been submitted. The account owner must enter the SQL password directly in the panel and submit the form. No upload, migration, seed, or account creation has occurred. Public DNS/custom HTTPS, backup/restore, persistent uploads, and SMTP are still unverified. Provider documentation currently advertises .NET 10, SQL Server 2022, remote connection and backup/restore for trial, but actual account features must be checked after DB creation. `New-StudyConfiguration.ps1` defaults to SQL certificate validation; do not opt into `TrustServerCertificate=true` based on its MonsterASP-only comment. This follow-up supersedes the unresolved database/contact items in the earlier report body.

## User goal and current boundary

The user asked to deploy Wedding Match end-to-end on `weddingmatch.website` and have three internal accounts: SuperAdmin, Moderator, Finance. The user confirmed using the existing SmarterASP 60-day trial for the initial demo. This report records observed progress only; the site has **not** been published and no hosted database has been created.

## Verified local release

- Branch/HEAD at inspection: `weddy-journey` / `3596ae832faeac95fb15cc44bc0e8e62351c788b`.
- Broad pre-existing worktree preserved: 120 tracked modified paths, 134 untracked paths, 0 staged; one worktree. No reset, cleanup, commit, or push.
- Current deploy-source fingerprint matches `ready.json`: `025253AF9A019F7096CF1A512B376AC986E7C6D492F502BE1C8B3D2677275AAD`.
- Ran `pwsh -NoProfile -File tools/Verify-StudyPackage.ps1 -ReleaseRoot 'D:\Gitgithubhub\Wedding\artifacts\readiness\20261022-162346-af4a1071\deploy\wedding-match-20261022-162628-93b3f7e9'`: PASS, 240 ZIP entries, SQL Server, `net10.0`, no user data or production credentials.
- Exact package SHA-256 remains `135F7EFFEE649C69539158F1E7672BD81308DB1F415B345B6335FDC09FD94AA2`; prior `ready.json` records the full local gates (backend 1141, frontend/lint/build/package smoke). Those are results from `2026-10-22T16:27Z`, not real-host verification and were not rerun in this report.

## Hosting panel observations (read-only)

- Hosting setup completed; plan `W60-US` shows “Expires in 60 days”. User chose this trial for the initial demo. It is not evidence of a paid Advance plan.
- Site `site1` is active and has a temporary HTTPS URL. Its usage displayed 0 visitors / 0 MB at inspection.
- `weddingmatch.website` is mapped to `site1`; the panel shows it as an HTTP link. The user appears to have completed this step; this agent did not add or delete domains.
- DNS manager lists SmarterASP nameservers `NS1.SITE4NOW.NET`, `NS2.SITE4NOW.NET`, `NS3.SITE4NOW.NET` and A records for `@`, `www`, and `*` pointing to `204.188.228.20`. Local system DNS lookup returned “DNS server failure”; public delegation/propagation is **UNVERIFIED**. No registrar DNS setting was changed.
- Hosted database quota is 0/1 MSSQL databases and 0/1000 MB; no database exists yet. No database was created, queried, migrated, or seeded.
- SmarterASP’s official FAQ says trial accounts do not include SMTP; password-reset email requires an external SMTP provider or user-approved paid upgrade. Treat trial automatic backups as unavailable unless the panel/support confirms otherwise.

## Account provisioning design checked in source

- Production package configuration is fail-closed: SQL Server provider, `ApplyMigrations=false`, `SeedDemoData=false`, blank connection string/JWT secret.
- `tools/Wedding.DeployAdmin` safely creates only the first SuperAdmin after reviewed migrations. It requires private local configuration and masked interactive password input; it never migrates, seeds, promotes, resets, or prints credentials.
- The existing SuperAdmin-only `/api/users/staff` flow and Admin UI create Moderator/Finance. No code change or test-account seed is needed; there are no default passwords. Each account needs full name, email, Vietnamese phone, and a password entered through the secure interactive flow/site.

## Outstanding blockers before deploy

1. User must decide whether hosted SQL starts fresh or receives a copy of existing local SQL data. Copying local customer/vendor information to this third-party host is not authorized by this report.
2. User must provide names, reachable email addresses, and phone numbers for the three roles. User selected separate emails but has not provided the values; passwords must not be sent in chat.
3. Create the single MSSQL database in the panel. The user enters its password directly into the provider UI and keeps it in local private config; never paste it into chat.
4. Confirm registrar DNS strategy and preserve existing email/other DNS records. Only then change A records or nameservers; domain is mapped but DNS is unverified.
5. Configure private production SQL/JWT/upload settings, apply the reviewed migration script to the confirmed host database after target/backup checks, deploy the exact ZIP, and smoke-test the temporary HTTPS URL.
6. Confirm custom-domain HTTPS and persistent uploads, then test login, role permissions, SQL persistence, and restore. Keep real personal-data collection closed until these gates pass.

## Safety / commands

- No upload/Web Deploy, registrar DNS change, SQL database creation/query, migration, seed, account creation, SMTP setup, payment, or production launch was performed.
- Public DNS read-only probe attempted via `Resolve-DnsName weddingmatch.website`; the system resolver returned `DNS server failure`, which is inconclusive.
- Official provider sources: [60-day trial](https://www.smarterasp.net/free_trial), [FAQ](https://www.smarterasp.net/FAQ/), [domain pointing](https://www.smarterasp.net/support/kb/a2090/how-to-point-domains-to-my-hosting-account.aspx), [MSSQL database creation](https://www.smarterasp.net/support/kb/a2392/how-can-i-create-an-mssql-database-from-the-hosting-control-panel.aspx), [Web Deploy info](https://www.smarterasp.net/support/kb/a2097/where-can-i-find-my-web-deploy-login-info.aspx).
