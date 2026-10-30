# Homepage and marketplace visual refresh — 25/10/2026

## Request scope

Move the marketplace search directly under the navbar, reference the latest pastel wedding-marketplace palette, and continue the previously requested mascot replacement using the supplied Bé Hý squirrel reference. This is local frontend work only; it does not change business rules, backend/API, database, migration, or deployment state.

## Implemented

- `client/src/features/home/HomePage.tsx`: the homepage search strip now renders first in the page content, immediately after the shared header and before the hero. It retains category, keyword, location, budget and the existing URL-backed `/browse` filter behavior.
- `client/src/features/customer/BrowsePage.tsx`: the existing `SearchFilterBar` now renders directly below the shared navbar and before the page title. Advanced filters, active-filter chips and sorting are unchanged.
- `client/src/components/WeddingMatchLayout.tsx`: preserves the marketplace-first Customer navigation from the prior checkpoint; actor-specific workspace nav, route guards, cart and notifications are unchanged.
- `client/src/styles/marketplace-experience.css`: the storefront navbar uses a blush/apricot gradient, dark-teal text, coral marketplace actions, warm-ivory panels, and sand/sage accents. Search remains wide on desktop and reflows at tablet/mobile widths. Browse buttons and selected category controls use the same palette.
- `client/public/wedding-match/be-hy.svg`: added a lightweight, transparent, code-native Bé Hý squirrel illustration from the user's supplied reference. Updated mascot references and alt text in `HomePage.tsx`, login, registration and the customer dashboard. Kept `milo.png` untouched as a legacy file; current frontend source no longer references it.
- `client/public/wedding-match/BRAND.md`: documents current palette, Bé Hý asset and Milo's legacy status without erasing the old Milo prompt history.
- `client/tests/marketplace-brand-layout.test.mjs`: guards homepage/browse search placement and local Bé Hý asset use.

## Verification

Commands run from `client/` on 25/10/2026:

- `npm.cmd test` — **416/416 passed**, 0 failed/skipped.
- `npm.cmd run build` — **passed** (TypeScript and Vite production build).
- `npm.cmd run lint` — exit 0; **13 warnings** remain in existing unrelated files/tests.
- PowerShell XML parse of `public/wedding-match/be-hy.svg` — **valid**, accessible title `Bé Hý`.
- Confirmed no `milo.png`/`Milo` references remain under `client/src`.
- `git diff --check` on the changed UI/docs — no whitespace errors; Git may report existing LF-to-CRLF working-copy notices.

No browser visual/E2E or hosted smoke was run. Production remains on the previously deployed package; this local UI refresh has **not** been deployed.

## Next

1. Visually inspect homepage and `/browse` at desktop and mobile viewport widths when browser QA is available.
2. If the layout is accepted, prepare a release package only on explicit deployment request.

