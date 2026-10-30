import { apiError } from "../api";

export const orderReadBlockedMessage = "Chưa kiểm tra được trạng thái mới nhất. Hãy tải lại hồ sơ trước khi thực hiện thao tác; thông tin bạn đang nhập vẫn được giữ.";

/** A failed read is never an empty order history or permission to create a replacement. */
export function OrderReadError({ label, error, retry, busy = false }: {
  label: string; error: unknown; retry: () => unknown; busy?: boolean;
}) {
  return <div className="co-error" role="alert">
    <p>Chưa tải được {label}. {apiError(error)}</p>
    <p className="mt-2">Chưa thể xác định dữ liệu hiện tại; thông báo này không có nghĩa hồ sơ của bạn đã bị xóa.</p>
    <button type="button" className="co-button secondary mt-3" disabled={busy} onClick={() => { void retry(); }}>{busy ? "Đang thử lại..." : "Thử lại"}</button>
  </div>;
}
