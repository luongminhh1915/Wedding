# Quyết định bổ sung — commission basis và điều kiện kích hoạt

Nguồn: câu trả lời trực tiếp của người dùng trong chat ngày 19/10/2026, sau khi được hỏi về basis và activation. Đây là quyết định nghiệp vụ đã xác nhận; không phải suy diễn từ giao diện, text Vendor hoặc dữ liệu seed.

## 1. Dịch vụ thông thường

`Commission Base = Giá trị dịch vụ trước VAT sau giảm giá hợp lệ do Vendor chịu − Khoản chi hộ hợp lệ đã được Admin xác minh/chấp thuận.`

`Commission Amount = Commission Base × Commission Rate.`

- Phải phân biệt giá bao gồm VAT và giá trước VAT; không trừ VAT hai lần.
- Platform-funded discount không tự giảm base nếu Vendor vẫn được thanh toán đầy đủ.
- Chi phí vận hành/nhân sự/kinh doanh nội bộ không mặc nhiên là khoản chi hộ.
- Khoản chi hộ cần thông tin/chứng từ và Admin xác nhận. Vendor khai báo không đủ để tự giảm base.
- Base không âm. Không đoán VAT/rate hoặc tiền chi hộ khi giao dịch thiếu dữ liệu.

## 2. Tổ chức cưới trọn gói A–Z

- Default12% chỉ tính trên phí quản lý/tổ chức/điều phối thực tế đã được Admin xác nhận.
- Gói một giá all-in cần tách/xác định phần commission basis và được Admin phê duyệt trước áp dụng.
- Không lấy toàn ngân sách cưới hay toàn giá hợp đồng A–Z làm base mặc định. Text mô tả phí trong Listing metadata không phải phê duyệt tài chính.

## 3. Combo

- Không thu trùng parent combo và component trên cùng giá trị.
- Thành phần có các rate khác nhau phải phân bổ giá trị/giảm giá minh bạch theo từng dịch vụ.
- Không tự áp một rate chung cho toàn combo khi chưa có chính sách riêng được Admin phê duyệt.
- Cách phân bổ cụ thể khi dữ liệu cũ không đủ vẫn cần model và xác nhận thích hợp; không tự đặt số liệu. Giữ pricing/customer discount hiện hữu trong module nội dung/lifecycle; không ngầm đổi nó thành commission allocation mới.

## 4. Historical snapshots / ngoại lệ riêng hợp đồng

- Hợp đồng đã chốt trước effective policy/formula mới giữ nguyên snapshot đã xác nhận; không tự tính lại.
- Ngoại lệ riêng hợp đồng đang thực hiện tuân thủ decision `2026-10-19-legacy-and-contract-commission-adjustments.md`: Vendor yêu cầu → thương lượng → Admin phê duyệt → lưu append-only history → activation riêng đủ điều kiện, xử lý chênh lệch qua adjustment/credit/refund phù hợp, không ghi đè payment/snapshot gốc.
- Chưa xác nhận chi tiết unsettled allocation/offset-vs-refund; thiếu thông tin thì không kích hoạt chênh lệch quyết toán tự động.

## 5. Dữ liệu bắt buộc cho giao dịch mới theo công thức mới

Lưu rõ giá trị dịch vụ, từng discount và bên tài trợ, VAT được loại trừ/nền giá, chi hộ được Admin duyệt, base, rate, amount, policy/formula version, thời điểm chốt và người phê duyệt ngoại lệ. VAT Vendor–Customer khác VAT trên phí hoa hồng nền tảng–Vendor; không dùng một trường để trừ lẫn hai loại.

Nếu schema/luồng hiện tại chưa đủ dữ liệu, bổ sung cấu trúc và quy trình xác nhận. Fail-closed cho việc chốt snapshot theo công thức mới, không tự bỏ qua VAT hoặc khấu trừ chi hộ chưa xác minh.

## 6. Ba điều kiện kích hoạt — đã chốt

Chính sách mới chỉ kích hoạt khi **đồng thời**:

1. Admin đã phê duyệt.
2. Đã tới ngày hiệu lực.
3. Hợp đồng/phụ lục liên quan đã được ghi nhận ký ngoài hệ thống theo quy trình.

Approval/request/chat khác Active rule. View/acknowledgment không được coi là ký pháp lý. Giữ original snapshot và không tác động các hợp đồng khác khi xử lý ngoại lệ một hợp đồng.
