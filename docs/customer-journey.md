# Hành trình khách hàng và giỏ combo

## Luồng sử dụng

1. Khách mới đăng nhập bằng mật khẩu, OTP hoặc Google được chuyển tới 10 câu hỏi thiết lập. Chưa hoàn thành thì không thể đi thẳng vào tổng quan, duyệt dịch vụ hoặc combo. Tài khoản NCC/admin giữ luồng riêng.
2. Hoàn thành thiết lập sẽ lưu trạng thái vào DB, tạo checklist và dự trù một lần, rồi mở Tổng quan. Không còn nút “Sửa hồ sơ”; mở lại URL wizard sau khi hoàn thành cũng chuyển về Tổng quan. Tải lại hay đăng nhập lại không tạo lại kế hoạch. Hồ sơ cũ đã thiết lập được migration đánh dấu hoàn thành, không reset dữ liệu.
3. Tại Combo, chọn theo danh mục dịch vụ. Mỗi danh mục có một dịch vụ; chọn dịch vụ khác trong cùng danh mục sẽ thay lựa chọn cũ. Có thể chỉnh nâng cấp, tiện ích và số lượng theo đơn vị tính.
4. Mỗi combo chỉ thuộc **một NCC**. NCC tự tạo combo từ các dịch vụ của mình, gửi duyệt rồi khách mới thấy. Khách có thể tùy chỉnh các dịch vụ cùng NCC, không trộn NCC khác vào giỏ. Đổi NCC cần xác nhận bắt đầu giỏ mới; không xóa bản nháp/đơn đã lưu. Các combo nhiều NCC từ phiên bản cũ chỉ còn là lịch sử; muốn tiếp tục cần chọn lại một NCC thành giỏ mới. API cũng chặn giỏ nhiều NCC, kể cả không gửi template ID.
5. Giỏ đang chỉnh được giữ theo tài khoản trên trình duyệt. **Lưu bản nháp** lưu vào DB, chưa tạo yêu cầu cho NCC và chưa sửa ngân sách. Có thể tiếp tục nháp trong mục Combo của Tổng quan.
6. **Bước tiếp theo** mở phần kiểm tra dịch vụ/giá dự kiến, chính sách hủy, số điện thoại và ghi chú. **Gửi yêu cầu cho NCC** mới tạo một combo cùng các yêu cầu dịch vụ của NCC đó. NCC tiếp nhận và gửi báo giá trong **Hợp đồng & tư vấn**; khách trao đổi, duyệt rồi xác nhận hợp đồng ở bước riêng. Bấm bước tiếp theo hay gửi yêu cầu đều chưa thu tiền và chưa xác nhận hợp đồng.

## Ảnh NCC, danh mục và trang chi tiết combo

- Thẻ combo có ảnh bìa, ảnh đại diện/thông tin NCC và ảnh nhỏ của các hạng mục. Chỉ sử dụng ảnh thật đã lưu trên combo, hồ sơ NCC hoặc bài dịch vụ liên quan; URL lỗi sẽ thử ảnh liên quan tiếp theo rồi hiển thị trạng thái chưa có ảnh, không lấy ảnh NCC khác để thay thế.
- Thanh **Danh mục dịch vụ** vẫn ở bên trái trên desktop. Ở màn hình rộng tối đa 960px, thanh nằm ngang phía trên danh sách, có thể cuộn ngang và tự đưa danh mục đang chọn vào khung nhìn; không làm trang tràn ngang.
- **Xem chi tiết combo** mở `/combo/:templateId`, hỗ trợ mở trực tiếp/tải lại. Trang có album ảnh, các hạng mục được thêm sẵn, phần tùy chọn chưa tính giá, thông tin NCC, điều kiện hủy và liên kết bài dịch vụ. Tổng trên trang là giá cơ bản tham khảo, chưa cộng nâng cấp/dịch vụ đi kèm và chưa áp dụng ưu đãi; giỏ vẫn tính lại qua API.
- **Chọn combo & tùy chỉnh** đưa các hạng mục mặc định của một NCC vào giỏ và mở giỏ để tiếp tục. Nếu giỏ đã có dịch vụ, phải xác nhận trước khi thay. Giữ ngày cưới/khu vực/số khách; không xóa nháp đã lưu, không tự gửi yêu cầu hay thu tiền. Chỉ mở trang chi tiết không sửa giỏ.
- Public builder trả về danh sách trường công khai của NCC và tối đa 12 ảnh/bài dịch vụ. Không serialize thông tin tài khoản, liên hệ riêng, ngân hàng, thuế hay tài liệu nội bộ. Điểm đánh giá chỉ công bố khi có ít nhất 3 đánh giá đủ điều kiện.

## Combo đã chọn, tư vấn và hợp đồng

### Danh mục và phân trang (18/10/2026)

- `/combo` gọi `GET /api/combos/catalog` với danh mục, NCC, từ khóa, thứ tự và trang; 6 mục trên desktop, 4 trên điện thoại. Dịch vụ được `Count/Skip/Take` tại DB. Ảnh, biến thể và tiện ích chỉ tải cho trang hiện tại cùng các dịch vụ đang nằm trong giỏ/preset. Không làm mất giỏ khi đổi trang/bộ lọc. Combo mẫu chỉ có sắp xếp đề xuất/mới nhất; dịch vụ có thêm giá tăng/giảm. Metadata slot JSON của combo mẫu cũ được kiểm tra quyền sở hữu trước khi phân trang, không cam kết đây là truy vấn tối ưu cho danh mục cực lớn.
- `GET /api/combos/orders/my/page` áp dụng bộ lọc bản nháp/đang theo dõi/kết thúc/lưu trữ trước khi phân trang và luôn giới hạn chủ tài khoản. `/api/customer-orders?comboIds=...` tải ảnh/quyền thao tác chỉ cho các combo của trang, tối đa 10 ID; không cho xem combo của tài khoản khác.
- `GET /api/customer-orders/page?kind=lead|quote|contract` lọc tại API rồi phân trang theo **nhóm cùng NCC + cùng combo**, không cắt đôi combo giữa hai trang. Số đếm là số nhóm; mỗi nhóm giữ đủ dòng dịch vụ phù hợp bộ lọc. Không còn bộ lọc chỉ chạy trong một trang 50 dòng như trước.
- Liên kết thông báo có `focus` được API tìm đúng nhóm và trang, kể cả hồ sơ lưu trữ; UI cuộn/tô viền hồ sơ sau khi tải. Hồ sơ không thuộc tài khoản hoặc đã mất khả dụng chỉ hiện trạng thái an toàn. Khi người dùng tự đổi trang/bộ lọc, bỏ focus thông báo nhưng giữ bộ lọc hiện tại. Tab tư vấn/báo giá/hợp đồng nằm trong URL để mở trực tiếp.
- Sidebar danh mục và giỏ desktop nằm dưới navbar theo `--wm-header-offset`, có giới hạn chiều cao/vùng chứa. Mobile dùng danh mục ngang, không ép cột sidebar vào màn hình nhỏ. Những thay đổi này không thêm migration, không reset dữ liệu và không sửa quy tắc một NCC/đặt cọc/hợp đồng.

- **Combo của hai bạn** (`/customer/dashboard?tab=combos`) hiển thị thẻ gọn theo combo/NCC, ảnh bìa, ảnh từng dịch vụ, tổng đã lưu và tiến độ. Mặc định thu gọn các hạng mục dài; có xem thêm, chi tiết và album ảnh. Ảnh lấy từ combo/hồ sơ NCC/bài dịch vụ liên quan, không bịa ảnh cho mục chưa có. Album tư vấn, báo giá và hợp đồng cũng dùng ảnh NCC công bố; ảnh không thay thế phạm vi công việc trong báo giá/hợp đồng đã thống nhất.
- **Hợp đồng & tư vấn** (`?tab=contracts`) có các tab Tư vấn, Báo giá, Hợp đồng và Hỗ trợ. Dữ liệu chỉ gom khi cùng NCC và cùng combo; các dịch vụ vẫn giữ trạng thái, phiên bản báo giá, điều khoản, cọc/chứng từ và quyền xử lý riêng. Chat, duyệt báo giá, xác nhận hợp đồng và khiếu nại vẫn ở đúng bước riêng.
- **Lưu trữ / Khôi phục** chỉ ẩn/hiện trong tài khoản khách, không xóa bản ghi, không hủy dịch vụ, không giải phóng ngân sách và không giấu hồ sơ khỏi NCC. Không có nút xóa cứng lịch sử.
- Bản nháp được mở lại để chỉnh đúng bản nháp; chưa gửi NCC và chưa chiếm ngân sách. Mở nháp/yêu cầu để chỉnh cần xác nhận nếu sẽ thay giỏ trình duyệt có sẵn. Giữ cấu hình dịch vụ, ngày cưới, số khách và ghi chú; đổi NCC/chọn combo mới không kéo theo ghi chú hoặc liên kết chỉnh sửa cũ.
- **Sửa yêu cầu đã gửi** tạo bản điều chỉnh có nguồn và lý do (10–2000 ký tự), không sửa đè đơn cũ. Lưu nháp điều chỉnh chưa ảnh hưởng yêu cầu gốc. Chỉ khi gửi bản mới thành công, server mới thay thế/rút bản cũ, vô hiệu báo giá/ưu đãi cũ và chuyển dự trù an toàn trong cùng giao dịch. Các dòng ngân sách do khách sửa/đã chi hoặc không còn do hệ thống quản lý được giữ nguyên, kèm cảnh báo đối chiếu. Bản nháp điều chỉnh giữ nguồn và lý do khi mở lại.
- **Rút yêu cầu / Hủy nháp** cần lý do. Server kiểm tra dữ liệu thực, không chỉ nhãn trạng thái: có hợp đồng kể cả nháp/chờ xác nhận hoặc đã có khoản cọc/báo chuyển tiền thì chặn rút/sửa toàn combo. Từng tư vấn nằm trong combo không được rút/sửa độc lập để làm lệch gói. Combo đã chốt một phần dẫn sang hồ sơ hợp đồng để xử lý từng dịch vụ; combo nhiều NCC cũ vẫn giữ lịch sử nhưng không dùng để chỉnh trực tiếp.
- **Dừng chờ xác nhận** khác với hủy hợp đồng đã chốt. Hợp đồng chờ xác nhận chưa có ràng buộc tiền mới được dừng trực tiếp. Sau xác nhận/có nghĩa vụ tài chính, khách gửi **Đề nghị thay đổi / Đề nghị hủy**; lý do và nội dung đề xuất được lưu cùng lịch sử phản hồi.
- NCC xử lý trong tab **Thay đổi & hủy**; yêu cầu có nghĩa vụ tài chính cần Finance/SuperAdmin đối chiếu trong **Yêu cầu thay đổi / hủy**. Luồng `PendingVendor → PendingFinance` (khi cần) `→ Acknowledged/Rejected`. Nút “Tiếp nhận xử lý” chỉ ghi nhận yêu cầu phối hợp, **không tự sửa hợp đồng, phát sinh thanh toán hoặc hoàn tiền**. Nghĩa vụ tài chính được kiểm tra lại tại thời điểm NCC tiếp nhận. Khách xem được phản hồi của cả hai bên.
- Quyền thao tác lấy từ API theo chủ sở hữu và trạng thái mới nhất; giao diện không tự suy quyền dựa vào số tiền/nhãn. Yêu cầu trùng, nguồn đã thay thế, báo giá hết hiệu lực và các thao tác trên tư vấn đã dừng đều bị server chặn. Các tác vụ nền không tự giải phóng dự trù hoặc hủy hợp đồng khi đã có tiền/cọc cần đối chiếu. Mọi thay đổi quan trọng có audit và thông báo trong ứng dụng.

### Triển khai và API vòng đời

Migration SQL Server bổ sung `20261017051915_CustomerOrderLifecycle` tạo `CustomerOrderStates` và `OrderChangeRequests`, có khóa ngoại/index/concurrency version; không xóa/ghi đè dữ liệu lịch sử. Cần triển khai frontend và backend tương ứng, backup DB và áp dụng migration trước khi dùng. Chỉ Development cho phép áp dụng migration khi startup với `Database:ApplyMigrations=true`; production bắt buộc tắt và áp dụng qua quy trình deploy có kiểm soát. Giữ `Database:SeedDemoData=false`.

- `GET /api/customer-orders`: metadata ảnh, trạng thái lưu trữ, nguồn chỉnh sửa, lịch sử đề nghị và capabilities cho từng combo/tư vấn/báo giá/hợp đồng của khách.
- `POST /api/customer-orders/{kind}/{id}/archive|restore`; `POST .../{combo|lead}/{id}/withdraw`; `POST .../lead/{id}/revise`; `POST .../contract/{id}/stop-pending`; `POST .../{combo|lead|contract}/{id}/requests`.
- `GET /api/customer-orders/requests/vendor|finance`; `POST /api/customer-orders/requests/{id}/vendor-review|finance-review`. Phân quyền tương ứng Customer, VendorOwner, Finance/SuperAdmin; không cho truy cập hồ sơ người khác.
- Gửi combo qua API combo hiện có, thêm `RevisionOfId` và `RevisionReason` cho bản điều chỉnh. API tính giá nhận nguồn để xem trước thay dự trù, không ghi DB trong bước xem trước.

## Xếp bàn và ảnh thiệp

- Tự động xếp khách giữ nguyên những khách đã có bàn, lấp hết ghế trống của các bàn có sẵn và không vượt sức chứa. Ví dụ bàn 10 đã có 6, nhóm mới 7 người sẽ tách 4 người vào bàn đó và 3 người sang bàn sau. Hết chỗ thì phần dư ở danh sách chưa xếp.
- Với mô hình `WeddingGuest.PartySize`, nhóm được chia thành các dòng cùng tên/nhóm, tổng số người không đổi. Bản ghi gốc giữ liên kết RSVP duy nhất; không tạo thêm phản hồi RSVP. Chạy lại không tách/nhân bản nhóm đã xếp.
- Ảnh bìa và album thiệp dùng chọn tệp từ thiết bị, có xem trước; không yêu cầu khách nhập URL ảnh. Ảnh cũ vẫn hiển thị cho đến khi thay. Nhạc nay có chọn MP3 từ thiết bị hoặc video YouTube, nghe thử/thay/bỏ rồi Lưu nháp hoặc Xuất bản để áp dụng. Tải lỗi không thay bản nhạc đang lưu; bỏ nhạc không xóa tệp cũ. Bản đồ vẫn nhập URL.
- MP3 tối đa12MB/tệp,20tệp/100MB mỗi hồ sơ (giới hạn kỹ thuật); chỉ người có quyền chỉnh hồ sơ được tải. Tệp lưu ở `Uploads:InvitationAudioRoot` ngoài publish; SQL giữ đường dẫn trong MusicUrl. YouTube lưu URL/videoID xác định từ URL, chỉ nhúng khi người xem bấm mở, không tự phát/tải/tách âm thanh; có link dự phòng nếu video bị chặn. Nội dung thiệp/RSVP không phụ thuộc nhạc. Nhạc được chia sẻ công khai qua URL như ảnh; chỉ tải nội dung có quyền sử dụng. Backup cần cả thư mục nhạc và DB; xem [lưu trữ upload](upload-storage.md).
- Upload nhận JPG/PNG/WebP tối đa 8 MB/tệp, album tối đa 12 ảnh, giới hạn mỗi hồ sơ 200 ảnh/200 MB. Máy chủ kiểm tra loại/kích thước, loại metadata vị trí/văn bản và không nhận SVG/ảnh động. Chỉ người có quyền chỉnh hồ sơ được tải; thiệp chỉ được gắn ảnh trong vùng lưu trữ của chính hồ sơ chung.
- Ảnh lưu ở `Uploads:InvitationRoot`, mặc định `src/Wedding.WebApi/App_Data/invitation-uploads` khi chạy local với content root dự án. Bản deploy IIS dùng thư mục bền vững ngoài gói publish `../private/uploads/invitations`; ảnh dịch vụ dùng `Uploads:ListingRoot=../private/uploads/listings`. Nếu dùng container, gắn persistent volume và cấu hình các root tương ứng. SQL Server giữ đường dẫn; backup/restore cần cả DB và thư mục ảnh. Không đưa ảnh khách hàng vào Git hoặc Docker image. Xem [quy trình deploy môn học](study-deployment.md).

## Ngân sách và dữ liệu

- Khoản dự trù do wizard tạo mới có khóa nguồn `BudgetItem.PlanTemplateKey`. Chỉ khoản có nguồn rõ ràng, chưa sửa/chưa chi/chưa liên kết mới được dùng thay dự trù mẫu khi gửi combo; khoản khách đã sửa hoặc dữ liệu cũ không có nguồn được giữ nguyên và có cảnh báo kiểm tra trùng.
- Không đoán nguồn chỉ bằng tên/số tiền. Migration chỉ thêm trường nullable, không tự gắn nhãn hoặc ghi đè ngân sách cũ.
- Hợp đồng liên kết sử dụng lại dòng ngân sách combo còn do hệ thống quản lý, tránh cộng chi phí lần nữa. Nếu khách đã tự sửa dòng đó thì giữ nguyên và yêu cầu đối chiếu thay vì ghi đè.
- Giá được kiểm tra lại khi gửi, bao gồm số khách, đơn vị tính, vùng phục vụ, sức chứa, tùy chọn và sự đồng ý ưu đãi của NCC. Không thu tiền trong giỏ.
- SQL Server cần áp dụng `CustomerOnboardingJourney` và `AddBudgetProvenance` trước khi chạy phiên bản này. Không xóa DB hoặc seed lại dữ liệu thực.

## Kiểm thử local

`tools/Wedding.QaCatalog` chỉ chấp nhận DB kiểm thử `WeddingPlanUi_0161333`. Các NCC/dịch vụ có tiền tố `[DEMO]` là dữ liệu hư cấu, không đặt dịch vụ hay chuyển tiền. Utility không được gọi trong API startup hoặc migration.

Frontend: `npm run build`, `npm test`, `npm run lint` trong `client`.

Backend: `dotnet test tests/Wedding.Tests/Wedding.Tests.csproj`. Các test SQL Server chỉ chạy khi đặt `WEDDING_TEST_SQLSERVER=1`, tạo DB test tên riêng và không dùng DB khách hàng. Dự án hiện target .NET 10 LTS, cần SDK/runtime .NET 10 tương ứng; không cần `DOTNET_ROLL_FORWARD=Major`. Các số nghiệm thu bên dưới là lịch sử từng đợt; kết quả deploy mới xem [quy trình deploy môn học](study-deployment.md).

Lần nghiệm thu trước khi đổi quy tắc một NCC: 12 test frontend và 73 test backend đạt, đã kiểm tra giỏ → lưu nháp → mở lại → gửi báo giá và tránh cộng trùng ngân sách trên SQL Server riêng. Quy tắc phối nhiều NCC ở lần đó đã được thay bằng quy tắc một NCC nêu trên; không xóa dữ liệu lịch sử.

Nghiệm thu sau thay đổi một NCC: 18 test frontend và 88 test backend đạt, gồm SQL Server; build thành công. Kiểm tra trình duyệt trên tài khoản QA riêng: URL wizard đã hoàn tất chuyển về Tổng quan; bàn 6/10 nhận thêm 4 từ nhóm 7, bàn sau nhận 3, tổng 13 người; tự xếp lần hai trả 0. Ảnh PNG hư cấu tải lên, lưu vào ảnh bìa/album của thiệp nháp và hiển thị lại đúng; không xuất bản thiệp. API tính giá từ chối giỏ nhiều NCC, public builder chỉ còn 3 combo một NCC trong fixture cũ. Đã chọn combo một NCC và mở “Bước tiếp theo: Kiểm tra & liên hệ”, dừng trước gửi yêu cầu; không phát sinh yêu cầu/hợp đồng/thanh toán mới trong lượt UI này.

Nghiệm thu bổ sung giao diện combo: 26 test frontend và 91 test backend đạt, gồm SQL Server; build frontend/backend thành công, lint không có lỗi (còn 10 cảnh báo có sẵn ở các phần khác). Đã kiểm tra trực quan trên desktop 1265px và khung iframe cùng origin 390px/768px: ảnh NCC/hạng mục, danh mục ngang, album chuyển ảnh/Escape, tải lại đường dẫn chi tiết, chọn gói vào giỏ và bước kiểm tra. Giỏ QA giữ đúng Hồ Chí Minh, 201 khách, ngày 20/01/2027; giỏ có sẵn hiển thị xác nhận thay thế và nút giữ giỏ hoạt động. Dừng trước gửi yêu cầu, không tạo RFQ/hợp đồng/thanh toán mới. Khung HTML responsive chỉ phục vụ kiểm thử tạm thời và không đưa vào bản build bàn giao.

Nghiệm thu vòng đời và giao diện hồ sơ ngày 17/10/2026: **43/43 test frontend, 111/111 test backend đạt, không skip; có SQL Server**. Build frontend/backend thành công; lint 0 lỗi, còn 10 cảnh báo có sẵn. Đã kiểm tra desktop 1265px và iframe 390px: thẻ combo/hợp đồng có ảnh từng dịch vụ, album chuyển ảnh/Escape, mở nháp giữ cấu hình/ghi chú, mở yêu cầu đã gửi có banner bản điều chỉnh và chặn gửi/lưu khi thiếu lý do, quay lại không phát lại lệnh mở cũ; các tab Tư vấn/Báo giá/Hợp đồng và hộp thoại dừng chờ xác nhận hiển thị đúng. Khung HTML QA đã gỡ và không nằm trong build bàn giao.

`tools/Verify-CustomerOrdersQa.ps1` mặc định chỉ đọc metadata trên API 5009 và xác thực process dùng chính xác DB `WeddingPlanUi_0161333` trước khi ghi. `-ApplyFixtures` mới thêm fixture hư cấu có nhãn QA (có kiểm tra tránh tạo lại): một nháp, một combo tư vấn hai dịch vụ, hai báo giá, một hợp đồng **chờ xác nhận, chưa ký**. Đã kiểm tra lưu trữ/khôi phục tồn tại sau tải lại bằng API và hoàn thành vòng lưu trữ → lọc lưu trữ → khôi phục qua giao diện trên bản nháp hư cấu; ảnh, dịch vụ và trạng thái được giữ nguyên. Không xác nhận hợp đồng, không tạo thanh toán/hoàn tiền. Chỉ migration cộng thêm được áp dụng lên DB QA riêng; không reset/seed lại hoặc chạy utility lên DB khách hàng.
