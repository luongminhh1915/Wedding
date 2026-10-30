# GitHub Copilot — Wedding Match

Tuân thủ quy tắc chung trong [AGENTS.md](../AGENTS.md). Khi tiếp nhận, đọc [handoff sống](../docs/AGENT_HANDOFF.md), [master checklist](../docs/master-upgrade-checklist.md) và decision records liên quan; không coi ghi chú “xong” của Codex/agent khác là bằng chứng thay cho source/tests.

- Mở đúng toàn bộ worktree local; kiểm branch, HEAD, staged/unstaged diff, untracked và worktree. Remote HEAD có thể thiếu phần code/migration chưa commit được nêu trong handoff.
- Kiểm độc lập thay đổi gần nhất của Codex trước khi sửa. Tiếp đúng module dở/bị lỗi đã xác định, không dựng lại services/combo/auth đã có và không chỉ sửa để làm tài liệu có vẻ đúng.
- Giữ kiến trúc/DTO/API/canonical-legacy compatibility, business decisions và snapshots. Không tự sửa tài chính, hợp đồng, rate/basis/settlement hoặc DB ngoài phạm vi được duyệt; không tự run app/migrate DB thật để thử.
- Lấy nhiệm vụ hiện tại từ yêu cầu người dùng. [Start prompt](../docs/COPILOT_START_PROMPT.md) và backlog chỉ gợi ý tiếp quản, không tự cấp quyền triển khai/deploy/commit/push.
- Sau mỗi module quan trọng, cập nhật checkpoint handoff ngay, checklist nếu trạng thái đổi, decision record khi có xác nhận mới; khi xong phải bàn giao đủ để Codex có thể tiếp tục mà không cần chat Copilot.
- Đọc lại tài liệu trước khi patch để tránh đè checkpoint của agent khác; giữ history và báo khác biệt. Báo đúng lệnh/test/skip/warnings, không tự nhận browser/provider/host đã verified.

Để kiểm tra hướng dẫn được dùng trong VS Code: mở đúng repository/harness, xem instruction discovery/References trong một chat mới. Nếu không thấy các file, đọc/đính kèm trực tiếp; không giả định đã tự tải toàn bộ handoff và không tự thay IDE settings.
