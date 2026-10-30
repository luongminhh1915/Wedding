# Mobile primary navbar visibility

Ngày 26/10/2026. Phạm vi: sửa điều hướng mobile theo ảnh người dùng gửi; chỉ frontend và regression test. Branch `weddy-journey`, HEAD gốc của lượt `3596ae832faeac95fb15cc44bc0e8e62351c788b`.

## Finding

At `max-width: 900px`, CSS hid `.wm-desktop-nav`. The existing `.wm-mobile-nav` was fixed at the bottom of the viewport, so the mobile page header displayed only the brand, cart and login/account actions. This made the primary page links appear to be missing when the user looked at the top of the page.

## Implemented

- Moved the responsive primary nav into normal layout immediately after `WeddingMatchHeader`, before route content. It stays sticky beneath the sticky brand header while scrolling.
- Made links a horizontally scrollable row, with a visible thin scrollbar and touch scrolling at narrow widths.
- Customer and anonymous navigation exposes the public customer links already present on desktop: Trang chủ, Khám phá dịch vụ, Combo cưới, Wedding Planner, Cẩm nang, Về Wedding Match, and Dành cho NCC. The account/login/cart/notification actions stay in the header.
- Vendor and internal actors keep `actorWorkspaceNavigation` shortcuts for their own role; this presentation change does not alter server authorization, routes, capabilities or tabs.
- Updated mobile header/scroll offsets to account for the sticky nav and removed the old bottom-nav reserved page padding.
- Added source/CSS regression checks for the nav placement, role gate and mobile sticky/overflow behavior.

## Changed files

- `client/src/components/WeddingMatchLayout.tsx`
- `client/src/styles/wedding-match.css`
- `client/tests/actor-navigation.test.mjs`
- `docs/AGENT_HANDOFF.md`
- `docs/reports/mobile-navbar-visibility-2026-10-26.md`

## Verification

- `node --test tests/actor-navigation.test.mjs` — **5/5 passed**.
- `npm.cmd test` from `client` — **423/423 passed**, 0 failed/skipped.
- `npm.cmd run lint` from `client` — exit 0; 13 existing warnings remain, all outside these three changed client files.
- `npm.cmd run build` from `client` — **passed** (`tsc -b` and Vite production build).
- `git diff --check` — exit 0. Git emitted existing LF-to-CRLF worktree warnings, not whitespace errors.
- Browser visual QA at phone/tablet viewports — **UNVERIFIED**. The code and CSS regression gates passed; no screenshot or device-browser acceptance is claimed.

No backend/API, authorization, database, migration, financial, host or production state was changed. The worktree already had extensive unrelated modified and untracked files; they were preserved. No commit, push, reset, clean or deployment was performed. Phase checklist unchanged because this is a presentation/navigation correction, not completion of a product phase.
