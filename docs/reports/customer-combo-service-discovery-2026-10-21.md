# Customer combo — khôi phục hiển thị dịch vụ lẻ21/10/2026

## Phạm vi và nguyên nhân

Theo yêu cầu trực tiếp mới, `/combo` phải hiện cả dịch vụ lẻ và gói combo. Không triển khai Phase5–7/deploy/main migration hoặc sửa tài chính. Root đọc instructions/handoff/checklist/decision và Git; agent riêng audit rồi chỉ tạo component/tests không chồng file.

Source trước sửa: default activeId=`templates`; paged API chỉ hydrate service rows khi có CategoryId/ListingId. Menu dịch vụ chưa bị xóa, nhưng default response không có service options để hiển thị. Chỉ thêm JSX từ response template không đủ.

## Đã triển khai

- Default page có khu **Tự chọn dịch vụ** trước danh sách combo, chips danh mục/count,4service preview/category từ API thật, ảnh/giá/chi tiết và CTA cấu hình/thêm giỏ. Combo hiện hữu vẫn giữ bên dưới, kể cả trường hợp0combo vẫn tìm được dịch vụ.
- Chọn danh mục/Xem tất cả dùng lại service catalog có paging. Deep-link category/listing không gọi thêm preview. Preview giữ keyword/giá/city/region/rating/vendor, bỏ số thành phần/savings chỉ dành combo.
- Component cards dùng chung cho preview và category view; giữ capacity/Table quantities, selected/replacement state, FixedPrice lock. Merge preview options vào lookup để configurator/cart thực sự nhận dịch vụ, không chỉ render ảnh.
- Không reset cart khi đổi filter/view, giữ one-Vendor và fixed snapshot rules; thêm guard khi option không còn trong lookup. Hook useCatalogPage thêm optional enabled, backward-compatible mặc định true. Không sửa BE/DB/schema/config.

Files source:

- `client/src/features/journey/combo/ComboBuilderPage.tsx`
- `client/src/features/journey/comboApi.ts`
- `client/src/styles/combo-discovery.css`
- Mới `client/src/features/journey/combo/ComboServiceCards.tsx`, `comboServiceDiscoveryModel.ts`
- Mới `client/tests/combo-service-cards.test.mjs`, `combo-service-discovery.test.mjs`

## Kiểm chứng mới

- `npm.cmd test` trong client: **275/275 PASS,0fail/skip**, gồm9cases mới (5component SSR/callback +4preview/navigation model).
- `npm.cmd run build`: PASS TypeScript/Vite. Lần đầu thiếu import unitName còn được dùng ở phần review-order; đã phục hồi import và chạy build lại thành công. Không che lỗi build đầu.
- `npm.cmd run lint`: exit0,13warnings có sẵn; Vite vẫn cảnh báo mixed static/dynamic LoginForm import.
- BE source/API/schema không thay đổi, không rerun fullBE chỉ vì sửa FE. Full1004SQL ở report Phase4C là lịch sử trước lượt này, không phải gate fullstack mới.
- Browser **actual SQL + API + Vite**, không mock: default có cả2service cards và1combo; chọn card→configurator→Thêm vào giỏ thành công; selected state đúng; category navigation/reload/Back giữ giỏ1item. Mobile viewport390x844: documentWidth375, không overflow ngang.
- Browser fixture không có ListingMedia nên hiện placeholder ảnh đúng, không bịa ảnh. Component SSR kiểm image/gallery props với dữ liệu ảnh; actual user uploads/gallery mới không được test thêm ở lượt này.
- Screenshot capture thành công lượt này: `artifacts/combo-service-discovery-20261021/default-page.png`, `mobile.png`; chỉ dữ liệu hư cấu, không lấy ảnh lượt Review cũ làm bằng chứng.

Không kiểm lại authenticated checkout/submit/order/contract/payment hoặc toàn E2E, không thay policy public nguồn để test xanh.

## Isolation và dữ liệu

GET read-only API đang chạy5008 `/api/combos/catalog` tại đầu lượt trả0templates và emptycategoryCounts/8categories. Đây là tình trạng eligible catalog lúc kiểm, **không chứng minh DB bị mất dữ liệu**; không query/migrate/reseed DB ứng dụng hoặc restart process của người dùng.

Browser dùng harness có guards đã đọc: fixedlocalhost+IntegratedSecurity, fresh GUID và SQL ownership marker,6actors hư cấu, DB `WeddingPlanTest_Review4CRepair_2a75dcbf41b04d3394103e4f2fbb394a`. Reuse runtime BE verified trước (không có BE edits), migrate/seed=false, upload roots artifacts; API5023(session8889)/Vite5183(session78695). Chỉ test local guest cart, không gửi order/tiền thật.

Cuối QA đã clear đúng giỏ hư cấu qua UI, reset viewport/đóng tab, Ctrl-C đúng2process (exit1 do shutdown) và CLI guarded drop DB fixture thành công06:45:05VN. Không còn listener5023/5183; media/closed manifest/harness/screenshot giữ artifacts. Chỉ dữ liệu test bị dọn, có thể tạo lại; không xóa dữ liệu thật. Runtime có warnings EF20601/20504 và MediatR development license; không tự đổi cấu hình SQL/system để giấu warning.

## Git / bàn giao

Đầu lượt `weddy-journey`/HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`,60tracked modified/51expanded untracked/0staged/1worktree. Sau source+docs62tracked/57untracked/0staged/1worktree;6untracked mới là4source/test files và decision/report này. Các phần khác giữ nguyên; không commit/push/reset/clean.

SourceSha256 (cùng hàm readiness, không chạy packaging script): `EB6BC842E34743F8AC05D79009615B8F176CF97ACDC5CD2ABC95DA52626B7935`. Không chứng nhận ZIP/package cũ theo hash mới. Handoff/checklist/startprompt đã cập nhật theo phạm vi; không làm lại Phase4C repairs.

Điểm tiếp: nếu catalog ứng dụng vẫn trống, audit read-only public availability/verification/participation/canonical pricing theo đúng dữ liệu thực khi được giao; không tự seed hoặc nới điều kiện. Extended fullsystem/Phase5–7/production migration/deploy vẫn không được cấp quyền từ yêu cầu UI này.
