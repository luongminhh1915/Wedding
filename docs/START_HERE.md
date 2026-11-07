# Wedding Match — bắt đầu tại đây khi tiếp quản

Cập nhật: **01/11/2026, Asia/Ho_Chi_Minh**. Tài liệu dành cho bất kỳ coding agent/IDE nào, không cần đọc lại chat. Đây là chỉ dẫn tiếp nhận, **không tự cấp quyền thực hiện toàn bộ backlog**.

## 1. Dự án trong một phút

Wedding Match là nền tảng lập kế hoạch đám cưới kết hợp tìm nhà cung cấp và giao dịch dịch vụ cưới. Khách mới đăng nhập → trả lời 10 câu → nhận checklist/timeline/ngân sách → tìm dịch vụ/combo → gửi nhu cầu → chat/báo giá → xác nhận hợp đồng → theo dõi thực hiện → đánh giá sau hoàn tất. Thiệp, RSVP, khách mời/xếp bàn và lịch hẹn là công cụ chạy song song.

Nhà cung cấp đăng ký shop → tạo dịch vụ một lần → duyệt nội dung public → tạo combo từ dịch vụ của chính shop → nhận Lead/chat → báo giá/hợp đồng → thực hiện dịch vụ. Bộ phận nội bộ có kiểm duyệt, hỗ trợ và tài chính; quyền của từng actor khác nhau.

Đây là dự án môn học đã có website và dữ liệu thật. **Không xem dữ liệu local là đồ bỏ, không reset/reseed để sửa lỗi.** Thương hiệu Wedding Match dùng tông hồng san hô/kem ấm, vàng nhạt và chữ xanh đậm theo các yêu cầu giao diện mới. Tên Weddy trong source cũ là lịch sử, không phải yêu cầu đổi tên entity hàng loạt.

## 2. Điểm dừng hiện tại — không bắt đầu lại

- Git01/11: `main` / `20b292b5384254c0d250766847570adbbe86bd48`. Working tree có thay đổi và file untracked; nhận toàn bộ source, không chỉ checkout HEAD. Không ép về branch/commit cũ của22/10.
- Core Customer/Vendor, unified services, fixed combos, private content agreements, independent reviews, authoring Studio, CMS, account provisioning, email recovery và social sign-in đã có. Nguồn/trạng thái nghiệm thu chi tiết ở [đặc tả](PRODUCT_FUNCTIONAL_SPEC.md) và [handoff](AGENT_HANDOFF.md); không xây lại.
- Website `https://weddingmatch.website/` đã deploy. Bản Studio30/10 được ghi nhận trong [release report](reports/vendor-studio-release-2026-10-30.md). Trạng thái mới01/11, hash bản đang chạy, schema host và kiểm thử phải xem Living Checkpoint/report mới; không dùng gate cũ để chứng nhận code mới.
- Nhiệm vụ01/11: cập nhật bàn giao portable, sửa lỗi chặn release, deploy các cải tiến đã sẵn sàng. Draft/submit, batch/session auto-moderation và private document upload là phạm vi release. Owner đã [xác nhận](decisions/2026-11-01-batch-and-session-auto-moderation.md) tự duyệt cả NCC chưa xác minh sau khi Admin xác nhận bật.
- Phase5 có policy/basis/adjustment API và UI; thứ tự scope là foundation/candidate, **Approved chưa là Active**. Không áp rate mới trước signed scoped evidence + approved basis + date. Giữ rate legacy và snapshot gốc.
- Phase6 có entity/CQRS/API client mới, **UI chưa hoàn tất**, schema/feature gate phải xem checkpoint. Không bật API còn dang dở chỉ vì build xanh; không tự tạo policy Approved từ đàm phán. Phase7 partnership/signed evidence và kích hoạt tài chính vẫn là dependencies còn thiếu.
- Số test trước/sau mỗi sửa, warnings/skip, migration nào chỉ test-owned và trạng thái host nằm ở checkpoint/report. Không đọc521FE trong bản dán hay17PASS/1SQLskip như bằng chứng đã kiểm toàn bộ chức năng mới.
- Các số liệu/mệnh lệnh22–30/10 phía dưới hoặc trong report cũ là lịch sử theo scope từng lượt; chỉ quyết định nghiệp vụ còn hiệu lực được kế thừa. Yêu cầu hiện tại của owner quyết định phạm vi tiếp theo.

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

> Tiếp quản Wedding Match trong toàn bộ worktree hiện tại. Đọc AGENTS.md, docs/START_HERE.md và Living Checkpoint đầu docs/AGENT_HANDOFF.md, report release mới, PRODUCT_FUNCTIONAL_SPEC, ARCHITECTURE_AND_OPERATIONS, PRODUCT_COMPLETION_ROADMAP, master checklist và decisions liên quan. Tự kiểm Git branch/HEAD/status/diff/staged/untracked/worktrees và source/caller/tests/schema/config; bảo toàn thay đổi, không ép về baseline cũ. Website đã có dữ liệu thật. Không làm lại core/Studio/CMS/auth/review đã có. Phase5 policy Approved không là Active; Phase6 foundation/API client không chứng minh UI/schema đã sẵn sàng. Giữ rate legacy/original confirmed snapshots/payments; Admin approval + effective date + externally signed scoped evidence + approved basis phải đầy đủ trước kích hoạt. Đọc feature flags và rollout limits; không tự bật Phase6, migrate DB ứng dụng, reset/reseed, deploy, commit/push hoặc sửa tiền. Nếu task chỉ audit/docs thì dừng đúng scope; nếu giao code thì triển khai một gap cụ thể, test phù hợp rồi cập nhật checkpoint. Số test và host status chỉ lấy từ bằng chứng mới, không từ một checklist cũ.
>
> Nhiệm vụ cụ thể của bạn: **[người giao việc điền tại đây]**.

## 6. Chuyển source để agent khác thực sự làm được

- Agent cần **source hiện tại và file untracked**, không chỉ Markdown hay checkout HEAD. Manifest snapshot [ở đây](reports/handoff-worktree-2026-10-22.json) chụp trước follow-up UI22/10, nên không phản ánh các bản sửa UI/paths mới. Nó không thay Git state hiện tại hoặc bản sao source. Latest paths/checkpoint ở report UI và AGENT_HANDOFF.
- Không tự commit để “đóng gói”; chủ dự án quyết định cách chuyển. Không copy `bin/obj/node_modules/dist/artifacts` như thể đó là source chuẩn; có thể cài/build lại. TRX local có thể không đi theo bản chuyển, nên reports giữ counts/hash/phạm vi và phải rerun khi source thay đổi.
- Không gửi `.env`, private config, publish profiles có credentials, token, DB backup, upload cá nhân hoặc dữ liệu khách thật cho agent ngoài phạm vi. Cấp cấu hình riêng qua kênh an toàn khi cần; tài liệu chỉ dùng tên key, không chứa giá trị bí mật.
- Nếu đổi máy và không có DB local: không dựng DB giả để claim đã có dữ liệu thật; dùng DB test unique cho verification. Việc chuyển dữ liệu thật cần chủ dự án xác nhận target, backup và kế hoạch restore.
- Sau khi đọc, agent mới cần trả lời: dự án làm gì, module hiện tại, nguồn đã kiểm, rủi ro tài chính, task đầu tiên và acceptance. Không yêu cầu người dùng giải thích lại toàn bộ lịch sử đã ghi.
- Hội thoại trước từng có lịch tự động “tiếp tục đi”; trạng thái hiện tại **chưa kiểm**, không khẳng định đã tắt. Trước khi agent mới cùng sửa worktree, xác minh có tác vụ khác đang ghi hay không; nếu cần chủ dự án tạm dừng lịch qua công cụ phù hợp. Bộ Markdown không tự dừng job hoặc khóa agent khác.
