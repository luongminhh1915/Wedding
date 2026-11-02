# Phase 4C — Đánh giá độc lập Shop, Service và Combo

Nguồn: yêu cầu và các xác nhận tiếp theo của người dùng ngày20/10/2026. Trạng thái business rules: **CONFIRMED**, không đổi trong lượt verification. Implementation: **IMPLEMENTED; migration chưa áp DB ứng dụng**. SQL test-only verification đã đạt; browser targeted có findings nên chưa nghiệm thu toàn Phase4C, xem cập nhật cuối tài liệu.

Trạng thái cập nhật21/10: các findings repair đã sửa và đạt fullBE1004actualSQL/0skip, FE266/build/lint, API và targeted browser; extended acceptance còn giới hạn. Phần verification20/10 ở dưới là lịch sử. Hai quyết định visibility/dispute được bổ sung riêng21/10, không sửa lịch sử quyết định cũ.

## Quyết định đã xác nhận

1. Ba target độc lập: Shop/Vendor, Service và Combo. Mỗi target có danh sách, điểm trung bình và thống kê riêng.
2. Chỉ Customer có giao dịch hợp lệ Completed được gửi review mới. Service phải có giao dịch tương ứng Completed; Shop cần giao dịch Completed với Vendor; Combo cần các hợp đồng thành phần cần thiết Completed, không dựa riêng `ComboOrder.Status.Completed`.
3. Customer hoàn thành Combo được review từng Service component đủ điều kiện, Combo tổng thể và Shop.
4. Trong một giao dịch: tối đa một Shop review cho cùng Vendor; tối đa một Combo review cho cùng Combo; tối đa một review cho mỗi Service riêng biệt. Cùng Customer được review lại cùng Shop/Service ở giao dịch mới. Stable Shop transaction key cho Combo là `ComboOrderId`, không phải mỗi BookingContract thành phần.
5. Chủ sở hữu được sửa review một lần trong vòng7 ngày từ khi đăng. Phải lưu lịch sử nội dung/điểm trước và sau, thời gian và người sửa; chỉ Customer sở hữu review được sửa.
6. Aggregation chỉ tính review trực tiếp cùng target. Shop không nhận điểm từ Service/Combo; Service và Combo cũng chỉ nhận điểm target tương ứng.
7. Combo review phải trỏ đúng public `ComboTemplate` và lưu version/snapshot tại thời điểm giao dịch. Custom Combo không có public template tương ứng không được gán vào template khác.
8. Giữ toàn bộ Review cũ và quan hệ lịch sử hợp lệ; không xóa/reset/backfill suy đoán.

## Baseline source assessment before implementation

- Trước Phase4C, Review có `ListingId`/`VendorId` bắt buộc, unique Customer+Listing, nullable `BookingContractId` với quan hệ one-to-one. Rows lịch sử có Listing target được ánh xạ xác định thành Service; giữ nguyên Id, content, photos, status, reply, `IsVerifiedBuyer`, score criteria và timestamps.
- Trước Phase4C, `ReviewRules.RefreshVendorAsync` tổng hợp Listing reviews vào `Vendor.RatingAvg/TotalReviews`. Implementation hiện nay tính Vendor cache từ Shop reviews trực tiếp; Service/Combo không được cộng sang Shop. Migration reset cache cũ về 0/0/false vì mọi Review cũ được xác định là Service, không phải Shop.
- `BookingContract` trỏ tới Lead; Lead có nullable `ComboOrderId`; `ComboOrderItem` trỏ Lead. Một ComboOrder có thể có nhiều BookingContract, do đó Shop uniqueness cho giao dịch Combo phải group theo ComboOrderId. Service eligibility vẫn kiểm từng BookingContract theo Listing.
- `ComboOrder.ComboTemplateId` xác định template trực tiếp khi có giá trị; custom order có thể null. Tuy nhiên ComboOrder không lưu `ComboTemplateRevisionId` hoặc serialized template proposal. ComboTemplate revisions giữ snapshot riêng nhưng không có FK từ order. Fixed ComboOrderItem chỉ giữ component snapshot per line; dynamic items không có snapshot template đầy đủ. Không thể chứng minh exact revision/name/terms/content của ComboOrder lịch sử từ current template hoặc timestamp.
- ComboOrder cũ có thể mang `ComboTemplateId` nhưng không giữ `ComboTemplateRevisionId`/snapshot đáng tin cậy. Không backfill revision/snapshot bằng timestamp hoặc template hiện tại. Service/Shop eligibility vẫn độc lập; legacy Combo review fail-closed.

## Implementation and migration record

- Migration: `20261020123641_IndependentShopServiceComboReviews`. Adds nullable review target/transaction references, a non-null `TargetType` defaulting existing rows to Service, ComboOrder revision/snapshot columns and `ReviewEditRequests`; expands the BookingContract relationship to multiple reviews; adds target check/unique/query indexes and ComboOrder revision FK.
- Existing Review rows are retained without rewriting review content/status/verification/history. The deterministic Listing→Service mapping is the only target backfill. Existing ComboOrders receive no inferred revision/snapshot. No historical ComboTemplate link is assigned.
- Up resets `Vendor.RatingAvg/TotalReviews/IsTopRated` to Shop-only derived values. Down checks for non-Service reviews, duplicate legacy Service keys, any edit history or any ComboOrder revision/snapshot; it throws rather than dropping those new records. If safe to downgrade, it recomputes the prior Service-based cache before removing the target schema.
- New ComboTemplate orders require a current approved revision. Each order stores that exact revision ID, sequence, raw revision JSON and ordered component/order snapshot. An approved public template without an approved revision fails closed for new order submission until versioned; it does not receive a fabricated revision.

## Confirmations received and implemented

- Legacy ComboOrders without reliable version/snapshot cannot be Combo-reviewed; eligible Shop and Service reviews remain available. Combo custom order with no public template never rates another template.
- Approved review edits: previous Approved content remains public and counted while a new edit is Pending; Moderator approval publishes the edit and changes aggregate; rejection keeps prior Approved content/rating. Maximum one approved edit, one Pending at a time, at most3 submissions in the original 7-day window; rejection does not reset the window. Owner-only, actor/time/before/after/reason persisted.
- Completed Combo review requires every necessary component contract to be Completed and exactly match its ComboOrder Lead, Customer, Vendor and Listing. Shop review for a ComboOrder is unique by ComboOrder, not individual contract. Service remains one per contract/listing.

## Verification status lịch sử — lượt triển khai trước follow-up20/10

- Frontend: `npm.cmd run build` PASS; `npm.cmd test -- --test-reporter=dot` 252/252 PASS; `npm.cmd run lint` exit0 with existing warnings.
- Backend: `dotnet test tests\\Wedding.Tests\\Wedding.Tests.csproj --no-restore -p:OutDir=D:\\Temp\\WeddingReviewFinalTests\\` 924 passed,0 failed,26 SQL-only skipped/950. SQL opt-in was forced empty; no SQL Server or application database was connected.
- `dotnet ef migrations has-pending-model-changes` reports no model drift; Up SQL script reviewed offline. Migration has **not** been applied. Current Review submission/edit/public/moderation flows have not been browser E2E tested.

SQL Server upgrade/replay/preservation tests must use a uniquely owned disposable test DB after verifying its target. Do not apply the migration to the application database as part of this phase.

## Verification follow-up20/10/2026 — không thay đổi business decisions

Follow-up nghiệp vụ21/10: public Combo reviews ẩn cùng availability của Combo; edit approval bị dispute hold như review mới, theo [decision bổ sung đã CONFIRMED](2026-10-21-review-visibility-and-dispute-holds.md). Không xóa hoặc rewrite historical review/snapshot.

- API build mới0warning/0error; backend targeted83/83 PASS,0skip có SQL Server riêng. Migration15→16, script replay, legacy preservation/no inferred order revision, constraints và guarded Down đã kiểm trên DB GUID do test sở hữu, sau đó cleanup.
- 18/18 scripted HTTP checks đạt. Browser fixture kiểm Customer Service/Shop/Combo submission, Moderator approvals/edit rejection+approval và public panels/filter. Đây là targeted QA, chưa phải full E2E acceptance.
- Đã tái hiện edit text/rating làm mất ảnh/4criteria sau approve, thiếu Shop opportunity của custom order, blank target names và response edit count lệch. Chưa sửa production hoặc tự quyết định semantics/policy khác trong lượt kiểm chứng.
- Full FE252 và BE924pass+26skip/950 ở trên là lịch sử; không chạy lại full suite chỉ vì sửa docs. Migration chưa áp DB ứng dụng/production; không kết nối DB đó, không bắt đầu Phase5–7.
- Bằng chứng/lệnh/test limits và bước tiếp: [báo cáo verification](../reports/phase4c-verification-2026-10-20.md). Giữ toàn bộ quyết định/snapshot/history, chỉ tiếp tục sửa khi có yêu cầu mới.

## Repair follow-up21/10/2026

Theo yêu cầu tiếp tục song song: preserve null/omitted photos+criteria trong edit, đủ before/after moderation, customShop opportunity, tên/count/cache đã sửa. Original scoring giữ nguyên; photo JSON `[]` vẫn explicit clear, không thêm null-clear criteria. Policy bổ sung21/10 đã tái sử dụng catalog/dispute guards, không xóa history hoặc rewrite bản Approved khi hold. FullBE1004/1004 cóSQL/0skip, FE266/266/build/lint/API và targeted browser đạt; không thêm schema/migration hoặc áp DB ứng dụng. Commands/files/remaining và screenshot limitation ở [report repair](../reports/phase4c-repairs-2026-10-20.md).
