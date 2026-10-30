# Tiếp tục refactor dịch vụ / combo và nội dung riêng —20/10/2026

## Kết quả

Đã triển khai thực tế luồng một nơi tạo dịch vụ → duyệt dịch vụ → chọn >=2 nguồn own-shop → combo giá cố định → duyệt public, đồng thời nối chat và đề xuất nội dung riêng Customer–Vendor. Giữ legacy/API/variant/add-on và snapshot tài chính; không deploy/push/reset/seed dữ liệu thật.

Đây là nghiệm thu **implementation + automated/HTTP API gate** của module, không phải tuyên bố đã xong toàn Master Prompt hoặc browser E2E.

## Khôi phục và bảo toàn công việc

- Đọc lại checklist, báo cáo gần nhất và đầy đủ bản “REFACTOR VENDOR SERVICES & COMBOS”; kiểm Git status/diff trước sửa.
- HEAD khi khôi phục là `3596ae8` (`3`), branch `weddy-journey`. Nhiều phần refactor dở trước đó đã nằm trong commit do thao tác bên ngoài lượt này; giữ nguyên, không revert hoặc tạo lại.
- Giữ toàn bộ thay đổi đang có; công việc mới vẫn chưa commit. Quyết định tài chính cũ được ghi lại trong `docs/decisions/2026-10-20-fixed-combos-and-private-service-changes.md`.

## Các phần hoàn thành

### Dịch vụ tạo một lần

- `Listing.PricingServiceItemId` xác định cấu hình bán chính; ListingForm tích hợp giá/đơn vị/capacity/variant/add-on và lựa chọn legacy rõ ràng.
- Một immutable Listing proposal giữ cả pricing; một lần gửi/duyệt, không tạo thêm dịch vụ chỉ để ghép combo. Canonical facet không xuất hiện thành một hồ sơ kiểm duyệt trùng trong queue.
- Bảo toàn ID biến thể/add-on không đổi; omitted payload không xóa; API legacy giữ tương thích, không tự merge nhiều facet cũ.
- Public card/detail/filter/sort dùng giá canonical khi có và fallback khoảng giá legacy khi chưa có; không lộ sourceVersion/legacy pricing selection qua public DTO.

### Combo giá cố định

- Picker chỉ nguồn own-shop Approved+Active, canonical và Package; tối thiểu2 Listing/ServiceItem độc lập, không nested/foreign/duplicate/optional swap; cùng category hợp lệ.
- Editor/card có ảnh, mô tả chi tiết, gallery/điều kiện, tìm/lọc/phân trang, Original/Combo/Savings hiển thị thật. Empty/pending/one-service states chỉ rõ việc tiếp theo.
- New FixedPrice không thể downgrade sang Dynamic qua draft/pending ID. Legacy Dynamic thật vẫn chỉnh/sử dụng được.
- Frozen component gồm giá/nội dung/phạm vi/ảnh và sourceVersion; nguồn đổi thì combo giữ bản cũ và hold/re-review, không tự repricing booking/contract.
- Customer detail/cart không cho thay thành phần nhưng tiếp tục giữ giá nguyên gói. Saved order ưu tiên frozen imagery/content. Giá gói vượt tổng lẻ không giả mức tiết kiệm hoặc tự tạo phân bổ âm: cần báo giá riêng.
- Sửa lỗi hai dịch vụ cùng danh mục dùng chung wizard budget seed trong cùng transaction; mỗi dòng có budget binding riêng, tổng đúng và cancel khôi phục seed nguyên trạng.

### Nội dung thực hiện riêng và chat

- API `/api/private-service-agreements/{combo|contract}/{id}`: GET workspace, POST đề xuất, POST `/{proposalId}/respond`, POST `/{proposalId}/withdraw`.
- Actual DB actor/role/ownership/activity kiểm server; staff không đọc/duyệt nội dung riêng; anonymous/foreign deny.
- Vendor gửi/rút, Customer đồng ý/từ chối. Một pending tại một thời điểm; pinned version, serializable write/app lock, unique sequence, status concurrency token; immutable body/original snapshot/final response, audit và typed in-app notification.
- Chỉ bản Accepted thay lớp nội dung riêng; original order/quote/contract/public catalog không bị rewrite. Đề xuất không có giá/commission/payment fields; tổng tiền, cọc, thanh toán, hoa hồng giữ nguyên.
- Original scope đọc frozen order component, không lấy catalog sống. Lead dừng/hết hạn hoặc target đổi làm mất quyền xác nhận; chủ đề xuất vẫn rút được pending stale/closed an toàn.
- Combo đã sang hợp đồng không nhận tiếp đề xuất combo; contract hiển thị nội dung combo đã đồng ý dưới dạng tham khảo, không copy toàn gói vào mỗi hợp đồng. Chặn chốt báo giá/hợp đồng lập trước accepted change hoặc khi còn pending; cần NCC phát hành lại và khách review.
- Panel lazy-load tại MyCombos/MyContracts/Vendor ContractList/LeadInbox; có đối chiếu gốc/hiệu lực/history, lỗi/retry, xác nhận khách hai bước, giới hạn payload và cảnh báo stale.
- Chat dùng axios/authStore/config API chung, current token, rejoin phòng sau reconnect, chống kết quả cũ vượt room/session và REST fallback/polling. Lỗi tải chat được hiển thị, không giả empty thành công.

## Database/local runtime

- Migration14 `20261020004225_FixedVendorPackages`: sáu cột bổ sung, legacy mặc định Dynamic; nullable canonical link/price/component snapshots.
- Migration15 `20261020055151_PrivateServiceAgreements`: bảng mới và FK NoAction/unique sequence; không drop/merge/backfill bảng cũ trong Up.
- SQL tests đã nâng/replay schema trên database riêng, giữ legacy/old financial rows và kiểm model drift; không chạy Down.
- Lúc kiểm đầu DB `WeddingMatch_Dev` có13 migration. Khi chuẩn bị apply, guard phát hiện DB **đã có15 migration**, tự dừng trước stop/backup/apply. Chưa xác định thao tác đã áp dụng trong khoảng đó; không coi là lượt này đã backup trước migration13→15 và không tự downgrade/replay.
- Sau xác minh chỉ đọc, đã backup **trạng thái schema15 hiện tại** bằng COPY_ONLY/CHECKSUM + RESTORE VERIFYONLY. Backup:
  `D:\sql\dev\MSSQL16.MSSQLSERVER\MSSQL\Backup\WeddingMatch_Dev_schema15_before_runtime_refresh_20261020_0612utc.bak`.
- SHA-256/số dòng toàn bộ cột cũ của55 bảng khớp trước/sau refresh API và sau restart. Đây là bằng chứng bảo toàn của thao tác refresh hiện tại, không phải bằng chứng lịch sử của lần apply diễn ra trước guard. Bảng đề xuất mới vẫn0 dòng trong DB thật.
- API5008 chạy `artifacts/refactor-final-runtime`, SQL Server WeddingMatch_Dev, `ApplyMigrations=false`, `SeedDemoData=false`. `/health/ready`, `/api/combos/catalog`, `/swagger/v1/swagger.json` đều200. Không dừng web5173 của người dùng.
- Bằng chứng local: `artifacts/refactor-main-preservation.json`, `refactor-main-before.txt`, `refactor-main-after-restart.txt`; chỉ count/hash, không dump PII/mật khẩu.

## Kiểm thử cuối

Gate: `artifacts/readiness/20261020-060533-c2159465/ready.json`, VerifiedUtc `2026-10-20T06:08:11.4764330Z` (13:08VN).

| Miền | Kết quả |
|---|---|
|Backend đầy đủ|**929/929 pass,0 skip**, SQL Server thật trên DB test riêng; migration/schema/replay/auth/finance/legacy regression.|
|Frontend|**229/229 pass**, TS/Vite production build đạt; lint0 error.|
|Private module|25 test trong full gate: actor/ownership/immutable/accept-reject-withdraw/stale/frozen scope/lead expiry/handoff và SQL persistence.|
|Budget collision|Targeted60/60 + SQL scenario1/1; full929 bao gồm bản sửa cuối.|
|HTTP API QA cuối|**28/28 pass** trên5018 với fixture hư cấu mới; chat, private actions, stale/role denies, original combo/contract/commission/public template DTO không đổi. Lượt trước29 check do khác nhánh fixture; không cộng hai lượt như test độc lập.|
|Package local|Build/ZIP inspection,6 negative guardrails, real HTTPS, API shape, actor/password/JWT/WebSocket revocation, SQL/image persistence restart đều đạt; không upload public.|
|Main runtime|Health/catalog/Swagger200, fingerprint55 bảng cũ khớp sau restart.|
|Browser|**Chưa kiểm tra trực quan**: CUA inventory apps/browsers rỗng; tạo IAB không khả dụng, mở panel queued vẫn không có surface. Không có screenshot hoặc bằng chứng desktop/mobile E2E cho bản này.|

Source SHA-256: `636F0759306BA463BFA607868F47CFB8BEBC8C0115135271D3656C6D7A0AECEB`.
ZIP SHA-256: `1756E6362F621F8758EE52B6940C8847C0C433FD2CA982FC27728216394DE751`.
Artifact chỉ đại diện source được verify, không đồng nghĩa host thật/load/backup-restore đầy đủ đã nghiệm thu.

Warnings:4 xUnit2031 cũ, lint React warnings, Vite mixed static/dynamic LoginForm import; không có build error. Functional smoke dùng khoảng200MB working set không phải load test100 khách; shared host256MB vẫn cần đo thực tế trước deploy.

HTTP QA evidence/script/giới hạn browser ở `artifacts/refactor-ui-qa/`; fixture có tên `WeddingPlanTest_RefactorUi_20261020_8b729c`, không dùng tài khoản khách thật. DB/process QA được dọn sau kiểm tra; source fixture và evidence giữ lại để tạo lại khi cần.

## File chính

- BE service: `Features/Listings/ListingServicePricing.cs`, create/update/submit/review/publication/DTO/query; `ModerationQueueFeature.cs`, `ServiceItem.cs`.
- BE combo: `ComboFixedPackages.cs`, `ComboTemplateLifecycle.cs`, `ComboCatalogFeature.cs`, `CombosFeature.cs`, `ComboBudgetSynchronization.cs`, combo entities/config/DTO/catalog paging.
- BE private: `PrivateServiceAgreement.cs`, application `Features/PrivateServiceAgreements/*`, controller/config/DbSet/migration15; Quote response và ConfirmContract handoff; Lead DTO/query thêm comboOrderId.
- FE service/combo: ListingForm/Table, VendorCombosPanel, FixedComboTemplateEditor/Picker, ListingCard/detail/price model, builder/detail/cart/model/saved-order snapshot display.
- FE private/chat: `features/private-agreements/*`, MyCombos/MyContracts/ContractList/LeadInbox mounts, ChatRoomModal/useChatRoom/chatTransportModel.
- Tests: UnifiedServiceAuthoringTests, FixedComboPackageTests, PrivateServiceAgreementTests, privacy/category historical fixtures; frontend fixed-combo, vendor editor, listing-price, chat-transport, private-agreements tests.
- Docs: master checklist, refactor checklist và quyết định20/10.

## Còn lại / ranh giới chưa hoàn thành

1. Browser desktop/mobile thực tế cho refactor khi có browser surface.
2. Phase4 review targets Shop/Service/Combo độc lập; chưa đổi aggregation/weighting cũ.
3. Phase5 scoped/effective commission rules + Admin editor, basis/VAT/vendor-vs-platform discount/approved pass-through/A–Z management fee. Quyết định nghiệp vụ đã có nhưng không tuyên bố engine mới đã hoạt động.
4. Phase6–7 negotiation/partnership documents/ba điều kiện activation; ngoại lệ hoa hồng riêng cho hợp đồng đang thực hiện, audit/history và settlement delta. Nội dung cùng giá ở module này **không thay thế** các phần tài chính đó.
5. Music/navigation/MoneyInput coverage và các gate tích hợp/restore/load/host còn trong master checklist.
6. Không checkout fixed package Guest/Table/Hour hoặc markup bằng cách tự đoán allocation; legacy vẫn được giữ. Không tự chuyển giá hợp đồng đã chốt khi sửa scope.

Deployment vẫn tạm dừng. Không push/force-push/commit/reset, không xóa hoặc seed DB thật.
