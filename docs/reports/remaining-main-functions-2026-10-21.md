# Hoàn thiện chức năng độc lập — 21/10/2026

## Phạm vi và baseline

Nguồn: người dùng yêu cầu “còn chức năng nào thì cố hoàn thành nốt đi”; Master Prompt phần14 nhạc thiệp và checklist Phase8. Tiếp nối service/combo discovery đã xong, không làm lại Phase4C. Các module Commission/Negotiation/Partnership chưa được mở trong lượt này; không tự quyết định tài chính hoặc kích hoạt rule mới.

Git đầu lượt: `weddy-journey`, HEAD `3596ae832faeac95fb15cc44bc0e8e62351c788b`,62tracked modified/57expanded untracked/0staged/1worktree; diff-check0. Đã đọc hướng dẫn, handoff/checklist/requirements, source/callers/tests và diff liên quan. Mọi thay đổi trước được giữ; không quy toàn worktree cho lượt này.

## Đã triển khai

### 1. Nhạc nền thiệp — IMPLEMENTED, automated VERIFIED; browser UNVERIFIED

- Tải MP3 từ thiết bị, kiểm đuôi/MIME/dữ liệu MPEG LayerIII liên tiếp/kích thước; bỏ ID3 metadata. Nghe thử, thay, bỏ nguồn nhạc, trạng thái tiến độ/lỗi; lỗi upload không xóa lựa chọn trước. Save bị chặn khi đang upload hoặc URL không hợp lệ.
- Giới hạn kỹ thuật12MB/tệp,20tệp/100MB mỗi wedding owner, không phải mức phí hoặc quyền lợi gói dịch vụ. Cần workflow quản trị riêng để dọn orphan; thay/bỏ nhạc không tự xóa file cũ.
- Upload xác minh Customer/owner/shared-editor trước đọc stream; Save kiểm role Customer active thật từ DB. Viewer/actor sai/foreign owner path bị chặn. Public audio là nội dung chia sẻ công khai như ảnh, không dùng cho tài liệu riêng tư.
- Tên fileGUID, ngoài webroot, `audio/mpeg`/nosniff/range/immutable GET. `Uploads:InvitationAudioRoot` default local `App_Data/invitation-audio`; cấu hình publish/private/env/QA thêm durable root ngoài release ZIP. SQL backup cần kèm thư mục nhạc.
- YouTube: allowlist host/routes, exact11-character raw videoID, canonicalURL; từ chối spoof/duplicatev/encodedID/unsafe protocols. Browser dùng YouTube embedded player, chỉ tải sau click, không autoplay; fallback mởYouTube. Không download/rip âm thanh. Tham khảo [official embedded-player parameters](https://developers.google.com/youtube/player_parameters).
- Tái sử dụng cột `MusicUrl`, derive source/videoID; không schema/migration mới. HTTP(S) audio cũ an toàn vẫn giữ, ảnh/RSVP không đổi. Không coi kiểm tra frame là full decoder/antivirus; không thể bảo đảm mọi MP3/video ngoài hệ thống phát được.

### 2. Rating catalog refresh — VERIFIED bằng QueryObserver

`approve`/`moderate-edit` invalidate actual keys `public-listings`, `listing-detail`, `combo-builder`, `combo-catalog-page` trên mọi filter/page/actor. Pending/edit/reply không tự đổi aggregate. Actual active observer test xác minh refetch; giỏ, đơn cũ, finance/private configuration không bị xóa hoặc thay. Không đổi eligibility/score/financial snapshots. Browser refetch trên catalog mới chưa chạy.

### 3. Loading/error và navigation — VERIFIED automated

- KPI lỗi không còn loading vô hạn; audit/verifications/disputes/legacy settlements phân biệt loading/error/empty, có read-only retry. Không hiển thị action từ stale failed-query data; quyền audit/mutations/giá và policy không đổi.
- Hash URL malformed percent/UTF8 không còn ném URIError trong layout effect; Unicode hợp lệ vẫn được decode một lần và tra bằng getElementById. Không rewrite menu/route.

## Files của lượt này (không bao gồm toàn bộ worktree bẩn)

Mới:

- `client/src/features/journey/invitations/invitationMusic.ts`
- `client/src/features/journey/invitations/InvitationMusicPlayer.tsx`
- `client/src/features/journey/invitations/InvitationMusicPicker.tsx`
- `client/src/features/journey/admin/AdminQueryReadState.tsx`
- `client/src/utils/anchorNavigation.ts`
- `client/tests/invitation-music.test.mjs`
- `client/tests/review-catalog-cache.test.mjs`
- `client/tests/admin-query-state.test.mjs`
- `client/tests/anchor-navigation.test.mjs`
- `src/Wedding.Application/Features/Invitations/InvitationMusicUrls.cs`
- `src/Wedding.Infrastructure/Services/InvitationAudioStorage.cs`
- `tests/Wedding.Tests/InvitationMusicTests.cs`
- `tests/Wedding.Tests/InvitationAudioStorageTests.cs`
- Báo cáo này.

Sửa: InvitationBuilder/InvitationView, reviewCacheModel, WeddingMatchLayout (hash effect), AdminJourneyPanels, SaveInvitationCommand, InvitationsController (POST Customer authorization), UploadController, `.gitignore`, `deploy.env.example`, `tools/{New-StudyConfiguration,Publish-StudySite,Verify-StudyDeployment}.ps1`, docs upload-storage/customer-journey/handoff/checklist/Copilot prompt. Root tổng hợp docs; agents chia file không chồng nhau. Harness ignored `artifacts/review4c-repair-qa/Program.cs` chỉ thêm tạo mẫu MP3 silence và invalid fixture, không là app startup.

## Kiểm chứng mới

| Gate | Lệnh/phạm vi | Kết quả |
|---|---|---|
| FE full | `npm.cmd test` trong client, sau tích hợp tất cả source | **296/296PASS**,0fail/skip; tăng21 từ275 baseline |
| FE lint | `npm.cmd run lint` | exit0,13warnings cũ; warning control-regex mới đã sửa |
| FE production build | `npm.cmd run build` | PASS; LoginForm mixed static/dynamic import warning cũ |
| API build | `dotnet build src/Wedding.WebApi/Wedding.WebApi.csproj --artifacts-path artifacts/remaining-main-20261021/api-build --verbosity minimal` |0warning/0error |
| BE full + actualSQL | Lệnh dưới,21/10 **07:25:17–07:26:49+07** | **1051/1051PASS**,0fail/skip;4xUnit2031 warnings cũ ở VendorBusinessGroupTests |
| Targeted agent BE | InvitationMusic/AudioStorage/InvitationUpload/Planner, opt-in blank |70pass/1SQLskip/0fail; lịch sử trước fullSQL, không dùng skip làm SQL proof |
| Schema | Full suite includes existing migration/model preservation gates + new SQL music shared-editor roundtrip | TestDB unique only; **không có migration mới**, không áp migration DB ứng dụng |
| Browser music | Isolated fixture/API/Vite setup; login form quan sát được, UI tiếp bị ngắt | **UNVERIFIED** upload/save/play/pause/YouTube/mobile; không có screenshot mới hoặc E2E Passed |

```powershell
$env:WEDDING_TEST_SQLSERVER='1'
dotnet test tests/Wedding.Tests/Wedding.Tests.csproj --artifacts-path artifacts/remaining-main-20261021/test-build --results-directory artifacts/remaining-main-20261021/test-results --logger 'trx;LogFileName=backend.trx' --verbosity minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1 RunConfiguration.MaxCpuCount=1
```

TRX: `artifacts/remaining-main-20261021/test-results/backend.trx`. SQLoptin1 dùnglocalhost; đã kiểm newtest fixedlocalhost/GUID/pre-existence guard/migrate onlyownedDB/cleanupguard trước chạy. Không dùng connection string app làm testtarget. SourceSha256 từ hàm Get-SourceFingerprint chuẩn repo: `AED953B3E7981B23BACA88314AE49EE1888706F390636E1853DC82A900EC07D9` (docs/artifacts/private excluded).

### Browser setup issue và cleanup — không che giấu giới hạn

Fixture GUID riêng `WeddingPlanTest_Review4CRepair_fa7cd982bbfd447e8cf02a4415e5e4d9`, marker+6fictionalactors, migrations test-only, API5023 với migrate/seed=false, Vite5183. Lần browser login đầu kế thừa `client/.env` VITE_API_URL localhost5008 nên synthetic email bị từ chối; không ghi nhận login thành công hoặc sửa dữ liệu main. Source LoginCommand chỉ SaveChanges sau credential hợp lệ. Đã sửa **process environment QA** VITE_API_URL=127.0.0.1:5023, không sửa .env của user. DirectHTTP login vào APItest thành công Customer, token không in/log/chia sẻ.

Sau đó browser tool bị ngắt khi quan sát form, không thao tác UI tiếp. Không có upload/thiệp/RSVP/contract thật được tạo. Đã dừng đúng sessions API95990 và Vite28600 (Vite cũ9224 cũng đã dừng trước), không kill process người dùng. Guarded harness drop đúng ownedDB thành công khoảng07:50VN; `Get-NetTCPConnection` không còn listener5023/5183. Closedmanifest/media/fictional audio retained trong ignoredartifacts; không xóa vật liệu của user. Tab QA là tab tạm không được mark deliverable/handoff; không khẳng định đã UI-close sau interruption. Không command test/build còn chạy ở lúc kết thúc. Runtime có warning EF20601 và MediatR devlicense hiện hữu; không đổi SQL/TLS/system settings để chữa.

## Còn lại — không tuyên bố hoàn thành toàn bộ dự án

1. Browser music với fixture mới: uploadvalid/invalid, lưu nháp/publish/reload, source replacement/remove, sharedEditor/Viewer, nativePlay/Pause/volume/error/retry, YouTubeblocked/mobile; actualHTTPRange/persistence-restart cũng chưa kiểm trong lượt này. Synthetic fixture là silence, không chứng minh chất lượng nhạc/thiết bị.
2. Role-specific sidebar/mobile drawer, navigation/MoneyInput fullkeyboard/mobile, latest combo/private/chat extended E2E. Reviewcatalog refresh đã có actualobserver regression, không làm lại implementation.
3. Password recovery/provider-only set-password/staff-first-password enforcement chưa có trọnluồng; SMTP/Google/provider live acceptance chưa đạt.
4. Phase5–7 và contract-specific finance adjustment còn thiếu; quyết địnhunsettled/refund/offset chưa đầy đủ. Không kích hoạt policy hoặc ghi đè original financial snapshots chỉ vì yêu cầu hoàn thiện chung.
5. Main migration16 trạng thái “chưa áp” là bàn giao trước, không query schema main mới. Không autoapply. Host/restore/load/production/deploy gates chưa đạt; chưa commit/push/deploy.

Git trước chốt docs75tracked/70untracked/0staged/1worktree; sau thêm report71untracked. HEAD/branch giữ nguyên, source fingerprint trên không bao gồm docs. Toàn bộ file untracked sẵn có phải đi theo handoff, không chỉ13source/tests mới.
