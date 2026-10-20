export function ComboNextSteps({ sent = false }: { sent?: boolean }) {
  return (
    <section
      className="combo-process"
      aria-label="Quy trình sau khi chọn combo"
    >
      <h3>
        {sent ? "Tiếp theo, bạn cần làm gì?" : "Sau bước này sẽ thế nào?"}
      </h3>
      <ol>
        <li>
          <strong>1. Gửi yêu cầu cho NCC</strong>
          <span>
            NCC nhận các dịch vụ và mong muốn bạn đã chọn. Chưa đặt dịch vụ.
          </span>
        </li>
        <li>
          <strong>2. NCC gửi báo giá</strong>
          <span>
            Trao đổi phạm vi công việc, lịch còn trống và báo giá từng dịch vụ
            trong “Hợp đồng & tư vấn”.
          </span>
        </li>
        <li>
          <strong>3. Bạn kiểm tra và quyết định</strong>
          <span>
            Duyệt báo giá phù hợp, rồi xác nhận hợp đồng và điều kiện cọc ở bước
            riêng. Không tự động thu tiền.
          </span>
        </li>
      </ol>
    </section>
  );
}
