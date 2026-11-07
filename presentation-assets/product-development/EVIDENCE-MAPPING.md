# Product development slide evidence mapping

## Scope and evidence rule

This mapping supports slides 12–15 of the EXE201 Outcome 3 presentation. It uses original product-history captures; it does not recreate application screens or infer unverified functionality. Screenshot pixels remain unedited apart from their placement/cropping within a slide frame. Slide annotations are in English; the UI inside screenshots remains in its original Vietnamese language.

Evidence labels mean:

- **Live public / Hosted live**: a recorded public or hosted surface. This does not prove every authenticated or end-to-end workflow.
- **Historical browser QA**: a recorded QA interface using test/fixture context. It is not public-deployment proof.
- **Recorded local / environment unknown**: a documented local or unidentified environment capture. It is not a hosted verification claim.
- **Live admin / read-only capture**: a current administrative surface captured without a mutation. It does not prove every administrative workflow.

## Slide 12 — Our Product Development Journey

| Section | Screenshot source | Source date / status | Used claim boundary |
| --- | --- | --- | --- |
| Foundation | `output/product-history-20261105/01_Anh_san_pham_theo_moc/2026-10-16/IMG062_desktop.jpg` | 2026-10-16, `prototype_or_local_unverified` | Early navy UI and responsive entry point; not public deployment. |
| Core Workflows | `output/product-history-20261105/01_Anh_san_pham_theo_moc/2026-10-19/IMG044_phase3a-services-desktop.jpg` | 2026-10-19, `historical_browser_QA` | Recorded vendor service management; QA context. |
| Deploy & Redesign | `output/product-history-20261105/01_Anh_san_pham_theo_moc/2026-10-30/IMG014_live-home-after.jpg` | 2026-10-30, `hosted_live` | Hosted public homepage after the visual redesign. |
| Public UX Refinement | `output/product-history-20261105/01_Anh_san_pham_theo_moc/2026-11-05/IMG010_live-blog.jpg` | 2026-11-05, `hosted_live` | Hosted server-backed Blog surface. |

## Slide 13 — From Basic Interfaces to a Wedding Marketplace

| Comparison | Before source / status | After source / status | Evidence boundary |
| --- | --- | --- | --- |
| Visual Identity | `.../2026-10-16/IMG062_desktop.jpg` — `prototype_or_local_unverified` | `.../2026-11-03/IMG002_live-home-desktop.jpg` — `hosted_live` | Documents a visual transition, not a claim about all public workflows. |
| Vendor Experience | `.../2026-10-19/IMG044_phase3a-services-desktop.jpg` — `historical_browser_QA` | `.../2026-10-30/IMG017_studio-desktop.jpg` — `local_or_unknown` | Vendor Studio is recorded local; it is explicitly not labelled as hosted-live evidence. |
| Service Discovery | `.../2026-10-20/IMG058_service-category-desktop.jpg` — `historical_browser_QA` | `.../2026-11-04/IMG007_live-browse-desktop.jpg` — `hosted_live` | Documents browsing and filtering surfaces. Comparison access is represented as a feature scope, not as an end-to-end test result. |

All abbreviated paths in this section begin with `output/product-history-20261105/01_Anh_san_pham_theo_moc/`.

## Slide 14 — Turning Ideas into Functional Workflows

| Workflow group | Screenshot source | Source date / status | Verification status shown on slide |
| --- | --- | --- | --- |
| Customer Journey | `output/product-history-20261105/01_Anh_san_pham_theo_moc/2026-10-18/IMG034_customer-journey-final-qa.jpg` | 2026-10-18, `historical_browser_QA` | Historical browser QA / fixture data |
| Vendor Workspace | `output/product-history-20261105/01_Anh_san_pham_theo_moc/2026-10-30/IMG017_studio-desktop.jpg` | 2026-10-30, `local_or_unknown` | Recorded local / environment unknown |
| Marketplace & Content | `output/product-history-20261105/05_Web_hien_tai_2026-11-05/02-browse-desktop/part-01.jpg` | 2026-11-05, `live-public` | Live public / captured Nov 5 |
| Platform Operations | `output/product-history-20261105/05_Web_hien_tai_2026-11-05/20-admin-overview/part-01.jpg` | 2026-11-05, `live-admin` | Live admin / read-only capture |

## Slide 15 — From Local Development to Public Deployment

| Section | Screenshot source | Source date / status | Evidence boundary |
| --- | --- | --- | --- |
| Public deployment proof | `output/product-history-20261105/05_Web_hien_tai_2026-11-05/01-home-desktop/part-01.jpg` | 2026-11-05, `live-public`, `https://weddingmatch.website/` | Confirms the recorded public homepage surface. Authenticated workflow, booking/payment and vendor usability testing remain unverified. |

## Catalog and document references

- Screenshot metadata: `output/product-history-20261105/historical-image-catalog.csv` and `output/product-history-20261105/current-capture-catalog.csv`.
- Product-history document: `output/product-history-20261105/WeddingMatch_HanhTrinhSanPham_20261105.pdf`, 79 pages. Its timeline and scope disclosures identify historical/local/QA captures separately from deployment evidence.
- Supporting report: `docs/reports/product-history-20261105.md`.

The `product-development-slides.html` source and `render-product-development-slides.mjs` renderer regenerate the four standalone 1920×1080 PNG files in this folder. Use the bundled runtime’s Node.js and Playwright dependency, as recorded in the workspace dependency inventory.
