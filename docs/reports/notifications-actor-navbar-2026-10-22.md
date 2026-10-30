# Thông báo căn giữa và navbar đúng actor

Ngày22/10/2026. Scope mới sau bộ docs bàn giao: [user decision](../decisions/2026-10-22-actor-navigation.md). Chỉ frontend + tests + cập nhật handoff; không tiếp engine Phase5–7 hoặc deploy.

## Đã sửa

1. NotificationsPage bỏ phụ thuộc utility `container` vốn chỉ giới hạn width, không căn giữa. CSS riêng dùng `width:min(1050px,calc(100% - 40px))`, `margin-inline:auto`, mobile gutter16px; pagination có padding trong panel. Heading/nội dung vẫn căn trái trong khung giữa, không thay global layout.
2. `actorNavigationModel` là cùng nguồn cho navbar desktop/mobile. Customer/anonymous giữ menu hiện có. Vendor/staff bỏ public Browse/Combo/Cẩm nang/Planner dành cho Customer.
3. Active state xét cả `pathname` và tab hợp lệ, giữ legacy Vendor listing-filter bookmarks. Invalid/unauthorized tab theo fallback dashboard hiện có, không tự cấp quyền. Unknown role không lộ workspace đặc quyền.
4. Common account/bell/logout và customer-only cart giữ nguyên. Navbar chỉ là primary shortcuts; đầy đủ chức năng vẫn ở role-filtered dashboard. Footer ngoài scope, không sửa.

| Actor | Primary shortcuts desktop; mobile thêm Cá nhân |
|---|---|
| VendorOwner | Tổng quan, Dịch vụ của tôi, Combo của tôi, Yêu cầu tư vấn |
| Moderator | Tổng quan, Kiểm duyệt, Đánh giá, Quản lý dịch vụ |
| Finance | Tổng quan, Tài chính, Bảng kê tháng, Tranh chấp |
| CustomerCare | Tổng quan, Tranh chấp, Hỗ trợ khẩn cấp |
| SuperAdmin | Tổng quan, Kiểm duyệt, Người dùng & đối tác, Tài chính |

Đây là lựa chọn shortcut từ capabilities/tabs hiện hữu, không thay quyền tài chính/backend. Các tab không nằm navbar vẫn ở dashboard.

## Files

- Sửa `client/src/components/WeddingMatchLayout.tsx` (giữ mọi thay đổi customer groups/anchor/session sẵn có).
- Thêm `client/src/components/actorNavigationModel.ts`.
- Sửa `client/src/features/notifications/NotificationsPage.tsx` và `notifications.css`.
- Thêm `client/tests/actor-navigation.test.mjs` (5cases) và `notification-page-layout.test.mjs` (4cases).
- Checkpoint/checklist/start guides/spec được cập nhật; không chạm controller/auth API/SQL/schema/provider/finance.

## Kiểm thử mới

Từ `client`:

```powershell
node --test tests/actor-navigation.test.mjs tests/notification-page-layout.test.mjs tests/notifications.test.mjs tests/customer-navigation.test.mjs tests/account-journey.test.mjs
npm.cmd test
npm.cmd run lint
npm.cmd run build
```

- Targeted24/24PASS,0skip: role matrix đúng tabs/source, query active, unknown role, renderer desktop/mobile, retained customer hierarchy/common actions, notification container/loading/error/unread/longtext/no-write-on-render.
- **Full frontend401/401PASS,0fail/0skip** (392baseline+9newcases).
- Lint exit0,0errors,13warnings cũ ngoài scope. Build TS/VitePASS; còn INEFFECTIVE_DYNAMIC_IMPORT LoginForm/AuthModal hiện hữu.
- Backend/SQL không sửa, không rerun. Gate1135BE trước đó là lịch sử audit Phase5 cùng backendsource; không dùng nó thay freshbrowser/API verification của navbar.

## Browser verification — đúng phạm vi

- Vite5173 đã chạy từ trước, không restart main/API. `/notifications` redirect `/login` trong browser QA vì không có authenticated session; không dùng credentials/account thật hoặc tạo dữ liệu để vượt guard.
- Tạo **static SSR preview từ actual components/source + actual CSS**, fake hook data cho6actor, không token thật/API/DB. Helper và6HTML nằm ignored `client/artifacts/actor-navbar-qa/`; không là production UI/demo route mới.
- Browser kiểm6role tại viewport1366×900 và390×844; thêm Finance320×760. Main width1050desktop, left/right chênh dưới1CSSpixel; mobile gutter xấp xỉ16px. Không horizontal overflow, kể cả notification dài. Desktop/mobile links đúng role matrix; Customer menu vẫn có Planner/thiệp/dịch vụ/combo.
- Lưu measurements `layout-checks.json`, ảnh `desktop.png` (VendorOwner), `mobile.png` (CustomerCare) cùng folder ignored. Cột di động Care4mục, các actor khác tối đa5.
- Đã reset viewport và đóng tab QA. Không click mark-read/read-all/logout hoặc mutation. Static buttons không hydrate; **không** gọi đây là authenticated functional E2E, API/JWT proof hoặc full keyboard/screen-reader acceptance. Those remain UNVERIFIED.

## Git/bàn giao

Baseline `weddy-journey`/HEAD3596ae832faeac95fb15cc44bc0e8e62351c788b,105tracked modified/123expandeduntracked/0staged/1worktree. Sau code có107tracked/126untracked; thêm decision/report thành107/128. Existing work được giữ, không quy toàn bộ diff cho task này. Historical manifest `handoff-worktree-2026-10-22.json` chụp **trước** UI changes, không phản ánh các bản sửa/paths mới; lấy Git state mới khi chuyển.

No backendstartup/migration/seed/DBmutation/commit/push/deploy. Source mới làm ZIP/readiness cũ stale; cần gate/package lại nếu user resume host. Next trong scope này chỉ là optional authenticated browser/interactive QA bằng fixture độc lập; không tự mở backlog tài chính hoặc làm lại navbar.
