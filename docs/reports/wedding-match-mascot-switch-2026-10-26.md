# Wedding Match mascot switch — 26/10/2026

## Scope

Use the exact mascot image attached by the user in the Wedding Match UI. This is a local frontend change only; it does not request or perform a production deploy.

## Applied

- Copied the user-provided PNG unchanged to `client/public/wedding-match/be-hy-mascot.png` (1254×1254, 1,732,863 bytes, PNG RGB color type with no alpha; SHA-256 `a4b7a0955ac6b03f6e34c298c16581e2a1c24cba2255dc4da201f8a5d56693c2`).
- Updated all four existing mascot placements to use this asset: homepage marketplace hero, homepage planning section, login/register story and customer dashboard.
- Added the shared `wm-mascot-image` class with `mix-blend-mode: multiply`, which blends the white background into the light ivory/blush page surfaces without modifying the supplied artwork.
- Kept the previous `be-hy.svg` and `milo.png` assets intact; neither is referenced by the current UI. Updated `BRAND.md` and the mascot/palette regression tests.
- No layout, workflow, API, permission, database, migration or financial changes.

## Verification

Run in `client/` on 26/10/2026:

- `npm.cmd test` — **418/418 passed**, 0 failed/skipped.
- `npm.cmd run build` — **passed**; verified Vite copied the PNG to `client/dist/wedding-match/be-hy-mascot.png`.
- `npm.cmd run lint` — exit 0, **13 existing warnings** in unrelated hooks/components/tests.
- Source and copied PNG hashes match; PNG signature/dimensions/type validated; `git diff --check` **PASS**.
- Browser visual/E2E and deployment — **not performed**. In-browser blend appearance remains unverified.

## Handoff

Source remains local; `https://weddingmatch.website/` will continue to serve its existing deployment until a separate authorized deployment. The worktree contains many unrelated pre-existing changes, all preserved. No commit, push, reset or clean was run.
