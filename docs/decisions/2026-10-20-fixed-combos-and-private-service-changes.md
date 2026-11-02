# Dịch vụ thống nhất, combo cố định và điều chỉnh nội dung riêng

Nguồn: bản “REFACTOR VENDOR SERVICES & COMBOS” và câu trả lời người dùng ngày20/10/2026. Deploy đang tạm dừng.

## Quyết định được áp dụng

1. My Services là nơi tạo dịch vụ duy nhất. Tái sử dụng Listing để giới thiệu/kiểm duyệt và ServiceItem làm cấu hình giá; `Listing.PricingServiceItemId` chỉ rõ cấu hình chính. Không tạo bảng dịch vụ thứ ba.
2. Giá, variant và add-on nằm trong cùng bản đề xuất Listing, một lượt gửi/duyệt. Không sao chép dịch vụ khi tạo combo; một nguồn có thể dùng nhiều combo.
3. Combo mới của Vendor dùng `FixedPrice`, ít nhất hai dịch vụ độc lập Approved+Active của chính shop. Cho phép hai dịch vụ khác nhau cùng danh mục. Giá nguyên gói áp dụng cho đúng thành phần đã duyệt.
4. Khách không tự thêm/bớt/đổi thành phần rồi vẫn áp giá công khai. Thay đổi được bàn trong chat theo Lead đang có; NCC gửi đề xuất nội dung riêng và khách xác nhận. Admin/Moderator chỉ kiểm duyệt nội dung public, không duyệt đề xuất nội dung riêng.
5. Đề xuất riêng là lịch sử bổ sung, không ghi đè dịch vụ/combo public hoặc order/contract ban đầu. Gửi, rút, từ chối và đồng ý đều lưu người/thời điểm/audit; chỉ bản Accepted mới là lớp nội dung đang áp dụng riêng.
6. Phạm vi triển khai hiện tại của đề xuất riêng là **cùng tổng tiền**, không thay tiền cọc, VAT, hoa hồng, thanh toán hay quyết toán. Điều chỉnh tài chính/hoa hồng hợp đồng cũ là quy trình khác, chưa được coi là hoàn thành ở module này.
7. Khi sang hợp đồng, không tự copy cả combo vào từng hợp đồng dịch vụ. Hiển thị nội dung combo đã đồng ý để đối chiếu; báo giá/hợp đồng lập trước thay đổi nội dung cần NCC phát hành lại và khách xác nhận. Đề xuất combo đang chờ phải được xử lý/rút trước khi chốt hồ sơ.

## Tương thích và giới hạn an toàn

- Legacy Dynamic, các cấu hình giá cũ, variant/add-on, order/contract và snapshot được giữ nguyên. Không tự gộp legacy nhiều cấu hình: NCC chọn rõ cấu hình chính khi chỉnh sửa.
- Combo FixedPrice mới hiện chỉ nhận dịch vụ đơn vị Package (mỗi nguồn một gói). Không tự suy số bàn/khách/giờ hoặc mở rộng biến thể trong lần refactor này. Đơn vị động cũ vẫn hoạt động qua legacy.
- Tổng giá bán lẻ và nội dung/ảnh/phạm vi nguồn được chụp tại bản combo gửi duyệt. Nguồn thay đổi thì combo giữ snapshot cũ và bị giữ khỏi bán mới đến khi rà soát/duyệt lại, không tự đổi giá bán hay giao dịch cũ.
- Chỉ hiển thị tiết kiệm khi giá combo thấp hơn giá bán lẻ. Giá bằng không/âm bị từ chối. Gói có giá vượt tổng bán lẻ có thể lưu/duyệt nội dung nhưng không checkout bằng phép phân bổ giảm giá âm; cần báo giá/quy tắc phân bổ riêng được xác nhận.
- Phân bổ ưu đãi VND dùng cơ chế hiện hữu theo tỷ lệ giá trị, giữ chính xác tổng VND; không tạo hoa hồng ở cấp combo rồi thu lại ở cấp dịch vụ. Không thay công thức commission/basis đã thống nhất.
- Vendor cũ giữ mức cũ đến Admin xác nhận. Hợp đồng chốt giữ nguyên snapshot. Ba điều kiện Admin duyệt + ngày hiệu lực + ghi nhận ký ngoài hệ thống và ngoại lệ tài chính hợp đồng đang thực hiện vẫn thuộc Phase5–7, không bị thay thế bởi đề xuất nội dung này.

## Dữ liệu

- Migration14 `FixedVendorPackages`: sáu cột bổ sung/nullable hoặc mặc định Dynamic, không tự merge/backfill dữ liệu dịch vụ.
- Migration15 `PrivateServiceAgreements`: bảng mới, FK NoAction và sequence unique; response concurrency token; nội dung gốc/lịch sử không được sửa/xóa qua ứng dụng.
- Không áp dụng Down migration, reset/seed database người dùng hoặc public deploy trong lượt này.
