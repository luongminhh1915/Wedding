import { ComboDialog } from "../journey/combo/ComboDialog";

export interface ApprovalConfirmationDetails {
  typeLabel: string;
  title: string;
  vendorName?: string | null;
  versionLabel?: string;
}

export function ApprovalConfirmationDialog({
  details,
  busy = false,
  error,
  onCancel,
  onConfirm,
}: {
  details: ApprovalConfirmationDetails;
  busy?: boolean;
  error?: string;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const cancel = () => {
    if (!busy) onCancel();
  };
  const confirm = () => {
    if (!busy) onConfirm();
  };

  return (
    <ComboDialog title="Xác nhận phê duyệt" onClose={cancel}>
      <div className="space-y-4 text-sm text-[#233a42] [overflow-wrap:anywhere]">
        <p>Bạn có chắc chắn muốn phê duyệt nội dung này?</p>
        <dl className="space-y-3 rounded-xl border border-[#dedccf] bg-[#fcf6ea] p-4">
          <div>
            <dt className="text-xs text-slate-500">{details.typeLabel}</dt>
            <dd className="mt-1 font-semibold">{details.title}</dd>
          </div>
          {details.vendorName && (
            <div>
              <dt className="text-xs text-slate-500">Nhà cung cấp</dt>
              <dd className="mt-1">{details.vendorName}</dd>
            </div>
          )}
          {details.versionLabel && (
            <div>
              <dt className="text-xs text-slate-500">Phiên bản đang xem</dt>
              <dd className="mt-1">{details.versionLabel}</dd>
            </div>
          )}
        </dl>
        <p className="text-xs leading-relaxed text-slate-500">
          Chỉ duyệt đúng nội dung đang xem, không thay đổi hoa hồng, hợp đồng
          hoặc giao dịch đã chốt và không tự bật lại trạng thái kinh doanh.
        </p>
        {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{error}</p>}
        <div className="flex flex-wrap justify-end gap-3 border-t border-[#dedccf] pt-4">
          <button
            type="button"
            autoFocus
            className="btn btn-outline"
            disabled={busy}
            onClick={cancel}
          >
            Hủy
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={busy}
            onClick={confirm}
          >
            {busy ? "Đang duyệt..." : "Xác nhận duyệt"}
          </button>
        </div>
      </div>
    </ComboDialog>
  );
}
