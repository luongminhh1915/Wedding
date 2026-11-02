# Wedding Match — quy tắc làm việc chung cho coding agents

Áp dụng cho Codex, GitHub Copilot và các agent làm việc trong repository này. Đây là quy tắc **quy trình**; không phải giấy phép triển khai mọi việc trong backlog hoặc thay đổi yêu cầu nghiệp vụ.

## Đọc trước khi tiếp quản

1. Đọc `docs/AGENT_HANDOFF.md`, trước hết checkpoint hiện tại, Git state, quyết định, known risks và next tasks; đọc sâu module liên quan trước khi sửa.
2. Đọc `docs/master-upgrade-checklist.md`, checklist module và decision records liên quan trong `docs/decisions/`.
3. Kiểm tra hướng dẫn ở thư mục con nếu có; trước khi sửa bất kỳ file hướng dẫn nào phải kiểm file tồn tại, đọc và giữ các quy tắc còn hiệu lực.
4. Đối chiếu bàn giao với **source, caller, tests, schema/config và Git thực tế**. Không mặc định tuyên bố COMPLETED của agent trước là đúng; báo khác biệt và phạm vi chưa xác minh.
5. Xác định phạm vi người dùng đang giao: báo cáo/audit/docs không tự cho phép sửa tính năng. Không triển khai backlog chỉ vì thấy trong handoff.

## Before Task — kiểm tra Git và phạm vi

Chạy các lệnh chỉ đọc phù hợp từ repo root:

```text
git branch --show-current
git rev-parse HEAD
git status --short
git diff --stat
git diff --cached --stat
git diff --check
git ls-files --others --exclude-standard
git worktree list --porcelain
```

- Đọc diff và file untracked liên quan, không chỉ diff tracked hoặc commit cuối. Ghi điểm dừng/module thật và dependency trước khi code.
- Không làm lại module đã triển khai đúng. Khi phải sửa, nêu bug/gap/evidence cụ thể và giữ phần đã hoạt động.
- Với nghiệp vụ quan trọng chưa được xác nhận, giải thích, ghi blocker và hỏi người dùng; tiếp việc độc lập an toàn nếu còn trong phạm vi. Không biến đề xuất thành quyết định đã chốt.
- Nếu code và docs khác nhau: code mô tả **hành vi hiện tại**, decisions mô tả **nghiệp vụ đã chốt**; không tự coi một bên luôn đúng. Giữ lịch sử, đánh dấu khác biệt/superseded với nguồn và ngày. Xung đột quan trọng chưa phân giải được phải hỏi, không tự xóa quy tắc cũ.

## Bảo toàn code, dữ liệu và quyền thực hiện

- Không tự ý reset/clean/checkout đè, xóa untracked, ghi đè thay đổi chưa commit hoặc công việc của agent khác. Thay đổi sẵn có thuộc người dùng trừ khi biết rõ nguồn.
- Không tự commit/push/force-push, deploy, tạo tài khoản/giao dịch thật hoặc thay cấu hình hệ thống ngoài phạm vi người dùng đã duyệt.
- Không tự chạy migration trên DB có dữ liệu thật; không drop/reset/reseed/restore đè/downgrade hoặc sửa lịch sử tài chính. Migration mới phải additive hoặc có kế hoạch chuyển đổi được duyệt; review và test trên DB riêng trước. Không chạy Down trên main để chữa lỗi.
- SQL Server là DB thật của dự án. Không chuyển sang SQLite/DB mới để né lỗi hoặc làm test xanh. Development đang có `ApplyMigrations=true`: bare API startup có thể áp schema. Kiểm target/config trước; không tự restart để thử. Production không migrate/seed startup.
- Test chỉ được tạo/drop **DB test unique do chính test sở hữu**, không trỏ biến test vào main/production. Không kill hàng loạt dotnet/node, đổi SQL service/RAM/TLS hay mở port để chữa build/test.
- Không ghi secrets, token, mật khẩu, private config, dữ liệu cá nhân, upload thật hoặc backup DB vào Git/docs/log chia sẻ.
- Một dịch vụ được tạo một lần; giữ canonical/legacy APIs và snapshot. Private content-only agreement Customer–Vendor không phải partnership contract hay commission adjustment.
- Vendor cũ giữ rate hiện tại tới chuyển đổi hợp lệ; original confirmed financial snapshots/payments không rewrite. Financial rules/basis/activation/settlement chỉ đổi theo quyết định đã xác nhận trong `docs/decisions/`, không suy từ metadata/UI/chat.

## During Task — checkpoint liên tục và phối hợp

- Giữ .NET/EF10 Clean Architecture, React19/TS/Vite và SQL Server; tái sử dụng feature/entity/API hiện có, không tự rewrite stack/module.
- Sau **mỗi nhiệm vụ/module quan trọng hoặc trước một điểm dễ gián đoạn**, cập nhật checkpoint trong `docs/AGENT_HANDOFF.md`: việc đã làm, việc dở, file/phụ thuộc, test đang chạy/đã xong, blocker và bước tiếp theo. Không chờ tới cuối phiên/quota.
- Khi tiến độ thực tế thay đổi, cập nhật checklist đúng phạm vi; không tick VERIFIED chỉ vì có UI/entity hoặc build xanh.
- Quyết định nghiệp vụ mới **đã được người dùng xác nhận** cần decision record có ngày/nguồn/phạm vi và trạng thái CONFIRMED/PROPOSED/SUPERSEDED/NEEDS CONFIRMATION. Không sửa Master Requirements hoặc xóa decision history để làm khớp code.
- Nếu nhiều agent: chia file/phạm vi không chồng lấn; chỉ một người tổng hợp handoff/checklist mỗi thời điểm. Đọc lại bản mới nhất trước patch tài liệu dùng chung. Nếu phát hiện thay đổi đồng thời/ngoài dự kiến, bảo toàn và báo cáo; không rollback của nhau hoặc tự tạo file lock/hook.

## After Task — kiểm chứng và bàn giao

- Sau sửa code, chạy build/test theo rủi ro: FE model/session + lint/build nếu FE; BE build/targeted tests + actual SQL khi chạm query/schema/transaction/auth/finance. Gate toàn bộ khi tích hợp/release phù hợp. Output riêng tránh DLL đang chạy bị khóa.
- `npm.cmd` dùng trong PowerShell để tránh npm.ps1 execution policy. SQL opt-in `WEDDING_TEST_SQLSERVER=1` chỉ cho localhost/test DB unique; thiếu SQL thì ghi SKIPPED/UNVERIFIED, không nhận đã test SQL.
- Docs-only: kiểm liên kết/đường dẫn, nội dung bắt buộc, nhất quán hướng dẫn, Git diff/check và bảo toàn source; không cần chạy lại toàn bộ suite nếu source không đổi. Ghi rõ kết quả nào từ lượt trước và source/hash/phạm vi của chúng.
- Báo lệnh, thời điểm/phạm vi, pass/fail/skip/warnings và giới hạn. Browser/provider/host/load/restore chưa kiểm thì UNVERIFIED; không lấy smoke một người làm cam kết100 concurrent.
- Cập nhật `docs/AGENT_HANDOFF.md` (checkpoint, completed/unverified/in-progress/remaining, Git, tests, risks, next task), checklist nếu có tiến độ mới và reports/decisions cần thiết. Không viết lại report cũ thành kết quả mới.
- Trước kết thúc/chuyển agent, lấy lại Git state, kiểm tài liệu bàn giao/unfinished commands và bảo đảm feature mới untracked được nêu rõ. Nêu chính xác điểm tiếp tục, không chỉ “tiếp tục đi”.
- Agent mới phải kiểm độc lập phần quan trọng, không tin tuyệt đối agent trước. Nếu chỉ được giao bàn giao/docs, **dừng và báo cáo sau khi tài liệu xong**, không thực hiện next tasks.

## Tổ chức tài liệu, không sao chép trùng

- `AGENTS.md`: quy tắc chung bền vững; không đặt snapshot Git/test count thường thay đổi vào đây.
- `.github/copilot-instructions.md`: bổ sung riêng cho Copilot và dẫn chiếu quy tắc chung, không nhân bản toàn bộ.
- `docs/AGENT_HANDOFF.md`: trạng thái sống, bằng chứng, rủi ro và điểm tiếp tục; không phải một lần export rồi bỏ.
- `docs/master-upgrade-checklist.md` / module checklist: tiến độ; `docs/decisions/`: nghiệp vụ có nguồn; `docs/reports/`: lịch sử kiểm chứng.

Các file hướng dẫn không tự chạy job, không thay permission/security của công cụ và không bảo đảm mọi agent/IDE luôn nạp hoặc tuân thủ. Khi chuyển harness/session phải xác minh instruction discovery; nếu không có, yêu cầu agent đọc file trực tiếp. Không tự sửa cài đặt IDE/global hoặc thêm enforcement automation/hook nếu chưa được yêu cầu.
