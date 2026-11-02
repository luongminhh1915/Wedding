# Deployment release verification

Verified locally on 17 October 2026; nothing was deployed remotely.

Release: `artifacts/study-deploy/wedding-match-20261017-113300-6dfaee72`.

Command:

```powershell
pwsh -NoProfile -File tools/Verify-StudyDeployment.ps1 `
  -ReleaseRoot artifacts/study-deploy/wedding-match-20261017-113300-6dfaee72
```

The smoke test used the actual published application and SQLCMD migration from
this release, a new disposable local SQL Server database, and loopback HTTPS on
ports 5011/5012. It did not restart the normal development API, alter an existing
database, upload anything to a host, or use real customer data.

All checks passed:

- Apply the packaged SQL script twice to an empty database, including Vietnamese
  seed text: 7 categories, 16 invitation templates, no demo users or vendors.
- Insert an inactive sentinel user, restart the production application, reapply
  the same SQL script, and confirm the user ID, name and password hash survive.
- Serve `/`, `/combo`, and `/customer/dashboard` as the React application.
- Return non-HTML 404 responses for missing API routes, unsafe upload paths, and
  a private configuration path instead of leaking the SPA fallback or secrets.
- Verify JSON health/readiness, invalid-login API validation, and HTTP-to-HTTPS
  redirection.
- Serve an image from the configured private storage directory and verify its
  exact SHA-256 hash is unchanged after application restart.

After a combo-builder request and 12 seconds of settling, the observed process
working set was **146.8 MB**, with **49.2 MB** private bytes. This is only a local,
single-user smoke result. It does not establish capacity for 100 concurrent
visitors or guarantee operation inside a shared host's 256 MB limit.

The disposable SQL database was removed after the run. QA-only certificate,
configuration and logs were retained under
`C:/Users/Admin/AppData/Local/Temp/WeddingStudyDeployment_eef5bc85d12a49d18ed6a04e72f614e2`.
Those files contain only generated local QA credentials and sentinel data.

Before collecting real customer data, repeat the deployment checklist in
`docs/study-deployment.md` on the actual host: configure a private SQL connection
and unique JWT secret, enable and verify HTTPS, confirm writable persistent
upload directories, and perform a downloadable database/upload backup and a
restore rehearsal. Passing this smoke test is not a substitute for those steps.
