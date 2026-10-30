# Wedding Match — bản đồ kỹ thuật, dữ liệu và vận hành

Cập nhật **22/10/2026**. Đây là hướng dẫn cho source hiện tại; không chứa credentials và không cấp quyền chạy DB ứng dụng. Đọc [START_HERE](START_HERE.md) trước.

## 1. Stack và ranh giới kiến trúc

- Backend .NET 10 / EF Core 10, SQL Server, MediatR/CQRS, JWT/BCrypt, SignalR. `global.json` và `.csproj` là nguồn phiên bản chính thức; không theo README/ảnh cũ .NET8/SQLite.
- Frontend React19, TypeScript6, Vite8, React Router, React Query, Zustand, axios, Tailwind/CSS. `client/package.json`/lockfile là nguồn phụ thuộc; không thay stack.
- Domain chứa entities/invariants, không phụ thuộc EF/UI. Application chứa commands/queries/interfaces/use cases. Infrastructure triển khai EF/storage/provider/jobs. WebApi là composition root/controllers/hubs. Không phải mọi project có thể phụ thuộc ngược nhau theo cây thư mục README.
- FE feature-based; tái sử dụng model/helper/session/transport hiện có. Không tạo store/API/module dịch vụ thứ ba chỉ vì thấy Listing và ServiceItem song song.

```text
client/src/main.tsx → App/routes → feature UI/query/session → axios / SignalR
                                      ↓ HTTP / hubs
Wedding.WebApi → Application handlers → Domain invariants
                      ↓ interfaces
              Infrastructure → SQL Server / storage / providers / jobs
```

## 2. Định vị code theo module

Các đường dẫn dưới đây tính từ repo root; đọc sâu caller/tests liên quan trước sửa.

| Miền | Frontend | Backend/data/test anchors |
|---|---|---|
| Routes/session | `client/src/routes/AppRoutes.tsx`, `ProtectedRoute.tsx`, `CustomerOnboardingGuard.tsx`, `features/auth/` | `Controllers/AuthController.cs`, `Features/Auth/`, `JwtSessionPolicy`, auth/account tests |
| Planner/onboarding/members | `features/journey/planner/`, `features/customer/CustomerDashboard.tsx` | `Features/Planner/PlannerFeature.cs`, `OnboardingStatusQuery.cs`, `MembersFeature.cs`, `WeddingController.cs`, WeddingProfile/Member |
| Budget | `components/tools/WeddingBudgetPlanner.tsx`, `personalBudgetModel.ts` dưới `features/customer/` | `WeddingToolsController.cs`, BudgetItem, `Features/Combos/ComboBudgetSynchronization.cs` |
| Guests/seating | `features/journey/planner/GuestsPlanner.tsx` | `Features/Planner/GuestsFeature.cs`, WeddingGuest/SeatTable |
| Invitations/music | `features/customer/components/invitation/`, `features/journey/invitations/` | `Features/Invitations/`, Invitations/Upload controllers, InvitationAudioStorage, InvitationMusicTests |
| Service catalog/authoring | `features/customer/BrowsePage.tsx`, `ListingDetailPage.tsx`, `features/vendor-portal/components/ListingForm.tsx` | `Features/Listings/`, `ListingCategoryAccess.cs`, ListingsController; Listing/Revision/ServiceItem/Variant/AddOn; UnifiedServiceAuthoringTests |
| Combo | `features/journey/combo/`, `features/journey/comboApi.ts` | `Features/Combos/`, ComboCatalogFeature/ComboTemplateLifecycle/ComboOrderVersionSnapshot; Combo.cs/ComboTemplateRevision; FixedComboPackageTests |
| Customer lifecycle | `features/customer/components/ContractWorkspace.tsx`, `CustomerOrderActionDialog.tsx`, `features/journey/customerOrders/` | `Features/CustomerOrders/`, CustomerOrdersController; CustomerOrderState/OrderChangeRequest |
| Lead/chat | `features/chat/`, `features/vendor-portal/components/LeadInbox.tsx` | Leads/Chat controllers, `WebApi/Hubs/ChatHub.cs`, `Infrastructure/Services/ChatRoomAccess.cs`, Lead/ChatMessage |
| Quote/booking | `features/journey/quotes/`, `components/MyContracts.tsx`, vendor contract components | `Features/Quotes/`, `Features/Contracts/Commands/ConfirmContract/ConfirmContractCommand.cs`, BookingContract, FinancialContractService |
| Private content agreement | `features/private-agreements/` | `Features/PrivateServiceAgreements/`, `PrivateServiceAgreementsController.cs`, PrivateServiceAgreement; dedicated capability/history tests |
| Independent reviews | CustomerReviewCenter, IndependentReviewPanel, ReviewModerationQueue | `Features/Reviews/IndependentReviewFeature.cs`, ReviewRules, ReviewsController; Review/ReviewEditRequest; IndependentReview* tests |
| Moderation confirmation | `features/admin/ApprovalConfirmationDialog.tsx`, ModerationQueue, ComboLifecycleCard | Existing listing/service/combo moderation handlers/versions, FE dialog/caller tests |
| Vendor guide | `features/vendor-portal/VendorJourney.tsx`, `vendorJourneyModel.ts` | `Features/VendorJourney/GetVendorJourneyQuery.cs`, VendorJourneyController, VendorJourneyTests |
| Legacy finance | AdminJourneyPanels/settlement-related UI | CommissionRules, Features/Commissions, Billing/Commissions/Settlements controllers, Commission/Settlement/Payment/CreditNote |
| Phase5 foundation | UI deferred, old VendorPartnershipPolicy is read-only | `Features/CommissionPolicies/` three feature files; three domain entities/configurations; CommissionPoliciesController; Commission* tests |
| Notification | `features/notifications/` | NotificationCenter/typed contexts/outbox workers, NotificationsController |
| Governance/staff/provider status | `features/admin/`, account UI | Governance/Users/IntegrationStatus controllers, staff/auth handlers |

`src/Wedding.WebApi/Controllers/` chứa controller API. `src/Wedding.Application/Features/` chứa use case; `src/Wedding.Domain/Entities/` chứa entities. Tìm theo `rg --files`/`rg`, không coi bảng này là danh mục mọi file.

## 3. Các quan hệ dữ liệu phải hiểu

```text
User(VendorOwner) → Vendor → Listing → ListingRevision / ListingMedia
                              ↘ ServiceItem → ItemVariant / ItemAddOn
Category → BusinessGroup        ↗

Vendor → ComboTemplate → ComboTemplateRevision + Items → ServiceItem references
Customer → ComboOrder + Items (frozen template/component snapshots)
                         ↘ Lead → ChatMessage / Quote → BookingContract
                                                      ↘ Commission → Settlement / Payment

Customer → WeddingProfile → Members / Checklist / Budget / Guests / Tables
Customer → WeddingInvitation → RSVP
Customer + completed transaction → Review(Shop|Service|Combo) → ReviewEditRequest

CommissionPolicyRule ↔ target / approved ComboTemplateRevision
CommissionComponentBasisApproval ↔ exact revision + service + sequence
CommissionAdjustmentRequest ↔ exact Commission + Contract + original snapshots
```

- Category và BusinessGroup khác nhau. Membership cũ không còn permission tạo dịch vụ; canonical Category/group vẫn kiểm tra hoạt động.
- Catalog revisions giữ live/proposed versions; order/contract snapshots giữ giao dịch. Không hydrate order lịch sử từ catalog hiện tại rồi ghi ngược thành snapshot “gốc”.
- `Listing.PricingServiceItemId` là cấu hình giá chính, phải giữ compatibility những dịch vụ legacy có nhiều cấu hình. Không tự merge hoặc đoán cấu hình chính.
- Review Service API historically dựa Listing/giao dịch; không tự đổi toàn bộ keys sang ServiceItem mà phá rows/indexes cũ.
- PrivateServiceAgreement, BookingContract và Partnership Contract là ba loại khác nhau. Chỉ hai loại đầu đã có workflow Customer–Vendor; loại partnership còn thiếu.

## 4. API và security boundaries

- FE route quan trọng: `/login`, `/register`, `/auth/continue`, `/planner/setup`, `/customer/dashboard`, `/browse`, `/combo`, `/combo/:templateId`, `/vendor/dashboard`, `/admin/dashboard`, `/account`, `/notifications`, `/invitation/:slug`. Aliases giữ trong AppRoutes.
- Customer dashboard dùng tab/deep link; role-safe intent phải qua helper hiện hữu. Không dùng `state.from` không lọc hoặc mở redirect ngoài origin.
- JWT server kiểm user còn active, role khớp và SecurityVersion trên mỗi request. Đổi password/khóa quyền làm token cũ không còn hợp lệ. Logout browser không bảo đảm bản sao token nơi khác bị revoke nếu SecurityVersion chưa đổi; chưa có refresh-token/full session registry.
- Backend kiểm ownership dù ID đến từ UI có sẵn. Room ID/LeadId/ComboOrderId không cấp quyền chat. SignalR kiểm lại participant/session; staff không tự được gửi tin Customer–Vendor.
- Policy routes `/api/commission-policies/rules` và `/component-bases`: Finance/SuperAdmin đọc, SuperAdmin create/submit/review. Adjustment `/api/commission-adjustments`: VendorOwner tạo; SuperAdmin list/decision. DTO body không được nhận actor/rate approval từ client ngoài trường thiết kế.
- `ApplicationDbContext.ExecuteSerializableAsync` có transaction Serializable và SQL `sp_getapplock` database-owned cho write; version/concurrency tokens và filtered unique indexes là lớp bảo vệ khác. Không thay lock bằng “button disabled”.
- SaveChangesAsync có guards append-only cho audit, revisions, confirmed finance snapshots, review edits, private agreements và Phase5 records. Các guards không biến raw SQL thành bất biến tuyệt đối; migration/admin SQL vẫn cần governance và tests.
- Errors Domain hiện thường map400; NotFound404, lỗi nội bộ500. Controller `[Authorize]` xử lý anonymous/wrong role. Handler tests không chứng minh toàn bộ middleware HTTP status/browser.

## 5. DB, migrations và hazard khi khởi động

- DB thật: SQL Server. Local config được ghi nhận là localhost/Windows Authentication/`WeddingMatch_Dev`; **không kết nối DB này trong phiên docs để lấy dữ liệu**.
- Có17 migrations trong source. Các mốc gần nhất:14 FixedVendorPackages,15 PrivateServiceAgreements,16 IndependentShopServiceComboReviews,17 `20261021145659_CommissionPolicyBasisAndAdjustments`.
- Migration17 thêm ba bảng mới, không backfill ledger; Down có guard nếu có history. Source model/snapshot đã kiểm bằng SQL tests riêng. **Main DB apply status UNVERIFIED**; không lấy table count/migration count trong source thay `__EFMigrationsHistory` thật.
- Development: ApplyMigrations=true, SeedDemoData=false theo config đang có. NonDevelopment cấm migrate/seed startup và kiểm schema đã được chuẩn bị.
- **Tắt migrate/seed không làm API read-only**: hosted jobs về Lead SLA, verification deadline, settlement, appointment, notification, K2 release vẫn có thể ghi nghiệp vụ. Vì vậy không startup main chỉ để đọc Swagger hoặc xem build có chạy.
- DesignTimeDbContextFactory dành scaffold/offline checks, không gọi API/workers; `WEDDING_MIGRATION_CONNECTION` chọn target riêng, default design-only. `database update` là thao tác khác, không chạy trong audit/docs.
- Migration15/16/17 và code mới có phần untracked. Phải chuyển cả source+model snapshot+designer+migration, không chỉ file `.cs` chính.

## 6. Cấu hình và dữ liệu lưu ở đâu

| Loại | Nguồn/key names | Lưu ý |
|---|---|---|
| DB | `Database:Provider`, `ConnectionStrings:DefaultConnection`, `Database:ApplyMigrations`, `Database:SeedDemoData` | SQL credential là bí mật; không để trong report |
| JWT | `JwtSettings:Secret/Issuer/Audience/ExpiryMinutes` | Production dùng secret riêng, không fallback Dev |
| Host/CORS | `AllowedHosts`, `Cors:Origins` | Exact host/origin, không allow-all credentials |
| Runtime private file | `WEDDING_PRIVATE_CONFIG` hoặc private JSON ngoài webroot | Env overrides; xác minh đường dẫn host thật |
| FE build input | `VITE_API_URL`, `VITE_PROXY_TARGET` theo config | Frontend env được public khi build; không chứa secret |
| Mail/Google | `Smtp:Host/Port/From/Username/Password`, `Google:ClientId` | Configured không đồng nghĩa provider delivery verified |
| Bank/payment | `BankWebhook:Secret/AccountNumber`, `Payments:BankCode/AccountNumber/AccountName` | Không gửi webhook thật/tiền thật để test tùy tiện |
| Media | `Uploads:InvitationRoot/InvitationAudioRoot/ListingRoot` | Durable ngoài release folder/webroot theo policy |

Key environment nested dùng `__`, ví dụ `Database__ApplyMigrations=false`. Không đưa values/private JSON/receipt URL/token lên chat/Git.

- Accounts/password hashes, planner, orders, contracts, reviews, audit, financial rows ở SQL; không phải browser localStorage. LocalStorage chỉ session/cart/UI context theo code.
- Ảnh/MP3 là file storage; DB lưu reference/metadata. Backup SQL không bao gồm file media.
- Local defaults `App_Data/invitation-uploads`, `App_Data/invitation-audio`, `App_Data/listing-uploads`; production release config hướng private durable roots. Không hardcode generic Monster paths cho SmarterASP khi chưa kiểm host.
- Public images khác private evidence/documents. Phase6/7 phải authorized download, MIME/size/path traversal checks, immutable version/checksum; không dùng `/uploads/...` công khai cho hợp đồng/CCCD.
- `Wedding.LocalDataBackup` phục vụ bảo toàn SQLite legacy, **không phải công cụ backup/import SQL Server**.

## 7. Build/test an toàn và cách đọc kết quả

Build không startup API:

```powershell
dotnet build src/Wedding.WebApi/Wedding.WebApi.csproj --artifacts-path artifacts/handoff-build
```

FE, từ `client`:

```powershell
npm.cmd test
npm.cmd run lint
npm.cmd run build
```

Dùng `npm.cmd` để tránh ExecutionPolicy của npm.ps1; không đổi policy toàn máy. Nếu chưa có node_modules, cài từ lockfile theo môi trường đã được phép. Không autofix lint toàn repo để làm handoff sạch.

BE non-SQL — cố ý skip SQL, **không được gọi SQL verified**:

```powershell
$oldSqlOptIn = $env:WEDDING_TEST_SQLSERVER
try {
  $env:WEDDING_TEST_SQLSERVER = ''
  dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --artifacts-path artifacts/handoff-tests --results-directory artifacts/handoff-results --logger 'trx;LogFileName=backend.trx'
  $testCode = $LASTEXITCODE
} finally { $env:WEDDING_TEST_SQLSERVER = $oldSqlOptIn }
if ($testCode -ne 0) { throw "Backend test failed: $testCode" }
```

Actual SQL: **chỉ đổi opt-in thành `'1'` sau khi đọc fixture và chứng minh localhost + GUID database + test sở hữu creation/cleanup**. Không truyền connection string ứng dụng vào biến này. `CommissionSqlTestDatabase` không nhận target từ env, checks DB chưa tồn tại rồi create, cleanup đúng tên của chính nó. Không disable SQL để test xanh khi module chạm query/schema/transaction/finance.

Fresh artifacts path cần restore (đừng thêm `--no-restore` khi assets chưa có). Dùng output riêng tránh DLL lock; không kill mọi dotnet/node hoặc đổi SQL service/RAM/TLS để chữa build.

Gate22/10 đã hoàn thành trước khi chuyển docs-only: targeted62/62, full1135/1135,0skip;4 warnings cũ. FE392 lịch sử. Exact commands/TRX/hash/timestamps ở [report audit](reports/phase5-copilot-audit-2026-10-22.md). Không rerun toàn bộ suite chỉ vì chỉnh Markdown; nếu đổi source sau checkpoint phải kiểm lại phù hợp.

Follow-up UI22/10 sau docs: notification center/role navbar có4source+2tests, **fullFE401/401/lint/buildPASS**,13warningscũ. Staticbrowser layout6rolesdesktop/mobile đạt, authenticatedE2E chưa kiểm. Không backend thay đổi/rerun SQL; xem [report UI](reports/notifications-actor-navbar-2026-10-22.md). Manifest trướcUI là lịch sử, không thay source/Git hiện tại.

## 8. Scripts không phải đều read-only

- `dotnet ef migrations has-pending-model-changes`/`migrations script` dùng design factory: kiểm model/sinh SQL offline, không áp migration. Không thay bằng `database update`.
- `Publish-StudySite.ps1`: build/package local + SQL offline, không coi ZIP được tạo là đã deploy. `Verify-StudyPackage.ps1` kiểm artifact.
- `Test-ReadyToDeploy.ps1`: gate toàn bộ có tests, DB/production smoke do QA sở hữu; **không phải docs-only read check**. `Verify-StudyDeployment.ps1` cũng startup isolated process và tạo/drop QA DB.
- Legacy `Verify-AuthQa.ps1`/`Verify-CustomerOrdersQa.ps1` có target DB/API QA cũ và `-ApplyFixtures` ghi dữ liệu. **Không chạy như generic smoke** nếu chưa chứng minh target/ownership mới.
- Một process/DB test có GUID do lần trước để lại không tự thuộc lần này; đọc manifest/ownership trước cleanup. Không tìm `Wedding*` rồi drop hàng loạt.

## 9. Providers, deploy và backup

SMTP/Google verifier/bank webhook code có; provider-status APIs chỉ báo cấu hình. Zalo/SMS/eInvoice tự động chưa có integration thật được nghiệm thu. Bank webhook hiện dùng signature/custom payload; không tự kết luận tương thích native SePay hoặc đã nhận tiền thật.

Đích gần nhất đã trao đổi: `weddingmatch.website` user báo thanh toán; SmarterASP Advance25.50USD/3tháng được duyệt theo checkout lúc21/10, chưa có bằng chứng host đã mua/active. Hosting đang tạm gác; giá/domain trạng thái cũ là lịch sử, phải verify khi resume. Không cần browse/mua host để hoàn thiện docs.

Hiện kiến trúc package cùng origin React/API/IIS, SQL riêng là persistence. Chưa được yêu cầu tách deploy FE/BE, không rewrite topology. Nếu sau này tách cần exact CORS/API/hub URLs/cookie hoặc bearer behavior/private files/SPA deep-links/HTTPS tests.

Trước release: kiểm runtime .NET10, SQL compatibility/quota/TLS, actual private roots và write permissions, exact host allowlist, HTTPS, migration plan reviewed, backup+restore drill, user files giữ qua redeploy/restart, Swagger không public Production. Old ready/ZIP21/10 **không chứa Phase5/Admin changes hiện tại**; phải re-gate/repackage.

`/health` chỉ liveness; `/health/ready` kiểm DB connect, không chứng nhận đủ schema/logic/provider. Một-user smoke/RAM khoảng200MB không là capacity100 concurrent. Muốn claim100 cần workload/measurement/limits/load test và host thật.

Backup phải nhất quán SQL + media + private config (bí mật xử lý riêng), có retention và test restore vào target độc lập. Không restore đè main hoặc đưa dữ liệu khách vào repo/cloud test không được phép. Các quyền deploy, thanh toán, DNS và chuyển dữ liệu phải được user cấp riêng.
