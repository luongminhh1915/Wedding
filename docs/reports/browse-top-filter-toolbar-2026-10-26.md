# Browse filter toolbar — 2026-10-26

## Request

Move the service browse filters to the top of the results, following the filter placement on the Combo page.

## Implemented

- Moved the browse filter controls out of the left-hand results column into a full-width toolbar before the active-filter chips and service results.
- Kept sorting visible in the toolbar and added a collapsed-by-default “Bộ lọc nâng cao” section for location, minimum/maximum price, budget presets, and rating.
- Preserved the existing filter state, request/query behavior, pagination, and API-backed results.
- Kept the layout responsive; on small screens the advanced fields form a compact two-column grid and the listing cards retain their existing horizontal layout.
- No backend, database, API contract, role permission, or financial-policy changes.

## Verification

- node --test tests/marketplace-brand-layout.test.mjs — **8/8 passed**.
- npm.cmd test — **423/423 passed**.
- npm.cmd run lint — exit 0; **13 warnings** remain in other files, with no warning in the touched browse regression test.
- npm.cmd run build — **passed** (tsc -b && vite build).
- git diff --check — exit 0; Git reports existing LF-to-CRLF conversion warnings for worktree files.
- Responsive browser visual QA/E2E — **UNVERIFIED**.

## Delivery state

Local source changes only; not deployed, committed, or pushed. The repository already contained extensive unrelated tracked and untracked work; all of it was preserved.
