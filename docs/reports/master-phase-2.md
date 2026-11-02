# Phase 2 — Vendor, nhóm kinh doanh và kế thừa chính sách

Ngày: 19/10/2026. Tiếp tục trực tiếp worktree `weddy-journey`; giữ nguyên toàn bộ bản vá Phase 1. Phase 1 đã đạt 369/369 backend + SQL, 89/89 frontend và Production package smoke; source Phase 2 được kiểm tra riêng, không tái sử dụng kết quả package cũ.

## Nghiệp vụ đã triển khai

- Năm nhóm kinh doanh lấy từ DB: Địa điểm & Tiệc cưới 3%; Trang trí & Thiệp cưới 8%; Quay chụp/Váy/Makeup 10%; Tổ chức cưới trọn gói A–Z 12%; Tiệc & Sự kiện khác 3%.
- Giữ nguyên bảy Category ID/slug cũ, ánh xạ về business group, không gộp/xóa category. Đổi tên đầy đủ planner, thêm category sự kiện khác riêng với tiệc cưới chính.
- Signup NCC chọn nhiều nhóm active/distinct, xem mô tả/tỷ lệ, tóm tắt và xác nhận đã đọc policy. Thiếu policy API thì chặn và cho thử lại, không fallback dữ liệu giả.
- API không nhận rate tự chọn: mọi `CommissionRate` được gửi rõ ràng đều bị từ chối. Client chỉ gửi group IDs và acknowledgement, không được tự gán trạng thái duyệt hay chính sách tài chính.
- Lưu user/vendor/membership + policy version/time trong transaction đăng ký hiện có. `Vendor.CommissionRate=0` chỉ là sentinel **kế thừa theo danh mục** ở factory riêng cho đăng ký mới, không phải miễn phí/hoa hồng 0%; resolver hiện hữu đã có nhánh kế thừa.
- Factory legacy `Vendor.Create` vẫn giữ khung 3–12%. Mọi NCC cũ giữ nguyên scalar rate như quyết định của người dùng; không tự suy đoán group từ rate, không backfill acknowledgement hoặc tự gọi rate cũ là negotiated đã duyệt.
- Vendor Dashboard có tab chỉ đọc **Chính sách hợp tác** từ API thật, phân biệt default theo group và tỷ lệ legacy. Chưa có nút thương lượng giả; editor/override/rule activation thuộc Phase 5–6.
- Khi NCC mới dùng kế thừa: xác nhận hợp đồng cần category và business group đang active, đã map; không fallback đoán 8% nếu thiếu. Full-service A–Z chưa có approved basis thì fail-closed trước mọi mutation, không tính 12% toàn ContractValue. Luồng legacy dương giữ hành vi hiện hữu.

## API và dữ liệu

| Endpoint | Hành vi |
|---|---|
| GET `/api/business-groups` | Public danh sách active, ID/code/name/description/defaultCommissionRate/requiresCommissionBasisApproval/policyVersion từ SQL. |
| GET `/api/business-groups/my` | Chỉ VendorOwner đang active, own-shop: membership, legacy/default mode, rate legacy nếu có, acceptance time/version. |
| POST `/api/auth/register-vendor` | Giữ credential/shop fields; thêm `BusinessGroupIds`, `PartnershipPolicyAccepted`; `CommissionRate` nullable chỉ để từ chối client cũ gửi rate với thông báo rõ. Không tạo lại route signup. |

Migration **`20261018171507_VendorBusinessGroups`**, Up đã được review:

- Hai bảng mới `BusinessGroups`, `VendorBusinessGroups`; unique group code, composite PK membership, FK Restrict.
- Hai cột nullable acceptance time/version trên Vendors; không UpdateData hàng Vendor cũ.
- Category thêm nullable group FK và basis-approval marker; map bảy seed ID đã biết, custom category vẫn unmapped, thêm category thứ tám `tiec-su-kien-khac`.
- Group seeds/version/timestamp cố định; cập nhật metadata planner và default category 12% nhưng legacy Vendor rate/snapshot không bị đổi.
- Không ghi/xóa BookingContract, Commission, PaymentTransaction, settlement, User hoặc dữ liệu cưới. Không chạy Down/rollback trên dữ liệu thật.

## File chính

- Domain: `BusinessGroup.cs` (kèm membership/policy IDs), `Vendor.cs`, `Category.cs`.
- EF: DB interfaces/context, `BusinessGroupConfiguration.cs`, `VendorConfiguration.cs`, `CategoryConfiguration.cs`, migration/model snapshot.
- Application/API: `BusinessGroupsFeature.cs`, `BusinessGroupsController.cs`, `RegisterVendorCommand.cs`, guard `ConfirmContractCommand.cs`.
- Frontend: `RegisterForm.tsx`, registration validation/types/CSS; business-group parser/query hook, `VendorPartnershipPolicy.tsx`, tab VendorDashboard.
- Test/tool: các test group/registration/schema, frontend group parser/payload; QA signup/Production schema/own-policy smoke dùng contract mới.
- Decision record: `docs/decisions/2026-10-19-legacy-and-contract-commission-adjustments.md`.

## Kiểm tra và trạng thái nghiệm thu

| Gate | Kết quả hiện tại |
|---|---|
| Frontend unit | PASS 96/96, không skip. |
| Frontend lint/build | PASS; 11 lint warning cũ và warning LoginForm static/dynamic import giữ nguyên. |
| Backend Release build để scaffold migration | PASS; chỉ scaffold offline, không kết nối/apply DB thật. |
| Backend unit + SQL, upgrade preservation | PASS **395/395**, không skip/fail; gồm 26 business-group cases và migration preservation. Bốn warning xUnit2031 về Assert.Single trong test, không phải lỗi runtime. |
| Package/Production SQL/HTTPS/restart/security + policy persistence | PASS — 6 package rejection guards, SQL migration idempotency, membership/ack persistence và actor boundary qua HTTPS, private-file restart và JWT/contact/SignalR security. |
| Main DB backup/apply schema/preservation | Đã COPY_ONLY/CHECKSUM backup và VERIFYONLY trước khi apply migration additive; giữ nguyên số đếm và fingerprint user/vendor ở lượt thực hiện. Kiểm tra lại 19/10: 10 migrations, group5/membership0 cho shop legacy; backup VERIFYONLY vẫn hợp lệ. Không apply lần nữa. |
| Browser desktop/mobile | PASS cho signup lấy 5 nhóm từ API thật, chọn 2 nhóm 8%/10%, summary độc lập, không submit vào DB thật. 390px không tràn ngang. UI policy mới/legacy được kiểm tra bằng DB QA riêng ở Phase 3A: nhóm8/10%/ack thật sau reload, legacy9%/membership rỗng không bị đoán nhóm; phát hiện và sửa lệch giờ SQL, gate cuối437BE/106FE bao phủ bản sửa. Xem báo cáo Phase3A, không dùng screenshot signup làm bằng chứng policy riêng. |

Gate đã hoàn tất trước gián đoạn, báo cáo được đồng bộ lại từ artifact và kiểm tra runtime thật ngày 19/10; không gọi đây là lần test mới của source Phase 3A.

- `artifacts/readiness/20261018-171826-7b6e5127/ready.json`, backend TRX cùng thư mục `test-results`.
- Source SHA-256 `4AC736D9FE2C6DC59E6998350499AD17E5EB874A4706898E8C0E3F69BA9B2C9F`.
- Site ZIP SHA-256 `64AAA0499703931D0326B135D566E0D32A87B9FA5249E6C2269D87303C7E25B8`.
- Backup: `D:\sql\dev\MSSQL16.MSSQLSERVER\MSSQL\Backup\WeddingMatch_Dev_before_business_groups_20261019_1792344058065-d3fcd58b.bak`.
- Baseline sau migration: Users6, Vendors1, Listings2, WeddingProfiles2, ChecklistTasks70, BudgetItems28. User fingerprint `62F0A041F07D3E2CDD33031DE63A87411B583BE5FE577D44E334B61BFBBC3B63`; vendor fingerprint `B4F81A8C5A08C491BAE2F68FBE06B95A371F05764403D9EA3D0995E7D5937D86` (fingerprint chỉ các trường được chụp, không thay thế backup).
- Screenshot QA lưu ngoài repo tại thư mục visualization của thread: `phase2-vendor-groups-desktop.png`, `phase2-vendor-groups-mobile.png`, `phase2-vendor-groups-mobile-detail.png`.

FE logs: `artifacts/master-upgrade/phase2-1792343787243/`. Không public deployment.

## Còn lại và giới hạn

- Chưa triển khai full scoped commission engine, Admin policy editor/effective dates, full-service approved-basis model, negotiation/counteroffer, partnership private contracts hoặc điều chỉnh riêng hợp đồng. Các mục này vẫn ở Phase 5–7; quyết định mới được ghi nhận nhưng **không giả vờ đã có workflow tài chính**.
- Tỷ lệ category/default mới không có nghĩa đã chốt VAT/base. Phép tính tiền, K1/K2, escrow, rounding/discount và ledger hiện hữu không được đổi trong phase này. A–Z mới bị chặn khi thiếu basis thay vì thu sai.
- Chưa tự chuyển policy legacy hoặc mở endpoint sửa scalar rate. Admin transition phải có scope/effective date/audit ở Phase 5.
- Không chạy full-service như một combo nhiều dịch vụ; structured service details và published revisions thuộc Phase 3–4.
- Trước Phase 3 cần xác nhận fixed-price combo so với khách/bàn/giờ và cách đổi thành phần; trước Phase 5–7 cần chốt VAT/base, unsettled allocation và cơ chế credit/refund/hợp đồng làm điều kiện kích hoạt.
