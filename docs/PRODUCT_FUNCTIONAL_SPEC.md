# Wedding Match — đặc tả chức năng và hành trình sản phẩm

Baseline đọc: **22/10/2026**. Đọc cùng [START_HERE](START_HERE.md), [checkpoint](AGENT_HANDOFF.md), [roadmap](PRODUCT_COMPLETION_ROADMAP.md). Đây là mô tả **hành vi đã có và đích đã xác nhận**, không tuyên bố toàn sản phẩm hoàn tất.

## 1. Mục tiêu, từ vựng và mức chứng minh

Khách cần tổ chức đám cưới dễ hiểu, tìm đúng dịch vụ, nhìn rõ gói mình chọn, trao đổi với shop và theo dõi giao dịch. Shop cần quản lý nội dung/bán hàng; nền tảng cần kiểm duyệt, bảo vệ dữ liệu, quản lý hoa hồng minh bạch.

- **Implemented**: source có entity/use case/caller; có thể còn thiếu acceptance hoặc integration.
- **Automated verified**: build/test đã chạy, chỉ bảo đảm phần coverage, source và thời điểm ghi trong report.
- **Browser/provider/host verified** là lớp riêng; không suy từ unit tests hoặc một lần smoke local.
- **Confirmed** là nghiệp vụ user đã chốt, không có nghĩa code đã xong. Proposed/Needs confirmation không được biến thành rule thật.

| Thuật ngữ | Nghĩa chính xác |
|---|---|
| Shop/Vendor | Hồ sơ nhà cung cấp do VendorOwner quản lý |
| Category | Loại dịch vụ bán; liên kết BusinessGroup chính sách hoa hồng |
| Listing | Bài giới thiệu public, ảnh, structured content, published/proposed revision |
| ServiceItem | Cấu hình bán/giá gắn Listing; không tạo module dịch vụ thứ ba |
| ComboTemplate/Revision | Gói public của một shop và phiên bản nội dung đã duyệt |
| ComboOrder/Item | Lựa chọn/đơn của khách, giữ snapshot giao dịch; không phải template sống |
| Lead | Nhu cầu tư vấn/booking của Customer với Vendor; ngữ cảnh chat |
| Quote | Báo giá Customer–Vendor; chưa phải policy tài chính nền tảng |
| BookingContract | Hợp đồng Customer–Vendor |
| PrivateServiceAgreement | Đề xuất thay đổi **nội dung cùng tổng tiền** trên order/contract; không là phụ lục hoa hồng |
| Commission/Settlement/Payment | Nghĩa vụ hoa hồng, bảng kê, thanh toán/đối soát hiện hữu |
| CommissionPolicyRule/BasisApproval | Foundation chính sách/cơ sở tính mới; Approved chưa được áp vào resolver |
| CommissionAdjustmentRequest | Yêu cầu/decision riêng một nghĩa vụ Commission; hiện chưa là delta hoặc lệnh đổi ledger |
| Partnership Contract | Hợp đồng nền tảng–Vendor, còn phải xây; không thay bằng BookingContract |

## 2. Actors và ranh giới quyền
**Cập nhật29/10/2026:** SuperAdmin đang hoạt động có thể tạo đủ6vai trò trong Người dùng & đối tác, gồm Customer, VendorOwner, Moderator, Finance, CustomerCare và SuperAdmin. NCC được tạo cùng shop chưa xác minh, không ghi nhận thay việc NCC chấp thuận điều khoản. Cấp thêm SuperAdmin cần xác nhận rõ; phiên người quản trị giữ nguyên. Canonical POST `/api/users`; legacy `/api/users/staff` vẫn chỉ3vai trò nhân sự. Đây là chức năng đã triển khai/kiểm chứng local; trạng thái host xem [báo cáo](reports/admin-user-provisioning-and-navigation-2026-10-29.md), nghiệp vụ xem [quyết định](decisions/2026-10-29-admin-create-all-user-roles.md). Các mô tả cũ về giới hạn tạo nhân sự được superseded trong phạm vi này.

| Actor | Hành vi chính | Không được suy rộng quyền |
|---|---|---|
| Anonymous | Xem nội dung đủ public, thiệp/RSVP public theo route | Không xem draft, lịch sử đơn, chat/private documents |
| Customer | Onboarding, planner, giỏ/đơn, Lead/chat, báo giá/hợp đồng của mình, reviews đủ điều kiện | Không sửa rate/public combo/đơn của người khác |
| VendorOwner | Shop/dịch vụ/combo của mình, Lead/chat/báo giá/hợp đồng, đề xuất riêng, gửi adjustment đủ điều kiện | Không tự approve public content/policy/basis hoặc sửa tiền đã chốt |
| Moderator | Duyệt nội dung/review, tác vụ hỗ trợ được cấp; sau này xử lý negotiation trung gian | Không quyết định cuối policy hoa hồng hoặc mặc nhiên đọc mọi tài liệu riêng |
| Finance | Bảng kê/đối soát theo endpoint, đọc policy/basis foundation | Không có quyền duyệt policy/basis/adjustment request mới |
| CustomerCare | Tác vụ hỗ trợ trong phân quyền hiện tại | Không tự có quyền tài chính/moderation/admin |
| SuperAdmin | Quản trị, cấp staff, kiểm duyệt/quyết định tài chính theo workflow | Vẫn phải tuân thủ snapshot, evidence, hiệu lực và ký ngoài hệ thống |

Public signup chỉ Customer/VendorOwner; staff được SuperAdmin cấp. JWT claims, DB role/activity/security version và ownership đều quan trọng; ẩn nút không phải authorization.

## 3. Hành trình Customer đã có

### 3.1 Tài khoản → onboarding → dashboard

1. Password login/signup; Google/OTP chỉ khi provider đã cấu hình. Tất cả qua `/auth/continue`, đích hợp lệ tùy actor.
2. Customer mới phải hoàn tất 10 câu tại `/planner/setup` trước không gian planner. Backend quyết định onboarding state; không chỉ dựa localStorage.
3. Hoàn tất tạo hồ sơ cưới, timeline/checklist/ngân sách mẫu theo use case hiện có, không seed lại mỗi lần mở hoặc login.
4. Đến `/customer/dashboard`; không phục hồi mục sửa wedding profile đã user yêu cầu bỏ. `/account`/đổi mật khẩu/liên hệ là phạm vi khác, vẫn có.
5. Logout/account switch dọn session/cache/chat tạm; không xóa dữ liệu DB hoặc giỏ riêng người khác. Customer không bị đưa lại `/vendor/dashboard` từ intent cũ.

Nguồn: `client/src/routes/`, `features/auth/`, `features/journey/planner/`, `CustomerDashboard.tsx`; backend Auth/Planner/Wedding features và controllers. Chi tiết ở [authentication](authentication.md), [customer journey](customer-journey.md).

### 3.2 Tổng quan, timeline/checklist và ngân sách cá nhân

- Dashboard tập hợp tiến độ, việc cần làm và đường dẫn sang các công cụ; không bỏ tính năng chỉ để giảm menu.
- Checklist/timeline lưu thật theo wedding scope; thao tác phải đúng membership/ownership. Nhóm việc mẫu không được nhân bản khi refresh/relogin.
- Ngân sách là **sổ dự toán cá nhân**, không phải bank ledger. `PlannedCost` là dự toán, `ActualCost` là tổng đã ghi chi/cọc. Trả thêm thì cập nhật tổng, không cộng tiền cọc lần hai.
- Còn theo một khoản = max(dự toán − tổng đã ghi chi, 0); chi vượt hiển thị riêng. Cờ `IsPaid` không phải chứng nhận pháp lý đã trả đủ.
- Khoản ngoài hệ thống khách tự ghi. Liên kết hợp đồng và tự đồng bộ khoản dự toán hiện có vẫn giữ; user chưa yêu cầu bỏ toàn bộ linkage.
- Thay ngân sách không được đổi Commission/PaidAmount/settlement. Đồng bộ combo chỉ chạm wizard estimate đủ provenance/pristine; phải giữ manual edits và khoản đã chi.

### 3.3 Khách mời, xếp bàn, thiệp và lịch hẹn

- CRUD khách mời và bàn, RSVP và liên kết wedding scope. Auto seating ưu tiên lấp chỗ trống: bàn 10 còn 4 chỗ có thể nhận 4 khách từ nhóm đông, không giữ cả nhóm tới mức bỏ trống bàn. Không tự thay thuật toán bằng “luôn giữ nhóm” trái yêu cầu.
- Thiệp chọn template, soạn thông tin, upload ảnh trực tiếp, public slug và RSVP. Không bắt khách phải tự host URL ảnh.
- Nhạc thiệp: MP3 upload có preflight/quota/server validation và durable storage; YouTube URL chuẩn hóa/allowlist/embed click-to-load/fallback. Không hứa autoplay do browser policy; không tải/tách âm thanh YouTube.
- Lịch hẹn có appointment types/availability/status và các luồng deposit/refund hiện hữu; giá trị tài chính theo use case thực, không lấy ngân sách cá nhân thay bằng chứng trả tiền.
- Automated coverage đã có; các luồng mobile/caret/IME/audio playback/provider ngoài hệ thống chưa được browser chứng nhận đầy đủ.

### 3.4 Khám phá dịch vụ và combo

- `/browse` và alias `/vendors` có filter keyword/category/khu vực/giá/rating, sorting/paging; các điều kiện kết hợp, URL giữ state cho Back/reload.
- `/combo` hiển thị **cả dịch vụ lẻ và combo**, không chỉ combo. Có ảnh shop/dịch vụ, giá, liên kết detail và hành động thêm giỏ đúng loại.
- Bộ lọc category dùng taxonomy server, không tự hardcode “nhóm chiết khấu” thành dịch vụ mới. Search/filter không làm mất giỏ.
- Combo detail riêng, nhóm các mục bao gồm rõ ràng, ảnh/phạm vi shop/nguồn. Trên điện thoại thanh phân nhóm phía trên nằm ngang; desktop giữ bố cục phù hợp.
- Public chỉ Approved+Active và các điều kiện shop/account/category/group/source tương ứng. Ẩn nội dung hoặc nguồn thay đổi không được làm mất lịch sử đơn đã mua.

### 3.5 Từ chọn gói tới tư vấn, báo giá và hợp đồng

1. Khách thêm dịch vụ lẻ hoặc đúng nguyên combo đã duyệt vào giỏ. FixedPrice không cho tự đổi thành phần mà giữ giá public.
2. Draft selection chưa gửi không tự coi là Lead hoặc thanh toán. Nút bước tiếp theo dùng API submit thực, validate lại server rồi tạo quan hệ Lead/order và dự toán theo use case.
3. Khách xem lại tại tab `combos`/lựa chọn & yêu cầu; card có ảnh/snapshot, trạng thái và next action. Archive chỉ ẩn khỏi view, không hard-delete/hủy lịch sử tài chính.
4. Lead mở đúng chat Customer–Vendor. Một ComboOrder có thể có nhiều Lead; phải chọn đúng conversation/dịch vụ, không lấy ngẫu nhiên Lead đầu tiên.
5. Vendor lập Quote; khách accept/reject/request changes theo capability. Accept Quote tạo/revise BookingContract chờ xác nhận, chưa đồng nghĩa commission đã phát sinh ở bước accept.
6. Customer review và confirm BookingContract trong cửa sổ hiện hữu 72h. Server kiểm owner/current version/điều kiện; luồng legacy chốt rate/category provenance và K1/K2 cùng transaction với liên kết liên quan.
7. Theo dõi thực hiện/completion/dispute/cancellation theo state machine. Không xóa hợp đồng đã chốt chỉ vì khách không thích nữa; dùng thao tác lifecycle có lịch sử.
8. Tình trạng lỗi API phải có loading/error/retry; không biến 500 thành “chưa có dữ liệu”, không cho stale response/account cũ mở action mới.

### 3.6 Muốn sửa thành phần sau khi đã chọn

- Trao đổi trong Lead chat, sau đó Vendor gửi `PrivateServiceAgreement` đúng combo order hoặc contract; Customer đồng ý/từ chối, Vendor có thể rút pending theo capability.
- Original + Effective + History tách rõ; tối đa một pending, version pinned, audit actor/time. Accepted mới là lớp nội dung áp dụng riêng. Không sửa template public hoặc original snapshot.
- Hiện chỉ **cùng tổng tiền**, giữ cọc/VAT/rate/payment/settlement. Thay giá phải phát hành báo giá/quy trình tài chính phù hợp, không lách qua text agreement.
- Nếu Quote/contract lập trước accepted combo change, Vendor cần phát hành lại và Customer xác nhận; pending agreement phải xử lý/rút trước chốt. Không copy toàn combo vào từng hợp đồng component.
- Staff không mặc nhiên đọc/duyệt đề xuất riêng. Đây khác hoàn toàn negotiation hoa hồng platform–Vendor.

### 3.7 Review sau hoàn tất

- Shop, Service và Combo là ba target độc lập, summary/list/filter/page/sort riêng; không cộng điểm Service/Combo vào Shop.
- Customer đúng giao dịch Completed mới tạo review mới. Combo phải đủ hợp đồng thành phần cần thiết Completed và đúng customer/vendor/listing/order.
- Shop review của Combo dùng stable `ComboOrderId`; Service dựa giao dịch tương ứng. Khách có giao dịch mới có thể review lại, nhưng không trùng target/giao dịch.
- Combo cần exact approved revision/snapshot tại order. Legacy thiếu bằng chứng không được đoán revision để review Combo; custom order không gán sang public template. Shop/Service đủ điều kiện vẫn độc lập.
- Legacy consultation reviews giữ nhãn và scoring cũ; không xóa vì đổi policy.
- Edit: một Pending, tối đa ba submission trong 7 ngày từ review gốc, tối đa một lần edit được approve. Approved cũ vẫn public khi edit pending; reject giữ bản cũ; approve mới cập nhật bản hiện hành và aggregate. Lưu before/after/actor/time/reason.
- Omitted/null ảnh/criteria trong edit giữ bản cũ; `[]` ảnh là explicit clear. Không tự thêm null-clear criteria hoặc đổi weighting.
- Combo ngừng đủ public thì review public ẩn cùng; dispute chặn approve edit như review mới. Không xóa dữ liệu review/snapshot.

## 4. Hành trình Vendor đã có

1. Đăng ký account/shop: **không chọn business group/commission rate lúc signup**. Legacy fields/membership giữ để tương thích, không dùng làm permission gate mới.
2. Mở VendorDashboard có guide/journey 10 bước, FAQ và next action dựa dữ liệu DB read-only. Đây không phải wizard bắt buộc/progress giả hoặc chứng nhận đã thu tiền.
3. Tạo dịch vụ ở **My Services** một lần: thông tin Listing, ảnh, structured details, pricing ServiceItem chính, variant/add-on trong cùng proposal.
4. Chọn mọi category active đã được cấu hình group active. Form hiện group/default commission từ server; Vendor không sửa rate/category-policy qua payload. Category của dịch vụ đã tồn tại không tùy ý đổi để né rate.
5. Lưu draft → submit → Moderator/SuperAdmin review → approved public baseline. Khi sửa bài đã public, live revision vẫn hiển thị tới khi proposal mới được duyệt. Có private preview/activity riêng.
6. Tạo FixedPrice combo từ ít nhất hai dịch vụ độc lập Approved+Active của chính shop; có thể cùng category, không duplicate ID/nested/foreign shop. Dùng lại nguồn, không copy dịch vụ.
7. Đặt giá nguyên gói, snapshot ảnh/nội dung/phạm vi/retail khi submit. Hiện components mới là unit Package; legacy Dynamic vẫn giữ, không suy số khách/bàn/giờ.
8. Chỉ hiện savings khi bán dưới retail. Markup không tạo savings âm hoặc checkout phân bổ âm; thiếu chính sách thì giữ block. Nguồn đổi làm combo cần rà/duyệt lại, không đổi đơn cũ.
9. Nhận Lead → chat đúng room → Quote → hợp đồng → thực hiện. Chỉnh nội dung đơn riêng qua agreement, không phải Admin public review.
10. Xem bảng kê/hoa hồng theo quyền và luồng hiện hữu. Chính sách hợp tác UI hiện read-only; không giả có thương lượng/rule activation nếu backend chưa làm.

## 5. Back-office, notifications và kiểm duyệt

- **Cập nhật22/10:** navbar desktop/mobile dùng chung role policy, Vendor/staff chỉ ownworkspace shortcuts; Customer/anonymous giữ menu riêng. Querytab active chính xác, sharedaccount/bell/logout giữ. Trang thông báo có khung giữa/gutters trên điện thoại;401FEtests/build/lint và staticbrowser6actorlayout đã kiểm. [Report](reports/notifications-actor-navbar-2026-10-22.md) nêu authenticatedE2E còn thiếu; không xem navbarvisibility là thay quyềnAPI.

- Unified queue cho nội dung liên quan; revision/source/ownership/public eligibility có server guards. Không để Vendor tự duyệt.
- Admin/Moderator thao tác duyệt Listing/ServiceItem/Combo có dialog xác nhận: xem đúng target/nội dung; Hủy không gọi mutation; chống double-submit; stale status/version fail-closed; lỗi API giữ note/retry.
- Dialog frontend không tự biến mọi legacy endpoint thành bắt buộc revision; một số API compatibility vẫn nhận RevisionId optional. Nếu muốn bắt buộc cần kế hoạch rollout, không silently breaking.
- Review/edit moderation riêng; phải xem đầy đủ before/after, không làm mất ảnh/criteria.
- Notification Center owner-scoped, unread/paging/read/read-all, typed contexts và deep link allowlist. Outbox/notification tồn tại không chứng minh SMTP/ZNS thật đã live.
- Finance/CustomerCare dùng tab/endpoint được cấp; không gọi queue Moderator chỉ vì cùng AdminDashboard.

## 6. Tài chính: có gì thật và chưa có gì

### 6.1 Luồng legacy đang thực thi — giữ nguyên

- `Vendor.CommissionRate > 0` được ưu tiên, nếu không thì category default. Chưa phải scoped/effective policy resolver mới.
- Confirm hiện dùng `CommissionRules.Split` trên ContractValue theo rate snapshot; Quote có VAT không làm engine tự loại VAT theo chính sách mới.
- K1/K2 hiện 50/50, confirm tạo cả hai; K2 khóa theo lịch sau cưới 3 ngày. Completion thiếu original rate hợp lệ phải fail-closed, không lấy rate shop hôm nay/8% để chữa.
- Ledger/payment/webhook/settlement/escrow/cancellation credit có code và tests; không thay chúng bằng field ở ngân sách hoặc bằng status Approved của request.

### 6.2 Quy tắc đã được user chốt cho engine mới

- Defaults theo group: địa điểm/tiệc 3%, decor/thiệp 8%, quay/chụp/váy/makeup 10%, A–Z 12% trên approved management fee, sự kiện khác 3%. Đây là mặc định, không tự ghi đè legacy.
- Ordinary base = giá trị trước VAT sau discount Vendor chịu − pass-through có chứng từ đã Admin duyệt; không âm. Không loại VAT hai lần; discount platform tài trợ mà Vendor vẫn nhận đủ không tự giảm base; chi phí nội bộ không phải chi hộ.
- A–Z chỉ management/organization/coordination fee được duyệt; không lấy toàn budget/giá all-in nhân 12%.
- Combo từng component có basis/rate được duyệt, phân bổ minh bạch và không parent+child double charge. Allocation ưu đãi cho khách không mặc nhiên là allocation cơ sở tính hoa hồng.
- Precedence mục tiêu: service/combo cụ thể → vendor+category → vendor → category → system. Service/Combo cùng cấp, không tự thêm tie-break chưa xác nhận.
- Chính sách chỉ Active khi **Admin approval + effective date + recorded externally signed Platform–Vendor agreement/appendix** cùng đủ. Chat/Approved/view/ack không là activation/chữ ký.
- Original confirmed snapshots và payment history bất biến; Vendor cũ giữ rate đến chuyển đổi hợp lệ. Ngoại lệ riêng contract không đổi contract khác hoặc policy toàn shop.

### 6.3 Phase 5 foundation hiện tại — implemented, audited

- `CommissionPolicyRule`: scope/target/version/rate/basis/window, Draft→Submitted→Approved/Rejected, actor/version/audit, overlap guard. Combo pin approved revision, Rate=null ở parent, coverage từng required/alternative component.
- `CommissionComponentBasisApproval`: revision/service/sequence/rate/basis description/exclusions/window/approval; append-only. Known category/group A–Z phải đúng AzManagementFee. Revalidate active shop/account/source/basis khi create/submit/approve; pending stale vẫn có thể reject đúng quyền.
- `CommissionAdjustmentRequest`: Vendor của contract Confirmed/Completed có nghĩa vụ outstanding dương mới gửi; frozen/escrow/disputed contract/settlement và stale CommissionVersion chặn approval. Closed/Overdue statement còn nợ không tự bị loại.
- Admin rejection được phép đóng yêu cầu stale/held, không nghĩa là hold đã được giải quyết. Gửi mới/approve vẫn phải qua đầy đủ guards.
- Quyết định adjustment chỉ lưu original rate/amount/outstanding/version snapshot, proposed rate và final decision; **chưa tính approved delta/new base/effective commission**, chưa settle/refund/offset.
- Chỉ SuperAdmin ghi/duyệt policy+basis/adjustment; Finance/SuperAdmin đọc policy/basis; GET adjustment list hiện SuperAdmin-only, chưa có Vendor self-history endpoint.
- Rate mới phải biểu diễn chính xác trong SQL decimal(5,4); từ chối phần thập phân dư, không tự làm tròn. Exclusions input chuẩn hóa/validate, không là chứng từ approved pass-through số tiền.

### 6.4 Các phần còn thiếu — tuyệt đối không gọi đã xong

Numeric basis inputs/evidence approval; immutable new financial snapshot đầy đủ; effective scoped resolver/expiry/fallback/precedence; signed partnership linkage; activation idempotent/atomic; negotiation; partnership private documents; effective adjustment/delta/manual reconciliation workflow; UI tương ứng. **Không bật policy mới chỉ để demo.**

Đặc biệt: approved per-component basis hiện là gate **duyệt policy Combo**, chưa được nối vào runtime xác nhận BookingContract của Combo. Không tuyên bố giao dịch mới đã dùng approved basis engine; trước rollout phải thiết kế/chứng minh boundary legacy/new và test confirm thực.

## 7. Những yêu cầu cũ đã bị thay thế

- Combo nhiều shop → **mỗi combo một shop**.
- Chọn nhóm hoa hồng lúc signup/chờ Admin cấp nhóm mới tạo dịch vụ → **chọn category khi tạo**, server hiện default/group.
- Customer tự sửa fixed combo giữ nguyên giá public → **nguyên gói; thay đổi trao đổi/đề xuất riêng**, hiện cùng tổng tiền.
- Review mọi trải nghiệm tư vấn mới → **Completed-only mới**, giữ consultation cũ có nhãn.
- SQLite reset/reseed startup → **SQL Server persistent**, không phá dữ liệu.
- weddingmatch.vn → user đổi **weddingmatch.website**, báo đã thanh toán; hosting vẫn tạm hoãn.
- Entity/API policy chưa có → nay **có foundation Phase5**, nhưng engine/activation vẫn chưa có. Không khôi phục backlog bằng cách tạo entity trùng.

## 8. Thế nào mới được gọi là sản phẩm hoàn chỉnh

Core journey có thật, nhưng chưa hoàn thiện toàn Master Prompt. Cần đủ workflow Phase5–7; UI tích hợp quyền/điều kiện; QA actor/end-to-end trên fixture sạch; SQL migrations/upgrades/backups; provider và hosting thật; tải/restore được đo. Chi tiết đầu việc và acceptance nằm ở [roadmap](PRODUCT_COMPLETION_ROADMAP.md); không dùng test pass để quảng cáo đã chịu 100 người đồng thời.
