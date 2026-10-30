# CONFIRMED — ngân sách cá nhân của khách hàng

Ngày: 21/10/2026, Asia/Ho_Chi_Minh. Nguồn: user trả lời câu hỏi về IsPaid/cọc: “phần ngân sách mang ý nghĩa cá nhân hoá cho khách hàng thôi, chi phí bên ngoài có thanh toán thì khách hàng sẽ tự điền số tiền mình chi trong khoản đã cọc, và số tiền còn lại là bn đấy cho người dùng dễ mường tượng”.

## Ý nghĩa đã xác nhận

1. Ngân sách là công cụ lập kế hoạch/theo dõi cá nhân của khách, không phải xác minh thanh toán, công nợ hay đối soát giữa Customer–Vendor/nền tảng.
2. Khách tự ghi các khoản chi/tiền cọc đã trả, bao gồm chi phí bên ngoài hệ thống, để hình dung số còn lại. Số tự ghi không xác nhận hoặc làm thay đổi payment/commission/contract/settlement.
3. Tái sử dụng PlannedCost và ActualCost: ActualCost là **tổng đã ghi chi, bao gồm cọc**, không cộng thêm một số cọc lần nữa. Khi trả thêm, khách cập nhật tổng mới. UI thể hiện còn theo dự toán; nếu chi vượt thì báo phần vượt, không biến thành “đã thanh toán đủ” pháp lý.

## Phạm vi triển khai, không suy rộng quyết định

- Chỉ đổi presentation/model frontend và hướng dẫn nhập: dự toán, đã chi/đã cọc, còn theo dự toán, trạng thái ghi chi; payload numeric/API/schema giữ nguyên. Không backfill hoặc sửa dữ liệu đã lưu.
- Không đổi ý nghĩa toàn cục hoặc rewrite BudgetItem.IsPaid: hiện cờ này còn dùng bảo vệ khoản dự toán đã chỉnh/chi trong combo synchronization, không phải ledger. UI không lấy IsPaid làm bằng chứng trả đủ.
- ConfirmContract/FinancialContractService đang có luồng tự tạo/đồng bộ khoản liên kết HĐ. User chỉ xác nhận chi ngoài hệ thống tự nhập; **chưa yêu cầu bỏ toàn bộ linkage hoặc đổi automatic sync**. Giữ hành vi đó, không tự sửa lịch sử hay cam kết mọi khoản đều do khách nhập tay.
- Số còn theo từng khoản là max(PlannedCost − ActualCost, 0), phần vượt hiển thị riêng. Tổng quỹ vẫn theo công thức hiện có: ngân sách hồ sơ hoặc tổng dự toán − tổng ActualCost; tổng âm vẫn cảnh báo vượt quỹ. Đây là phép trình bày kế hoạch, không công thức tài chính giao dịch mới.
- Các chính sách VAT/basis/rate/activation/snapshot/adjustment đã chốt không bị thay đổi. Không mở Phase5–7 hoặc deploy.

## Supersedes có giới hạn

Làm rõ blocker “cần xác nhận ngân sách đã chi/đã thanh toán đủ” ở report Customer business và handoff ngay trước trả lời này: không chọn phương án ép IsPaid thành “trả đủ”. Chuyển sang UX sổ cá nhân, không sửa flag/financial history. Giữ nguyên lịch sử audit và kết quả test trước; CONFIRMED không tự có nghĩa browser/build đã VERIFIED.
