# Quyết định: Hoa hồng NCC cũ và điều chỉnh riêng hợp đồng

Ngày xác nhận: 19/10/2026 (Asia/Saigon). Nguồn: yêu cầu tiếp tục triển khai do người dùng gửi ngày 19/10/2026, Phần 2. Đây là quyết định nghiệp vụ đã được người dùng xác nhận, bổ sung Master Prompt; không phải cơ chế tài chính mới đã triển khai đầy đủ.

## 1. Quyết định đã chốt

### NCC cũ

- Giữ nguyên tỷ lệ hoa hồng chung hiện có của shop, kể cả đối với giao dịch mới. Nâng cấp schema hoặc danh mục không tự chuyển NCC cũ sang chính sách theo danh mục.
- Chỉ chuyển khi Admin xác nhận chuyển đổi hoặc phê duyệt chính sách mới. Phạm vi và ngày hiệu lực phải được ghi nhận; không lấy ngày deploy làm ngày chuyển đổi.
- Việc bảo toàn tỷ lệ cũ không được diễn giải thành bằng chứng rằng tỷ lệ đó từng được thương lượng/phê duyệt. Không tự tạo hồ sơ phê duyệt, hợp đồng hay người phê duyệt giả cho dữ liệu cũ.

### NCC đăng ký mới

- Chọn một hoặc nhiều nhóm kinh doanh; lấy tỷ lệ mặc định từ backend, không nhận tỷ lệ tự đặt từ trình duyệt.
- Phạm vi hoạt động của shop khác danh mục của từng dịch vụ. Không sao chép một tỷ lệ duy nhất của nhóm đầu tiên thành tỷ lệ áp dụng cho toàn shop.
- Trong Phase 2, `Vendor.CommissionRate = 0` ở đường đăng ký mới là dấu hiệu kỹ thuật **kế thừa chính sách danh mục**, không phải miễn/thu hoa hồng 0%. Đường tạo Vendor cũ vẫn kiểm tra và bảo toàn tỷ lệ dương hiện hữu.
- Mặc định 12% của nhóm A–Z không cho phép tính trên toàn bộ ngân sách hoặc khoản chi hộ. Khi chưa có cơ sở tính được chấp thuận, phải ngăn việc xác nhận giao dịch mới theo tỷ lệ này; không tự suy diễn phí quản lý từ tổng giá gói.

### Thời điểm chốt chính sách

- Mốc chọn chính sách là lúc hợp đồng được xác nhận chính thức (`ConfirmedAt`), không phải ngày tạo hợp đồng/dự án.
- Chỉ xét chính sách thuộc đúng phạm vi và đã có hiệu lực tại mốc đó. Nếu ngày hiệu lực muộn hơn ngày Admin phê duyệt, dùng ngày hiệu lực.
- Khi xác nhận, giữ snapshot hoa hồng ban đầu. Chính sách mới không tự sửa các giao dịch đã chốt, dù chúng vẫn đang thực hiện.

## 2. Ngoại lệ có kiểm soát: Điều chỉnh riêng một hợp đồng

Vendor có thể yêu cầu điều chỉnh cho hợp đồng Customer–Vendor đang thực hiện và đủ điều kiện. Đây là một hồ sơ riêng, không phải thay đổi trực tiếp snapshot hoặc áp dụng hồi tố chính sách toàn shop.

Luồng đã được yêu cầu:

`Vendor gửi yêu cầu → Moderator tiếp nhận → trao đổi/counteroffer → Admin quyết định → lưu thỏa thuận/phụ lục nếu cần → kích hoạt điều chỉnh đúng hợp đồng và phạm vi`.

- Moderator hỗ trợ và đề xuất; quyết định cuối cùng thuộc Admin theo phân quyền hiện tại.
- Chấp nhận trong chat, gửi counteroffer hoặc đánh dấu đã xem tài liệu không được tự kích hoạt một tỷ lệ mới.
- Hồ sơ cần mã yêu cầu, hợp đồng đích, người tạo/phụ trách/phê duyệt, trạng thái, lý do, lịch sử trao đổi, tỷ lệ ban đầu/đề xuất/được duyệt, cơ sở tính, phạm vi, thời điểm hiệu lực và tài liệu liên quan.
- Form phải lấy số liệu hiện tại từ backend: hoa hồng ban đầu, đang có hiệu lực trên hợp đồng, chính sách hiện hành của Vendor và tình trạng thanh toán/quyết toán. Không nhận các giá trị đó như sự thật do client cung cấp.
- Phê duyệt hợp đồng A không sửa hợp đồng B hoặc chính sách toàn shop.

### Hai phạm vi được yêu cầu

| Phạm vi | Ý nghĩa đã xác nhận | Giới hạn |
|---|---|---|
| `WholeContract` | Tính lại đối với toàn bộ giá trị thuộc cơ sở tính đã thỏa thuận | Nếu đã có quyết toán, chênh lệch phải có nghiệp vụ minh bạch; không ghi đè chứng từ cũ. |
| `UnsettledOnly` | Giữ phần đã quyết toán; mức mới chỉ áp dụng cho phần còn lại theo thỏa thuận | Mặc định ưu tiên khi đã phát sinh quyết toán hoa hồng; Admin phê duyệt phạm vi cuối cùng. |

`WholeContract` và `UnsettledOnly` là tên thiết kế dự kiến, chưa phải enum/API được bàn giao. Chưa được tự quy đổi “phần chưa quyết toán” thành toàn bộ K2 hoặc `ContractValue - DepositAmount`.

Hợp đồng đã hoàn tất **và** quyết toán toàn bộ không tham gia luồng điều chỉnh dành cho hợp đồng đang thực hiện. Trường hợp đặc biệt cần quy trình phê duyệt tài chính riêng. Không tự mở rộng quy tắc này thành cho phép mọi hợp đồng Completed nhưng chưa quyết toán đi qua luồng thường; cần làm rõ điều kiện hợp lệ.

## 3. Bảo toàn và biểu diễn dữ liệu

Phải phân biệt các khái niệm sau; không dùng một field thay cho tất cả:

| Khái niệm | Dữ liệu cần bảo toàn |
|---|---|
| Original Commission | Snapshot lúc xác nhận: tỷ lệ, cơ sở/giá trị tính và chính sách nguồn khi được triển khai; bất biến sau khi chốt. |
| Effective Commission | Kết quả có hiệu lực của snapshot gốc và những điều chỉnh đã được phê duyệt/kích hoạt hợp lệ; có thể khác theo phần giao dịch. |
| Commission Adjustment | Bản ghi bổ sung, append-only: yêu cầu nguồn, người duyệt, lý do, phạm vi, thời điểm hiệu lực, trước/sau, phần chênh lệch và thỏa thuận liên quan. |
| Commission Settlement / Payment | Chứng từ, phân bổ tiền và lịch sử đã quyết toán/thanh toán; không xóa hay sửa âm thầm để làm khớp một tỷ lệ mới. |

- Không thay `CommissionRateSnapshot` cũ bằng 7% khi phê duyệt ngoại lệ 10% → 7%.
- Không sửa số tiền đã trả, tham chiếu ngân hàng, lịch sử webhook hoặc bảng kê đã chốt để giả lập điều chỉnh.
- Khi có chênh lệch cần bù trừ/credit/hoàn trả, lưu nghiệp vụ bổ sung liên kết nguồn và chứng từ thực tế theo công thức được xác nhận. Phê duyệt tỷ lệ không đồng nghĩa đã chuyển/hoàn tiền.
- Mỗi hành động quan trọng cần actor, thời gian, lý do, audit và kiểm tra ownership/role tại backend; xử lý nguyên tử và chống áp dụng trùng.
- Không xóa lịch sử hợp đồng, không backfill số liệu tài chính còn thiếu bằng tỷ lệ đang có hiệu lực hôm nay.

## 4. Các câu hỏi chưa được xác nhận — không tự đặt công thức

1. Cơ sở hoa hồng của dịch vụ thường là trước/sau giảm giá, đã gồm VAT hay chưa? Với A–Z, ai xác nhận phí tổ chức/quản lý và giá trị loại trừ chi hộ? Với combo nhiều danh mục, phân bổ ưu đãi và cơ sở theo dòng hay dùng một thỏa thuận riêng?
2. `UnsettledOnly` tính trên những phân bổ nào khi thanh toán một phần, escrow đang khóa, bảng kê đã chốt nhưng chưa thu, tiền trả thừa hoặc dispute? “Đã thanh toán” và “đã quyết toán” không được tự coi là đồng nghĩa.
3. Chênh lệch do `WholeContract` được giữ credit cho kỳ sau hay hoàn tiền thật; ai phê duyệt, giới hạn và chứng từ nào? Không mặc định dùng credit-note hủy hợp đồng cho nghiệp vụ mới này.
4. Có bắt buộc hợp đồng/phụ lục hợp tác đã ký và Active trước khi kích hoạt rule/adjustment, hay Admin có thể kích hoạt độc lập trong một số trường hợp? Ngày hiệu lực phải xử lý thế nào khi tài liệu được ký sau?
5. Giữ hay thay khung 3–12%, chia K1/K2 50/50 và cửa sổ khóa K2 hiện hữu đối với rule mới? Chưa có xác nhận thay đổi nên giữ hành vi hiện tại, không tự áp dụng quy tắc mới.
6. Hợp đồng đã Completed nhưng chưa quyết toán toàn bộ có phải dùng luồng tài chính riêng không? Cần xác nhận trước khi mở điều kiện gửi yêu cầu.

## 5. Trạng thái triển khai và thứ tự phụ thuộc

- **Phase 2:** bổ sung nhóm kinh doanh, membership đa nhóm, acknowledgement chính sách, đăng ký mới kế thừa danh mục; bảo toàn NCC cũ và guard không tự tính 12% A–Z khi thiếu cơ sở. Kiểm thử và migration được báo cáo riêng, không được suy ra hoàn thành chỉ từ tài liệu này.
- **Phase 5 — chưa triển khai bởi quyết định này:** rule engine theo scope/thời gian, quản lý Admin, snapshot đầy đủ, original/effective/append-only adjustment và đối soát chênh lệch.
- **Phase 6 — chưa triển khai bởi quyết định này:** hồ sơ thương lượng toàn shop/danh mục/dịch vụ/combo và yêu cầu riêng hợp đồng, chat/counteroffer, Moderator review, Admin approval. Tái sử dụng transport nhưng không nhầm chat Lead với hồ sơ thương lượng.
- **Phase 7 — chưa triển khai bởi quyết định này:** hợp đồng hợp tác Admin–Vendor, phụ lục, private documents/PDF, immutable versions, liên kết rule/adjustment và kích hoạt theo điều kiện được xác nhận.

Không có tài liệu/hồ sơ, nút UI, chat hoặc bản ghi “Approved” đơn lẻ nào là bằng chứng rằng một rule/adjustment đang có hiệu lực. Phải kiểm tra trọn luồng frontend–API–database, phân quyền, dữ liệu sau tải lại và các test tương ứng trước khi đánh dấu hoàn thành.
