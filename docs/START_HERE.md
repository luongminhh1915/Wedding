# Wedding Match — bắt đầu tại đây khi tiếp quản

Cập nhật: **22/10/2026, Asia/Ho_Chi_Minh**. Tài liệu dành cho bất kỳ coding agent/IDE nào, không cần đọc lại chat. Đây là chỉ dẫn tiếp nhận, **không tự cấp quyền thực hiện toàn bộ backlog**.

## 1. Dự án trong một phút

Wedding Match là nền tảng lập kế hoạch đám cưới kết hợp tìm nhà cung cấp và giao dịch dịch vụ cưới. Khách mới đăng nhập → trả lời 10 câu → nhận checklist/timeline/ngân sách → tìm dịch vụ/combo → gửi nhu cầu → chat/báo giá → xác nhận hợp đồng → theo dõi thực hiện → đánh giá sau hoàn tất. Thiệp, RSVP, khách mời/xếp bàn và lịch hẹn là công cụ chạy song song.

Nhà cung cấp đăng ký shop → tạo dịch vụ một lần → duyệt nội dung public → tạo combo từ dịch vụ của chính shop → nhận Lead/chat → báo giá/hợp đồng → thực hiện dịch vụ. Bộ phận nội bộ có kiểm duyệt, hỗ trợ và tài chính; quyền của từng actor khác nhau.

Đây là dự án môn học nhưng dự kiến thu thập dữ liệu thật và triển khai trong thời gian ngắn. **Không xem dữ liệu local là đồ bỏ, không reset/reseed để sửa lỗi.** Thương hiệu hiện tại là Wedding Match, navy + kem là màu chính, vàng là điểm nhấn. Tên Weddy trong source cũ là lịch sử, không phải yêu cầu đổi tên entity hàng loạt.

## 2. Điểm dừng hiện tại — không bắt đầu lại

- Branch/HEAD được kiểm: `weddy-journey` / `3596ae832faeac95fb15cc44bc0e8e62351c788b`. Có nhiều thay đổi **chưa commit và untracked**; chỉ lấy remote HEAD sẽ thiếu chức năng/migration.
- Core Customer/Vendor, dịch vụ thống nhất, combo FixedPrice, đề xuất nội dung riêng và review Shop/Service/Combo đã có code thật. Không xây lại chúng.
- Phase 5 mới là **backend foundation**, đã audit/sửa lỗi và test SQL. Policy/basis/adjustment request có thật; **chưa có engine công thức mới/activation/signed agreement linkage đầy đủ**. Phase 6 Negotiation và Phase 7 Partnership Contracts vẫn chưa triển khai trọn vẹn.
- Gate backend mới nhất: **1.135 pass / 0 fail / 0 skip**, gồm actual SQL Server trong DB test unique. Targeted Phase 5: **62/62**, gồm 19 SQL-only test methods. Build API qua test-project build thành công. Còn 4 xUnit2031 warnings cũ. Xem [báo cáo audit](reports/phase5-copilot-audit-2026-10-22.md).
- Frontend mới nhất **401/401PASS + lint/build22/10**, sau yêu cầu thông báo căn giữa và navbar theoactor;13lintwarningscũ. Browser staticSSR actualcomponents/CSS đủ6role desktop/mobile đạt layout/links; authenticated E2E toàn sản phẩm/provider/host/load/restore chưa chứng nhận. Xem [report UI](reports/notifications-actor-navbar-2026-10-22.md);392/39221/10 là lịch sử.
- Có **17 migration trong source**. Migration17 chỉ được áp dụng trên DB test do test sở hữu; lượt audit không kết nối/migrate DB ứng dụng/production. Trạng thái DB ứng dụng hiện tại phải kiểm an toàn, không suy từ source.
- Hosting đang tạm gác. User báo đã trả tiền `weddingmatch.website`; quyền sở hữu/DNS/host active chưa xác minh. Không tự mua hosting/deploy từ các cuộc trao đổi cũ.
- **Sau docs, user giao thêm UI notifications/navbar; phạm vi này đã hoàn tất và cập nhật bàn giao.** Không tự tiếp backlog tài chính/deploy; agent tiếp theo vẫn phải nhận task cụ thể từ user.

## 3. Đọc theo thứ tự

1. [Quy tắc chung](../AGENTS.md) và [checkpoint sống](AGENT_HANDOFF.md), trước hết phần Living Checkpoint mới nhất. Copilot đọc thêm [hướng dẫn riêng](../.github/copilot-instructions.md).
2. [Mô tả sản phẩm và chức năng](PRODUCT_FUNCTIONAL_SPEC.md): actors, hành trình, module có thật, invariants và giới hạn.
3. [Kiến trúc, dữ liệu và vận hành](ARCHITECTURE_AND_OPERATIONS.md): source map, API, startup hazards, test, storage, deploy.
4. [Kế hoạch hoàn thiện sản phẩm](PRODUCT_COMPLETION_ROADMAP.md): ưu tiên, dependencies, acceptance và câu hỏi còn mở.
5. [Master checklist](master-upgrade-checklist.md), [refactor checklist](vendor-services-combos-refactor-checklist.md), decision records liên quan module. Chúng giữ lịch sử, không đọc một checkbox cũ như kết luận hiện tại.
6. Source + caller + tests + schema/config liên quan trước mỗi thay đổi. Tài liệu không thay bằng chứng thực thi.

Các hồ sơ nghiệp vụ quan trọng:

- [Basis và ba điều kiện activation](decisions/2026-10-19-commission-basis-and-activation.md).
- [Legacy và ngoại lệ riêng hợp đồng](decisions/2026-10-19-legacy-and-contract-commission-adjustments.md), đọc cùng [guardrails mới](decisions/2026-10-21-phase5-7-financial-workflow-guards.md).
- [Dịch vụ thống nhất, combo cố định, đề xuất riêng](decisions/2026-10-20-fixed-combos-and-private-service-changes.md).
- [Review độc lập](decisions/2026-10-20-independent-reviews.md) và [visibility/dispute](decisions/2026-10-21-review-visibility-and-dispute-holds.md).
- [Bỏ chọn nhóm lúc đăng ký Vendor](decisions/2026-10-21-service-category-without-vendor-membership.md).
- [Ngân sách cá nhân](decisions/2026-10-21-personal-customer-budget.md).

## 4. Kiểm tra bắt buộc trước khi sửa

Chạy từ repo root; đọc cả untracked liên quan, không chỉ diff tracked:

```powershell
git branch --show-current
git rev-parse HEAD
git status --short
git diff --stat
git diff --cached --stat
git diff --check
git ls-files --others --exclude-standard
git worktree list --porcelain
```

Không commit/push/reset/clean/checkout đè; không xóa untracked; không deploy hoặc chạy migration lên DB ứng dụng. Development startup có `ApplyMigrations=true`, vì vậy **không bare `dotnet run` để thử** trước khi kiểm target/config/quyền. SQL Server là DB thật; không đổi SQLite để né lỗi.

## 5. Prompt chuyển tiếp — copy đoạn này cho agent mới

> Tiếp quản Wedding Match trong toàn bộ worktree hiện tại. Đọc AGENTS.md, docs/START_HERE.md và Living Checkpoint đầu docs/AGENT_HANDOFF.md; sau đó đọc PRODUCT_FUNCTIONAL_SPEC, ARCHITECTURE_AND_OPERATIONS, PRODUCT_COMPLETION_ROADMAP, master checklist và decisions liên quan. Tự kiểm Git branch/HEAD/status/diff/staged/untracked/worktrees; bảo toàn mọi thay đổi. Backend foundation Phase 5 đã audit/sửa và đạt 62 targeted + 1135 full tests có SQL riêng, không đồng nghĩa activation hoàn tất. Không làm lại Customer/Vendor/services/fixed combos/private agreements/independent reviews đã có. Legacy rates/original confirmed financial snapshots/payments bất biến; Admin approval, effective date và externally signed Platform–Vendor agreement phải cùng đủ trước kích hoạt. Không dùng BookingContract hoặc content-only PrivateServiceAgreement thay partnership/negotiation. Không migrate DB ứng dụng, reset/reseed dữ liệu, deploy, commit/push hoặc tự quyết chính sách tài chính. Nếu được giao tiếp tục code, chọn một module trong roadmap, nêu gap cụ thể, triển khai và test theo rủi ro rồi cập nhật checkpoint. Nếu chỉ được giao đọc/bàn giao thì dừng sau báo cáo. Các tài liệu không tự cấp quyền làm backlog.
>
> Nhiệm vụ cụ thể của bạn: **[người giao việc điền tại đây]**.

## 6. Chuyển source để agent khác thực sự làm được

- Agent cần **source hiện tại và file untracked**, không chỉ Markdown hay checkout HEAD. Manifest snapshot [ở đây](reports/handoff-worktree-2026-10-22.json) chụp trước follow-up UI22/10, nên không phản ánh các bản sửa UI/paths mới. Nó không thay Git state hiện tại hoặc bản sao source. Latest paths/checkpoint ở report UI và AGENT_HANDOFF.
- Không tự commit để “đóng gói”; chủ dự án quyết định cách chuyển. Không copy `bin/obj/node_modules/dist/artifacts` như thể đó là source chuẩn; có thể cài/build lại. TRX local có thể không đi theo bản chuyển, nên reports giữ counts/hash/phạm vi và phải rerun khi source thay đổi.
- Không gửi `.env`, private config, publish profiles có credentials, token, DB backup, upload cá nhân hoặc dữ liệu khách thật cho agent ngoài phạm vi. Cấp cấu hình riêng qua kênh an toàn khi cần; tài liệu chỉ dùng tên key, không chứa giá trị bí mật.
- Nếu đổi máy và không có DB local: không dựng DB giả để claim đã có dữ liệu thật; dùng DB test unique cho verification. Việc chuyển dữ liệu thật cần chủ dự án xác nhận target, backup và kế hoạch restore.
- Sau khi đọc, agent mới cần trả lời: dự án làm gì, module hiện tại, nguồn đã kiểm, rủi ro tài chính, task đầu tiên và acceptance. Không yêu cầu người dùng giải thích lại toàn bộ lịch sử đã ghi.
- Hội thoại trước từng có lịch tự động “tiếp tục đi”; trạng thái hiện tại **chưa kiểm**, không khẳng định đã tắt. Trước khi agent mới cùng sửa worktree, xác minh có tác vụ khác đang ghi hay không; nếu cần chủ dự án tạm dừng lịch qua công cụ phù hợp. Bộ Markdown không tự dừng job hoặc khóa agent khác.
