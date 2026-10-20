# Phase 3C/3D — vòng đời Combo và phiên bản xuất bản dịch vụ

Ngày: 19/10/2026. Tiếp tục branch `weddy-journey`; giữ nguyên thay đổi hợp lệ chưa commit. Phase 3C là Combo lifecycle/private preview; Phase 3D là Listing publication/activity và tích hợp kiểm duyệt liên quan. Không thay thế phần Commission/Negotiation/Partnership của Phase 5–7.

## Kết quả tại thời điểm báo cáo

**Đã triển khai và kiểm tra:** vòng đời Combo/Listing, 565/565 backend có SQL, 142/142 frontend ở gate cuối sau sửa mobile; browser/API QA và migration DB local chính đã hoàn tất trong phạm vi bên dưới. Không tuyên bố toàn bộ Master Prompt đã hoàn thành. Chưa public deploy.

## Phase 3C — Combo của một nhà cung cấp

- Tách bản công bố `ComboTemplate/Item` khỏi đề xuất `ComboTemplateRevision`. Mỗi lần lưu tạo snapshot mới Draft hoặc Pending; Vendor, Moderator và SuperAdmin đều không tự xuất bản qua thao tác lưu.
- Bản mới chưa duyệt không hoạt động và không xuất hiện công khai. Sửa combo đã duyệt không thay tên/mô tả/ảnh/điều khoản/thành phần/giảm giá của bản công bố cho đến khi Moderator/SuperAdmin phê duyệt đúng phiên bản Pending mới nhất.
- Từ chối bắt buộc lý do 1–2.000 ký tự, lưu reviewer/role/thời điểm/ghi chú. Gửi lại Draft/Rejected tạo Pending mới; lịch sử cũ giữ snapshot, lý do và dấu vết superseded. Không cho duyệt revision cũ, đã xử lý hoặc thuộc combo khác.
- `IsActive` độc lập moderation. Tạm ngừng không biến Approved thành Rejected/Archived và không xóa giao dịch. Duyệt thay đổi nội dung giữ trạng thái kinh doanh hiện tại của combo đã công bố. Kích hoạt yêu cầu bản Approved, chưa hết hạn và các thành phần còn hợp lệ; combo lên lịch tương lai chưa xuất hiện trước ngày bắt đầu.
- Giữ quy tắc một combo/một NCC, mỗi ngành một dịch vụ mặc định, từ 1–12 ngành; tối đa 100 lựa chọn thay thế/ngành, cùng NCC và cùng ngành. Backend kiểm approved/active service, Listing approved+active, Vendor/account active, verified và đồng ý tham gia combo; server suy ra ListingId từ ServiceItem, không tin ID client.
- Gallery tối đa 10 ảnh, điều khoản tối đa 4.000 ký tự, khoảng hiệu lực UTC. Chấp nhận HTTP(S) an toàn hoặc đúng đường dẫn ảnh server đã tải lên; chặn credentials/executable/traversal và vượt giới hạn. Omitted/null Content giữ metadata đề xuất gần nhất, object mới thay thế toàn bộ. Nội dung mô tả không đổi công thức tài chính.
- Public builder, paged catalog và direct price đều dùng bản Approved+active trong thời hạn và tái kiểm tra cả dịch vụ mặc định/lựa chọn thay thế. ID trực tiếp không vượt được điều kiện công khai. Tổng/giảm giá/rounding/customer pricing hiện hữu giữ nguyên; không thêm fixed-sale hoặc commission allocation mới.
- API private preview nhận template và revision ID độc lập, chỉ owner active hoặc Moderator/SuperAdmin active. Preview trả dịch vụ/ảnh/variant/add-on thật của revision được chọn; public DTO không trả proposal/history. History danh sách chỉ mang metadata, không lặp toàn bộ snapshot.
- Handler kiểm UserId, role và trạng thái actor thực trong DB; Vendor khác nhận NotFound với dữ liệu private, Customer/Finance/CustomerCare không được quản lý/duyệt. Save/submit/review/activity dùng transaction serializable; lỗi không để lại nửa proposal, item hoặc notification.
- Snapshot JSON, template ID, sequence, tác giả/role/thời điểm tạo không được sửa/xóa qua EF async persistence; workflow metadata được chuyển trạng thái có kiểm soát. Approved history không bị supersede bởi lần chỉnh sửa mới.
- Bản legacy chưa có history không bị migration gán lịch sử giả. Legacy Pending đủ điều kiện vẫn được staff duyệt/từ chối tường minh, có ghi chú/audit; giao dịch legacy giữ nguyên.

### Frontend Combo

- Tab `Combo của tôi` tái sử dụng ServiceItem/variant/add-on/consent hiện hữu; thêm editor và lifecycle card chung cho Vendor/staff. Có loading/error/retry/empty, lưu nháp, lưu và gửi duyệt, gửi lại, tạm ngừng/kích hoạt, lý do và lịch sử.
- Editor chọn dịch vụ mặc định/tùy chọn theo NCC/ngành, hiển thị giá cấu hình thật; giữ reference không còn khả dụng để người dùng sửa tường minh, không âm thầm bỏ thành phần.
- Vendor tải ảnh thiết bị qua endpoint upload thật, xem gallery và chọn ảnh bìa. Không tạo ảnh/giá/NCC giả. Staff không được cấp thêm quyền upload của Vendor chỉ vì dùng chung editor.
- `/combo/{id}?preview=vendor&revision={revisionId}` dùng chung trang detail, có banner chỉ đọc, back về đúng phân hệ; tắt CTA giỏ/đặt/tư vấn trong preview. Query flag chỉ chọn presentation, không cấp quyền backend. Cache private tách theo actor/revision.
- Staff xem proposal mới nhất, nhập lý do và gửi đúng RevisionId; trạng thái đã thay đổi bị server từ chối. Thời điểm UTC/SQL bare ISO hiển thị nhất quán theo Việt Nam. SortOrder DTO lấy giá trị thật của baseline/proposal, không reset mặc định khi sửa.

## Phase 3D — Listing publication và activity riêng

- Thêm `Listing.IsActive` riêng khỏi Draft/PendingApproval/Active/Rejected/Hidden. Owner bật/tắt kinh doanh không thay moderation, không tự duyệt hoặc mở lại bài bị staff Hidden.
- Listing Active/Hidden khi sửa lưu bản đề xuất `ListingRevision`, giữ nguyên nội dung/giá/media/structured metadata đang công bố. Lần chỉnh sửa đầu chụp baseline để truy vết nội dung sẵn có, không giả reviewer hay ngày duyệt quá khứ. Draft/Rejected chưa xuất bản giữ luồng direct-edit tương thích.
- Nội dung/album/metadata omitted kế thừa đề xuất gần nhất, không lùi về bản công bố. Editor/private detail đọc đúng revision; public detail không trả lịch sử đề xuất. Revision của Listing khác không đọc được qua ID ghép.
- Submit kiểm tra ownership, actor/shop active, category active, verified và hồ sơ pháp lý/ngân hàng Approved hiện hữu. Published edit Draft/Rejected gửi duyệt thành revision Pending mới; chưa làm thay đổi public baseline.
- Staff active đúng role mới được approve/reject; kiểm RevisionId và Pending mới nhất để ngăn stale review. Từ chối cần lý do 1–1.000 ký tự; từ chối bản sửa không ẩn/ghi đè bản đã công bố. Duyệt revalidate nội dung/dependencies rồi apply atomic, giữ trạng thái kinh doanh và staff Hidden nếu có.
- Lưu review note, audit và notification in-app tới owner. Listing snapshot/content/tác giả/sequence không được overwrite/delete qua EF async persistence. Không dùng acknowledgement hay kiểm duyệt nội dung làm approval tài chính.
- My Services thêm trạng thái hoạt động riêng, editor/preview/history proposal; staff queue nhận đúng proposal chờ duyệt. Public browse/detail/review/lead/appointment/combo và các đường đọc liên quan tôn trọng Listing activity. Không bỏ chức năng hoặc nới quyền để bản nháp xuất hiện.
- Đây chưa phải hoàn thiện toàn bộ Phase 4: thống nhất queue Listing/ServiceItem/Combo, paging/filter nâng cao và review Shop/Service/Combo vẫn còn. Không tuyên bố đã thêm immutable revisions cho mọi thao tác ServiceItem cũ.

## Database và bảo toàn dữ liệu

Migration mới `20261019115608_PublicationRevisions` đã review `Up` và qua SQL integration/package smoke:

- Thêm `Listings.IsActive` non-null, default true để không tự tắt Listing legacy; public vẫn cần moderation hợp lệ.
- Thêm nullable `ComboTemplates.ContentJson`, `ReviewNote`, `ValidFromUtc`, `ValidUntilUtc`.
- Tạo `ComboTemplateRevisions` và `ListingRevisions`, unique `(parentId, Sequence)`, FK delete Restrict và các index truy vấn. Chỉ thêm schema; không UPDATE/DELETE dữ liệu Vendor/User/Listing/Combo/order/contract/commission cũ, không backfill financial snapshot hoặc lịch sử được duyệt giả.
- SQL opt-in test bắt đầu từ `20261019053813_ListingServiceDetails`, chèn fixture legacy tổng hợp, migrate latest hai lần, kiểm không pending migrations/model changes. Giữ published combo/item ID/nội dung, legacy scalar rate, hash/security version, order locked totals/discounts/selection/cancellation, confirmed contract commission snapshot/version và receipt/paid commission.
- Sau upgrade, fixture lưu rồi staff duyệt proposal mới và tiếp tục xác nhận order/contract/payment cũ không bị tính lại. Các bài SQL trước tiếp tục kiểm bảo toàn Listing description/media/cancellation và metadata legacy.
- Test/smoke chỉ tạo/xóa DB `WeddingPlanTest_*` riêng do lượt kiểm tra sở hữu; không dùng DB chính làm fixture. Không tăng SQL timeout hoặc bỏ SQL để có gate xanh.

**DB local chính: ĐÃ ÁP DỤNG AN TOÀN.** `WeddingMatch_Dev` đã được backup `COPY_ONLY,CHECKSUM` và `RESTORE VERIFYONLY WITH CHECKSUM` thành công tại `D:\sql\dev\MSSQL16.MSSQLSERVER\MSSQL\Backup\WeddingMatch_Dev_before_publication_revisions_20261019_4d28a9.bak`. Sau đó áp dụng đúng SQL của release đã kiểm tra: migration count11→12, latest `20261019115608_PublicationRevisions`, hai bảng revision mới đều0 dòng;2 Listing legacy có IsActive=true. Row count và SHA256 JSON theo thứ tự các cột gốc của **53 bảng hiện có khớp chính xác trước–sau**, gồm tài khoản/hash, Vendor/rate, Listing/album, khách/thiệp/bàn, checklist/ngân sách, contract/commission/order nếu có. Baseline hiện tại:6 Users,1 Vendor,2 Listings,2 media,70 checklist,28 budget,2 wedding profiles; contract/commission/order đều0. Không sao chép PII vào báo cáo. Chứng cứ và query chỉ đọc ở `artifacts/master-upgrade/phase3cd-main-preservation.txt` và `phase3cd-main-fingerprint.sql`.

API local5008 chạy bản Release cập nhật, ApplyMigrations=false và SeedDemoData=false trong process; `/health/ready`, modern combo catalog và Swagger đềuHTTP200. Debug build riêng đạt0 warning/0 error. Giữ Vite5173 của người dùng, không đổi cấu hình SQL/power/security; không `EnsureDeleted`, reseed, chạy `Down` hoặc bật auto-migrate Production.

## Các file chính

- Domain/EF: `Combo.cs`, `ComboTemplateRevision.cs`, `Listing.cs`, `ListingRevision.cs`, cấu hình revision/Listing/Combo, `IApplicationDbContext`, `ApplicationDbContext`, migration/designer/model snapshot.
- Backend Combo: `ComboTemplateLifecycle.cs`, `CombosFeature.cs`, `ComboCatalogFeature.cs`, `ComboPaginationFeature.cs`, `ComboPricingFeature.cs`, `CombosController.cs`.
- Backend Listing: `ListingPublication.cs`, DTO/summary/editor/detail/pending mappings, Update/Submit/Approve/Reject/Toggle handlers, `ListingsController.cs`; activity guards trên các đường public hiện hữu.
- Frontend Combo: `VendorCombosPanel.tsx`, `ComboTemplateEditor.tsx`, `ComboLifecycleCard.tsx`, `comboTemplateEditorModel.ts`, `ComboAdministration.tsx`, `ComboDetailPage.tsx`, `comboDetailModel.ts`, `comboApi.ts`, `ServiceItemManager.tsx`.
- Frontend Listing: `ListingTable.tsx`, `ListingForm.tsx`, `listingEditModel.ts`, `ListingDetailPage.tsx`, listing API/hooks/types và moderation UI.
- Tests: `ComboTemplateLifecycleTests.cs` (47 cases gồm SQL), `ListingPublicationTests.cs` (26 cases), cập nhật fixture/assertion hiện hữu, `combo-template-editor.test.mjs`, `listing-lifecycle.test.mjs` và các bài detail/edit regression.

## Gate cuối của source này

Gate cuối sau sửa mobile kết thúc **19:19:43 ngày 19/10/2026 (UTC+7)**; `VerifiedUtc=2026-10-19T12:19:43.6549867Z`. Đây là artifact cuối Phase3C/3D sau migration/DTO/UI/tests. Gate19:03:22 trước sửa mobile được giữ như checkpoint lịch sử, không dùng thay cho source mới. Triển khai Phase4 về sau phải tạo gate/artifact mới, không gắn kết quả này cho source đã thay đổi.

| Hạng mục | Kết quả |
|---|---|
| Backend, gồm SQL Server integration | **PASS 565/565**; executed565, fail/error/timeout/notExecuted0; SQL enabled, không skip. |
| Frontend unit | **PASS 142/142**. |
| Frontend lint | **PASS**, 0 error; 10 warning hiện hữu, không tắt analyzer để giấu. |
| Frontend production / backend Release build | **PASS**; warning static/dynamic LoginForm import hiện hữu không chặn build. |
| Package + negative guards | **PASS**, ZIP225 entries, 6 rejection checks, không dữ liệu user/DB/production credentials. |
| Packaged Production + disposable SQL/HTTPS | **PASS**: migration idempotency, SPA/API404 isolation/readiness, catalog trước/sau restart, persistence hash/private image và các smoke hiện hữu. |
| Account/security smoke | **PASS**: registration/role isolation, own-list page/private draft, advance-notice role isolation, password/JWT/contact/REST+Hub/đã revoke WebSocket. |
| Browser/API QA | **PASS trong phạm vi ghi nhận bên dưới**; một thao tác confirm UI bị lỗi công cụ, đã ghi rõ và kiểm chứng tiếp bằng API, không coi bước UI đó đã đạt. |
| DB local chính | **PASS** backup+VERIFYONLY, additive migration11→12, row count/hash53 bảng gốc không đổi, runtime local200. |

Artifact:

- `artifacts/readiness/20261019-121724-21f6f351/ready.json`
- `artifacts/readiness/20261019-121724-21f6f351/test-results/backend.trx`
- Release: `artifacts/readiness/20261019-121724-21f6f351/deploy/wedding-match-20261019-121836-d956a621`
- Log: `artifacts/master-upgrade/phase3cd-ready-mobile-final.log`
- Source SHA256: `39C94E340D37AF0626642058BFE59C5831116BFA69F3E0E617AE1DC588780C23`
- Site ZIP SHA256: `55EEBE0EC2D4BBAE996816BC71E03698A9F2BE4C0CE627503BD5A4214D20E965`

Lượt readiness đầu có hai assertion cũ coi Active/Hidden luôn không được sửa; đã cập nhật test theo nghiệp vụ mới: lưu proposal, không sửa public baseline, album hay snapshot. Không bỏ hai trường hợp này để đạt gate. Working set smoke cuối khoảng162.1→195.1MB chỉ là workload chức năng nhỏ, **không chứng minh tải đồng thời 100 khách** hoặc đủ giới hạn host thật.

### Browser/API QA thực tế, desktop và mobile

- Database tổng hợp riêng `WeddingPlanTest_PublicationUi_20261019_4d28a9`, API5014/Vite5179; tài khoản QA Vendor/Moderator, không thao tác user thật. Chuyển tài khoản bằng logout/login đúng phân hệ.
- Vendor UI tạo/lưu Combo Draft, thêm thành phần đã duyệt, upload PNG từ thiết bị qua endpoint thật; server trả opaque URL, gallery/cover hiển thị. Private preview dùng trang detail, có dịch vụ/giá/ảnh/điều khoản thật của fixture và không có CTA giao dịch. Anonymous preview401, public catalog không có Draft.
- Moderator UI từ chối kèm lý do; Vendor API gửi lại tạo revision3 và giữ phản hồi trước; Moderator UI duyệt đúng revision3. Public catalog chỉ xuất hiện sau duyệt. Vendor UI sửa tên bản đã công bố/lưu Draft revision4; API đối chiếu tên public cũ nguyên vẹn, history4 phiên bản gồm rejection/reviewer/timestamps vẫn còn.
- Listing published edit qua API tạo bản riêng, public title/price/album cũ giữ nguyên. Staff queue UI hiển thị đúng proposal/metadata/ảnh, yêu cầu lý do khi reject. Vendor UI nhận đúng lý do, mở history, sửa mô tả/lưu nháp; public vẫn bản cũ. Hộp confirm gửi lại bị công cụ browser timeout, trạng thái API vẫn Draft; không gửi lặp mù. Đã gửi lại và duyệt qua API, đọc lại nội dung mới và truy vấn revision Approved đầu vẫn nội dung cũ. Không ghi đây là E2E thuần UI thành công.
- Owner activity API tắt Listing trả anonymous404; tắt Combo làm selectedTemplate biến mất khỏi catalog; bật lại theo đúng trạng thái đã duyệt.5 notification in-app tới đúng Vendor fixture, không gửi email/SMS ra ngoài.
- Mobile viewport390×844: Listing dialog và queue không tràn ngang (client/scroll375/375). Combo card/editor ban đầu document scroll380/client375: `.combo-photo` CSS100% ghi đè Tailwind width làm thumbnail co giãn sai; đã thêm khung ảnh80×80px và wrapping, kiểm tra lại card/editor/history document375/375. Không che tràn bằng overflow-hidden toàn trang, không bỏ nút/chức năng.
- Screenshot trong `C:/Users/Admin/.codex/visualizations/2026/10/16/01a14397-369e-7862-9140-f563154bb4f2/`: `phase3cd-combo-card-desktop.png`, `phase3cd-combo-card-mobile.png`, `phase3cd-moderation-mobile.png`, `phase3cd-combo-preview-desktop.png`. Ảnh mascot được dùng rõ ràng làm fixture QA, không thêm vào catalog thật.
- Đã reset viewport, đóng tab QA; tab cũ bị lỗi được đóng qua lifecycle API của cùng browser. Chỉ dừng đúng API5014/Vite5179 do lượt này tạo, xóa đúng DB QA nêu trên. Upload fixture được chuyển nguyên file vào `artifacts/master-upgrade/phase3cd-qa-upload/` để còn kiểm tra, không xóa ảnh người dùng. MainDB/API5008 và Vite5173 được giữ.

## Ranh giới tài chính và phần tiếp theo

- Không đổi CommissionRules, K1/K2, escrow/ledger/payment/cancellation, đơn vị định giá khách/bàn/giờ/gói hoặc công thức discount hiện hữu trong phase nội dung/publication. Approve Listing/Combo **khác** approve chính sách tài chính.
- Giữ quyết định mới của người dùng ở [commission basis và activation](../decisions/2026-10-19-commission-basis-and-activation.md): dịch vụ thường trước VAT sau giảm giá Vendor chịu, trừ chi hộ Admin xác minh; platform-funded discount không tự giảm base nếu Vendor nhận đủ; A–Z chỉ phí quản lý được Admin duyệt; combo phân bổ theo dịch vụ/rate, không thu trùng.
- Công thức/policy mới chỉ active khi đủ Admin duyệt + tới ngày hiệu lực + hợp đồng/phụ lục liên quan được ghi nhận ký ngoài hệ thống. Metadata mô tả, chat, view hoặc acknowledgement không đáp ứng các điều kiện đó.
- Phase 5–7 scoped rule engine/basis snapshot, negotiation/counteroffer/chat riêng, partnership private files/versioning và contract-specific append-only adjustment **chưa triển khai**. Giữ legacy rate/original snapshot; unsettled allocation/offset/refund còn cần xác nhận trước tự động quyết toán.
- Tiếp tục Phase 4 queue/filter/review và Phase 8 music/navigation theo dependency, không đánh dấu xong từ vài component. Giữ integration/host/HTTPS/SQL permissions/persistent uploads/backup restore/resource verification riêng; không tự public deploy hoặc thay SQL/mạng/power/security settings.
