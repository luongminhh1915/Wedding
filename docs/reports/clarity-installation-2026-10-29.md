# Microsoft Clarity installation — 29/10/2026

User supplied and explicitly requested installation of project `z516afvvw2` in the site's HTML head. Scope is this analytics tag only; previous Admin/navbar/SMTP rollout remains unchanged.

## Implemented and deployed

- Added the supplied asynchronous loader once in `client/index.html` head, preserving all prior SEO/title changes.
- Restricted loading to `weddingmatch.website` and `www.weddingmatch.website`; no local, QA or unrelated-host tracking.
- Synchronously skips initial `/reset-password` (case insensitive, optional trailing slash/subpath) and URLs containing token/access_token/id_token/code query/fragment keys. Existing recovery links contain a raw fragment token before the React effect clears it; analytics must not observe that token.
- Added `data-clarity-mask="true"` to body. All descendant text/images, including public content and portaled dialogs, are explicitly masked in recordings. Page structure/click heatmaps remain available; no custom identity/email event or manual consent override was added. This does not assert that all possible URL metadata is anonymized or replace the owner's analytics/privacy settings.
- Built frontend assets were identical to the preceding deployed release:83/83files hash matched. Therefore updated only `/site1/wwwroot/index.html` through the authenticated hosting file editor. No maintenance/restart, API/DLL/asset/private-config change, database connection/migration or real-account test.
- Fresh pre-change public HTML equals the prior release exactly and is saved in ignored `artifacts/clarity-20261029/index.live-before.html`. File editor was independently compared to that backup before saving. HTML textarea normalizes CRLF/lone CR; saved response equals build after newline normalization.

## Verification and limits

- `node tools/build-study-client.mjs D:/Gitgithubhub/Wedding/artifacts/clarity-20261029/client-dist`:TypeScript/Vite PASS. First invocation used a relative output path and was refused by the existing guard before building; corrected absolute invocation passed.
- `npm.cmd run lint`:exit0,13existingwarnings.
- Ignored runtime smoke executes the actual built inline script against9synthetic host/path/query/hash cases:9/9PASS, exactly one head tag, correct async endpoint, body mask present. No new permanent test suite added for this HTML-only change.
- Live HTTPS homepage200, readiness200; actual HTML matches build after newline normalization, exactly one project occurrence. Verification at`2026-10-29T08:59:09Z` (15:59VN). Build HTML SHA256`476BB6AF7B1CEE0DC2A989477534D6E2ECD5EE8C67A529491B22AB70AF0734B9`; normalized-editor live response raw SHA256`B0E4F846E2C7D0F71D8C083D52FFDC38890C73C3BAD44CC0C3722DB33F0A966C`.
- Actual browser sees inserted head script`https://www.clarity.ms/tag/z516afvvw2`, body mask true and rendered main page. This proves loader execution/script insertion, not successful external script download or ingestion.
- **External loading and dashboard ingestion UNVERIFIED/BLOCKED on this machine:** `Resolve-DnsName www.clarity.ms -Type A -DnsOnly` returns`0.0.0.0`; direct HTTPS tag request fails DNS resolution. An independent GoogleDoH attempt timed out; web reader could not fetch the endpoint either. No DNS/hosts/proxy/security setting was modified or bypassed. Owner should check Clarity Dashboard using a network/browser that permits Clarity. Do not report recordings/heatmaps received until actually observed.
- CUA's read-only DOM scope does not expose Performance API; attempted resource-timing inspection was unavailable, not a network-success claim. No full frontend/backend suite or SQL rerun was needed for this static HTML change; previous499FE/1205BE results are historical for the preceding Admin release.

Ignored evidence: build/lint logs, snippet-checks.json, live-html-verification.json, live-browser.json, network-limitation.json and installed-head.png. New report is untracked; preserve it with the existing broad worktree. No commit/push/reset/clean.

Official references: [head installation](https://learn.microsoft.com/en-us/clarity/setup-and-installation/clarity-setup), [masking descendants](https://learn.microsoft.com/en-us/clarity/setup-and-installation/clarity-masking). No claim that this technical installation establishes legal consent/compliance.
