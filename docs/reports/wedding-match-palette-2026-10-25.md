# Wedding Match — đồng bộ bảng màu toàn giao diện (25/10/2026)

## Phạm vi

Theo yêu cầu người dùng, đồng bộ tông màu thương hiệu đã gửi tới các màn hình khách hàng, nhà cung cấp, planner, combo, thông báo, xác thực, kiểm duyệt và quản trị. Chỉ thay đổi giao diện; không đổi luồng, API, quyền truy cập, dữ liệu, database, migration hay chính sách tài chính. Worktree đã có nhiều thay đổi từ trước; chỉ xử lý các màu giao diện ở frontend và không commit/deploy.

## Đã làm

- Chuẩn hóa màu thương hiệu: deep teal `#233A42`, warm ivory `#FCF6EA`, champagne `#F1DBA2`, soft coral `#E38C82`, sage `#98AB98`.
- Đưa các màu trung tính Tailwind slate trong app shell về thang ivory/champagne/sage-teal để các class chung kế thừa cùng tông.
- Đồng bộ các màu navy/kem/xám-vàng cũ được dùng cho chữ, panel, viền và điểm nhấn trong 46 file frontend; giữ riêng các màu trạng thái thành công/cảnh báo/lỗi.
- Đổi navbar chung sang nền ivory–champagne và chữ teal; CTA marketplace/search vẫn coral với chữ teal.
- Cập nhật hướng dẫn thương hiệu và thêm `client/tests/brand-palette.test.mjs` để khóa bảng màu, app shell dùng chung và navbar.

## Kiểm chứng

Chạy tại `client/` ngày 25/10/2026:

- `npm.cmd test` — **418/418 PASS**, gồm 2 test bảng màu mới.
- `npm.cmd run build` — **PASS** (TypeScript + Vite production build).
- `npm.cmd run lint` — exit 0, còn **13 warnings** đang có ở logic/hooks/tests không liên quan tới đổi màu.
- `git diff --check` — **PASS**, không phát hiện whitespace error; Git chỉ phát cảnh báo chuyển LF/CRLF đã có trong worktree.
- Browser visual/E2E và deploy — **chưa chạy**; chỉ xác nhận bằng frontend tests/build.

## Giới hạn

Màu trạng thái thành công, cảnh báo, lỗi và màu minh họa riêng vẫn mang ý nghĩa nghiệp vụ/khả năng đọc, không bị ép thành màu thương hiệu. CSS `brand-previews.css` giữ nguyên vì chỉ định nghĩa các mẫu preview không được route giao diện hiện tại sử dụng. Không sửa mascot hoặc ảnh.

## Amendment — người dùng yêu cầu khôi phục màu trước đó (25/10/2026)

Bộ màu teal/ivory/champagne/coral/sage ở phần trên đã bị người dùng yêu cầu thay thế ngay sau đó. Source hiện được trả về palette thương hiệu cũ: navy `#25334C`, warm ivory `#F2EFE4`, mustard `#D2AC2B` (gold đậm `#A7841D`), kèm slate-blue trung tính; navbar dùng navy với chữ ivory. Mascot Bé Hý được giữ nguyên hình dáng nhưng recolor về navy/ivory/mustard. Regression test được cập nhật theo palette hiện hành. Các kết quả 418 test/build/lint phía trên là lịch sử của palette tạm thời, **không xác nhận source sau hoàn nguyên**; xem checkpoint mới nhất trong `docs/AGENT_HANDOFF.md` để lấy gate mới.

### Gate sau khi hoàn nguyên

- `npm.cmd test` — **418/418 PASS** trên palette hiện hành.
- `npm.cmd run build` — **PASS**.
- `npm.cmd run lint` — exit 0; **13 warnings hiện hữu**, không phát sinh warning riêng cho màu.
- PowerShell XML parse `client/public/wedding-match/be-hy.svg` — **PASS**.
- `git diff --check` — **PASS**, Git chỉ in cảnh báo LF→CRLF từ dirty worktree.
- Browser visual/E2E và deploy — **chưa xác minh**.

## Amendment — user clarified the reference is the blush/pink palette (26/10/2026)

The 25/10 navy/ivory/mustard restoration above is superseded. The user explicitly clarified that the supplied Wedding Match collage is the intended palette and asked for all screens to follow it. Current local-only direction:

- Canvas/cards: warm ivory `#FCF6EA`; soft backgrounds/borders use blush `#D49587` and peach `#E4B7A6`.
- Main actions and selected controls: coral `#E38C82` with deep ink `#233A42` text for contrast.
- Deep ink `#233A42` remains for typography and restrained structure; champagne `#F1DBA2` and sage `#98AB98` are secondary accents only.
- Shared navbar is a light ivory-to-peach/blush gradient, not navy. Bé Hý keeps the same silhouette, with a coral/blush tail and peach cheeks.

Updated 48 client source/asset files with brand color values and primary-action utility classes; refined the shared theme, common controls and homepage UI preview components; updated the brand guide and regression test. Layout, API, permissions, database, migrations, customer/vendor workflows and financial behavior were not changed. No deployment was made.

### Current verification

- `npm.cmd test` — **418/418 PASS**.
- `npm.cmd run build` — **PASS** (TypeScript + Vite production build).
- `npm.cmd run lint` — exit 0; **13 warnings**, on existing unrelated hooks/components/tests.
- SVG XML parse and `git diff --check` — recorded in the 26/10 handoff checkpoint after final verification.
- Browser visual/E2E and deploy — **UNVERIFIED**.
