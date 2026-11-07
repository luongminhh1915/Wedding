import { ComboDialog } from "../journey/combo/ComboDialog";
import type { BulkModerationProposal } from "./bulkModerationModel";

export function BulkModerationConfirmationDialog({ proposal, busy, error, onCancel, onConfirm }: {
  proposal: BulkModerationProposal; busy: boolean; error?: string; onCancel: () => void; onConfirm: () => void;
}) {
  return <ComboDialog title={proposal.mode === "automatic" ? "Bật tự động duyệt trong phiên này?" : "Xác nhận duyệt hàng loạt"} onClose={() => { if (!busy) onCancel(); }}>
    <div className="space-y-4 text-sm text-[#233a42]">
      <p>{proposal.type === "listing" ? "Phạm vi: tất cả bài giới thiệu dịch vụ đang chờ duyệt trên hệ thống." : "Phạm vi: bài giới thiệu dịch vụ, dịch vụ thành phần và combo đang chờ duyệt trên toàn hệ thống, không giới hạn theo bộ lọc đang xem."}</p>
      <p role="alert" className="rounded-xl border border-amber-300 bg-amber-50 p-3 text-amber-950">Bao gồm nội dung của nhà cung cấp chưa được xác minh giấy tờ. Xác nhận có thể đưa nội dung hợp lệ ra công khai mà không xem từng mục.</p>
      <p>Mỗi lượt xử lý tối đa 20 mục cho mỗi loại. Các điều kiện hợp lệ, phiên bản và quyền kiểm duyệt vẫn được máy chủ kiểm tra; nội dung không hợp lệ sẽ được bỏ qua và báo số lượng.</p>
      {proposal.mode === "automatic" && <p>Chỉ hoạt động khi màn hàng chờ này đang mở và đúng phiên tài khoản hiện tại. Mỗi lượt cách ít nhất 3 giây; tạm dừng khi gặp lỗi hoặc mục cần xem lại. Đóng màn, đăng xuất hay đổi tài khoản sẽ dừng. Không lưu chế độ này trên trình duyệt hoặc bật tự duyệt phía nhà cung cấp.</p>}
      <p className="text-xs text-slate-500">Không xác minh shop, đổi hoa hồng, hợp đồng hay giao dịch đã chốt; không tự bật lại trạng thái kinh doanh.</p>
      {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{error}</p>}
      <div className="flex flex-wrap justify-end gap-3 border-t pt-4">
        <button type="button" autoFocus className="btn btn-outline" disabled={busy} onClick={() => { if (!busy) onCancel(); }}>Hủy</button>
        <button type="button" className="btn btn-primary" disabled={busy} onClick={() => { if (!busy) onConfirm(); }}>{busy ? "Đang xử lý..." : proposal.mode === "automatic" ? "Xác nhận bật tự động duyệt" : "Xác nhận duyệt lượt này"}</button>
      </div>
    </div>
  </ComboDialog>;
}
