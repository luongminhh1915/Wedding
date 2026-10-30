# Wedding Match

## Current storefront palette (26/10/2026)

Use the user-approved warm blush direction consistently across customer, vendor, moderation, finance, admin, auth, planner, notification and combo screens. Keep warm ivory `#FCF6EA` as the main canvas and card surface; blush `#D49587` and peach `#E4B7A6` for soft surfaces and the shared navbar; coral `#E38C82` for primary actions, selected controls and key highlights. Use deep teal-navy `#233A42` for readable text, icons and restrained structure, champagne `#F1DBA2` and sage `#98AB98` only as supporting accents. Secondary text remains slate-blue. Avoid large dark navy panels or a navy navbar. Keep success, warning and error colors semantically distinct.

## Bé Hý

Current mascot: Bé Hý is the exact supplied smiling squirrel artwork with glasses, a star, a deep teal vest and coral bow, stored as `client/public/wedding-match/be-hy-mascot.png`. The supplied 1254×1254 PNG has an opaque white background (no alpha); shared mascot images use `mix-blend-mode: multiply` on the site's light surfaces so the white backdrop blends into the warm ivory palette. It is used in the marketplace homepage, login/register and customer dashboard. Keep the original file unchanged. The prior code-native redraw `client/public/wedding-match/be-hy.svg` is retained as an unused fallback; it is not the current UI mascot.

Additional supplied pose: `client/public/wedding-match/be-hy-flowers.png` shows Bé Hý holding flowers. It is a separate transparent 1254×1254 PNG used beside the public service-discovery introduction and in the homepage marketplace demo; preserve its alpha channel and keep it as the same character, not a second brand mascot.

## Legacy mascot — Milo

Retained as a legacy asset at `client/public/wedding-match/milo.png`; current UI no longer references it.

### Archived generation prompt for Milo

> Create a single original compact, very cute penguin mascot named Milo for the Vietnamese wedding planning platform Wedding Match. Production-ready isolated web asset, transparent background with real alpha. One complete full-body character only, centered with generous transparent margin, no text, no letters, no logos, no props, no scenery, no shadow plane. A small plump pear-shaped penguin, short little flippers, rounded head fused smoothly into body, tiny cream feet, dark navy outer body and head (#25334C), warm ivory face patch and belly (#F2EFE4). Two tiny round navy eyes and subtle rosy blush, soft friendly closed smile, tiny muted mustard beak (#D2AC2B) and a very small gold bow tie: gold must occupy less than 4 percent of the image. One flipper slightly raised in a welcoming wave, the other close to body. Clean modern 2.5D soft clay / matte vinyl illustration, minimal simplified silhouette, smooth well-defined edges, subtle soft shading, not fuzzy, not furry, no excessive decorative details. Designed to read clearly at 80px and fit into a clean premium navy-and-cream wedding website. Entire character must be visible without cropping, near-square composition.

Current logo (29/10/2026): `brand-mark.svg` is a transparent code-native vector inspired by the owner-supplied infinity-heart ribbon and spark reference, using champagne/peach/blush. Shared header/footer display the symbol on a white rounded plate, as explicitly requested by the owner; the stacked Playfair Display wordmark sits outside that plate. The favicon uses the same vector. Footer uses a flat warm blush surface `#F1D1C5` with deep teal text; no dark mode is introduced. `mark.svg` is retained as the unused legacy Wedding Match monogram. Product previews on the home page are presentational React/CSS illustrations, not screenshots of Weddy, and are labeled as examples.

Homepage reference redesign (29/10/2026) supersedes the older homepage mascot placements described above: the new romantic hero and eight-category grid use the generated illustrative photos documented in ASSETS.md; no mascot appears on this homepage. Existing mascot uses elsewhere are preserved. Marketplace listing/vendor data remains real API data, while planner card examples are explicitly labeled; no invented customer counts or ratings are added.

Inter and Playfair Display font files were copied from the existing project assets; original source attribution remains in `../weddy/SOURCES.md`.
