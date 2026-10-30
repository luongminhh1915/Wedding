# Homepage marketplace navigation refresh — 24/10/2026

## Scope

Continue the user's request to emphasize Wedding Match's marketplace, widen the homepage search bar horizontally, and replace the current mascot. This is a local UI change only; no deployment, API, database, migration, or financial-policy changes were made.

## Implemented

- `client/src/components/WeddingMatchLayout.tsx`: the Customer desktop navigation now puts the service marketplace forward with a search icon and the secondary descriptor “Nhà cung cấp & dịch vụ”; labels the planning dropdown “Wedding Planner”; adds “Về Wedding Match” linking to the existing `#cong-cu` section; and shortens the vendor entry to “Dành cho NCC”. Actor-specific navigation, mobile navigation, route guards, account, notifications, and cart behavior are unchanged.
- `client/src/features/home/HomePage.tsx`: grouped the hero copy and marketplace illustration separately from the quick-search form. This lets the search panel use the full hero width while retaining existing category, keyword, location, budget inputs and URL-backed `/browse` filtering behavior.
- `client/src/styles/marketplace-experience.css`: redesigned customer header spacing/marketplace CTA and made desktop search a single wide horizontal row above 900px. Narrower screens switch to responsive multi-column/stacked controls.

## Mascot status — not implemented

The requested squirrel mascot is based on the user's Bé Hý reference. The built-in image-generation tool is unavailable in this session. The imagegen skill disallows silently switching to the CLI/API fallback, which requires a locally configured `OPENAI_API_KEY` and explicit user confirmation. The user was asked whether to create a compact code-native SVG directly in the project or use the CLI fallback. Until the user selects an option, homepage copy and source continue to use the existing Milo PNG; no broken image reference or placeholder was introduced.

## Verification

Commands run from `client/` on 24/10/2026:

- `npm.cmd test` — **413/413 passed**, 0 failed/skipped.
- `npm.cmd run build` — **passed** (TypeScript and Vite production build).
- `npm.cmd run lint` — exit 0; **13 warnings** remain in existing unrelated source/tests.
- `git diff --check` — no whitespace errors in changed UI files; Git may emit existing LF-to-CRLF working-copy warnings elsewhere.

No browser screenshot/E2E was run for this styling-only change. Production remains on the previously deployed package; this change has not been deployed.

## Next

1. Apply the user's choice for mascot asset format; update the homepage and `client/public/wedding-match/BRAND.md` if replacing the production mascot.
2. Run frontend tests/build/lint and, if browser automation is available, visually check desktop and mobile navigation/search.
3. Do not deploy unless the user asks for deployment.

