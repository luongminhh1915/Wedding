# Master upgrade — audit và checklist theo phase

Ngày bắt đầu: 18/10/2026. Baseline: branch `weddy-journey`, commit `468e507`.
Nguồn yêu cầu: Master Prompt người dùng gửi (1.482 dòng, phần A–L, 47 acceptance criteria); hai ảnh đính kèm về điều hướng planner và dropdown hoa hồng.

## Cách đọc và nguyên tắc chuyển phase

- **CÓ**: tìm thấy luồng/logic thực trong source; không đồng nghĩa đã đạt toàn bộ yêu cầu mới.
- **SỬA**: tận dụng module hiện có, bổ sung/sửa phần thiếu.
- **MỚI**: chưa có nghiệp vụ tương đương, phải bổ sung đúng phạm vi.
- `[ ]` chưa nghiệm thu; `[x]` chỉ dùng sau khi có kết quả kiểm tra. Không đánh dấu xong theo số lượng component/entity.
- Mỗi phase phải có báo cáo FE, BE, DB/migration, authorization, build/test, rủi ro còn lại. Test SQL chỉ tạo/xóa DB `WeddingPlanTest_*` do test sở hữu; không reset DB thật.
- Không đổi tài chính, migration dữ liệu cũ, mở cổng mạng hay cấu hình chung của SQL khi chưa đủ phạm vi/xác nhận. Hợp đồng, đơn hàng và snapshot cũ luôn được giữ.
- Bốn vai trò trong yêu cầu là bốn phân hệ chính; giữ nguyên sáu actor hiện có: `Customer`, `VendorOwner`, `Moderator`, `Finance`, `CustomerCare`, `SuperAdmin`. Admin trong yêu cầu ánh xạ `SuperAdmin`, không tạo role trùng.

## Bản đồ hệ thống hiện tại

| Miền | Thành phần cần tái sử dụng | Lưu ý audit |
|---|---|---|
| Hạ tầng | .NET/EF Core 10 Clean Architecture; React 19/TS/Vite; SQL Server; MediatR; SignalR | Không thay stack. `global.json`, các `.csproj`, `client/package.json` là nguồn phiên bản thực; README cũ từng ghi .NET 8/SQLite. |
| Actor/auth | JWT + BCrypt, current-user DB validation, security version, `/auth/me`, route guards | Public signup chỉ Customer/Vendor; staff do SuperAdmin cấp. SMTP/Google là tích hợp phụ thuộc cấu hình, không mặc nhiên đã sẵn sàng. |
| Vendor/category | `Vendor`, `Category`, `BusinessGroup`, `VendorBusinessGroup`, signup | Phase 2 có 5 nhóm/8 category và đăng ký đa nhóm; legacy giữ scalar đến Admin xác nhận, scoped rule engine chưa có. |
| Dịch vụ | `Listing` + `ListingMedia/Revision`; `ServiceItem` + variant/add-on | Listing là bài giới thiệu, ServiceItem là cấu hình bán/định giá. Phase 3A/B/D có SQL paging/structured details/private preview/published revisions/activity; không tạo service module thứ ba. |
| Combo | `ComboTemplate/Item/Revision`, `ComboOrder/Item`, pricing engine | Phase 3C có draft/submit/reject/resubmit/private preview/activity; combo NCC và giỏ khách vẫn là hai thực thể khác nhau, giữ single-vendor và financial snapshot. |
| Hợp đồng hiện hữu | `BookingContract`, Lead, Quote, ledger/settlement | Là hợp đồng Customer–Vendor, **không phải** hợp đồng hợp tác nền tảng–Vendor. |
| Chat/notification | Lead-scoped ChatMessage + SignalR; Notification outbox + typed contexts | Có hạ tầng thật, chưa có conversation thương lượng riêng. Không nới quyền Lead chat để giả lập negotiation. |
| Công cụ khách | Onboarding 10 câu, timeline/checklist/budget, guests/seating, thiệp/RSVP, lịch hẹn | Không bỏ chức năng để làm gọn menu; giữ onboarding và dữ liệu cưới dùng chung. |
| Deploy | private config/uploads, offline SQL migration, same-origin package + QA smoke | Không public deploy trong công việc này. Production cấm tự seed/migrate; SQLite chỉ Development tường minh/test. |

## Phase 1 — Audit và chuẩn bị

Trạng thái: **đạt gate**, đã xác minh lại sau gián đoạn ngày 19/10; kết quả cụ thể ở [báo cáo Phase 1](reports/master-phase-1.md).

- [x] Đọc hết Master Prompt; kiểm tra branch/worktree và quét cấu trúc toàn repo, phân công audit các miền FE/BE/domain/schema/tests.
- [x] Xác định module tái sử dụng, khoảng thiếu, dependencies và các ranh giới tài chính.
- [x] Đọc auth/session/routing/backend role và ownership guards; xác định các đường rò dữ liệu cần sửa sớm.
- [x] Kiểm tra SQL thật chỉ đọc: DB mục tiêu/schema/migration, số đếm và fingerprint tài khoản; không lấy PII/secret vào báo cáo.
- [x] Lập checklist Phase 1–9 và bản đồ đủ 47 acceptance criteria bên dưới.
- [x] Đính chính README về stack/DB/chạy local; cấu hình test gate chạy tuần tự, không thay service/RAM SQL.
- [x] Vá + test quyền đọc thông báo chuyển tiền: Customer/Moderator/CSKH không đọc, Vendor chỉ shop mình, Finance/Admin toàn hệ thống.
- [x] Vá + test POST review không đọc/tạo review cho Listing chưa public hoặc chủ shop bị khóa.
- [x] Vá + test tính giá/submit combo từ ID giỏ cũ không vượt qua trạng thái khóa tài khoản NCC.
- [x] Ngừng endpoint seed HTTP cũ: không cho request kích hoạt initializer, không trả danh sách mật khẩu demo sai thực tế; chỉ giữ khởi tạo theo startup policy tường minh.
- [x] Frontend 89/89 + lint/build, backend 369/369 có SQL, package/runtime smoke; dữ liệu thật không đổi.
- [x] Chốt báo cáo/rủi ro; người dùng xác nhận legacy rate giữ đến Admin xác nhận. Các quyết định tài chính còn thiếu được chặn tại phase tương ứng, không tự suy diễn.

## Phase 2 — Vendor và Service Category

Trạng thái: **đạt gate trong phạm vi Phase 2** — 395/395 backend có SQL, 96/96 frontend, package/HTTPS/security/restart đạt; signup desktop/mobile đã kiểm tra. Chi tiết ở [báo cáo Phase 2](reports/master-phase-2.md). Năm default áp dụng cho Vendor mới; không tự chuyển Vendor cũ hoặc ghi đè snapshot. Admin editor/rule engine vẫn thuộc Phase 5, chưa hoàn thành.

| Công việc | Hiện trạng / source |
|---|---|
| Năm nhóm kinh doanh có mô tả/chính sách do backend cấp | **ĐÃ CÓ** BusinessGroup + Vendor–Group unique/FK; giữ 7 Category ID/slug, không gộp xóa danh mục. |
| Mức mặc định 3/8/10/12/3%; Admin quản lý, có audit | **ĐÃ LƯU DB**, FE đọc API; **CHƯA CÓ** Admin editor/effective/audit, tiếp tục Phase 5. Không có endpoint public/Vendor sửa tỷ lệ. |
| Đổi tên planner A–Z, thêm Tiệc & Sự kiện khác | **ĐÃ CÓ** planner full name/default12%/basis marker, giữ ID/slug, category thứ 8; A–Z chưa được duyệt base thì fail-closed, legacy và snapshot không đổi. |
| Signup chọn nhiều group, summary, xác nhận đã đọc policy | **ĐÃ CÓ** validate group/ack/active phía server, từ chối mọi rate client gửi, atomic user/vendor/membership/ack; AC1–4 đã kiểm thử. |
| Vendor xem phạm vi và default policy | **ĐÃ CÓ** tab read-only dùng API thật, phân biệt legacy/default; thương lượng/history/rule thuộc Phase 5–7, chưa gắn nút giả. |
| Gate | **PASS** persistence/relogin/tamper/atomic rollback/migration preservation, FE/BE/SQL/package. Admin policy update chưa tồn tại, test quyền editor chờ Phase 5. |

## Phase 3 — Service & Combo

Phụ thuộc: danh mục/membership Phase 2; xác nhận giá combo cố định so với định giá khách/bàn/giờ trước khi thay công thức.

Module **Phase 3A–3D đã triển khai và đạt gate trong phạm vi báo cáo** — list/search/paging, structured service/A–Z metadata, private preview, separate activity, published revisions và Combo lifecycle. Xem [3A](reports/master-phase-3a.md), [3B](reports/master-phase-3b.md), [3C/3D](reports/master-phase-3cd.md). Browser/API QA, sửa mobile và migration local3C/3D đã hoàn tất; một confirm UI bị lỗi công cụ được kiểm chứng tiếp bằng API, không gọi đây là E2E thuần UI đầy đủ hoặc đã deploy.

Gate source cuối Phase 3A: **437/437 backend có SQL + 106/106 frontend**, lint/build/package/HTTPS/security/restart đạt; Swagger local200, kiểm tra browser desktop/mobile/Back/logout và policy mới/legacy đạt. Giữ nguyên dữ liệu thật và toàn bộ thay đổi hợp lệ chưa commit.

Gate Phase3B: **492/492 backend có SQL +121/121 frontend**, build/lint/package/HTTPS/security/restart đạt; đã backup+VERIFY rồi áp dụng migration nullable vào DB local, fingerprint dữ liệu cũ không đổi. Browser lưu/đọc lại metadata+giá numeric, caret/paste và form mobile đạt; music/Combo lifecycle không được đánh dấu xong theo gate này.

Gate source cuối Phase3C/3D sau sửa mobile: **565/565 backend có SQL +142/142 frontend**, lint0error/10warning hiện hữu, production build/package/6 negative guards/HTTPS/account/security/restart đạt. Artifact `artifacts/readiness/20261019-121724-21f6f351/ready.json`, VerifiedUtc `2026-10-19T12:19:43.6549867Z`; migration additive `20261019115608_PublicationRevisions` đã áp dụng local sau backup+VERIFYONLY,53 bảng dữ liệu gốc khớp trước/sau. Source/hash artifact trong báo cáo.

- [x] **My Services — phần list Phase 3A**: tái sử dụng `ListingTable`/form cũ; API SQL search/title/category/location + status AND, own-shop paging/count/updated time, URL filters, mobile states, giữ endpoint array cũ.
- [x] **Phase3C/3D — Activity riêng**: Listing/Combo có IsActive riêng moderation, owner/staff guards phù hợp; tạm tắt không tự duyệt/bỏ staff Hidden, duyệt nội dung không tự đổi trạng thái kinh doanh đã công bố. Public/direct-price kiểm activity thật, không chỉ filter FE.
- [x] **Phase3B** form nhóm section, structured includes/excludes/quy trình/phụ phí/điều kiện/portfolio/video; giữ full album/cover/upload; NULL/omitted metadata không xóa nội dung cũ, atomic save.
- [x] **Phase3B** Full-Service scope/capacity/stages/outsourcing/mô tả management fee/payment terms; Category flag thật, không coi combo là Full-Service hoặc suy ra approved commission basis từ text.
- [x] **Service Customer Preview — Phase3A/3D**: dùng chính trang detail, read-only banner/CTA-off; owner/staff đọc đúng revision, draft foreign/customer deny, cache theo actor/revision, Back giữ filter. Combo preview đã bổ sung ở3C, không suy ra từ preview service.
- [x] **Phase3B/3C** My Combos: tab độc lập giữ ServiceItem/variant/add-on/participation cũ; editor/card/history/private preview và loading/error/retry/empty thật, staff dùng chung editor với quyền riêng.
- [x] **Phase3C** draft/submit/reject reason/resubmit/active riêng: save tạo immutable proposal, note/reviewer/time lưu DB, stale review deny, reject/resubmit giữ history; không auto-publish kể cả staff save.
- [x] **GIỮ + regression** own-vendor, approved/active defaults và allowed options, Listing/shop/account eligibility, server-side totals/rounding/old-order snapshot. Không đổi unit pricing/discount thành fixed-sale hoặc commission formula mới.
- [x] **Phase3C** gallery/upload/cover/terms/UTC validity + private preview dùng dịch vụ/ảnh thật; omitted Content giữ metadata, object thay thế; savings dùng pricing hiện hữu, không tự đặt giá/ảnh/commission.
- [x] **Phase3C/3D** Listing/Combo published version độc lập proposal, immutable snapshot/authors/history, review atomic/notification, dependency revalidate; không tính lại confirmed order/contract/paid commission. ServiceItem cũ không được tuyên bố đã có revision mới chỉ vì có Listing/Combo revision.
- [x] Gate source/local release: Vendor A không đọc/sửa private B, role/identity/inactive/stale denial; draft không public cả direct GUID; create/edit/submit/resubmit/preview; FE142/BE565+SQL/build/package/security. SQL upgrade từ Phase3B hai lần bảo toàn legacy/snapshots.
- [x] Browser/API QA: Vendor create/draft/upload/private preview, Moderator UI reject+approve Combo, Vendor API resubmit/history, published-edit baseline preservation; Listing queue/reject/reason/history/edit UI và final resubmit/approve API. Confirm UI có lỗi công cụ được ghi riêng, không coi E2E thuần UI đã đạt. Mobile375/375 sau sửa thumbnail; đã reset/đóng/dọn đúng fixture/process riêng. Xem báo cáo3C/3D.
- [x] Backup COPY_ONLY/CHECKSUM+VERIFYONLY, apply additive migration3C/3D vào WeddingMatch_Dev:11→12; fingerprint/count53 bảng gốc khớp trước/sau. Main runtime5008 health/catalog/Swagger200, Debug0warning0error; không reset/seed/backfill. Gate sau mobile fix đã đạt19:19:43VN.

## Phase 4 — Moderation & Customer

Phụ thuộc: lifecycle/preview/revision Phase 3; quyết định review eligibility và tổng hợp rating trước đổi chính sách.

**Phase4A đã đạt gate:651/651 backend có SQL +146/146 frontend**, build/lint/package/HTTPS/security/restart, targeted86 và browser/API QA đạt. Kiểm actor/ownership thực, revalidate dependencies trước duyệt, requiredReviewVersion chống duyệt Pending cũ, atomic notification/audit; sửa lỗi EF khi thay variant/add-on và nhãn Pending đúng ngữ cảnh. Không có migration mới, không đổi tài chính; runtime5008 cập nhật,55 bảng gốc khớp trước/sau. Artifact `artifacts/readiness/20261019-170757-20a732e7/ready.json`; VerifiedUtc17:10:14 ngày19/10 (00:10:14 ngày20/10VN). Xem [báo cáo4A](reports/master-phase-4a.md). Queue paging/filter/rating và toànPhase4 chưa hoàn tất.

- [x] **Phase4A targeted/runtime/full gate** ServiceItem own/moderation/consent guard, approve/reject đúng nội dung, reason/notification/audit, child-replacement SQL và bảo toàn confirmed/paid snapshots; runtime local đã cập nhật, không seed/rewrite data. Chi tiết warnings/artifact/boundaries trong báo cáo4A.

**Cập nhật Phase4B 20/10:**861/861 BE có SQL +199/199 FE, package/realHTTPS/security/restart đạt; unified queue/service+combo filters/Completed-only review và category→contract provenance đã tích hợp. Independent review targets/aggregation và các phase tài chính tiếp theo vẫn chưa hoàn tất; xem báo cáo `service-category-phase4b-2026-10-20.md`.

- [x] **Phase4B** Queue Listing/ServiceItem/Combo thống nhất: SQL paging/search/type/status, newest proposal thay vì published baseline, private full detail/gallery, required revision/version và reason; dùng lại decision handlers/notification/audit. API role từ DB và browser duyệt combo giả lập đạt.
- [x] **Phase4B regression** moderation khác IsActive, giữ published baseline và confirmed/paid snapshots; ServiceItem/Listing/Combo dependency và actor guards đạt full SQL gate. Không suy ra toàn Phase4 đã xong.
- [x] **Phase4B** combo filters AND: giá dự kiến/địa điểm shop/khu vực phục vụ/số dịch vụ/tiết kiệm/đánh giá shop; sort giá/newest/rating/savings. Dùng engine/unit pricing hiện có; không tạo rating combo hoặc rate chung mới.
- [x] **Phase4B** service và combo filters URL/back/reload/detail, chips/xóa filter, combo mobile dialog; không reset giỏ khi đổi URL. Public chỉ eligible approved+active. Browser bộ lọc dịch vụ desktop/mobile và queue đạt; chi tiết giới hạn browser combo trong báo cáo.
- [x] **Phase4B — quyết định review đã chốt** chỉ Customer có đúng giao dịch Completed được tạo đánh giá mới; Confirmed quá ngày cưới không tự đủ điều kiện. Giữ đánh giá tư vấn cũ với nhãn riêng và weighting hiện hữu, không backfill/xóa/tính lại điểm. Role/ownership/public/duplicate/dispute kiểm server; modal loading/error/empty thật.
- [ ] **MỚI/SỬA** target Shop/Service/Combo độc lập, distributions/filter/sort/page. Không tự trộn điểm hoặc đổi verified/unverified weighting hiện hữu khi chưa có quyết định.
- [ ] Gate: direct-private URL/POST denial; mod approve/reject→notification→Vendor read; filters AND + paging/back; duplicate/spam/ownership/eligible review; FE/BE/SQL/build.

## Phase 5 — Commission System

Phụ thuộc: Phase 2–4; **business-gated**, không kích hoạt công thức chưa chốt.

- [ ] **CÓ/SỬA** category default SQL; thay resolver hiện chỉ vendorRate→categoryRate (`CommissionRules.cs:7`) bằng hệ thống scoped/effective rules.
- [ ] **MỚI** scope system/category/vendor/vendor+category/service/combo; priority như Master; cấm overlapping same-scope rules không xác định, ngày hiệu lực và lý do/audit.
- [ ] **MỚI** commission basis type/amount/exclusions và immutable rule/basis/rate snapshot tại giao dịch; A–Z không mặc định tính trên tổng chi phí cưới.
- [ ] **SỬA** Admin Commission Management + Vendor Chính sách hợp tác; default vs actual/override/source/date/history rõ ràng.
- [ ] **CÓ, GIỮ** K1/K2, payment ledger/webhook, credit note/escrow/settlement trừ khi có yêu cầu/xác nhận khác. Không tính parent combo + child cùng một giá trị.
- [x] **SỬA — phần bảo toàn snapshot** completion từ chối nếu thiếu/sai original rate trước đổi trạng thái hoặc tạo khoản nợ; không fallback rate shop hôm nay/8%, không backfill đoán. Category/group/source/version chốt một lần, legacy thiếu provenance giữ NULL. Công cụ xử lý ngoại lệ tài chính vẫn thuộc các mục chưa hoàn thành bên trên.
- [ ] Gate: 5 mức ưu tiên/effective expiry/overlap, base A–Z, discount allocation, rounding, historical snapshot, cancellation/payment non-regression, Admin-only + audit; FE/BE/SQL/build.

## Phase 6 — Negotiation System

Phụ thuộc: model rule/basis Phase 5. **Nghiệp vụ mới**, tái sử dụng transport/outbox, không dùng nhầm Quote khách hàng.

- [ ] **MỚI** request scope/current backend policy/proposed rate+base/reason/term/evidence/assignee/status/history.
- [ ] **MỚI** dynamic reason form (6 lý do), câu hỏi hỗ trợ không bắt buộc số liệu NCC chưa có, optional cooperation plan không tự thành nghĩa vụ, summary trước gửi.
- [ ] **MỚI** counteroffer/version/actor/response; Vendor phản hồi, Moderator đề xuất/chuyển duyệt, Admin quyết định cuối.
- [ ] **SỬA/MỚI** negotiation conversation typed scope, persistent messages/page/participants, private attachments + notifications. Lead chat hiện staff read-only, FK bắt buộc LeadId (`ChatRoomAccess.cs:37`, `ChatMessage.cs:8`).
- [ ] **MỚI** chat không đổi rate; Approved request khác Active rule; kích hoạt có điều kiện/effective date sau quyết định liên kết hợp đồng.
- [ ] Gate: cross-shop/chat denial, role matrix, validation/tamper/private files, counteroffer history, approve≠activate, no mutation from message; FE/BE/SQL/build.

## Phase 7 — Partnership Contract Management

Phụ thuộc: rule Phase 5 + negotiation Phase 6; **không dùng BookingContract Customer–Vendor làm hợp đồng hợp tác**.

- [ ] **MỚI** hồ sơ platform–Vendor, contract types/status/dates/participants; transitions hợp lệ và trạng thái signed-outside-system tường minh.
- [ ] **MỚI** Admin create/upload/search/filter; Vendor My Contracts, private PDF preview/download, vendor ownership, Moderator không mặc định toàn quyền.
- [ ] **MỚI** immutable version/file checksum/upload actor; parent phụ lục/gốc; không overwrite signed document và không coi view/acknowledge là chữ ký pháp lý.
- [ ] **MỚI** links negotiation/rule/scope/expiry; future effective changes, expiry fallback, giữ history/order snapshots.
- [ ] **SỬA** typed notification contexts mới cho partnership/negotiation/policy, tận dụng owner-scoped Notification Center; reminder/new/expiry/update.
- [ ] Gate: upload validation/MIME/size/traversal + secure download, IDOR/Moderator deny, versions/appendices/date transitions/expiry, rule links immutable; FE/BE/SQL/build.

## Phase 8 — UI/UX

Phụ thuộc: nghiệp vụ các phase trên hoạt động; ảnh tham chiếu không thay thế chức năng thực.

- [ ] **CÓ/SỬA/MỚI** thiệp đang lưu `MusicUrl` và render audio controls (`WeddingInvitation.cs:18`, `InvitationView.tsx:205`); thiếu audio upload/source model/YouTube embed validation/fallback. Giữ photo upload/RSVP; không tải/tách âm thanh YouTube.
- [ ] **MỘT PHẦN Phase3B** shared MoneyInput đã có model+validation+caret/paste/IME guard, dùng ở Listing min/max, quote unitPrice/deposit, budget3fields;121FE unit + browser caret/paste đạt. Tiếp tục áp dụng các ô tiền còn lại/kiểm IME thực; không format tỷ lệ % như tiền, không đổi finance rules.
- [ ] **SỬA** customer nav bỏ hàng “Tiện ích song song” (`CustomerDashboard.tsx:132`), gom chức năng vào menu có phân cấp; giữ deep links/active state/notification/account/onboarding.
- [ ] **SỬA** Vendor/Admin/Moderator role-specific navigation, không nhét Service/Combo trong lịch hẹn; desktop/sidebar, mobile drawer phù hợp branding navy–cream–gold.
- [ ] Gate: responsive keyboard/focus/active routes; upload/play/pause/YouTube unavailable/autoplay; MoneyInput caret/bounds/serialize; FE/BE/SQL/build + browser verification.

## Phase 9 — Testing & Integration

Phụ thuộc: các phase đã nghiệm thu, decisions đã ghi nhận. Gate cuối **không thay thế gate từng phase**.

- [ ] Chạy đủ FE unit/lint/build, BE unit + SQL integration (không skip SQL), disposable schema migration/update/idempotency + dữ liệu cũ.
- [ ] Full actor matrix và ownership, public drafts deny, private evidence/contracts, vendor self-approve/self-rate tampering.
- [ ] End-to-end signup→service→moderation→combo→customer→booking→snapshot/payment/review; negotiation→counteroffer→approval→contract/rule→expiry.
- [ ] Runtime Production package same-origin/HTTPS/health/SPA/API isolation/restart/persistent private files; backup restore drill, không public deploy tự động.
- [ ] Final report exact source/artifact/test counts, skipped/warnings và integrations/host conditions chưa xác nhận. Không nói “đủ 100 khách” nếu chưa load test có giới hạn.

## Bản đồ 47 acceptance criteria

| AC | Đích kiểm thử/nghiệm thu | Phase | Baseline |
|---|---|---|---|
| 1–4 | Đa nhóm, default đúng, không tự rate, lưu membership | 2 | ĐÃ NGHIỆM THU — backend/SQL/persistence/HTTP/UI |
| 5 | Draft/edit/submit service | 3 | Phase3B/3D code+SQL gate đạt, published revisions giữ live baseline; browser/main migration đang thực hiện |
| 6–7 | Moderator duyệt/từ chối; public chỉ approved | 4 | Listing/Combo latest-revision/reason/notification/public guards đạt gate3C/3D; queue tổng thể Phase4 còn lại |
| 8 | Own approved+active combo components | 3 | ĐÃ kiểm thử3C defaults/options/listing/vendor/account + IDOR + server identity |
| 9 | Total/savings đúng | 3 | Dynamic pricing hiện hữu giữ regression/tamper/rounding/snapshot; chưa thêm fixed-sale |
| 10–11 | Combo moderation + private preview | 3–4 | Phase3C code/FE/BE/SQL gate đạt; browser E2E cuối đang thực hiện |
| 12–13 | Filters nhiều điều kiện/detail đúng | 4 | Phase4B service/combo filters + URL/cart preservation đạt FE/BE/SQL/package; phạm vi browser ghi trong báo cáo20/10 |
| 14 | Eligible review/anti-duplicate/reply/moderation | 4 | Completed-only new reviews + giữ nhãn legacy đạt; Shop/Service/Combo targets độc lập vẫn thiếu |
| 15–16 | Music + responsive navigation | 8 | SỬA/MỚI |
| 17–19 | Default, override, priority | 2,5 | CÓ scalar/category, engine MỚI |
| 20–23 | Base/A–Z/no double charge/history | 5 | SỬA/MỚI, business-gated |
| 24–31 | Form/request/chat/counteroffer/final approval/history/no chat mutation | 6 | MỚI trên transport hiện có |
| 32–38 | Partnership create/private files/IDOR/preview/version/rule link | 7 | MỚI, không nhầm booking |
| 39–40 | Expiry fallback + retained financial/contract history | 5,7 | MỚI/SỬA |
| 41–44 | Persistence/auth/migration/API-FE sync | Mỗi phase,9 | Gate, không tick chỉ theo source |
| 45–47 | Build/test report/regression | Mỗi phase,9 | Gate, kết quả từng lần |

## Quyết định cần xác nhận (không tự suy diễn)

1. **ĐÃ CHỐT 19/10/2026:** tỷ lệ scalar NCC cũ giữ cho giao dịch mới tới khi Admin xác nhận chuyển đổi/policy mới có hiệu lực. Không tự gán nhãn negotiated đã được duyệt; snapshot original tại thời điểm xác nhận hợp đồng giữ nguyên. Ngoại lệ riêng hợp đồng chỉ qua adjustment workflow được Admin duyệt, xem decision record.
2. **ĐÃ CHỐT 19/10/2026 — trước Phase5:** ordinary base trước VAT sau giảm giá Vendor chịu, trừ khoản chi hộ có chứng từ đã được Admin xác minh/chấp thuận; không trừ VAT hai lần, không trừ platform-funded discount khi Vendor vẫn được thanh toán đủ, không coi chi phí nội bộ là chi hộ; base không âm. A–Z chỉ phí quản lý/tổ chức thực tế được Admin duyệt. Thiếu dữ liệu phải bổ sung và chặn áp dụng công thức mới, không đoán. Xem decision `2026-10-19-commission-basis-and-activation.md`.
3. **Nếu mở rộng pricing sau Phase3:** cần xác nhận fixed-price so với dynamic đơn vị khách/bàn/giờ/gói, quy tắc thêm/bớt/đổi component và nhiều item/category. Phase3C/3D giữ engine và một item/category hiện hữu, không đổi công thức để hoàn thiện nội dung/lifecycle.
4. **ĐÃ CHỐT nguyên tắc trước Phase5:** mixed-category combo phân bổ giá trị/discount theo từng dịch vụ/rate, không áp một rate chung chưa được Admin duyệt và không thu trùng parent+child. Model/chi tiết phân bổ khi thiếu dữ liệu legacy vẫn cần quyết định; giữ K1/K2/escrow, chưa triển khai engine mới trong Phase3.
5. **ĐÃ CHỐT 20/10/2026 — review mới:** chỉ giao dịch Completed được tạo review mới; review tư vấn cũ giữ và gắn nhãn riêng. Đã áp dụng + regression, không đổi weighting cũ. Rating Shop/Service/Combo độc lập và công thức tổng hợp mới vẫn chưa chốt/triển khai.
6. **ĐÃ CHỐT 19/10/2026 — trước Phase6/7:** activation cần đủ **Admin duyệt + tới ngày hiệu lực + hợp đồng/phụ lục liên quan được ghi nhận ký ngoài hệ thống**. Approval/chat không tự Active; không coi view/acknowledgment là chữ ký. Moderator không có quyền duyệt cuối chính sách tài chính.

Các câu trả lời nằm ở `docs/decisions/2026-10-19-legacy-and-contract-commission-adjustments.md` và `docs/decisions/2026-10-19-commission-basis-and-activation.md`. Phase2 dùng nhóm/default backend cho NCC mới, giữ legacy; A–Z chưa có approved basis phải fail-closed thay vì lấy12% toàn ContractValue. VAT/base/ba điều kiện activation đã có quyết định nhưng engine/schema/workflow Phase5–7 chưa triển khai; unsettled allocation/offset/refund vẫn chưa chốt. Không tự kích hoạt hoặc sửa snapshot từ descriptive metadata/approve nội dung.

### Bổ sung Phase 5–7: điều chỉnh riêng hợp đồng đang thực hiện

- [ ] Giữ **Original Commission** bất biến; mô hình **Effective Commission** lấy từ original + các adjustment hợp lệ, không overwrite lịch sử.
- [ ] Vendor đề xuất từ đúng hợp đồng của mình; scope toàn hợp đồng hoặc phần chưa quyết toán (mặc định B nếu đã có settlement); eligibility không gồm hợp đồng hoàn tất và quyết toán toàn bộ.
- [ ] Moderator tiếp nhận/chat/counteroffer; Admin quyết định cuối; ghi code/status/actor/rate/base/scope/effective date/reason/evidence/agreement links.
- [ ] Activation là bước riêng có điều kiện; approval/chat không tự thay ledger; không ảnh hưởng hợp đồng khác hoặc policy toàn shop.
- [ ] Chênh lệch đã thanh toán dùng adjustment/credit/refund có audit, không sửa PaidAmount/payment events cũ. Công thức cho unsettled base và cơ chế offset/refund còn cần quyết định.
- [ ] Regression: 10% original → 7% effective chỉ hợp đồng A, B giữ nguyên; paid history/partially settled/fully settled, concurrent approvals, dates, rollback và ownership.

### Ưu tiên bổ sung 20/10/2026 — hướng dẫn và hành trình nhà cung cấp

- [x] Tổng quan NCC với dữ liệu thật của riêng shop, việc nên làm tiếp theo; không dựng wizard bắt buộc hoặc tỷ lệ hoàn thành giả.
- [x] Hướng dẫn chi tiết 10 bước, 7 FAQ, chỉ rõ thao tác/kết quả/ai xử lý tiếp, CTA tới chức năng hiện có; combo/lịch hẹn tùy chọn.
- [x] Hướng dẫn theo ngữ cảnh trong từng tab; URL giữ bước khi Back/reload, giữ bookmark bộ lọc dịch vụ cũ.
- [x] API chỉ đọc, actor/ownership từ DB, số liệu SQL tổng hợp; không schema/migration/ghi dữ liệu tài chính.
- [x] Desktop/mobile, backend37 test mới +688 test đầy đủ có SQL, frontend155 test; build FE/BE và lint đạt trong phạm vi báo cáo.
- [x] Cập nhật API local; SHA-256/số dòng 55 bảng nghiệp vụ + migration history không đổi sau restart. Không deploy/push.

Chi tiết: `docs/reports/vendor-journey-guide-2026-10-20.md`. Đây là phần hướng dẫn được ưu tiên theo yêu cầu mới, **không đánh dấu hoàn tất các module tài chính/Phase4–9 còn lại**.

### Ưu tiên bổ sung 20/10/2026 — danh mục xuyên suốt, Phase4B

- [x] Tìm dịch vụ theo dropdown + icon categories từ API, keyword/area/price/shop-rating AND, sort/paging/URL/back/reload/mobile. Giữ navy–cream–gold.
- [x] Vendor tạo Listing chỉ trong active business-group membership; ServiceItem kế thừa category từ Listing, không cho client tự gán group/rate. Edit dịch vụ cũ không đổi category.
- [x] Admin bổ sung nhóm kinh doanh add-only + lý do/audit; không đổi scalar legacy, không tự xác nhận chính sách mới hoặc làm thay đổi hợp đồng.
- [x] Confirm contract lưu immutable category/group/rate provenance; CategoryDefault cho NCC mới, LegacyVendor giữ mức cũ; A–Z chưa approved basis tiếp tục chặn.
- [x] Additive migration13 thêm6 cột nullable; backup COPY_ONLY/CHECKSUM + VERIFYONLY;55 bảng nghiệp vụ cũ khớp số dòng/SHA-256 sau upgrade/restart. Main API5008 cập nhật, không seed.
- [x] Gate hiện tại: **861/861 backend,0 skip,có SQL;199/199 frontend; lint0error/10warning cũ; FE/BE/package/HTTPS/security/persistence-restart đạt**. Artifact `artifacts/readiness/20261020-002151-460226f2/ready.json`, VerifiedUtc00:24:10 ngày20/10 (07:24VN), gồm sửa short label A–Z trên mobile.
- [ ] Phần còn lại theo thứ tự phụ thuộc: review targets độc lập (chưa đổi aggregation), scoped commission/basis engine, negotiation, partnership contracts và contract-specific adjustment; music/navigation/MoneyInput coverage; final integration/restore/load/host verification. Không tuyên bố toàn Master Prompt đã hoàn tất hoặc đã public deploy.

Báo cáo tích hợp: `docs/reports/service-category-phase4b-2026-10-20.md`; snapshot chi tiết: `docs/reports/category-contract-snapshots-2026-10-20.md`.
