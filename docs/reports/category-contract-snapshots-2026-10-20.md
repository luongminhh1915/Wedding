# Category → contract → commission snapshot — 20/10/2026

## Phạm vi

Theo ưu tiên mới: danh mục dịch vụ là dữ liệu canonical xuyên suốt tới hợp đồng và nguồn tỷ lệ hoa hồng. Không triển khai ngầm công thức VAT/chi hộ/A–Z mới, scoped rule activation hoặc ngoại lệ quyết toán khi những module đó chưa đủ điều kiện.

## Đã triển khai

- Xác nhận hợp đồng lấy danh mục từ `Lead.Listing.Category`, kiểm tra helper `ListingCategoryAccess` dùng chung. Không nhận category/rate từ client. Kiểm tài khoản Customer thật, đang hoạt động, role và ownership.
- Vendor cũ có tỷ lệ dương giữ tỷ lệ riêng. Vendor mới scalar0 lấy default của đúng danh mục, cần membership hợp lệ. A–Z mới vẫn bị chặn tới khi có approved basis; không lấy12% toàn giá gói.
- Ghi một lần category ID/name, business-group ID/name, nguồn `LegacyVendor`/`CategoryDefault`, phiên bản policy vào lần xác nhận. Legacy rate không được gán giả phiên bản policy của nhóm hiện tại.
- Snapshot tỷ lệ và metadata bất biến sau xác nhận, kể cả NULL lịch sử: không backfill từ danh mục hiện tại. Không tạo FK snapshot tới catalog có thể đổi tên.
- Confirm/detail/customer-list/vendor-list/complete trả cùng metadata snapshot. UI hợp đồng Customer/NCC phân biệt metadata đã chốt, chưa chốt và dữ liệu lịch sử chưa lưu; không dùng tên hiện tại giả làm tên lịch sử.
- Completion không fallback tỷ lệ shop hôm nay hoặc8% khi snapshot thiếu/sai; từ chối trước thay trạng thái/tạo khoản nợ. Kiểm actual active actor/role/ownership; giữ quyền hiện hữu Customer chính chủ, VendorOwner chính shop, Moderator, Finance, SuperAdmin.
- Giữ nguyên math K1/K2/rounding/escrow hiện hữu cho snapshot hợp lệ; không sửa payment/settlement cũ. Chưa đánh dấu hoàn thành toàn bộ Phase5–7.

## Database

Migration `20261019174943_ContractCategorySnapshots`: Up chỉ thêm6 cột nullable vào `BookingContracts`; không UPDATE/DELETE/backfill, không seed, không đổi số tiền hoặc snapshot cũ. Agent này chỉ chạy migration trong database `WeddingPlanTest_ContractCategory_*` do test sở hữu, không áp dụng vào `WeddingMatch_Dev` hoặc restart API chính.

Tích hợp root sau targeted tests: đã backup COPY_ONLY/CHECKSUM+RESTORE VERIFYONLY rồi áp dụng offline migration12→13 vào `WeddingMatch_Dev`;55 bảng nghiệp vụ giữ nguyên số dòng/fingerprint cột cũ qua upgrade và restart. Full final gate861BE/199FE có SQL/package/HTTPS đạt; chi tiết và artifact tại `service-category-phase4b-2026-10-20.md`. Không backfill metadata cũ hoặc deploy public.

## Kiểm thử

- Backend regression **168/168,0 skip**, có SQL Server thật: `artifacts/category-contract-tests/results/contract-category-verified.trx`. Bao gồm49 test mới category/completion và regression Finance/VendorBusinessGroup/ComboBudget/ComboTemplateLifecycle/CustomerOrderLifecycle.
- SQL test nâng từ migration12→13 trên fixture có hợp đồng cũ, bảo toàn rate/status, không tự lấp metadata; completion giữ khoản đã trả một phần; xác nhận hợp đồng mới/reload đọc snapshot đúng.
- Frontend **6/6 test mới**, build production thành công; root cần chạy lại gate tổng thể sau tích hợp các agent.
- Lần targeted đầu39/41: hai assertion escrow dùng fixture cách2 ngày không còn đảm bảo trước thời điểm release sau17:00UTC. Đã sửa fixture thành ngày hôm qua để thật sự còn cửa sổ khóa, không đổi production release rule; log fail đầu được giữ tại `contract-category.trx`.
- Bốn warning xUnit2031 cũ trong VendorBusinessGroupTests; build không có error. Chưa gọi đây là browser E2E; root phụ trách QA tích hợp.

## File chính

- `src/Wedding.Domain/Entities/BookingContract.cs`
- `src/Wedding.Application/Features/Contracts/Commands/ConfirmContract/ConfirmContractCommand.cs`
- `src/Wedding.Application/Features/Contracts/Commands/CompleteContract/CompleteContractCommand.cs`
- `src/Wedding.Application/Features/Contracts/DTOs/ContractDto.cs` và3 query projections
- `src/Wedding.Infrastructure/Persistence/ApplicationDbContext.cs`
- `src/Wedding.Infrastructure/Persistence/Configurations/BookingContractConfiguration.cs`
- migration/Designer/ModelSnapshot
- `tests/Wedding.Tests/ContractCategorySnapshotTests.cs`
- `client/src/types/contract.types.ts`, `client/src/features/contracts/*`, Customer MyContracts, Vendor ContractList, `client/tests/contract-category.test.mjs`

## Còn lại

Snapshot này là **category/rate provenance**, không phải snapshot commission basis đầy đủ hoặc bằng chứng Admin phê duyệt formula mới. VAT/discount sponsorship/pass-through, allocation combo, effective scoped rules, negotiation/partnership documents và contract-specific adjustments vẫn theo checklist Phase5–7. Không deploy/push/reset/data deletion; thay đổi hợp lệ chưa commit giữ nguyên.
