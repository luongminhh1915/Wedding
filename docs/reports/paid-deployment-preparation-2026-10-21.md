# Chuẩn bị triển khai trả phí — 21/10/2026

## Điểm dừng mới20:03VN — user tự hoàn tất đăng ký hosting

User đã báo thanh toán `weddingmatch.website`, rồi trả lời **“ok”** sau bảng giá Advance25,50USD/3tháng. [Decision mới nhất](../decisions/2026-10-21-paid-deployment.md) xác nhận tiếp phương án; **không tiếp tục hỏi lại giá25,50 chỉ vì lịch sử pending bên dưới**.

- Domain paid là **user-reported**, activation/ownership/DNS chưa độc lập verified. Không đọc receipt hoặc ghi token URL/PII vào tài liệu.
- Browser xác minh lại Advance .NET, radio3Months, tổng25,50USD/setup0; không kỳ36tháng/add-on. Continue Registration mở `member10.smarterasp.net/register1?plantype=449&term=quarterly`, form **Create Your Account** còn trống.
- Dừng trước nhập Username/Email/password, chọn Google/GitHub, Sign up đồng ý Terms và thanh toán. User tự thao tác; không tạo account, solve CAPTCHA, lưu credentials/thẻ hoặc thanh toán hộ. [Ảnh form trống](../../artifacts/deploy-preflight-20261021/advance-quarterly-registration.png) và tab được giữ để bàn giao. Không đọc tiếp form sau khi user bắt đầu nhập secrets.
- Hosting mới **APPROVED/NOT PURCHASED**, chưa có tài khoản/đích host/SQL được xác minh. Tiếp đúng điểm: user hoàn thành account/payment và báo đã mua; rồi xác minh runtime/private physical paths/SQL target trống hay dữ liệu cũ+backup/temp host/canonicalWebsite/HTTPS trước upload/remoteSQL. Không cần mua thêm domain/hostingP.A/IP/SSL trả phí; tổng tăng đáng kể phải báo lại.
- Chỉ5docs và screenshot ignored thay đổi. Không sửa source/config/DB, startup/rebuild/fullsuite; FE348/BE1073/SQL/package gates bên dưới vẫn là **lịch sử**, không host proof. Source/private uploads/JWT/financial snapshots giữ nguyên. CHƯA PUBLIC, chưa DNS/upload/remoteSQL/data transfer.

## Lịch sử — user đổi sang weddingmatch.website trước khi báo thanh toán

User trả lời **“lấy cái weddingmatch.website đi”**: xác nhận thay domain, **chưa duyệt hosting25,50USD/3tháng**. [Decision cập nhật](../decisions/2026-10-21-paid-deployment.md). Phần .vn bên dưới là lịch sử, không còn mục tiêu mua/deploy.

- Browser P.A kết quả `weddingmatch.website` hiện “CÓ THỂ MUA TÊN MIỀN NÀY”,49.000đ/năm tại lúc đọc. Đã chọn vào giỏ và bỏ chọn các add-on DNSSEC/Email Pro; không thay domain/DNSSEC thực đã mua nào.
- Quan sát checkout mới nhất chỉ **1mục weddingmatch.website,01năm,49.000đ,VAT3.920đ,tổng52.920đ**; .vn đã không còn. Không tiếp tục bấm remove cũ khi state thay đổi; không xóa giỏ toàn bộ, tạo order/account hoặc thanh toán. Hai tab tiếp tục giữ cho user tự thao tác; không nhập/gửi PII hoặc secrets.
- Agent phụ read-only đối chiếu [P.A bảng giá](https://www.pavietnam.vn/vn/bang-gia-ten-mien.html): gia hạn.website **746.000đ/năm chưa VAT**, không dùng49.000đ làm giá gia hạn. Điều kiện ưu đãi chi tiết chưa xác minh riêng; bước checkout hiện cho01năm. Tình trạng còn trống/giá là snapshot, không giữ chỗ.
- [Ảnh tổng checkout mới](../../artifacts/deploy-preflight-20261021/website-cart-paid.png), không PII. Chưa bấm “Thanh toán đơn hàng”; domain chưa thuộc user, chưa public.
- `rg` trong src/client/src/tools không thấy hardcode `weddingmatch.vn` (loại bin/obj/App_Data). Chỉ cập nhật5docs đang có; không chỉnh source/config hoặc rebuild, không gọi SQL/API. Gate348FE/1073BE/package trước đây vẫn là bằng chứng lịch sử, không là test mới của lượt đổi tên miền.
- Bước tiếp: user tự hoàn tất mua tên mới khi chấp nhận giá cuối; xác nhận riêng hosting25,50USD/3tháng. Sau khi có host/account mới kiểm runtime/targetDB/backup/private paths/temp+canonical allowlist/DNS/HTTPS, không migrate dữ liệu thật để thử.

## Kết quả và điểm dừng lịch sử — trước khi user báo thanh toán/duyệt hosting

**CHƯA PUBLIC. Chưa mua domain/hosting, tạo tài khoản, chấp nhận điều khoản, thanh toán, đổi DNS, upload hoặc ghi SQL từ xa.** User đã đồng ý phương án trả phí theo báo giá cũ, nhưng checkout hosting cao hơn nên đã hỏi duyệt lại. Không tự tăng ngân sách hoặc mua kỳ 36 tháng.

[Decision có nguồn/phạm vi](../decisions/2026-10-21-paid-deployment.md). Giữ source và tất cả thay đổi/untracked trước đó; không mở Phase5–7 hay sửa dữ liệu tài chính. Root cập nhật tài liệu chung, agent phụ chỉ audit offline/read-only.

## Kiểm chứng browser thực, không phải đơn hàng

1. P.A search báo `weddingmatch.vn` còn đăng ký được, 450.000đ/năm tại thời điểm tra cứu. Đã thêm đúng domain và bỏ chọn DNSSEC/Email Pro upsell ở bước trước checkout; không thay cấu hình domain thực nào.
2. Checkout tự thêm bốn domain phụ và hosting P.A, ban đầu 609.400đ gồm VAT. Sau thao tác loại ưu đãi, đã quan sát lại giỏ **1 mục: weddingmatch.vn, 01 năm, 450.000đ, VAT 0, thành tiền 450.000đ**. Không thanh toán/đăng ký chủ thể. Không nói kết quả search là giữ chỗ tên miền.
3. [SmarterASP signup Advance](https://www.smarterasp.net/signup?plantype=449) mặc định 36 tháng/178,20 USD. Chuyển radio về **3 tháng, 8,50 USD/tháng, tổng 25,50 USD**, setup 0. Giá 4,95 USD chỉ với kỳ 36 tháng. **14,85 USD/3 tháng trong preflight là ước tính sai kỳ hạn**, không còn dùng.
4. Đã báo sai lệch và gửi câu hỏi duyệt 25,50 USD/3 tháng, chờ trả lời. Chưa bấm Continue Registration, nhập email/credentials, ký điều khoản hoặc trả tiền. Không chọn add-on, mua .vn qua generic $12 Domains hoặc đổi host để né phê duyệt.
5. Hai tab P.A checkout và SmarterASP signup được giữ để user tự hoàn thành khi đã đồng ý giá đúng. Thông tin cá nhân, mật khẩu/OTP/CAPTCHA/điều khoản/thanh toán do user thao tác trực tiếp; không gửi vào chat.

Ảnh browser thật trong ignored artifacts, không chứa thông tin cá nhân:

- [Giỏ P.A sau bỏ upsell](../../artifacts/deploy-preflight-20261021/domain-cart-paid.png).
- [Advance đúng kỳ 3 tháng](../../artifacts/deploy-preflight-20261021/advance-three-months-checkout.png).

## Recheck local package — không chạy lại toàn bộ gate

Source deploy fingerprint và artifact vẫn khớp gate trước đó:

- Source SHA256 `86C5401571FE81AFDE035EF2B3A7C1CB782D432B2335B854B28CBF8B4395835A`.
- Release root `artifacts/readiness/20261021-112442-19349708/deploy/wedding-match-20261021-112642-d49c4be6`.
- ZIP SHA256 `5F867CCEE46F860C58BAEE8D8448344DFEC904E7756E79F991E3CFA71BB0742A`.
- SQL SHA256 `5625A627EED9D2F04B1E24877D3FC3E9A7DCF2B11853284C872B3688ED8B5CE4`.

Lệnh read-only đã chạy lại, exit 0:

```powershell
pwsh -NoProfile -File tools/Verify-StudyPackage.ps1 -ReleaseRoot artifacts/readiness/20261021-112442-19349708/deploy/wedding-match-20261021-112642-d49c4be6
```

231 entries, net10/SQL Server, không userdata/production secrets, `PublishedRemotely=false`. **Không chạy lại FE/BE/SQL suite** trong lượt chuẩn bị tài khoản này vì không sửa source. 348 FE/1073 BE actual SQL/0 skip và 13 FE/4 xUnit warnings là kết quả **18:27:52 VN trước đó**, không phải test mới; phạm vi và lệnh trong [preflight](deployment-preflight-2026-10-21.md).

## Checklist trước khi triển khai lên SmarterASP

- User đã duyệt phương án25,50USD/3tháng sau bảng chi phí; chờ user tự hoàn tất account/payment hosting. Domain đã báo thanh toán, activation/DNS chưa kiểm. Xác minh tổng cuối, không thêm sản phẩm/kỳ dài hơn.
- Xác minh .NET10/IIS runtime thực. Package hiện framework-dependent, không tự giả định server hỗ trợ chỉ vì có quảng cáo.
- Lấy physical document root và nơi ghi private config/uploads thực. Hướng dẫn cũ minh họa Monster `/wwwroot` + `../private`; **không copy nguyên đường dẫn đó sang SmarterASP**. Có thể dùng `WEDDING_PRIVATE_CONFIG` và upload roots tuyệt đối phù hợp host, không đưa secrets vào ZIP/public root.
- Giữ SQL Encrypt=true và xác minh cert/hostname. `-TrustHostedSqlCertificate` trong guide là opt-in cho trường hợp Monster cũ, **không mặc định dùng cho SmarterASP**.
- Xác định temp host và canonical **`weddingmatch.website`** theo trả lời mới (và www nếu thực dùng); `.vn` là mục tiêu đã superseded. Allowlist chính xác AllowedHosts/CORS trong cấu hình riêng, không dùng `*`. Giữ JWT signing key qua restart/redeploy/chuyển domain.
- Xác định DB mới hay dữ liệu local cần giữ. Review target/backup/schema trước remote SQL; không dùng main DB để thử migration. Production ApplyMigrations=false, SeedDemoData=false; tạo admin qua bootstrap interactive, không seed demo/default password public.
- SQL artifact có UTF-16LE BOM, `GO`, `:ON ERROR EXIT` và session SET. Chưa xác minh importer SmarterASP hỗ trợ nguyên các guard này; không bỏ guard để ép import. Dùng cách import được kiểm chứng trên đúng target, dừng khi lỗi.
- Upload verified ZIP, giữ private config/uploads khi redeploy; gắn domain và HTTPS hợp lệ trước public đăng nhập. Kiểm restart không mất dữ liệu, upload/private-file access, auth/role/SignalR, backup và restore trên DB riêng.
- Không tự bật CDN/proxy hay trust forwarded headers để chữa HTTPS/redirect; kiến trúc hiện tại giả định IIS scheme đúng. Nếu hạ tầng thực có proxy, cần xác minh trust boundary/config riêng.
- Realhost, DNS/HTTPS, provider delivery, remote SQL permissions, backup/restore và load100 **UNVERIFIED**. Không suy RAM 202,1MB local thành bảo đảm 100 concurrent hoặc đủ app pool 256MB.

## Git/bảo toàn

Baseline đầu lượt: branch `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`, 101 tracked modified/87 expanded untracked/0 staged/1 worktree; diff --check exit0 (LF→CRLF warnings). Không gán toàn bộ dirty state cho lượt này. Chỉ cập nhật tài liệu và ảnh ignored; không commit/push/reset/clean, DB/API startup, code/config hệ thống hoặc migration.

Kiểm lại **19:30:51 VN**: branch/HEAD giữ nguyên, **101 tracked modified/89 expanded untracked/0 staged/1 worktree**, diff --check exit0 (61 notices LF→CRLF, không whitespace error). Hai file mới là decision trả phí và báo cáo này; bốn tài liệu đã có được cập nhật: handoff, master checklist, Copilot start prompt và preflight lịch sử. Kiểm 6 tài liệu/48 relative links: **0 missing**. Tất cả agent phụ đã hoàn thành read-only audit; không build/test/API/SQL job mới còn chạy. Hai tab mua hàng giữ cho user, không tự chuyển tiếp qua bước đăng ký hoặc thanh toán trong khi giá mới chưa được duyệt.
