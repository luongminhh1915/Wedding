import { AlertTriangle } from "lucide-react";
import { ComboDialog } from "../journey/combo/ComboDialog";

export interface ApprovalConfirmationDetails {
  typeLabel: string;
  title: string;
  vendorName?: string | null;
  versionLabel?: string;
  isVendorVerified?: boolean;
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
        {details.isVendorVerified === false && (
          <div role="alert" className="rounded-xl border border-amber-300 bg-amber-50 p-3.5 text-amber-900 text-xs">
            <div className="flex items-start gap-2.5">
              <AlertTriangle className="text-amber-600 shrink-0 mt-0.5" size={17} />
              <div>
                <p className="font-semibold text-amber-950 text-sm">Nhà cung cấp chưa được duyệt giấy tờ / chưa xác minh</p>
                <p className="mt-1 text-amber-800 leading-relaxed">
                  Nhà cung cấp này hiện chưa hoàn tất hoặc chưa được phê duyệt hồ sơ giấy phép kinh doanh / xác minh đối tác. Bạn có chắc chắn muốn duyệt dịch vụ này không?
                </p>
              </div>
            </div>
          </div>
        )}
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
