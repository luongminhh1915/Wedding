# Deployment and Google Search readiness — 2026-10-23

## User goal and result

The user wants the website to be online and discoverable in Google by the next time they check it. The local source is now prepared for public-route crawling and a fresh deployment package passed the local release gate. **The website has not been deployed, its hosted SQL Server database does not exist yet, and Google Search Console has not been verified or submitted.** It is therefore not accurate to say the site is already live or indexed.

Google does not promise same-day crawling or inclusion. Google says a recrawl request can take a few days to a few weeks and is not a guarantee of immediate or eventual indexing; sitemap submission is a discovery hint, not a guarantee. The site also needs public HTTP 200 pages, crawlable/indexable content and no crawler block. See [Ask Google to Recrawl](https://developers.google.com/search/docs/crawling-indexing/ask-google-to-recrawl), [Google Search technical requirements](https://developers.google.com/search/docs/essentials/technical), and [Build and Submit a Sitemap](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap).

## Existing work preserved / host check

- Branch/HEAD: `weddy-journey` / `3596ae832faeac95fb15cc44bc0e8e62351c788b`.
- Start-of-task status: 120 tracked modified paths, 136 untracked paths, 0 staged, one worktree. These changes pre-existed this task; they were preserved and not attributed to this SEO work.
- Re-read `AGENTS.md`, `.github/copilot-instructions.md`, living handoff, master checklist, and the 22/10 deployment reports before work.
- Read-only inspection of the already-open SmarterASP MSSQL panel on 23/10 still showed `0 / 1` MSSQL DB, `0 / 1000 MB`, “No databases found”. The creation form is open, but no DB/password was submitted. User must privately enter the DB password and create it. No host SQL, upload, deploy, seed, account provisioning, DNS changes, or production operations were performed.
- User-approved deployment decision remains a new empty host database, with no copy of local customer/vendor data.

## SEO implementation

- `client/public/robots.txt` allows crawling and points at `https://weddingmatch.website/sitemap.xml`; it does not disallow private paths, so Google can read the route-level `noindex` instructions.
- `client/public/sitemap.xml` lists only stable public landing/catalog routes: `/`, `/browse`, `/combo`, `/cam-nang`, `/invitation`. It intentionally excludes personal invitations and actor dashboards.
- `client/index.html` has branded title/description/Open Graph defaults and an initial `index,follow` robots meta for the public landing HTML. Google warns it may skip JavaScript rendering when the original response is `noindex`, so the public home must not start noindex and expect JS to remove it.
- `client/src/utils/seoPolicy.mjs` explicitly allows only public landing, service catalog/detail, combo catalog/detail, public guide, and invitation gallery routes. It canonicalizes aliases (`/vendors` → `/browse`, `/wiki` → `/cam-nang`) and removes query/trailing slash variants.
- `client/src/components/RouteSeoMetadata.tsx` updates page title, description, Open Graph, canonical URL, and robots on route changes. Account/auth/planner/customer/vendor/admin pages, unknown routes, and individual invitation slugs stay `noindex,nofollow` without a canonical URL.
- `src/Wedding.WebApi/SearchRobotsPolicy.cs` + middleware in `Program.cs` applies an HTTP `X-Robots-Tag: noindex, nofollow` to all non-public app/API routes (including dashboards, personal invitation slugs, and unknown routes), while public catalog/content routes and static sitemap/robots/assets stay crawlable. This protects private URLs even if Google does not execute client JS.
- `client/tests/seo-indexing.test.mjs` covers the route allowlist/denylist, aliases/query cleanup, and sitemap/robots privacy boundaries.
- `tests/Wedding.Tests/SearchRobotsPolicyTests.cs` covers server route policy and response-header behavior.

This is a client-rendered SPA, not server-side rendered HTML. Google can render JavaScript, but SEO text and route metadata are applied after the app starts; this package has not been browser-tested against Googlebot. Google recommends SSR/static rendering as stronger options because not all crawlers run JavaScript and some pages can have render limitations; see [JavaScript SEO basics](https://developers.google.com/search/docs/crawling-indexing/javascript/javascript-seo-basics). Server-side rendering or pre-rendered public pages remain a follow-up if search visibility/content rendering is poor.

## Verification on current local source

Completed 23/10/2026, around 00:15 Vietnam time:

```powershell
cd D:\Gitgithubhub\Wedding\client
npm.cmd test
npm.cmd run lint
npm.cmd run build
cd D:\Gitgithubhub\Wedding
pwsh -NoProfile -File tools/Test-ReadyToDeploy.ps1
pwsh -NoProfile -File tools/Verify-StudyPackage.ps1 -ReleaseRoot 'D:\Gitgithubhub\Wedding\artifacts\readiness\20261022-171215-2835f544\deploy\wedding-match-20261022-171442-c22893f4'
```

Results:

- Frontend unit suite: **411/411 passed, 0 failed, 0 skipped**.
- Lint: exit 0; 13 warnings in existing unrelated code/tests; no warning in the SEO files.
- TypeScript/Vite production build: PASS.
- Targeted backend `dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --no-restore --filter 'FullyQualifiedName~SearchRobotsPolicyTests'`: **33/33 passed** (including response-header behavior).
- Full `Test-ReadyToDeploy.ps1`: **LOCAL_GATES_PASSED**. Backend **1174/1174**; SQL tests enabled and used disposable test-owned DBs only; Release package/guardrails, six packaging rejection checks, HTTPS production smoke, SQL migration replay, API/SPA/auth/authorization/restart/security checks passed. The application DB was not touched. The full smoke is not a host test or load test.
- Separate package verification: PASS; 242 ZIP entries; `wwwroot/index.html`, `wwwroot/robots.txt`, and `wwwroot/sitemap.xml` are included; SQL Server/net10.0; no user data or production credentials.
- Artifact folder: `artifacts/readiness/20261022-171215-2835f544/deploy/wedding-match-20261022-171442-c22893f4`.
- ZIP: `WeddingMatch-site.zip`, SHA-256 `26A3D3D6637480F16E2B0B2ED1D5DBD661A97CADC6570FE4149E283F55E4D38D`.
- Source fingerprint: `91C5675869ABF1854A1A62636F827CCAF38C90FFA9FD903CADBBFDC15169827B`.
- `ready.json`: `PublishedRemotely=false`; verification is local only.
- Functional security smoke working set reached **203.0 MB**, private bytes **75.7 MB**. The verifier emitted a warning that a shared host with 256 MB may be tight. This small single-user smoke is not a concurrency/load benchmark; inspect actual host usage and upgrade only with user approval if resource limits are inadequate.

## Remaining gates to appear in Google

1. User creates the MSSQL database from the open provider panel by entering its password privately. Then share only the database name and SQL server hostname, never password or full connection string.
2. Prepare private production SQL/JWT settings and review the generated additive migration against the confirmed fresh DB. Apply only to that new DB; production startup remains migration/seed off.
3. Upload the exact ZIP above after confirming the host target, configure environment/private settings, and smoke-test temporary URL, custom HTTPS, API, SQL persistence, process restart and persistent uploads. Re-run release gate/package if source changes.
4. Create the first SuperAdmin through the private bootstrap flow; then create Moderator and Finance from the Admin screen. Passwords stay in the user's secure UI/terminal, never chat/docs.
5. Verify public DNS and HTTPS. Add/verify the domain property in Google Search Console using DNS ownership, then submit `https://weddingmatch.website/sitemap.xml` and request indexing for the homepage. Search Console has not been changed in this task.
6. Confirm public pages return HTTP 200, have actual useful public content, and are not blocked. Use Search Console URL Inspection and Page Indexing reports after submission.

Final Git check: branch/HEAD unchanged (`weddy-journey` / `3596ae832faeac95fb15cc44bc0e8e62351c788b`); **123 tracked paths changed, 145 untracked paths, 0 staged, one worktree**. The initial 120/136 dirty paths were preserved; new task files remain untracked, as expected. `git diff --check` passed (Git emitted line-ending normalization notices on pre-existing dirty files); sitemap XML and newly added file whitespace checks passed.

No commit, push, reset, clean, database creation, migration to application/host DB, deployment, account creation, or Search Console submission was performed.
