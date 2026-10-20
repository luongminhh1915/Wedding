##  THIẾT KẾ CẤU TRÚC DỰ ÁN (SOLUTION & FOLDER STRUCTURE)

### 1. Backend: .NET 10 Clean Architecture

Cấu trúc Solution gồm 4 project chính tuân thủ quy tắc phụ thuộc:  
`Domain` $\leftarrow$ `Application` $\leftarrow$ `Infrastructure` $\leftarrow$ `WebApi` (Presentation)

```text
Wedding/
├── Wedding.sln
│
├── src/
│   ├── Wedding.Domain/                      # [TẦNG 1: LÕI NGHIỆP VỤ]
│   │   ├── Common/                          # BaseEntity, IAggregateRoot, ValueObject
│   │   ├── Entities/                        # User, Vendor, Category, Listing, Lead,
│   │   │                                    # Voucher, BookingContract, Commission, Review...
│   │   ├── Enums/                           # LeadStatus, ContractStatus, UserRole...
│   │   ├── Exceptions/                      # DomainException, NotFoundException...
│   │   └── ValueObjects/                    # Money, PhoneNumber, Address...
│   │
│   ├── Wedding.Application/                 # [TẦNG 2: USE CASES & CQRS]
│   │   ├── Common/                          # Behaviors (Validation, Logging), Exceptions
│   │   ├── Interfaces/                      # IApplicationDbContext, IJwtProvider,
│   │   │                                    # IZaloZnsService, IVietQrService, IStorageService...
│   │   ├── Features/                        # Tổ chức theo tính năng (Vertical Slices):
│   │   │   ├── Auth/                        # Commands (Register, Login), DTOs, Validators
│   │   │   ├── Listings/                    # Queries (GetListings, GetDetail), Commands (Create, Moderate)
│   │   │   ├── Leads/                       # SendLeadCommand, AcceptLeadCommand, SLA Worker
│   │   │   ├── Contracts/                   # CreateContractCommand, ConfirmContractCommand (BR-005)
│   │   │   ├── Commissions/                 # CalculateSettlementQuery, VietQrPaymentCommand (FM-001..004)
│   │   │   ├── WeddingTools/                # Invitation, RSVP, Budget, Checklist
│   │   │   └── Reviews/                     # SubmitReview, ApproveReview (FM-005, BR-009)
│   │   └── Mappings/                        # AutoMapper hoặc Mapster Profiles
│   │
│   ├── Wedding.Infrastructure/              # [TẦNG 3: HẠ TẦNG & TÍCH HỢP]
│   │   ├── Persistence/                     # EF Core ApplicationDbContext, Configurations
│   │   │   ├── Configurations/              # Fluent API Entity configurations
│   │   │   └── Migrations/                  # EF Core DB Migrations
│   │   ├── Services/                        # Triển khai các Interface từ Application:
│   │   │   ├── JwtProvider.cs               # Sinh & giải mã JWT
│   │   │   ├── VietQrService.cs             # Sinh mã QR đối soát hoa hồng
│   │   │   ├── ZaloZnsService.cs            # Bắn thông báo ZNS < 10s
│   │   │   ├── CloudinaryStorageService.cs  # Lưu trữ nén ảnh portfolio
│   │   │   └── AiStylistService.cs          # OpenAI/Gemini semantic prompt
│   │   └── BackgroundJobs/                  # Quartz.NET / BackgroundService (Check SLA 2h, Day 25 Settlement)
│   │
│   └── Wedding.WebApi/                      # [TẦNG 4: PRESENTATION & ENTRY POINT]
│       ├── Controllers/                     # AuthController, ListingsController, LeadsController...
│       ├── Middlewares/                     # GlobalExceptionHandlingMiddleware
│       ├── Extensions/                      # DependencyInjection extensions
│       ├── Program.cs                       # Cấu hình pipeline DI, Swagger, CORS, Auth
│       └── appsettings.json                 # ConnectionStrings, JWT Secret, 3rd Party Keys
```

### 2. Frontend: React TypeScript (Feature-Based Structure)

Khởi tạo bằng **Vite + React + TypeScript + Tailwind CSS / Vanilla CSS**:

```text
client/ (hoặc wedding-client/)
├── public/
├── src/
│   ├── assets/                              # Logo, icons, wedding moodboard samples
│   ├── components/                          # UI dùng chung: Button, Modal, Card, Input, Table, Badge...
│   ├── layouts/                             #
│   │   ├── CustomerLayout.tsx               # Header, Footer cho Cô dâu/Chú rể
│   │   ├── VendorLayout.tsx                 # Sidebar, Header cho Nhà cung cấp
│   │   ├── AdminLayout.tsx                  # Sidebar phân quyền cho Admin/Mod/Finance/CSKH
│   │   └── AuthLayout.tsx                   # Form đăng nhập/đăng ký
│   │
│   ├── features/                            # Chia theo từng phân hệ nghiệp vụ:
│   │   ├── auth/                            # Login, Register, OTP Modal, useAuthStore
│   │   ├── catalog/                         # Trang chủ, bộ lọc 7 ngành, thẻ bài đăng, chi tiết gói
│   │   ├── ai-moodboard/                    # Trắc nghiệm hình ảnh gu cưới, màn hình kết quả gợi ý
│   │   ├── leads/                           # Form gửi tư vấn, popup nhận Voucher 8 ký tự, chat In-App
│   │   ├── vendor-portal/                   # Quản lý bài đăng, hộp thư lead, tạo hợp đồng
│   │   ├── contracts/                       # Màn hình duyệt hợp đồng 2 chiều (Customer)
│   │   ├── settlement/                      # Bảng kê hoa hồng ngày 25, popup quét mã VietQR (Vendor & Finance)
│   │   ├── wedding-tools/                   # Trình tạo thiệp cưới, trang công khai RSVP, quản lý ngân sách
│   │   └── admin/                           # Danh sách duyệt bài (Mod), thống kê GMV (Super Admin)
│   │
│   ├── services/                            # Axios instance, Interceptors gắn Bearer token, base API calls
│   ├── routes/                              # AppRoutes.tsx, ProtectedRoute.tsx (RBAC check)
│   ├── store/                               # Zustand store: authStore, weddingBudgetStore...
│   ├── types/                               # TypeScript Interfaces khớp DTO Backend
│   ├── App.tsx
│   └── main.tsx
```

---

### 3. Lưu trữ dữ liệu và chạy local

- Source hiện tại dùng **.NET 10 / EF Core 10, React 19 + Vite 8**. Các cây thư mục trên là tổng quan kiến trúc, không phải cam kết mọi tích hợp minh họa đã được cài đặt.
- Local mặc định dùng **SQL Server `localhost`, DB `WeddingMatch_Dev`, Windows Authentication**, theo `src/Wedding.WebApi/appsettings.Development.json`. SQL Server phải đang chạy; không đổi tên DB để né lỗi kết nối.
- API không xóa/tạo lại DB khi khởi động. Development có thể áp dụng migration; **demo seed đang tắt**. SQLite chỉ là lựa chọn Development tường minh cho test, không phải DB của người dùng đang chạy local hoặc DB production.
- Không dùng `EnsureDeleted`, reset/reseed, hoặc mật khẩu demo để xử lý lỗi đăng nhập trên DB có dữ liệu thật.

Mở hai terminal tại thư mục repo:

```powershell
# Terminal 1: backend (build lại code mới; Ctrl+C để dừng)
dotnet run --project .\src\Wedding.WebApi --launch-profile http
```

```powershell
# Terminal 2: frontend; npm.cmd tránh vướng npm.ps1/ExecutionPolicy
cd .\client
npm.cmd run dev
```

Frontend: `http://localhost:5173`; API: `http://localhost:5008`; SQL readiness: `http://localhost:5008/health/ready`.

### 4. Deploy và kế hoạch nâng cấp

Production bắt buộc SQL Server, không tự seed/migrate lúc API chạy. Cấu hình connection string và JWT secret trong private config/biến môi trường, không commit secret:

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<production SQL Server connection string>
Database__SeedDemoData=false
Database__ApplyMigrations=false
```

Áp dụng SQL migration đã review trước khi phát hành, giữ uploads ngoài package, backup và thử restore. `tools/Test-ReadyToDeploy.ps1` kiểm tra package trên DB QA dùng một lần; không thay thế kiểm tra hosting thật. Gate không tự bật/dừng dịch vụ SQL và không đổi cấu hình bộ nhớ chung của instance.

Theo dõi yêu cầu Master Prompt, hiện trạng và điều kiện chuyển phase tại [checklist nâng cấp](docs/master-upgrade-checklist.md). Không đánh dấu chức năng hoàn thành chỉ vì có giao diện hoặc entity.
