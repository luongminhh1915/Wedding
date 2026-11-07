# Deploy 02/11/2026

- Domain: https://weddingmatch.website/; production SQL Server `db_ad0009_weddingmatch`.
- Branch/main HEAD20b292b; existing tracked/untracked work preserved; no commit/push/reset.
- Release ZIP SHA256: `8F592B6B81828D976D8993473315F8256808DEFF1AA53A2D8776C90D35775550` (247entries; host-upload checksum verified in preceding work).
- Scope: batch/session moderation, active staff service editing, private verification documents, navbar music/listen-only guests, petals. Negotiation foundation remains disabled.
- Initial startup500.30: package required22 while host had21. No existing data was changed by the failed startup.
- Owner explicitly approved migration22 on02/11. Local TCP1433 timed out; SQL Studio rejected unsupported script types without applying it.
- A temporary one-off deploy utility on the hosting server executed pinned additive SQL in a guarded transaction; no public query endpoint. Applied at17:01VN. Original IIS config restored afterward; normal app startup migrations/seed remain off.
- SQL SHA256: `5D35383CF53027E272FCFBB7F2C9FB001042BEFF76C6CF19C6DEFCBC08F8494C`.
- Schema22:64tables;2new empty negotiation tables;9trusted FKs/14indexes. Original62table row counts and column shapes preserved. Users82/Vendors58/Listings63; contracts/commissions/payments0. No financial policy activation or snapshot rewrites.
- Fresh pre-release backup remains private; prior restore/CHECKDB/owned cleanup passed. No restore/Down/reseed on production.
- LIVE: `/health/ready`200; homepage200; listings API63;5frontend entryJS/CSS hashes match exact release. Browser homepage displays new navbar music control and marketplace. No new production fixture/accounts/uploads created by this deploy.
- Earlier gates, not rerun here: frontend541/541; backend1369/1369 (actual isolated SQL); lint0errors/14warnings; package smokePASS. Owner requested skipping further tests.
- Remaining: authenticated actor/moderation/provider browser flows after deploy UNVERIFIED; Phase6 UI/activation unfinished. Existing provider config was not edited.
- Final cleanup: IIS content matches the original configuration; temporary runner moved out of `/site1` into `/private/deploy`, owned maintenance file moved private. Readiness remains200. Git:55tracked paths changed/33untracked files/0staged; `git diff --check` exit0 (line-ending warnings only).
- Local evidence (ignored artifacts): `artifacts/release-handoff-20261101/migration22-hosted-result.json`, `live-release-check.json`, `live-home-deployed.png`. Private backups/config never included in Git or siteZIP.

- UI patch22:32VN: owner uploaded `Frontend-Paging-MobileNav.zip` (SHA83FF77754E18410AF74F72B4FDB8DBAD3B72E3E103F0AF813D7A075DB0ABF92A) and extracted to `/site1`; earlier `/private/deploy` extraction was private-only. Root independently fetched5entryJS/CSS and AdminDashboard-CzK6H1GB.js: all hashes match, home/ready200/catalog63. Admin chunk SHA89D195B28CC2F6A65E11190FFAD2245BFF77AFE47CDB33D1963DBB2CBA989F82. No SQL/config/IIS changes or private cleanup in this verification. Mobile/authenticated UI and final WorkQueue state UNVERIFIED.
