# Wedding Match — second Bé Hý pose — 26/10/2026

## Scope

Add the user's second Bé Hý image as an additional mascot pose without replacing the first image or changing marketplace behavior. This is a local frontend change; it does not deploy the site.

## Applied

- Copied the supplied 1254×1254 PNG unchanged to `client/public/wedding-match/be-hy-flowers.png` (PNG color type 6, transparency preserved; SHA-256 `2DBEB6BAD5FADAFCA9CAB0F124BE065E07145B82CA7ED2207D711022412866DF`).
- Added the flower-holding pose beside the service marketplace `/browse` introduction. The primary Bé Hý artwork remains in its existing homepage, auth and customer dashboard locations.
- Added responsive sizing so the pose stays secondary on narrow screens; no route, search, backend, database, permissions or financial behavior changed.
- Updated `client/public/wedding-match/BRAND.md` and the live handoff checkpoint.

## Verification

Run in `client/` on 26/10/2026:

- `npm.cmd test` — **419/419 passed**, 0 failed/skipped (includes a check for the new alpha-channel asset and `/browse` placement).
- `npm.cmd run build` — **passed**; Vite copied the asset to `client/dist/wedding-match/be-hy-flowers.png`, and source/dist SHA-256 match.
- `npm.cmd run lint` — exit 0 with **13 warnings** (existing warnings in other files).
- `git diff --check` — **PASS**; Git emitted only existing LF-to-CRLF working-copy notices.
- PNG header verified as 1254×1254 with color type 6 (RGBA); source and deployed-build file SHA-256: `2DBEB6BAD5FADAFCA9CAB0F124BE065E07145B82CA7ED2207D711022412866DF`.
- Browser visual/mobile QA and live deployment: **not performed**; appearance on the hosted page is unverified.

## Handoff

The source change is local only. The broad pre-existing dirty worktree is preserved; no commit, push, reset, clean or deploy was run.
