# Homepage redesign, CMS cover upload, footer and brand — 29/10/2026

## Scope and current result

Owner asks whether Admin can publish blog posts, asks to make it usable, replaces the cover-image URL field with direct upload, requests a palette-matched footer and the supplied infinity-heart/spark brand identity. No dark mode requested. Existing SuperAdmin/Moderator CMS create/edit/draft/publish/delete routes and dashboard tab were present; they were reused.

**IMPLEMENTED / local runtime and hosted integration VERIFIED.** The owner subsequently added four homepage references and explicitly requested a white backing behind the logo. The combined release described below supersedes the earlier CMS-only staging package. The earlier gates and owned SQL acceptance remain historical evidence for the unchanged CMS/backend; final frontend and hosted checks were rerun after the homepage changes.

## Changes

- Visible Cẩm nang CMS shortcut for SuperAdmin and Moderator in desktop/mobile actor navigation. Existing roles/menu permissions retained.
- Extracted CMS editor, cover picker and model from OperationsPanels, preserving its export/callers. Choose JPG/PNG/WebP from device, optimize/upload, preview, replace or remove reference. No URL copy/paste required; legacy HTTPS images still render. Save blocked while upload pending; failed replacement preserves current cover; requests bind actor/token/session revision and cancel on unmount. Uploaded-but-discarded images may remain unreferenced; no automatic deletion was added.
- New-post slug follows title until manually edited; existing article slugs stay unchanged. Draft/publish controls, list/detail error states, related-service pins and plain-text content remain. This adds cover photos, not a rich HTML/body-image editor.
- `POST /api/admin/articles/images`, multipart`file` → `{url}`, active SuperAdmin/Moderator only; `GET /uploads/articles/{guid}.{jpg|png|webp}` public. Exact MIME/extension/signature/size/dimension validation, metadata stripping,8MB/file,5,000files/1GB technical quota. Reuses existing raster validator; structural validation is not antivirus/full decoding. Process-local write lock, not a distributed quota lock. Failed-write cleanup removes only a file actually opened by this request.
- New optional Uploads:ArticleRoot; default is sibling`articles` of configured ListingRoot, so the current host uses persistent private/uploads/articles without private-config edits. Without configured roots, local fallback is App_Data/article-uploads. SQL stores the managed URL, not image bytes. Old files are retained when a reference changes.
- Existing article commands and draft query now check active CMS actor in DB, validate existing schema lengths/slug and allow exact managed article URLs or HTTPS. Public reads remain published-only. No schema/model migration.
- New transparent code-native `brand-mark.svg` based on supplied infinity-heart/ribbon/spark reference, stacked serif wordmark, shared header/footer/favicon. Old mark.svg retained. Footer flat warm blush#F1D1C5, teal text, full-width band, mobile scaling; reviewed text contrast4.79–8.36:1. No dark-mode/global palette rewrite.

## Source paths in this task

New backend: ArticleCmsAccess.cs, ArticleCmsInput.cs, ArticleImageReferences.cs under Application/Features/Governance; Infrastructure/Services/ArticleImageStorage.cs; WebApi/Controllers/ArticleImagesController.cs; tests/Wedding.Tests/ArticleImageUploadTests.cs. Existing GovernanceFeature.cs and OperationsFeature.cs updated only for CMS; pre-existing NotificationRecipient changes preserved.

New frontend: journey/admin/ArticleCms.tsx, ArticleCoverPicker.tsx, articleCmsModel.ts, tests/article-cms.test.mjs. Existing OperationsPanels.tsx reexports/editor detail; ArticlesPage.tsx cover URL resolver; actorNavigationModel.ts and existing actor-navigation tests; WeddingMatchLayout.tsx logo/icon; wedding-match.css logo/footer; index.html favicon. New public/wedding-match/brand-mark.svg and BRAND.md update. Existing broad dirty/untracked work preserved.

## Gates on the earlier CMS source snapshot

- `npm.cmd test`:511/511PASS,0fail/skip, including12newCMS tests and5updated navigation tests.
- `npm.cmd run lint`:exit0,13existingwarnings; no new CMS warnings.
- `WEDDING_TEST_SQLSERVER=1 dotnet test tests/Wedding.Tests/Wedding.Tests.csproj -p:ArtifactsPath=D:/Gitgithubhub/Wedding/artifacts/cms-brand-fullgate-20261029/build --logger 'trx;LogFileName=full-backend.trx' --results-directory artifacts/cms-brand-fullgate-20261029/results`:1230/1230PASS,0fail/skip,4existingxUnit2031warnings. New targeted25/25PASS including2actualSQL facts on their own GUID databases. Initial new-test failures were FormFile HeaderDictionary/async harness setup, corrected before final gates.
- `tools/Publish-StudySite.ps1 -OutputRoot artifacts/cms-brand-release-20261029`:TypeScript/Vite, Release API publish and package guardPASS,247ZIPentries. Release`wedding-match-20261029-092844-725e0abe`; ZIP SHA256`744BFA130C88B0A630A6624EE47E1FFC7292CE266FE6DA9F27F7960ADF792254`.
- Offline migrationSQL SHA256`E6B01C98C8A712964DDD24A0AAEF59EC0D0407FA094870DD9F0B853772E9191A` equals prior live schema20 script exactly. DO NOT APPLY; none needed/applied.
- Independent second-agent static review found no concrete auth/storage/publication blocker. Root caught and corrected cleanup deleting a pre-existing collision path; final gates include created-file-only fix.

## Actual SQL/API/browser acceptance

Used only newly created/marked owned localhost DB`WeddingPlanTest_CmsBrand_63bfbd3135844d36ba3a56075e8888d0`, fake.invalid Admin/Moderator/Customer/Vendor actors, isolated upload roots, false startup migration/seed and disabled external providers. Exact final published API+frontend served only127.0.0.1:5029. Initial stale browser token from a consumed older fixture was revalidated/rejected normally; login used this fixture's random private credentials.

ActualHTTP21checksPASS: anonymous draft401, Customer/Vendor draft/upload403, Admin browser draft persisted inSQL, draft excluded publicly, managed cover URL/image200+nosniff, Moderator upload/create/publish/public detail/unpublish/delete, SVG400. First direct multipart test sent generic PowerShell octet-stream MIME and was correctly rejected; fixed the QA client to send image/jpeg, no app relaxation.

Actual browserPASS: Admin opens new CMS navbar shortcut; title auto slug; chooses device JPEG through file picker; pending upload disables save; optimized WebP preview; save draft; reopen includes image/body; publish; exact public article URL has content and loaded managed cover. A first click on the view link only focused/scrolled in the native browser harness; authoritative acceptance used its observed exact href via navigation, checked URL/content. No public-visibility claim was based on the stale CMS screen.

Responsive DOM checks1440/1024/900/390px: no document horizontal overflow, editor fits (297.6px at390), actual footer backgroundrgb(241,209,197), new shared logo source. Desktop editor/footer screenshots inspected. The IAB mobile screenshot is scaled oddly; mobile DOM geometry verified, full physical-device visual acceptance not claimed.

Guarded cleanup PASS: ownership marker/seed identities verified, exact owned API stopped, only this GUID DB dropped non-forced, independent DBabsence check PASS. Ignored fixture files/media/logs retained; no recursive deletion. No real hosted account/article/photo/email/financial writes in QA. Evidence in artifacts/cms-brand-qa-20261029; full test/publish logs in artifacts/cms-brand-*.

## Hosted rollout

Completed using the new combined package, not the earlier CMS-only ZIP. Fresh `/site1-before-home-cms-20261029.zip` server backup completed with COMPRESS Success; it is outside the public webroot. Fresh private/IIS downloads are kept only in ignored `.deployment/private/home-cms-release-20261029/`. The private file matched the working corrected SMTP baseline; IIS matched the gated package. The new ZIP was uploaded to `/private/deploy/home-cms-20261029/`, extracted into `/site1/` with DECOMPRESS Success, then this task's exact owned app_offline.htm was moved into the same private staging folder, overwrite disabled. The owned maintenance marker was verified before moving; readiness returned200 after restart at approximately21:15VN. No production migration, direct SQL, real article/account/image/email/financial write, private configuration change or upload-folder replacement was performed.

Final release: `artifacts/home-cms-release-20261029/wedding-match-20261029-135905-40b650ac`; ZIP60,719,139bytes,250entries, SHA256`CCDEFCD9492115D0258A502072980A45D3F0E7C2ABD2182C762476E93A2AB7A5`. Offline SQL remains`E6B01C98C8A712964DDD24A0AAEF59EC0D0407FA094870DD9F0B853772E9191A`: DO NOT APPLY. All four application DLLs match the earlier CMS gate/build byte-for-byte. No full backend rerun was needed for subsequent homepage/CSS/asset-only edits; the earlier1230/1230result is not presented as a second run.

Final commands: `npm.cmd test`511/511PASS0fail/skip; `npm.cmd run lint`exit0/13existingwarnings; `tools/Publish-StudySite.ps1 -OutputRoot artifacts/home-cms-release-20261029` TS/Vite/Release publish/packageguardPASS. Logs: artifacts/home-cms-final-frontend-20261029.log, home-cms-final-lint-20261029.log, home-cms-publish-20261029.log.

Post-release read-only checks:126/126static files under published wwwroot match local SHA256 exactly (HTML, scripts, styles, fonts, logo and image assets included); home/login/cam-nang/providers/readiness200; anonymous draft API401; private configuration URL404; missing managed article image404. Public provider configuration is unchanged byte-for-byte. A fresh private-config download after release matches the before backup exactly, preserving corrected Gmail/Google/Facebook settings. Hosted authenticated CMS publishing/upload is UNVERIFIED: acceptance used the exact same application DLLs with owned local SQL, rather than creating real production test articles. Remote DLL hashes are not independently downloaded/verified.

## Added homepage reference redesign and white logo

- HomePage.tsx and new home-romantic.css implement the romantic ivory/blush photo hero, wide functional search below hero, eight API-backed category photo cards, real marketplace preview/empty state, four pastel planner cards, FAQ and closing CTA. Removed the old duplicated homepage demo/orbit sections and homepage mascot only; other pages keep their mascots. Existing search/filter URL model, actor-aware dashboard start target and actual planner routes are reused.
- Two new fictional illustrative photographs were generated through the built-in ImageGen tool and copied unchanged to `client/public/wedding-match/homepage/`: hero-romantic-couple.png1086×1448 and categories-editorial-grid.png1774×887. They are labeled as illustrations and never attributed to real vendors/customers. Exact prompts/provenance are in [ASSETS.md](../../client/public/wedding-match/ASSETS.md). Their combined original PNG payload is about5.17MB; image compression/LCP on a slow mobile connection was not measured.
- No invented vendor counts, ratings, customers or video. The current hosted public catalog returns0items; the homepage honestly displays its empty state. Planner examples are labeled as illustrations.
- Header/footer logo symbol has a white rounded plate; the wordmark sits outside. Warm blush footer and existing global theme remain; no dark mode added.
- Independent source review found three small-text contrast gaps, fixed before the final gate. Scoped selectors avoid old mobile display overrides; mobile search inputs16px, keyboard focus outlines and native FAQ details retained.

Actual browser checks on the final build: hero image loads,8category cards and4planner cards render;1440/1024/900/390px document width fits viewport. Search submits category+keyword+location+maxPrice into browse URL and existing visible filter chips; category card goes to its actual categoryID; FAQ opens with Enter; homepage has0mascot images. On localhost Clarity does not load. Live browser confirms loaded hero/categories/planner cards, white logo backgroundrgb(255,255,255), footerrgb(241,209,197), mobile390fit/16pxcontrols, one correct Clarity tag and bodymask retained. External Clarity collect/dashboard ingestion remains UNVERIFIED per its existing report. Physical-device/slow-network/load testing was not run.

Evidence: `artifacts/home-cms-preview-20261029/` contains responsive.json, live-assets.json, live-api-checks.json and live desktop/mobile/footer screenshots. Read-only preview served final frontend on127.0.0.1:5193, proxied only public GET/HEAD categories/listings/providers and blocked all mutations; no DB was connected by that preview.

## Cleanup and final Git state

Owned preview PID32864 was checked against its exact node command/port, stopped, and ports5193/5029 have no listeners. Preview exec session16643 exited from that deliberate stop. Browser viewport override reset, QA/active hosting tabs closed and public site retained as deliverable. An older stale host tab12 remained inaccessible/unclosable; its old CMS-only package had only been selected, never uploaded. Do not submit that obsolete form. There is no remaining app_offline on site1 or pending release job.

Final branch`weddy-journey`,HEAD`3596ae832faeac95fb15cc44bc0e8e62351c788b`;177trackedchanged,214normaluntracked entries(221expanded files),0staged,1worktree; `git diff --check`exit0 with existing line-ending warnings captured separately. Numerous changes predate this scope; all were preserved. No commit/push/reset/clean. New source/assets/report must be included when transferring the uncommitted worktree.
