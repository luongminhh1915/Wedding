# Phase 3A — Quản lý danh sách dịch vụ và bản xem trước

Ngày: 19/10/2026. Tiếp tục branch `weddy-journey`, baseline HEAD `468e507`; giữ nguyên các thay đổi chưa commit của Phase 1–2. Đây là module đầu của Phase 3, không phải tuyên bố hoàn tất cả Service & Combo.

## Kiểm tra phiên theo lịch và khôi phục

- Đọc lịch sử gần nhất trong chính thread, Git status/diff/diff check/ba commit cuối, checklist, các báo cáo và decision record.
- Lượt tự động gần nhất có bằng chứng là preflight lúc 02:04 ngày 19/10; báo cáo ghi rõ chưa thực hiện lượt lập trình 03:05. Không thấy báo cáo/kết quả code riêng của lượt 03:05 trong lịch sử và thư mục báo cáo được kiểm tra; không suy đoán lý do lịch không tạo kết quả.
- Artifact Phase 2 có gate 395 backend + SQL, 96 frontend; báo cáo còn ghi “Chờ” ở một số gate đã đạt. Không chạy lại migration chỉ vì tài liệu chưa cập nhật.
- Đọc SQL local chỉ metadata/số đếm: 6 Users, 1 Vendor, 2 Listings, 2 WeddingProfiles, 70 ChecklistTasks, 28 BudgetItems, 5 BusinessGroups, 0 VendorBusinessGroups, 10 migrations. Migration `20261018171507_VendorBusinessGroups` đã có; Vendor cũ vẫn rate dương và acknowledgement null.
- `RESTORE VERIFYONLY ... WITH CHECKSUM` xác nhận backup trước Phase 2 hợp lệ. Không restore đè, seed, xóa hay sửa dữ liệu thật trong lượt này.

## Đã triển khai

### Danh sách dịch vụ của chính Vendor

- API mới `GET /api/listings/my-listings/page` nhận `keyword`, `status`, `page`, `pageSize`, trả `{ items, totalCount, page, pageSize, totalPages }`.
- VendorOwner đang active và có shop active được đọc dữ liệu shop mình. Backend kiểm actor thực trong DB trước query; không nhận VendorId/UserId từ client, không để row/count của shop khác xuất hiện.
- Search tên, danh mục hoặc khu vực, trim/case normalization; kết hợp status bằng AND trước count và phân trang SQL. Giới hạn từ khóa 200 ký tự, pageSize 1–50, page từ 1, kiểm overflow offset và enum chưa định nghĩa.
- Thứ tự `UpdatedAt ?? CreatedAt` mới nhất trước, sau đó ID để phân trang ổn định. Thêm `updatedAt` nullable vào summary DTO, không thêm cột hoặc migration.
- Giữ endpoint `/api/listings/my-listings` trả array cho bộ tạo dịch vụ/combo cũ. Không đổi giá, discount, lifecycle hoặc snapshot.
- Frontend dùng API page thật, bộ lọc lưu trên URL, chọn 10/20/50 dịch vụ/trang, tổng số, chuyển trang, reset trang khi đổi điều kiện, loading/error–retry/empty/beyond-last states.
- Card navy–cream, thao tác rõ trên mobile; giữ tạo/sửa/gửi duyệt và lý do từ chối. Hiển thị thời gian UTC từ SQL theo múi giờ Việt Nam. Cache own-listings theo actor; invalidation prefix giữ tương thích caller cũ.

### Xem trước như khách hàng

- Link từ My Services mở chính trang chi tiết hiện hữu với `?preview=vendor`; gallery/giá/mô tả dùng chung, không tạo trang mockup khác.
- Preview chỉ thay presentation cho VendorOwner/Moderator/SuperAdmin; query parameter không cấp quyền. API `ListingAccess` hiện hữu quyết định owner/staff/private/public, draft không được public cho Customer hoặc Vendor khác.
- Banner nêu bản xem trước chỉ đọc và trạng thái; tắt tư vấn/đặt lịch/giỏ/viết review/share trong preview, không tạo lead hay booking do bấm thử.
- Link quay lại đúng phân hệ Vendor hoặc kiểm duyệt; nút Back giữ lịch sử bộ lọc. Detail cache có actor ID/role để không dùng nhầm bản riêng giữa phiên.
- Bỏ điểm 4.8 và badge xác thực hardcode không có dữ liệu chứng minh trên trang chi tiết. Giữ review API/eligibility/weighting hiện hữu; không tự đặt công thức tổng hợp rating mới.

### Lỗi phát hiện khi nghiệm thu runtime

- Giờ acknowledgement chính sách sau SQL reload bị lệch 7 tiếng: `datetime2` UTC có thể được serialize không kèm offset và bị browser hiểu thành giờ local. Thêm normalization UTC riêng cho hiển thị policy, giữ timestamp DB không đổi, có regression test cho bare ISO/Z/explicit offset. Browser sau sửa hiển thị 07:44:01 thay vì 00:44:01 trên cùng bản ghi QA.
- Swagger Development trả 500 tại upload ảnh thiệp do tham số `IFormFile` được gắn `[FromForm]`. Phần sửa metadata upload và test sinh tài liệu thật cho toàn controller được nghiệm thu cùng source cuối; không thay validation/authorization/storage của upload. Hướng dẫn của chính thư viện phiên bản đang dùng: [Swashbuckle 6.6.2 — Handle Forms and File Uploads](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/tree/v6.6.2#handle-forms-and-file-uploads).

## File chính

- `src/Wedding.Application/Features/Listings/Queries/GetVendorListings/GetVendorListingsPageQuery.cs` (mới)
- `src/Wedding.Application/Features/Listings/Queries/GetVendorListings/GetVendorListingsQuery.cs`
- `src/Wedding.Application/Features/Listings/DTOs/ListingDto.cs`
- `src/Wedding.WebApi/Controllers/ListingsController.cs`
- `client/src/features/vendor-portal/components/ListingTable.tsx`
- `client/src/features/vendor-portal/hooks/useListings.ts`
- `client/src/features/vendor-portal/vendorListingFilters.ts` (mới)
- `client/src/types/listing.types.ts`
- `client/src/features/customer/ListingDetailPage.tsx`
- `client/src/features/customer/hooks/useBrowse.ts`
- `client/src/features/customer/listingPreview.ts` (mới)
- `client/tests/vendor-listing-filters.test.mjs`, `client/tests/listing-preview.test.mjs` (mới)
- `client/src/features/vendor-portal/businessGroupPolicy.ts`, `components/VendorPartnershipPolicy.tsx`, `client/tests/business-group-policy.test.mjs` (sửa timestamp policy)
- `tests/Wedding.Tests/VendorListingPaginationTests.cs` (mới, 39 cases gồm SQL)
- `tools/Verify-AccountSecurity.ps1` (thêm HTTP listing tests trong QA đã được guard)
- `src/Wedding.WebApi/Controllers/UploadController.cs`, `tests/Wedding.Tests/SwaggerDocumentTests.cs` (regression Swagger upload)
- Checklist và báo cáo Phase 2/3A.

## Kiểm thử và gate

**Module Phase 3A đạt gate.** Lần cuối hoàn tất lúc 07:53:36 ngày 19/10/2026 (UTC+7), sau cả bản sửa giờ policy và Swagger. Không lấy artifact trung gian hoặc Phase 2 để chứng minh source cuối.

| Gate | Kết quả |
|---|---|
| Frontend unit | PASS **106/106**, fail/skip/cancel = 0. |
| Frontend lint | PASS, 0 lỗi; 11 warning hiện hữu giữ nguyên. |
| Frontend TypeScript + production build | PASS; warning LoginForm static/dynamic import hiện hữu không chặn build. |
| Backend + SQL integration | PASS **437/437**, executed437, fail/notExecuted/skip0; gồm 39 pagination cases và 3 Swagger cases mới. Bốn warning xUnit2031 cũ trong test Phase 2, không giấu bằng cách tắt analyzer. |
| Swagger regression | Full real controller document sinh thành công, multipart `file`/`files` dạng binary; giữ role/request/form limits. Targeted test ban đầu vướng CS1061 ở assertion đọc property framework, đã sửa sang constructor metadata; lượt targeted sau đạt3/3 và full gate437/437. |
| Backend Debug / Release build | PASS; Debug build0warning/0error. |
| Package + rejection guards | PASS, 223 ZIP entries, 6 negative checks, không DB/user files/production credentials. |
| Production SQL + HTTPS + restart/security smoke | PASS, schema idempotent, business-group persistence, modern combo catalog, own-list page/filter/count/private404/role401403/array compatibility, JWT/password/contact/SignalR privacy, private file sau restart. |
| Main runtime sau refresh | API5008 readiness200; Swagger200/180paths/có paged listing và multipart schema; frontend5173 proxy own-page401 khi anonymous (không404/SPA fallback). Không mở/đổi port mạng, không seed/migrate ở lần chạy local đã refresh. |
| Main DB cuối lượt | Users6, Vendors1, Listings2, Profiles2, Checklist70, Budget28, Migrations10; legacy vendor1 vẫn rate dương/acknull. Không tạo account/listing thử trong DB thật. |

Artifact cuối:

- `artifacts/readiness/20261019-005151-74624ca3/ready.json`
- `artifacts/readiness/20261019-005151-74624ca3/test-results/backend.trx`
- Release local: `artifacts/readiness/20261019-005151-74624ca3/deploy/wedding-match-20261019-005240-58156d3f`
- Source SHA-256: `1980BC92BB269AA0A06BDE663DE4D60CC8A09B766F27EAF44A5F225F7FE05B6A`
- Site ZIP SHA-256: `18363C870D25DC84A6D1C69F90E581C78B986BC09B9A3CB8752E4A98E9DDBEC0`
- Frontend unit output: `artifacts/master-upgrade/phase3a-frontend-final.log`; targeted Swagger TRX: `artifacts/master-upgrade/swagger-regression-results/swagger.trx`.

Hai artifact 434-test trước đó là trung gian trước khi sửa Swagger; không dùng để deploy bản cuối. Không public deployment. Working set smoke khoảng153→189MB chỉ là workload nhỏ, **không chứng minh sức tải đồng thời**. Host thật/HTTPS/SQL permissions/uploads/backup và provider integrations vẫn cần kiểm tra riêng. QA API cũng có warning license MediatR hiện hữu; điều kiện dùng khi deploy chưa được nghiệm thu trong module này.

### Browser QA thực tế

- Dùng API của release đã build với SQL DB tạm `WeddingPlanTest_VendorUi_1792370595870_32790b3`, không trỏ DB thật. Chỉ tạo dữ liệu tổng hợp qua API trong DB này: một Vendor đa nhóm và 12 bản nháp; ảnh fixture là asset Milo local, không phải ảnh dịch vụ đang bán.
- Đăng nhập đúng Vendor dashboard; page 1 trả 10, page 2 trả đúng 2, tổng 12. Kết hợp từ khóa `tiệc 01` + Draft trả 1, đổi filter reset page. Reload và Back từ preview giữ nguyên URL/filter.
- Bản xem trước đọc đúng draft đã lưu; các nút lead/review/appointment đều có count 0. Mobile không tràn ngang; riêng My Services viewport390/clientWidth375/scrollWidth375, preview cũng width=scrollWidth.
- Policy UI thực đọc hai nhóm 8%/10% cùng version và ack time; sau khi chuyển **fixture QA** thành legacy9%/không membership hoặc ack, reload hiển thị tỷ lệ cũ9% và không tự suy đoán nhóm.
- Đăng xuất rồi mở lại cùng private GUID + preview flag: “Không tìm thấy dịch vụ”, không hiển thị cache nháp cũ.
- Thử gửi duyệt NCC fixture chưa xác minh bị từ chối yêu cầu hoàn thiện pháp lý/ngân hàng; đây là guard hiện hữu, không được bỏ để giả lập luồng thành công. Không tuyên bố browser đã gửi duyệt thành công trong điều kiện đó.
- Đã đóng tab QA, reset viewport, dừng API5014/Vite5179 và QA Vite5178 cũ của agent; xóa **chỉ DB QA tạm do lượt này tạo** sau khi kiểm tra đúng tên và listener đã dừng. Dữ liệu tổng hợp không cần khôi phục; không xóa dữ liệu user. Vite5173 của người dùng được giữ.
- Screenshot thật lưu trong `C:\Users\Admin\.codex\visualizations\2026\10\16\01a14397-369e-7862-9140-f563154bb4f2`: `phase3a-services-desktop.png`, `phase3a-services-mobile.png`, `phase3a-private-preview-mobile.png`, `phase3a-private-preview-after-logout.png`, `phase2-policy-new-vendor.png`, `phase2-policy-legacy-vendor.png`.

## Phần chưa hoàn thành / quyết định cần giữ

- Phase 3 còn structured service/full-service fields, published revisions, separate activity lifecycle, My Combos management và private combo preview. Pagination/preview không đồng nghĩa các mục này hoàn thành.
- Combo fixed sale so với dynamic khách/bàn/giờ, cách thay thành phần/cardinality chưa được xác nhận: giữ công thức hiện tại, không sửa trong module này.
- Commission rule engine, negotiation/chat/counteroffer và partnership contracts/contract-specific adjustments vẫn thuộc Phase 5–7, chưa giả lập bằng UI.
- Vendor cũ giữ chính sách đến Admin xác nhận và tới ngày hiệu lực. Original snapshot hợp đồng giữ bất biến; ngoại lệ cần workflow riêng/audit/Admin approval/activation và xử lý chênh lệch bổ sung, không ghi đè payment.
- VAT/base, unsettled allocation, offset/refund và điều kiện phụ lục vẫn cần quyết định trước triển khai phần tài chính tương ứng.
- Không deploy production, commit/push hoặc thay cấu hình SQL/remote access.
