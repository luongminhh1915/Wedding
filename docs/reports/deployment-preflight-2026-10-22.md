# Wedding Match — deployment preflight 22/10/2026

## Kết quả

**Local release gates đã qua; chưa triển khai public.** Người dùng yêu cầu hỗ trợ host và xác nhận hosting SmarterASP Advance hiện **chưa đăng ký/thanh toán**. Vì chưa có hosting account hoặc DB đích để cấu hình và kiểm chứng, không upload, không sửa DNS, không nhập secrets, không thanh toán và không chạy migration trên DB ứng dụng/production.

Người dùng trước đó báo đã trả tiền domain `weddingmatch.website`. Đây là thông tin do người dùng cung cấp; trạng thái kích hoạt, chủ sở hữu và DNS chưa được xác minh trực tiếp.

## Git và bảo toàn trạng thái

- Branch: `weddy-journey`
- HEAD: `3596ae832faeac95fb15cc44bc0e8e62351c788b`
- Worktree đang có nhiều file tracked đã sửa và nhiều file untracked từ trước; không staged. Giữ nguyên toàn bộ, không quy các thay đổi đó cho preflight này.
- `git diff --check`: không phát hiện whitespace error; Git phát cảnh báo line-ending LF→CRLF ở các file dirty hiện có.
- Không có code source/runtime nào được sửa trong lượt preflight này; báo cáo/checkpoint/checklist được cập nhật để ghi trạng thái deploy.

## Lệnh và bằng chứng

Lệnh chạy ngày 22/10/2026, kết thúc khoảng 23:27 giờ Việt Nam:

```powershell
pwsh -NoProfile -File tools/Test-ReadyToDeploy.ps1
```

Kết quả: exit code `0`, `LOCAL_GATES_PASSED`.

- Backend full suite: **1,141 passed, 0 failed, 0 skipped**; TRX tại `artifacts/readiness/20261022-162346-af4a1071/test-results/backend.trx`.
- Frontend tests, lint và Vite/TypeScript production build: PASS. Lint còn warnings đã tồn tại; build có cảnh báo output folder nằm ngoài project root theo cấu hình artifact.
- API Release publish và same-origin site package: PASS, target `net10.0`.
- Package guardrails và packaged Production smoke: PASS. Smoke bao gồm API/SPA/health, auth/account security/role isolation/chat authorization, persistent process restart trong môi trường smoke, private-image handling và SQL migration apply/replay.
- SQL trong smoke chỉ dùng DB local unique do test sở hữu (`WeddingPlanTest_Study_<GUID>`), được tạo riêng rồi cleanup. Không kết nối, migrate, seed, reset hoặc ghi vào DB ứng dụng/production.
- Không có browser E2E trên host thật, provider integrations, backup/restore từ dữ liệu người dùng hoặc kiểm thử tải concurrent. Đây không phải xác nhận “100 người dùng đồng thời”.

## Artifact nội bộ

Release root:

```text
artifacts/readiness/20261022-162346-af4a1071/deploy/wedding-match-20261022-162628-93b3f7e9
```

- `WeddingMatch-site.zip`: 52,120,058 bytes; SHA-256 `135F7EFFEE649C69539158F1E7672BD81308DB1F415B345B6335FDC09FD94AA2`.
- `sql/migrate.sql`: SHA-256 `365A11EC4C186891440C923FC82E04B1130A9C37535C380FAEECF4B86AB986C9`.
- `release.json` ghi target `net10.0`, `IncludesUserData=false`, `IncludesProductionSecrets=false`, `PublishedRemotely=false`; `ready.json` báo `LOCAL_GATES_PASSED` và `PublishedRemotely=false`.
- Đây là artifact của source fingerprint trong `ready.json`, không phải bản đã tải lên host. Nếu source thay đổi trước deployment, phải chạy lại gate và đóng gói lại; không upload artifact cũ.

## Giới hạn và rủi ro còn lại

- Gói release không chứa user data hay production credentials. Chưa biết DB hiện tại trên máy người dùng chứa bao nhiêu tài khoản/giao dịch/dữ liệu cần chuyển. **Không tạo DB trống thay thế hoặc chạy migration trước khi inventory/backup và chọn phương án giữ dữ liệu.**
- `sql/migrate.sql` chỉ được sinh offline và kiểm tra trong packaged smoke trên DB test; chưa áp dụng vào host. Việc migrate DB có dữ liệu cần xác nhận đúng target, backup có thể restore, tương thích schema và quyền SQL trước khi chạy.
- Chưa có cấu hình riêng cho host: SQL connection secret/user permissions, JWT signing secret, SMTP/reset URL, Google origin/client, storage persistent uploads, provider URL/physical root, HTTPS certificate, domain/DNS và logs/monitoring.
- `ready.json` ghi working set sau account/WebSocket smoke khoảng **205.4 MB**. Đây là số đo smoke cục bộ, không phải benchmark capacity; cần quan sát tài nguyên sau khi deploy và test tải phù hợp trước khi mời người dùng thật.
- Nhà cung cấp hiện công bố ASP.NET Core **.NET 10.x** và SQL Server (bao gồm các phiên bản được liệt kê trên trang hosting); đây chỉ là thông tin sản phẩm, chưa được xác minh trên account/site cụ thể. Xem [SmarterASP ASP.NET Core hosting](https://www.smarterasp.net/asp.net_core_hosting).

## Bước tiếp theo

1. Người dùng tự đăng ký/kích hoạt hosting đã chọn và thanh toán trực tiếp; không gửi mật khẩu, OTP, token hoặc connection string trong chat.
2. Xác minh site/runtime/physical root/temporary URL và quyền database trong control panel. Xác minh domain riêng đã hoạt động, chưa đổi nameserver/DNS theo hướng dẫn chung khi chưa biết target records chính xác.
3. Kiểm kê DB local cần mang lên host và lập phương án backup + restore/migration có thể khôi phục; tuyệt đối không dùng DB trống nếu sẽ làm mất dữ liệu người dùng.
4. Tạo cấu hình secrets ở host bằng cơ chế private của provider; kiểm tra HTTPS trước khi mở đăng ký/login công khai.
5. Chạy lại release gate nếu source thay đổi, sau đó triển khai có kiểm soát, migrate đúng DB đã backup và chạy smoke/restore verification trên host.

**Trạng thái cuối:** package ready locally; public hosting, DB migration, domain/DNS/HTTPS và user-data transfer đều **NOT DONE / UNVERIFIED**. Không commit, push, reset hoặc clean.
