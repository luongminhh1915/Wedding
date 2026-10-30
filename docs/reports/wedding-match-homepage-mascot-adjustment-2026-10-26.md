# Wedding Match homepage mascot and preview-card polish — 26/10/2026

## Scope

Use a plain surface for the homepage marketplace demo panel and replace its white-backed small mascot with the user's transparent Bé Hý flower pose. Local frontend only; no deployment requested or performed.

## Applied

- Changed `.marketplace-art-window` from radial/linear gradients to the solid warm ivory `#FCF6EA` fill.
- Changed only the homepage marketplace hero mascot to `/wedding-match/be-hy-flowers.png`; the existing primary pose in other sections and flows remains untouched.
- Added regression coverage for the solid demo surface and correct transparent asset.

## Verification

Run in `client/` on 26/10/2026:

- `npm.cmd test` — **420/420 passed**, 0 failed/skipped; regression assertion covers both the flat preview surface and transparent image usage.
- `npm.cmd run build` — **passed**; Vite copied `be-hy-flowers.png` to `client/dist`, and source/dist SHA-256 match.
- `npm.cmd run lint` — exit 0 with **13 existing warnings** in unrelated files.
- `git diff --check` — **PASS**.
- Browser visual QA and deployed-site verification: **not performed**; the live page remains unchanged.

## Handoff

Source changes are local only; the live site is unchanged until a separately authorized publish. Existing unrelated dirty and untracked work has been preserved.
