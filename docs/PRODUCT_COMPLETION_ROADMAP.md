# Wedding Match — cách tư duy và hoàn thiện sản phẩm từ checkpoint hiện tại

Ngày **22/10/2026**. Đọc [đặc tả](PRODUCT_FUNCTIONAL_SPEC.md), [kiến trúc](ARCHITECTURE_AND_OPERATIONS.md), [handoff](AGENT_HANDOFF.md) và [master checklist](master-upgrade-checklist.md). Đây là backlog có dependencies/acceptance, **không phải yêu cầu tự động làm hết**. Yêu cầu cuối phiên hiện tại là tài liệu rồi dừng.

## 1. Đánh giá sản phẩm hiện tại

| Phân hệ | Code hiện có | Còn thiếu để nghiệm thu |
|---|---|---|
| Customer core | Auth/onboarding/planner/budget/guest/thiệp/RSVP/appointment/discovery/cart/order/chat/quote/booking/review | Extended browser, keyboard/mobile/caret, nhiều-Lead/stale/session race, provider/storage thật |
| Vendor core | Journey/guide, open-category unified services, fixed combos/public moderation, sales/contract/private content lifecycle | Full integrated browser, source-change/re-review boundaries và các actions theo quyền |
| Staff core | Quản lý actor, moderation/review queue/confirmation, notification, legacy finance/support | Browser actor matrix/HTTP integration rộng; chưa là commission-management UI mới |
| Phase5 foundation | Policy/basis/adjustment record+API+guards, migration17, SQL/concurrency/history tests | Numeric basis/evidence, effective resolver, financial snapshots, activation, effective delta/reconciliation, UI |
| Phase6 negotiation | Transport/audit/outbox patterns sẵn để tái sử dụng | Typed business workflow/conversation/counteroffer/files/assignment/history/notifications |
| Phase7 partnership | Chưa có module chính thức | Platform–Vendor contract/document/version/signed evidence/private access/links/expiry |
| Release | Packaging/private config/offline SQL/local smoke tooling | Latest package gate, real host/DNS/HTTPS/providers/restore/load; deploy tạm gác |

Không nói “toàn bộ yêu cầu hoàn tất”. Không xây lại các core module để có giao diện mới nhanh hơn. Tiếp cận gap cụ thể theo source và acceptance.

## 2. Thứ tự phụ thuộc đúng — tránh vòng lặp Phase5↔7

```text
Phase5 foundation hiện có
  → basis dữ liệu số/evidence + calculator/snapshot contract, CHƯA ACTIVE
  → Phase6 negotiation (quyết định có cấu trúc, chưa thay tiền)
  → Phase7 partnership/signed-document linkage
  → tích hợp resolver/activation Phase5 đủ ba gate
  → effective adjustment/manual Finance workflow đã được chốt
  → UI tích hợp khi user resume UI
  → full E2E / release readiness → deploy khi user cho phép
```

Không thể bắt “Phase5 activation hoàn tất trước Phase6” trong khi signed linkage chưa có ở Phase7. Phân biệt **foundation có thể nghiệm thu riêng** với **activation cần dependency**; không giả signed record hoặc bỏ điều kiện để đóng checkbox.

Hiện ConfirmContract vẫn dùng legacy ResolveRate/Split(ContractValue). Guard approved per-component basis mới bảo vệ **duyệt policy Combo**, chưa được nối vào runtime confirm booking. Đây là gap quan trọng trước rollout mới, không được ghi “hợp đồng Combo mới đã dùng engine mới”. Không tự thay legacy path để giải quyết gap này trong task docs.

## 3. P0 — bảo toàn và tiếp nhận, trước bất kỳ coding task nào

1. Lấy đủ worktree/untracked, đọc checkpoint/source/config/tests/schema; kiểm Git đúng AGENTS. Nếu thiếu migration/feature untracked thì dừng tuyên bố đủ source.
2. Xác định nhiệm vụ user hiện tại; chọn một module/gap. Không chạy app main hoặc migrations để audit tài liệu.
3. Record baseline/dirty paths/commands/actors/fixtures. Code đúng giữ nguyên, không reset/clean/checkout đè; file đang thay từ agent khác phải phối hợp scope.
4. Secrets, dữ liệu thật và quyền production tách khỏi source bàn giao. Test fixtures synthetic, SQL unique owned only.

Acceptance: người nhận giải thích được ba loại hợp đồng/agreement, legacy vs new engine, invariants và first task; không bị phụ thuộc đọc chat.

## 4. P1 — hoàn thiện Phase5 inactive data/calculation foundation

### 4.1 Numeric commission basis và evidence

Thiết kế additive quanh models hiện có; descriptive BasisDefinition/ExcludedCostsJson không đủ để tính tiền.

- Lưu service value, inclusive/exclusive VAT rõ ràng, valid Vendor-funded vs platform-funded discounts, approved pass-through từng khoản/evidence/admin actor/time, A–Z approved management fee.
- Lưu formula version, source/target/revision, effective window, approval decision, raw inputs và computed base/amount; immutable khi chốt theo workflow.
- Xác thực server: không double VAT, không lấy nội bộ làm chi hộ, không dùng evidence Pending/Rejected, không âm/NaN/missing/inconsistent totals, đúng owner/service/revision/category.
- Combo allocation riêng commission, không copy DiscountShare rồi gọi là approved basis. Missing policy hoặc numeric/evidence data thì fail-closed new path; legacy snapshot không bị sửa/backfill.
- Chọn DTO/schema tối thiểu nhưng đủ đối soát, không cất JSON mô tả thay numeric controls. Nếu nghiệp vụ quan trọng chưa rõ, đưa câu hỏi và giữ activation dormant.

Acceptance tests: VAT inclusive/exclusive, Vendor/platform-funded discounts, pass-through pending/rejected/approved/foreign, A–Z only management fee, negative/missing inputs, combo mixed rates/no double charge, decimal precision/approved rounding, audit/ownership/concurrency/SQL persistence. Không tự đặt số liệu thuế/chi hộ thật.

### 4.2 Resolver và immutable snapshots — thiết kế/test nhưng chưa bật

- Tái sử dụng rule6 scopes theo5 mức đã chốt. Filter approved/effective/not expired/valid signed link/basis đủ; không last-write-wins.
- Same exact target overlap guard đã có; phải giữ inclusive-window semantics hoặc hỏi trước khi thay; hai Submitted overlap cần Admin reject một bản, không tự chọn bản mới nhất.
- Service vs Combo cùng cấp cho một component là câu hỏi chưa giải quyết. Không tự xếp Service cao hơn Combo để test pass.
- New snapshot cần đầy đủ inputs/base/rate/amount/policy/formula/revision/approver/time/document refs. Không overwrite các trường confirmed legacy; thiếu historical provenance giữ thiếu, không suy từ current catalog.
- Chỉ tạo/bật path mới sau signed dependency và explicit rollout; không thay toàn bộ ConfirmContract/FinancialContractService trong một patch lớn.

Acceptance:5-level precedence đã xác nhận, future/effective/expiry/boundaries/ambiguous conflicts/fallback, valid vs unsigned/expired docs, old contracts unchanged, one-time snapshot/idempotence/atomic failure, cancellation/payment non-regression. Nếu chưa signed model thì test interface/pure decisions nhưng **không mock success trong production**.

## 5. P2 — Phase6 Negotiation backend/domain

- Đây là platform–Vendor request, không Quote khách và không Lead chat được mở rộng tùy tiện.
- Request scope/targets/current policy lấy server, proposed rate+basis/term/reason/evidence; optional cooperation plan không tự thành nghĩa vụ.
- Sáu nhóm lý do trong Master: đối tác mới, đơn giá trị cao, phát triển doanh số, chi phí/biên lợi nhuận, chiến lược/dài hạn, khác. Dynamic questions không ép Vendor mới cung cấp số liệu chưa có.
- Conversation typed với participants/ownership, persistent messages/page/attachments, counteroffer revision/actor/time, Moderator assignment/discussion/recommendation, Admin final decision.
- State machine/status names là thiết kế phải map rõ, không coi các tên dự kiến Pending/InDiscussion/AwaitingApproval/Approved/Rejected/Cancelled đã có enum.
- Reuse outbox/SignalR transports phù hợp nhưng channel/room auth riêng, không thêm nullable LeadId khắp hệ thống cho nhanh nếu chưa phân tích compatibility.
- Pending/counteroffer/Approved/messages không được gọi ledger/rate activation. History append-only, optimistic concurrency + serializable final decisions.

Acceptance: Vendor chỉ shop mình, Customer/foreignVendor/unauthorized staff deny; Moderator không final approval; stale/double decisions; fileprivacy; message/policy unchanged; notification exact context; SQL/outbox atomic. UI đang deferred, không chỉ tạo mockform rồi gọi Phase6 xong.

## 6. P3 — Phase7 Partnership Contracts backend/domain

- Entity riêng platform–Vendor với loại hồ sơ, code/participants/parent appendix/scope/negotiation/policy/effective/expiry/status.
- Private PDF upload/download/preview; MIME/size/traversal/content validation, checksum, uploader/time/version, immutable signed original. Không public image URL, không overwrite file signed.
- Ghi nhận ký **ngoài hệ thống** có evidence/actor/time; view/acknowledgment không là signature. Không cần tự xây e-signature vượt yêu cầu.
- Vendor đọc hồ sơ shop mình, không sửa bản official; Admin quản lý đúng policy; Moderator không mặc nhiên đọc mọi tài liệu. Expiry/revocation/appendix links phải rõ, không mất history.
- Liên kết signed agreement phải đúng Vendor/scope/policy/window, không chỉ có một signed PDF bất kỳ là gate pass cho mọi rule.

Acceptance: wrongrole/IDOR/privateupload/get/preview/download, immutable version+checksum, appendixparent/scope, signed/effective/expiry transitions, audit/notification, no public leak; SQL migrations additive và data-preservation. Có test unsigned/viewed/ack-only vẫn không activation.

## 7. P4 — tích hợp activation và ngoại lệ hợp đồng

Chỉ khi dependencies sẵn và các câu hỏi tiền đã chốt:

- Activation độc lập/idempotent, đủ Admin approval + date + signed scoped document. Giao dịch mới chốt snapshot theo rule hợp lệ; original contract cũ giữ nguyên.
- Adjustment hiện chỉ decision record một Commission. Bổ sung negotiation/scope/basis/effective amount/document links/history theo thiết kế, không rename Approved thành Effective mà thiếu activation.
- Vendor yêu cầu đúng contract/Commission; Completed còn outstanding đủ điều kiện; fully paid/cancelled zero debt không vào luồng thường. Frozen/EscrowLocked/disputed contract/settlement chặn approval/activation, không bypass.
- Admin vẫn có thể reject/đóng request stale/held, version-pinned; không có nghĩa hợp đồng đã hết dispute. Replacement request vẫn phải eligibility mới.
- Ghi Original vs Effective và chênh lệch bằng record riêng, không sửa CommissionAmount/PaidAmount/bank ref/statement đã chốt. Hiện quyết định chỉ append-only/manual, **không auto offset/refund**.
- Finance manual reconciliation/evidence phải là use case riêng, đúng quyền, audit và linked records; chưa chốt thì chỉ thiết kế/ghi blocker, không thực hiện tiền thật.

Acceptance: contractA10%→7% effective theo agreed base, B/shop không đổi; oldsnapshot/payments byte-equivalent; partial/closed/overdue/frozen/dispute/stale/concurrent/multiple obligations; no duplicate delta; finalizing request không gọi settlement engine ngầm. Không dùng cancellation credit note làm adjustment shortcut.

## 8. P5 — hoàn thiện trải nghiệm trước khi public

Đây là kiểm chứng/tích hợp, không dựng lại UI đã có:

- Customer: newlogin→10questions→dashboard; saved combo/service images/details; clear next step; quote/contract/change states/capabilities; read-error/retry, Back/reload, account switch; private same-total history/reissue; review gates/edit preservation.
- Vendor: signup không chọngroup, category/group/default rõ, one service editor, preview/version/publicbaseline, minimum2 fixed components, source-change hold, guide/action/lead đúng room.
- Staff: confirmation Hủy/confirm/doubleclick/stale/errorretry; review before/after; role-specific tabs; Finance không nhận quyền moderator; UI mới Phase5–7 chỉ khi user resume UI.
- Browser desktop/mobile/keyboard/focus, money input caret/paste/IME/empty/bounds/numeric payload, guest CSV/seating, music upload/save/reload/play/Range/fallback. Không đổi financial formula để làm UI thuận tay.
- Multi-Lead chat: participant isolation, reconnect/rejoin/polling, dedup/ordering/session changes/late401, stale response không ghi sang user mới.

Acceptance cần fixture synthetic/DB unique và API+browser evidence; không đăng nhập tài khoản thật rồi tạo contract/payment để “thử nhanh”. Browser unavailable thì ghi UNVERIFIED, không lấy model/SSR tests thay.

## 9. P6 — release và deploy khi user cho phép

- Re-gate source hiện tại FE/BE/actualSQL/authorization/migrationupgrade/idempotency/preservation; regenerate latest package, exact source/artifact hashes. ZIP cũ không bao phủ source mới.
- Xác minh host .NET10/IIS/SQL/privatepaths/quota/backup/TLS/HTTPS/CORS/allowlist/domainDNS bằng account user tự thao tác; không chi tiền/tạo account/đổi DNS ngoài authority.
- Chọn clean target hay chuyển dữ liệu thật: backup SQL+media+private config, restore drill ở target độc lập, reviewed forward migrations, maintenance/rollback strategy không restore đè main tùy tiện.
- Production false migrate/seed, no demo credentials/public Swagger, uploads persist redeploy/restart, auth+chat+private files+API deep link SPA guards, actual externalprovider checks.
- Nếu cần100 concurrent: định nghĩa user workload, active request mix, SQL/query/pool/CPU/RAM/storage; đo trên môi trường gần host, báo error rate/p95/throttle giới hạn. Không hứa từ một người smoke.

## 10. Câu hỏi chỉ hỏi khi sắp tác động tiền

Chưa được tự quyết:

1. UnsettledOnly phân bổ khi partial payment/posted statement/overpayment nhiều Commission thế nào? Không đồng nhất outstanding balance, K2 và giá trị dịch vụ chưa quyết toán.
2. Commission allocation cụ thể cho mixed components/discount/markup/Guest/Table/Hour khi thiếu numeric inputs. Customer pricing share không là approval tài chính.
3. Service rule và Combo rule cùng cấp nếu đều áp một component: chọn/xung đột như thế nào?
4. Rounding base/VAT/commission/delta mới theo cấp/currency thế nào? Đã chặn rate dư precision, nhưng đó **không phải** quyết định rounding tiền.
5. Finance manual reconciliation: evidence, quyền final, giới hạn và liên kết record chênh lệch. Auto offset/refund không được phép theo decision hiện tại.
6. Lead SLA domain/controller2h khác worker/reason24h: chốt policy trước thay timers/status.
7. Breaking mandatory revision cho các legacy moderation APIs có được rollout không?

**Không hỏi lại** ordinary/A–Z formula principles, ba điều kiện activation, Completed còn outstanding, same-exact-target overlap reject, review target/visibility/dispute, combo mộtshop/fixedprice/private same-total hoặc bỏgroup lúcsignup: đã có decisions.

## 11. Quy trình làm một module cho agent tiếp theo

1. Nêu gap/evidence/caller/schema/test, scope files, dependencies và câu hỏi nghiệp vụ thật sự.
2. Thêm test tái hiện trước hoặc cùng patch; sửa nhỏ qua kiến trúc hiện có, bảo toàn API/legacy/snapshots; migrations additive, chỉ test-owned DB.
3. Chạy FE model/session/lint/build nếu FE; BE build/targeted/actualSQL khi query/schema/auth/finance; gate integration phù hợp. Không chạy full suite chỉ vì docs đổi.
4. Phân biệt expected reject, business bug, fixture bug, environment failure, skip và warning. Không sửa assertion để che sai nghiệp vụ.
5. Ngay sau module hoặc trước gián đoạn, cập nhật Living Checkpoint/handoff/checklist/report: changedpaths, implemented/verified/unverified/inprogress, commands/time/result, risks, exactnexttask. Decision mới chỉ ghi CONFIRMED khi user trả lời.
6. Lấy lại Git state, không commit/push/reset/clean; dừng đúng scope và báo còn gì. Agent sau phải kiểm độc lập, không tin tuyệt đối chữ “done”.
