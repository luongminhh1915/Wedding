# Appearance release — 03/11/2026

## Deployment result — 01:25VN

- **DEPLOYED** to https://weddingmatch.website/ using exact D399 full package below. Hosting backup `/site1-before-appearance-20261103.zip` Success; ZIPs uploaded privately, both extract jobs Success at `/site1`, owned maintenance moved to `/private/deploy/appearance-20261103/app_offline.htm`; website reopened.
- `node artifacts/release-appearance-20261103/verify-live-appearance.mjs`: **19/19 PASS, exit0**, at01:22VN. Evidence: `artifacts/release-appearance-20261103/live-appearance-verification-2026-11-02T18-22-45-805Z.json`. Homepage/adminSPA exactindex,5entryassets+AdminDashboard SHA match, same-originAxios, appearance GET200/exact6boundedfields, readiness200, catalog63.
- Additional HTTPS checks: anonymous PUT `/api/upload/appearance/settings` with `{}` returned401; providersGET200, emailOTP/recovery enabled. Fresh private-config downloads before/after deployment are byte-identical. No secret/config replacement, SQL, migration, reseed, contract or financial writes.
- Actual guest Browser check: desktop homepage/menu4existingtracks rendered; mobile390x844 shows5bottomnav links, no horizontal overflow; Blog navigation succeeds. Audio remains paused, visitor45% volume preference retained. Temporary viewport reset. Screenshots: `artifacts/release-appearance-20261103/live-home-desktop.png` and `live-home-mobile.png`.
- **UNVERIFIED:** authenticated Admin/Moderator settings-save/MP3upload/delete, audible playback, real provider login, fullsuite/SQL rerun. Package build + FE18/18 +BE28/28 were verified during preparation; lint0errors/20warnings, backend4existingxUnit2031warnings. No failures/skips in targeted tests. No claim of load testing.
- Browser correction: direct `mcp__cua_repl.js` accesses the authenticated IAB; omission from ALL_TOOLS does not prove permission loss. No global plugin/security changes needed.
- Git remains main@20b292b5384254c0d250766847570adbbe86bd48;59tracked paths changed,38untracked files (37status entries),0staged,1worktree. All pre-existing changes preserved; no commit/push/reset. Next: owner acceptance of authenticated appearance authoring; no redeploy needed.

## Preparation history — superseded by deployment above

- Status: PREPARED, NOT DEPLOYED. Source/untracked changes preserved; no commit/push/reset.
- Original frontend-only ZIP SHA E9772656F61454BB70F5968ADE645DA082856EC19108C2AF6D67F9EE53F9B52A calls localhost5008. Live host appearance/settings GET404; that ZIP alone cannot complete the feature.
- Fixed production same-origin packaging, staff session/abort/singleflight/server-ACK handling, visitor-volume race and petals settings/limited density/reduced motion/mobile control placement.
- Added typed bounded4KiB privateAppearanceSettingsStorage with atomicserializedwrites/corruptfallback; GETpublic/PUTactiveModerator-or-SuperAdmin. No DB schema or financial changes.
- FE18/18 targeted navigation/music checks PASS; BE28/28 AppearanceSettingsTests PASS, no SQL. Production package build and Verify-StudyPackage PASS. Lint0errors, warnings remain; no full suite or new provider/browser E2E claims.
- Exact release: artifacts/deploy-appearance-20261103/02-WeddingMatch-Appearance.zip, SHA D39939F19B72CB97F97521D941DE781352DE24E4E3CC3A1CF153940061926004. 248ZIPentries/221files, no userdata/credentials/SQL; migration/seed startupfalse, negotiationsfalse.
- Maintenance ZIP01 contains only owned app_offline.htm marker weddingmatch-appearance-20261103-d39939f19b72. Extract01then02 to/site1; rename owned maintenancefile afterqueueSuccess. Existing privateconfig/uploads remain outside package. No host action performed in this turn.
- Follow-up: verify appearance GET200, anonymous/nonstaffPUT denial without datawrites, readiness/catalog/providerflags and frontendhashes; owner browserQA settings/MP3upload/volume.
- Rollback: restore previous verified02Nov application files and frontend patch83FF77754E18410AF74F72B4FDB8DBAD3B72E3E103F0AF813D7A075DB0ABF92A; no DBDown/restore/reseed.
