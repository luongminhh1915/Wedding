# Admin xác nhận duyệt public content —21/10/2026

## Kết quả — IMPLEMENTED + automated frontend VERIFIED

User tạm gác hosting và yêu cầu Are you sure khi Admin duyệt combo/dịch vụ. [Decision](../decisions/2026-10-21-admin-approval-confirmation.md). Không deploy/account/payment/DNS/upload/remoteSQL, không schema/backend/financial change.

Baseline: `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`,101tracked modified/89expanded untracked/0staged/1worktree, diffcheck0 (line-ending warnings). Dirty source/untracked trước đó được bảo toàn; không quy toàn bộ cho task.

## Đã triển khai

- Mới `client/src/features/admin/ApprovalConfirmationDialog.tsx`: tái sử dụng native ComboDialog, tên/type/shop/version, Hủy autofocus, Xác nhận duyệt, busy khóa callback/nút/Escape/backdrop/close; error để retry. Không network trực tiếp.
- `client/src/features/admin/ModerationQueue.tsx`: ReviewDetail mở xác nhận cho Listing/ServiceItem/Combo, capture request+content để gửi đúng revision/hash; xác nhận lại validates snapshot/currentstate, không API khi mở/hủy. Mutation nhận request đã xác nhận, giữ invalidation/onDone hiện có.
- `client/src/features/journey/admin/ComboAdministration.tsx`: ReviewItem thêm cùng xác nhận cho legacy service items; identity/hash/status/note/content guard, không đổi rules/discount editor.
- `client/src/features/journey/combo/ComboLifecycleCard.tsx`: cùng UX cho staff review combo; đúng proposal name/revision, owner/staff/busy guard, legacy contents thay đổi phải xem lại, không tự gán snapshot/revision.
- Cả3caller dùng synchronous useRef lock chống double-confirm; lỗi giữ modal/note, onSettled mở retry; từ chối vẫn bắt buộc reason theo model cũ. Không thay policy/permissions/backend contract.
- Mới tests `approval-confirmation-dialog.test.mjs`:7/7PASS; `admin-approval-confirmation.test.mjs`:37/37PASS actual SSR/callback integration. Root đã chạy lại toànFE gồm44tests mới:392/392PASS,0fail/skip. Hook/leaf UI được kiểm soát; không network/DB/browser lifecycle proof.

## Source audit backend — không phải API test mới

Read-only audit xác định đúng3lối approval UI đang dùng. `/api/listings/{id}/approve` giữ Moderator/SuperAdmin+active actor+Pending/latest revision; `/api/combos/items/{id}/review` giữ opaque reviewVersion+eligibility và canonical item deny; `/api/combos/templates/{id}/review` giữ staff/active/Pending/latest/component validation/self-review deny. Không bỏ revision/hash khi xác nhận. AllListingsManager chỉ Active/Hidden và đã có confirm riêng; useApproveListing không còn caller, không mở rộng scope.

## Gate mới trên source cuối

- `npm.cmd run build`:exit0, TypeScript/VitePASS. Build session59103 đã kết thúc; không suy từ build này rằng browser/host đã đạt.
- `npm.cmd run lint`:exit0,13warnings hiện hữu/0errors; không warning tại3caller/shared dialog mới.
- `node --test tests/approval-confirmation-dialog.test.mjs`:7PASS; `node --test tests/admin-approval-confirmation.test.mjs`:37PASS/0skip, do2agent sở hữu files riêng chạy trước root gate.
- `npm.cmd test`:**392/392PASS,0fail/skip**, command exit0,3,30giây, Node reported duration2797,97ms. Tăng44 từ348FE cũ; không lấy348 trước đổi source làm fresh proof.
- `npm.cmd run lint` chạy lại sau testfile ổn định:exit0,0errors/13warnings cũ. Targeted lint2filetests/dialog0warnings/errors. Build source4TSX đã chốt trước fulltest; không sửa source sau build.
- Test coverage:5target scenarios trên3caller; mở/hủy không write và giữ note; confirm exact allowlisted request/revision/hash đúng1lần; pending khóa cancel/duplicate; thành công đóng; API failure giữ dialog và explicit retry; note/status/version/content đổi bị chặn; legacy cùngID không revision không duyệt nhầm; stafffalse không exposed/retained callback approval. Identity guard được đọc trong code, không gọi đó là một browser/API identity test riêng.
- Không BE/API/SQL migration/test, DB thật/startup; backend/schema không đổi. Source audit không thay acceptance auth/SQL. Browser native focus/escape/mobile và actual Admin E2E hiệnUNVERIFIED, không demo bằng ghi approval thật.

Root sole docs owner; component/test và readonly scope audit agents không sửa tài liệu chung. Các agent đã hoàn thành; không build/test/server command riêng còn chạy. Không restart API hoặc tạo fixtureDB; không cần cleanup dữ liệu.

## Bàn giao / phần chưa nghiệm thu

- Tại20:36VN đã chốt code/fullFE/lint/build; giữ source và mọi dirty/untracked trước đó. Không commit/push/reset/clean.
- Browser Admin thực (mở/cancel/focus restore/Escape/mobile/API-error ở UI) **UNVERIFIED**; chỉ automated SSR/callbacks, không tuyên bố E2E/API authorization thực đạt mới. Không thực hiện approval thật để demo.
- Source/code thay đổi so với release local18:28: **ZIP/ready.json cũ không chứa chức năng mới**. Nếu user tiếp tục deploy, phải recheck source và chạy lại release gate/package; không upload artifact cũ theo checkpoint hosting.
- Scope đã hoàn thành ở mức implementation+automated frontend. Hosting đang tạm gác; không tự tiếp Phase5–7 hoặc backlog, không tự tạo account/đổiDNS/migrate khi chờ user.

Git chốt20:39:30VN: branch/HEAD giữ nguyên; **104tracked modified/94expanded untracked/0staged/1worktree**, diffcheck0 (64line-ending notices). So baseline101/89,3tracked mới thay đổi là3caller;5untracked mới là dialog,2tests,decision vàreport. Không gán101/89 đã có cho task. Tổng phạm vi root/agents:6code/testfiles +5docs, giữ tất cả thay đổi khác. Kiểm5docs/50relative links:0missing. Không lệnh hoặc agent phụ còn chạy, không DB/process cleanup hoặc commit/push.
