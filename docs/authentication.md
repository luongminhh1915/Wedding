# Đăng ký, đăng nhập và phân hệ tài khoản
**Current hosted29/10 — all-role Admin provisioning DEPLOYED/integration verified:** canonical active-SuperAdmin POST `/api/users` supports all6roles with explicit SuperAdmin confirmation. OwnedSQL/API30checks and browserCustomer/Vendor/SuperAdminPASS; full499FE/1205BE0skip. Host package extractionSuccess,readiness200,newroute anonymous401,84/84liveFEassets match,publicproviders unchanged. Email-OTP UI hidden; password/Google/Facebook/recovery preserved. No schema migration/private-config change/real-account test; authenticated hosted provisioning, full social OAuth and owner reset consumption remain UNVERIFIED. [Rollout report](reports/admin-user-provisioning-and-navigation-2026-10-29.md). Earlier local-only/provisioning and SMTP checkpoints below retain historical evidence.

**Current implementation29/10 — Admin cấp đủ6vai trò; đang kiểm chứng trước phát hành:** SuperAdmin đang hoạt động có thể tạo Customer, VendorOwner, Moderator, Finance, CustomerCare và SuperAdmin mới qua `POST /api/users`. User đã xác nhận quyền tạo thêm SuperAdmin; [decision](decisions/2026-10-29-admin-create-all-user-roles.md). Frontend499/499 và backend1205/1205 PASS gồm SQL Server; root đã kiểm30API checks cho đủ6vai trò và browser với Customer/NCC/SuperAdmin hư cấu trên fixture riêng. Những kết quả này thuộc bản local mới, **chưa cập nhật host** tại checkpoint này. UI đăng nhập đã ẩn tùy chọn mã OTP email; API OTP và SMTP/khôi phục vẫn được giữ. Nút khám phá nổi bật rời navbar desktop/mobile; form khám phá dịch vụ nằm dưới phần giới thiệu trang chủ. [Báo cáo phạm vi và kiểm chứng](reports/admin-user-provisioning-and-navigation-2026-10-29.md). Các checkpoint theo ngày bên dưới giữ lịch sử và không thay thế trạng thái mới này.

**Current Gmail recovery29/10 01:00VN — DELIVERY VERIFIED:** replacement owner-entered App Password authenticated235, fresh host config rebase changed only SMTP password, exact private upload bytes verified and pool recycled. Owner authorized one retry from actual login UI and confirmed receiving the recovery email. HTTPS home/readiness/reset/providers200; Google/Facebook preserved, private-config public path404. Password/token/link not read or changed by agent; actual owner password reset/one-time consumption E2E remains UNVERIFIED. No code release, migration or direct SQL in this corrective rollout. Earlier credential rejection/configuration-only checkpoints below are historical. [Corrective report](reports/gmail-recovery-corrected-2026-10-29.md).

**Delivery diagnosis29/10 00:18VN — recovery BLOCKED:** owner reports no message. Exact-host read-only account/request aggregates confirmed eligible active Admin/request exists; one mail-free, TLS-validated explicit Gmail AUTH LOGIN probe returned535/5.7.8 (credentials rejected). No further message, SQL write, password change or host config change. Current provider flags mean keys are configured; they do not establish working recovery. Owner must verify the App Password belongs to the intended Gmail and enter a valid replacement privately; never send secrets to chat or repeatedly retry rejected credentials. [Diagnosis](reports/gmail-delivery-diagnosis-2026-10-29.md). Earlier configuration-success checkpoints remain valid only within their stated limits.

**Gmail host update29/10/2026 00:06VN — configuration APPLIED/VERIFIED:** owner supplied App Password through hidden CLI; fresh remote private JSON was backed up, only SMTP/reset-origin merged and uploaded outside web root, exact remote bytes verified, existing IIS pool recycled. HTTPS home/readiness/reset/providers200; `passwordRecovery` and `emailOtp` now true; Google/Facebook configuration preserved. One owner-authorized real recovery request acknowledged in browser; inbox delivery/full reset E2E remain UNVERIFIED. No user password changed, no application release/migration or direct SQL write. Existing normal recovery API may create/revoke its own reset-token rows. Earlier unconfigured/preparation statements below are historical. [Host continuation](reports/gmail-host-configuration-2026-10-28.md).

**Gmail recovery preparation28/10:** owner chose Gmail; new `tools/Set-StudyEmailConfiguration.ps1` prepares a separate private copy through the guarded provider writer, preserving SQL/JWT/social/uploads.24new helper checks +17social compatibility checks PASS, no real credentials/network/SMTP/deployment. Owner App Password still required; existing hosted recovery remains unconfigured until owner setup/private-config application/live delivery verification. [Setup report/command](reports/gmail-recovery-preparation-2026-10-28.md).

**Current hosted status28/10/2026:** schema20/exact gated social-login release deployed with explicit owner approval. Facebook requests only public_profile, no mandatory email/manual-link step. Google settings/existing accounts preserved. Enabled blue Facebook button, exact live assets, HTTPS/SQL readiness and negative-auth checks verified. Actual owner OAuth sign-in/link UNVERIFIED; Meta testing/public-review restrictions and SMTP delivery remain pending. Later dated checkpoints supersede earlier local/pending-release statements below. [Current report](reports/social-login-without-email-2026-10-27.md).

## Luồng theo actor

Mọi actor dùng chung `/login`; không cho người đăng nhập tự chọn quyền. Vai trò lấy từ DB và API trả về sau xác thực.

| Actor | Cách có tài khoản | Sau đăng nhập |
| --- | --- | --- |
| Customer | Đăng ký khách hàng, SuperAdmin cấp, hoặc Google/Facebook khi đã cấu hình; OTP email không còn hiển thị trên UI đăng nhập | Khách mới: 10 câu hỏi `/planner/setup`; đã thiết lập: `/customer/dashboard` hoặc trang khách hợp lệ đang xem |
| VendorOwner | Đăng ký đối tác hoặc SuperAdmin cấp kèm hồ sơ shop | `/vendor/dashboard` |
| Moderator | SuperAdmin cấp trong Người dùng & đối tác | `/admin/dashboard`, các tab kiểm duyệt được phép |
| Finance | SuperAdmin cấp trong Người dùng & đối tác | `/admin/dashboard`, các tab tài chính được phép |
| CustomerCare | SuperAdmin cấp trong Người dùng & đối tác | `/admin/dashboard`, các tab hỗ trợ được phép |
| SuperAdmin | Bootstrap tài khoản đầu tiên hoặc SuperAdmin đang hoạt động cấp thêm có xác nhận; không đăng ký công khai | `/admin/dashboard` |

Đăng ký công khai chỉ có Customer/NCC. Nhân sự nội bộ sử dụng mật khẩu, không được tạo tài khoản nhân sự qua Google/OTP. **Cập nhật29/10:** biểu mẫu quản trị hỗ trợ cả6vai trò, gồm SuperAdmin mới; luôn giữ phiên của Admin đang tạo, không đăng nhập thay người vừa được cấp. Endpoint nhân sự legacy `/api/users/staff` vẫn chỉ nhận3vai trò nhân sự như trước.

## Lỗi chuyển phân hệ đã sửa

Trước đây `state.from`/intent chỉ kiểm tra đường dẫn nội bộ, nên khách đã thiết lập có thể quay lại `/vendor/dashboard` được nhớ từ tài khoản NCC. Guard chỉ hiện lỗi 403 tại URL đó. Ngoài ra, interceptor 401 chỉ xóa localStorage, không xóa user/token trong Zustand đang hiển thị.

Hiện tại:

- Password, đăng ký, OTP và Google đều qua `/auth/continue`. Đích quay lại được lọc theo actor; chặn auth-loop, URL ngoài site, đường dẫn mã hóa/sai kiểu. Khách không thể nhận intent của NCC/admin, kể cả URL viết hoa hoặc encoded.
- Đi trực tiếp vào phân hệ khác quyền sẽ chuyển về không gian của actor hiện tại, không để kẹt tại 403 hoặc mở nhầm dữ liệu. API vẫn kiểm tra quyền, không chỉ dựa vào giao diện.
- Đăng xuất xóa token/user, hủy query, xóa cache, xóa intent cũ và dùng replace về trang đăng nhập. Giỏ riêng từng khách/bản nháp DB không bị xóa.
- Phiên khôi phục từ trình duyệt phải được `/api/auth/me` xác nhận trước khi mở dữ liệu actor. JSON hỏng, token lỗi/hết hạn hoặc role không hợp lệ không làm crash ứng dụng. Khi chưa xác minh được do mất mạng, có nút thử lại/đăng xuất, không đoán vai trò.
- Khi đổi tài khoản, cây giao diện được mount lại theo session revision để bỏ state tạm và đóng kết nối/chat cũ. Các tab cùng origin đồng bộ đăng xuất/đổi tài khoản qua storage event. `localhost`, IP và subdomain là các origin riêng, không dùng chung phiên.
- 401 chỉ kết thúc phiên nếu phản hồi thuộc token hiện tại; 401 từ lần đăng nhập sai hoặc request của tài khoản trước không xóa phiên mới. `/me` cũ cũng không ghi đè tài khoản mới. Mỗi JWT mới có `jti` riêng, kể cả đăng nhập lại cùng tài khoản trong cùng giây.
- Menu tài khoản/điều hướng di động đi theo actor. Finance/CSKH không gọi query bài chờ duyệt dành cho Moderator/SuperAdmin.

JWT là bearer token có thời hạn, chưa có refresh token hoặc danh sách thu hồi riêng cho từng lần logout. Logout kết thúc phiên trên trình duyệt; không khẳng định bản sao token ở thiết bị khác đã bị thu hồi. Server kiểm tra tài khoản hoạt động, role và `SecurityVersion` trong DB trên mỗi request. Đổi mật khẩu hoặc thay đổi trạng thái khóa/kích hoạt tăng phiên bản này, nên mở khóa không làm token cũ sống lại. Production phải dùng HTTPS, JWT secret riêng và không bật seed demo.

## Đổi mật khẩu và bảo vệ phiên

- Nút chìa khóa “Đổi mật khẩu” trên thanh tài khoản dùng được cho cả 6 actor, không mở lại biểu mẫu hồ sơ cưới hoặc thay đổi onboarding.
- `POST /api/auth/change-password` nhận `currentPassword`, `newPassword`, `confirmPassword`; không nhận ID/role để đổi thay người khác. Phải xác minh mật khẩu hiện tại, mật khẩu mới khác mật khẩu cũ và đáp ứng giới hạn 8 ký tự / 72 byte UTF-8.
- Cập nhật hash, tăng `SecurityVersion` và audit trong cùng transaction; audit/response không chứa mật khẩu hoặc hash. Thành công trả 204, không phát JWT mới; giao diện xóa phiên/cache và yêu cầu đăng nhập lại.
- Request đổi mật khẩu được gắn với token lúc mở hộp thoại. Đổi tài khoản ở tab khác không được làm request chuyển sang actor mới; phản hồi cũ không đăng xuất phiên mới.
- JWT thiếu, sai hoặc trùng claim `security_version` bị từ chối. Sau khi cập nhật bản này, người dùng có token phát hành trước đó cần đăng nhập lại; tài khoản và dữ liệu vẫn giữ nguyên.
- Migration `ChangePasswordSecurityVersion` chỉ thêm `Users.SecurityVersion` mặc định 0. Áp dụng cùng các migration đã review trước khi chạy bản API mới; không reset schema hoặc dữ liệu.
- Chat kiểm tra quyền sở hữu phòng và phiên hiện tại cho mỗi lần tham gia/rời/gửi; chỉ người tham gia được gửi, SuperAdmin/Moderator giữ quyền đọc hỗ trợ như HTTP. Broadcast dùng nhóm theo lead + người dùng + role + phiên bản hiện tại, không gửi vào phòng chung chỉ biết GUID. Socket/token cũ không nhận broadcast mới sau khi phiên bản/role/quyền thay đổi; kết nối đóng khi JWT hết hạn.

## Khôi phục mật khẩu qua email

- Trên trang đăng nhập, “Quên mật khẩu?” luôn hiển thị và mở form nhập email. Chỉ nút gửi liên kết bị vô hiệu hóa cho tới khi API báo `passwordRecovery=true`; nếu email chưa cấu hình, form giải thích cách nhận hỗ trợ. Trang đặt lại `/reset-password` vẫn hiển thị lỗi an toàn nếu link thiếu/hết hạn.
- `POST /api/auth/password-reset/request` trả cùng thông điệp và HTTP 202 cho email sai định dạng, không tồn tại, tài khoản khóa, bị giới hạn, SMTP lỗi hoặc yêu cầu hợp lệ để tránh tiết lộ tài khoản. Mỗi IP chịu rate limit auth hiện có; mỗi tài khoản hoạt động tối đa một yêu cầu/phút và năm yêu cầu/giờ.
- Chỉ gửi khi SMTP và `Auth:PasswordResetBaseUrl` hợp lệ. URL phải là origin HTTPS ở môi trường deploy (localhost được phép HTTP lúc dev), không gồm path/query/fragment. Email mở `/reset-password#token=...`; token không nằm trong query HTTP hay access log thông thường.
- Token ngẫu nhiên 256-bit chỉ lưu SHA-256 trong `PasswordResetTokens`, dùng một lần và hết hạn sau 30 phút. Link cũ chỉ bị vô hiệu sau khi SMTP chấp nhận email chứa link mới; nếu gửi lỗi, token mới bị vô hiệu còn link cũ vẫn dùng được. API luôn trả thông điệp chung.
- `POST /api/auth/password-reset/confirm` kiểm tra link, tài khoản còn hoạt động, độ dài mật khẩu 8 ký tự / tối đa 72 byte UTF-8 và xác nhận trùng. Cập nhật BCrypt hash, tăng `SecurityVersion`, ghi audit và vô hiệu mọi reset link khác trong cùng transaction; mọi JWT cũ bị thu hồi và người dùng đăng nhập lại. Dùng được cả tài khoản chỉ đăng nhập bằng Google/OTP và nhân sự, nếu còn truy cập email tài khoản.
- Migration additive `20261022124728_PasswordRecoveryTokens` chỉ thêm bảng/token indexes/FK; rollback từ chối nếu bảng đã có token. Migration được tạo offline qua `DesignTimeDbContextFactory`, chưa áp dụng vào DB ứng dụng.

## Đăng nhập Google

- Trên trang đăng nhập, nút Google do Google Identity Services tải từ `https://accounts.google.com/gsi/client`; client ID chỉ hiển thị khi `Google:ClientId` được cấu hình. Frontend gửi ID token lên `POST /api/auth/google`; server kiểm tra chữ ký Google, issuer, audience/client ID, hạn dùng và `email_verified`.
- Google chỉ cấp phiên `Customer`. Tài khoản mới đi theo onboarding 10 câu; VendorOwner/nhân sự dùng mật khẩu. **Cập nhật28/10:** subject đã gắn trả về đúng tài khoản cũ; email trùng Customer chưa gắn subject tạo Customer độc lập với contact email NULL, không bắt liên kết thủ công và không chuyển lịch sử từ tài khoản khác. Email trùng Vendor/nhân sự vẫn dùng mật khẩu. Liên kết Google trong trang bảo mật là tùy chọn, chỉ nhận token đã xác minh có đúng email tài khoản hiện tại; không tự tháo/chuyển subject. [Decision](decisions/2026-10-27-social-login-without-contact-email.md).
- `GET /api/auth/google/link-status` và `POST /api/auth/google/link` chỉ dành cho Customer có phiên hợp lệ. UI chỉ hiện phần liên kết ở tài khoản Customer; request được gắn với token/phiên mở trang để việc đổi tài khoản ở tab khác không làm chuyển thao tác sang actor mới.
- Google OAuth, OTP và khôi phục email chỉ hoạt động khi provider tương ứng đã cấu hình. API trả trạng thái cấu hình; trạng thái đó không chứng minh email đã gửi được hoặc đăng nhập Google đã được chạy thật.
- Deployment cần đặt `Google:ClientId` cho Web application client, đăng ký origin chính thức trong Authorized JavaScript origins; local cần đăng ký đúng `http://localhost` và port đang dùng. Email recovery cần `Smtp:Host`, `Smtp:From`, thông tin SMTP phù hợp và `Auth:PasswordResetBaseUrl`. Các giá trị bí mật SMTP chỉ lưu trong private config/environment, không gửi qua chat hoặc commit.

Development hiện tại được kiểm tra theo cấu hình file: Google Client ID và SMTP host/from chưa có giá trị; Facebook cần App ID, App Secret và Graph API version. Vì vậy UI giữ nút Google/Facebook ở trạng thái chưa cấu hình; đăng nhập mạng xã hội và gửi email thật chưa thể nghiệm thu đến khi người triển khai tự cấu hình provider. Luồng recovery vẫn có nút hiển thị, nhưng không gửi yêu cầu cho tới khi SMTP và reset URL sẵn sàng.

## Đăng nhập Facebook

- Trang đăng nhập luôn hiển thị nút Facebook; chỉ hoạt động khi server đủ App ID, App Secret và Graph API version. **Cập nhật28/10:** trình duyệt chỉ xin public_profile rồi gửi access token cho API; không xin email hoặc lưu token vào local storage. Nút màu xanh #1877f2, chữ/icon trắng.
- POST /api/auth/facebook xác minh token ở server với Facebook, kiểm tra đúng App ID, còn hiệu lực và hồ sơ /me trả cùng subject. Chỉ Customer được đăng nhập/tạo mới; subject đã có dùng đúng tài khoản đó, subject mới tạo Customer trực tiếp dù thiếu contact email. Không yêu cầu email/manual linking hoặc nhập lịch sử tài khoản khác theo email.
- Facebook không tự liên kết theo email. Nếu email đã tồn tại, Customer đăng nhập phương thức cũ trước rồi vào **Tài khoản → Mật khẩu & bảo mật → Đăng nhập bằng Facebook**. API link chỉ chấp nhận email đúng tài khoản và subject chưa thuộc user khác. Vendor và nhân sự vẫn đăng nhập bằng mật khẩu.
- GET /api/auth/facebook/link-status và POST /api/auth/facebook/link yêu cầu Customer đã đăng nhập. Facebook access token và app secret không được ghi vào log hoặc cấu hình frontend.
- Cấu hình private server-side: Facebook:AppId, Facebook:AppSecret, Facebook:GraphApiVersion. Provider status chỉ công khai App ID/API version khi đủ cấu hình; App Secret không bao giờ trả về frontend. Nút chưa cấu hình vẫn hiện nhưng disabled để khách biết phương thức có sẵn mà chưa bật.

## Trạng thái xác minh 26/10/2026

- Google Customer sign-in/link và Facebook Customer sign-in/link đã có source FE/API/domain/schema. Nút Google/Facebook luôn có chỗ hiển thị; nếu thiếu cấu hình thì bị vô hiệu hóa và nêu trạng thái, lỗi tải provider có thể thử lại.
- Facebook migration `20261026083936_FacebookCustomerIdentity` chỉ thêm `Users.FacebookSubject` nullable và unique filtered index. Script nâng cấp offline đã xem; migration **chưa áp dụng** vào bất kỳ database ứng dụng/host nào.
- Frontend tests `426/426`, frontend build PASS, lint exit 0 với 13 warnings hiện hữu ngoài auth; backend auth tests `37 pass / 1 SQL-only skip`; API build PASS. Kiểm tra provider thật trên trình duyệt chưa chạy.
- Cấu hình Google/Facebook thật và lượt đăng nhập OAuth end-to-end chưa được xác minh; code hiện chưa được deploy kèm migration này. Không coi nút hoặc test mock provider là đăng nhập mạng xã hội đã hoạt động trên host.

## Google trên host — cập nhật 27/10/2026

- Bản release đang chạy tại `https://weddingmatch.website` đã được bổ sung public `Google__ClientId` vào environment settings của `/site1/web.config`. API trả đúng ID và trang `/login` render nút Google thật; SQL readiness vẫn HTTP200. Đây là thay đổi cấu hình trên binary cũ đã có Google, không deploy backend/Facebook hoặc áp migration.
- Google Cloud đang External/Testing và đã có một test user; chủ tài khoản phải tự chọn tài khoản/consent để nghiệm thu đăng nhập. Chưa xác minh tạo/đăng nhập Customer thật, liên kết email cũ, hoặc mở cho mọi người. Branding/public privacy còn thiếu; không đặt URL giả để publish.
- Giữ Google environment setting khi deploy lần sau hoặc chuyển có kiểm soát sang private config hiện hữu; script publish tạo lại web.config. Google Client Secret không được sử dụng bởi luồng ID-token hiện tại. Xem [báo cáo cấu hình live](reports/google-host-configuration-2026-10-27.md).

## Đăng ký và cấp tài khoản qua quản trị

- Frontend có label/autocomplete, nhập lại mật khẩu, hiện/ẩn, báo lỗi từng trường, chặn gửi hai lần. API thực sự chạy validators trong handler; không phụ thuộc vào pipeline validation toàn ứng dụng đang chưa được nối.
- Email được trim/lowercase; họ tên/liên hệ được trim theo domain. Không trim mật khẩu có khoảng trắng có chủ đích. Mật khẩu mới ít nhất 8 ký tự, tối đa 72 byte UTF-8 theo BCrypt, không nhận toàn khoảng trắng. Tài khoản cũ mật khẩu ngắn vẫn đăng nhập được, không tự đổi mật khẩu cũ.
- Tên tối đa 150 ký tự, email 150, điện thoại Việt Nam 10 chữ số bắt đầu bằng 0; thương hiệu NCC 200, tỉnh/thành 100. **Cập nhật21/10:** đăng ký NCC không chọn group/rate và server từ chối rate client tự gửi; chọn Category khi tạo dịch vụ để xem default/group từ server. Câu mô tả signup chọn3/8/10/12% của baseline17/10 đã superseded bởi [decision mới](decisions/2026-10-21-service-category-without-vendor-membership.md); legacy rate/snapshot không đổi.
- Tạo tài khoản/kiểm tra email trùng trong transaction serializable; có test SQL đồng thời chỉ tạo một email. Slug NCC có xử lý dấu tiếng Việt và tránh trùng. Public DTO không cho truyền role để nâng quyền.
- **Canonical29/10 — `POST /api/users`:** chỉ SuperAdmin đang hoạt động; body `fullName,email,phoneNumber,password,role`, role thuộc đúng `Customer|VendorOwner|Moderator|Finance|CustomerCare|SuperAdmin`. Handler đọc lại quyền/trạng thái Admin trong cùng transaction serializable với việc tạo User, shop nếu có và audit. Email đã tồn tại bị từ chối; không nâng quyền, thay mật khẩu hoặc ghi đè tài khoản cũ. Response là `UserManagementDto`, không phát token hay trả hash/mật khẩu.
- Với `VendorOwner`, thêm `brandName,city`: tạo đúng một shop, slug riêng và tỉnh/thành chuẩn; shop chưa xác minh và dùng category defaults hiện có. Không nhận group/rate tài chính từ biểu mẫu, không ghi NCC đã chấp thuận điều khoản, không tự duyệt dịch vụ/combo hay kích hoạt thỏa thuận hoa hồng riêng. Các snapshot/hợp đồng cũ không bị sửa.
- Với `SuperAdmin`, thêm `confirmSuperAdminCreation=true`; mật khẩu mới tối thiểu12ký tự và tối đa72byte UTF-8. UI yêu cầu checkbox cấp toàn quyền rồi hộp thoại xem lại tên/email/vai trò; Hủy không gửi request. API kiểm tra lại xác nhận và mật khẩu, không chỉ dựa vào UI. Năm vai trò còn lại giữ tối thiểu8ký tự/tối đa72byte theo quy tắc hiện có.
- Biểu mẫu gắn request với Admin/token/session revision khi mở, chặn gửi trùng và phản hồi cũ khi đổi phiên. Thành công cập nhật danh sách người dùng, xóa mật khẩu khỏi bản nháp và giữ nguyên phiên Admin. Lỗi giữ thông tin để sửa/thử lại; hệ thống chưa tự gửi mật khẩu ban đầu qua email.
- `POST /api/users/staff`: chỉ SuperAdmin đang hoạt động, body `fullName,email,phoneNumber,password,role`; role chỉ `Moderator|Finance|CustomerCare`. API trả `UserManagementDto`, không trả token/hash/mật khẩu. Có audit, không log mật khẩu. Nhân sự được bàn giao mật khẩu ban đầu qua kênh riêng an toàn; chưa tự gửi email.
- Khóa/kích hoạt: Moderator không quản lý tài khoản nhân sự; SuperAdmin không bị khóa từ thao tác này. Backend kiểm tra lại actor trong DB, không chỉ ẩn nút trên UI.

**UI hiện tại29/10:** đăng nhập bằng mật khẩu, Google/Facebook và “Quên mật khẩu?” được giữ; mục gửi mã OTP email được ẩn khỏi UI, API/provider flag vẫn tồn tại. Gmail recovery đã được chủ tài khoản xác nhận nhận email ở checkpoint đầu tài liệu; hoàn tất đổi mật khẩu từ liên kết và OAuth thực vẫn phải phân biệt với kiểm thử local/mock. Lượt cấp tài khoản29/10 chỉ dùng fixture hư cấu, không tạo tài khoản thật hoặc gửi email thật để thử.

## Nghiệm thu local 17/10/2026

- 63/63 test frontend; 134/134 backend với `WEDDING_TEST_SQLSERVER=1`, không skip. Frontend/API build thành công; lint 0 lỗi, 10 cảnh báo cũ ngoài phần auth.
- Kiểm tra trình duyệt trên origin QA riêng `auth-qa.localhost:5176`: vào URL NCC khi chưa đăng nhập; đăng nhập NCC đúng portal; logout → mở lại URL NCC → đăng nhập Customer đã thiết lập về dashboard khách; Customer mở trực tiếp URL NCC được chuyển lại dashboard khách. Tab thứ hai khôi phục phiên qua `/me`, nhận logout và chuyển actor sang SuperAdmin mà không giữ nội dung khách.
- SuperAdmin thấy biểu mẫu cấp nhân sự; form trống bị chặn/báo lỗi phía client. Tạo 3 nhân sự hư cấu qua API QA và xác nhận phiên SuperAdmin không đổi. Không xác nhận hợp đồng, thay mật khẩu, gửi email hay thực hiện thanh toán.
- Form đăng ký Customer/NCC hiển thị đúng các trường và lỗi bắt buộc, không có quyền nội bộ để tự chọn. Khách QA mới đăng ký qua API đăng nhập trên trình duyệt được chuyển thẳng tới bước 1/10; không hoàn tất wizard hoặc tạo kế hoạch trong lượt này. Finance đăng nhập thấy đúng các tab tài chính, không thấy quản lý người dùng/cấp nhân sự hoặc kiểm duyệt; tổng quan tải thành công. Các tab trình duyệt QA tạm đã đăng xuất và đóng, không thay phiên ở origin `localhost` của người dùng.
- `tools/Verify-AuthQa.ps1` mặc định chỉ kiểm tra provider/read-only. `-ApplyFixtures` chỉ append 5 tài khoản hư cấu có tiền tố `[QA AUTH]` vào DB duy nhất `WeddingPlanUi_0161333`, yêu cầu API 5009 đúng binary auth QA. Utility không tạo SuperAdmin, không reset/ghi đè tài khoản, kiểm tra đủ 6 role qua login + `/me` và API khác quyền trả 403. Không log thông tin đăng nhập/token và không được dùng trong startup/deploy.

Các kết quả ở mục nghiệm thu trên là vòng auth trước phần đổi mật khẩu. Bản mới có migration cộng thêm `ChangePasswordSecurityVersion`; cần cập nhật backend, frontend và schema cùng nhau. Kiểm thử mới dùng DB hư cấu tên riêng, không reset/seed lại DB người dùng. Xem thêm [kiểm chứng gói deploy](deployment-verification.md).

## Hosted Facebook/Google checkpoint — 27/10/2026

Historical hosted status27/10; the28/10 social-contact and29/10 recovery/current-implementation checkpoints supersede it where stated. With explicit owner approval, additive FacebookCustomerIdentity migration19 and the matching verified release were deployed to weddingmatch.website. Google and Facebook settings now persist in private server configuration; SQL/JWT/other settings and startup migration/demo-seed=false were preserved. Facebook App Secret stays server-only.

Live HTTPS readiness/providers/homepage200, enabled Facebook button plus Google GIS and invalid synthetic provider tokens400JSON are verified. Full local gate426 frontend/1180 backend tests passed,0skip; existing13lint/4analyzer warnings remain. Actual OAuth consent/login/link/collision is UNVERIFIED: IAB Facebook attempt did not surface a popup or completed consent; owner should test in Chrome with an app-role/test account. Meta public review/business verification/privacy/deletion handling remain incomplete. SMTP email OTP/password recovery are still unconfigured. Do not reapply migration or claim all-customer sign-in from provider status alone. Exact release/backup/restore/host evidence: [Facebook report](reports/facebook-setup-progress-2026-10-27.md).

## Hosted update28/10 — contact email optional for social Customers, deployed

User requested one-click social sign-in without Facebook email permission or mandatory email-account linking. New source requests only public_profile/id/name, identifies by verified provider subject and supports Customer accounts without contact email. Existing provider-bound User IDs remain stable; an unbound Google email collision creates an independent Customer instead of transferring another account's history. Normal email/password registration remains unchanged. Provider-owned first-use selection/consent and server token verification still apply.

Migration20 makes contact email optional only for provider Customers, preserves unique non-null emails and rejects missing-email staff in SQL; no fake email/SMTP destination/claim. Notifications remain InApp. Historical426FE/1183BE/0skip and isolated SQL42PASS cover the unchanged exact release. Explicit approval received28/10; fresh backup, one scoped real schema20 apply, successful extraction to `/site1/`, maintenance removal and live verification completed. Google/Facebook/private SQL/JWT settings, original17users/non-history row counts/identity-field aggregates preserved. Homepage/readiness/providers200, exact CSS/JS hashes, enabled blue Facebook button, invalid synthetic tokens400JSON and anonymous link-status401 passed. No positive real OAuth session claimed. See [decision](decisions/2026-10-27-social-login-without-contact-email.md) and [report](reports/social-login-without-email-2026-10-27.md); do not repeat older pending migration/deployment steps.
