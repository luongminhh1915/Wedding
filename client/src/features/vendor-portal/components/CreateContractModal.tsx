import React, { useState } from 'react';
import { X, FileText, Gift, DollarSign, Calendar, Image as ImageIcon, AlertCircle } from 'lucide-react';
import { useVendorContracts } from '../hooks/useVendorContracts';

interface CreateContractModalProps {
  isOpen: boolean;
  onClose: () => void;
  leadId: string;
  customerName: string;
  defaultVoucherCode?: string;
  defaultWeddingDate?: string;
  defaultBudget?: number;
  onSuccess?: () => void;
}

export function CreateContractModal({
  isOpen,
  onClose,
  leadId,
  customerName,
  defaultVoucherCode = '',
  defaultWeddingDate = '',
  defaultBudget = 0,
  onSuccess
}: CreateContractModalProps) {
  const { createContract, isCreating } = useVendorContracts();

  const [voucherCode, setVoucherCode] = useState(defaultVoucherCode);
  const [contractValue, setContractValue] = useState<string>(defaultBudget ? defaultBudget.toString() : '');
  const [depositAmount, setDepositAmount] = useState<string>('');
  const [weddingDate, setWeddingDate] = useState<string>(defaultWeddingDate ? defaultWeddingDate.substring(0, 10) : '');
  const [contractImageUrl, setContractImageUrl] = useState<string>('');
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    const val = parseFloat(contractValue);
    const dep = parseFloat(depositAmount || '0');

    if (isNaN(val) || val <= 0) {
      setError('Vui lòng nhập giá trị hợp đồng hợp lệ.');
      return;
    }

    if (isNaN(dep) || dep < 0 || dep > val) {
      setError('Tiền cọc không hợp lệ (phải từ 0 đến giá trị HĐ).');
      return;
    }

    try {
      await createContract({
        leadId,
        voucherCode: voucherCode.trim() ? voucherCode.trim().toUpperCase() : undefined,
        contractValue: val,
        depositAmount: dep,
        contractImageUrl: contractImageUrl.trim() || undefined,
        weddingDate: weddingDate ? new Date(weddingDate).toISOString() : undefined
      });

      if (onSuccess) onSuccess();
      onClose();
    } catch (err: any) {
      setError(err.message || 'Lỗi tạo hợp đồng.');
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-950/60 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-white rounded-3xl max-w-lg w-full overflow-hidden shadow-2xl border border-rose-100 relative">
        <button
          onClick={onClose}
          className="absolute top-4 right-4 w-9 h-9 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 hover:text-slate-700 transition z-10"
        >
          <X className="w-5 h-5" />
        </button>

        <form onSubmit={handleSubmit} className="p-7 space-y-4">
          <div>
            <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-purple-50 text-purple-600 text-xs font-bold mb-2">
              <FileText className="w-3.5 h-3.5" /> Tạo Hợp Đồng Chốt Khách
            </div>
            <h3 className="text-xl font-bold text-slate-900" style={{ fontFamily: "'Playfair Display', serif" }}>
              Lập Hợp Đồng Dịch Vụ Cưới
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Khách hàng: <strong className="text-slate-800">{customerName}</strong>
            </p>
          </div>

          {error && (
            <div className="p-3 rounded-xl bg-rose-50 border border-rose-100 text-rose-600 text-xs font-medium flex items-center gap-2">
              <AlertCircle className="w-4 h-4 flex-shrink-0" />
              {error}
            </div>
          )}

          <div className="space-y-3">
            {/* Voucher 8 ký tự BR-004 */}
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                <Gift className="w-3.5 h-3.5 text-amber-500" />
                Mã Ưu Đãi Voucher 8 Ký Tự (BR-004)
              </label>
              <input
                type="text"
                placeholder="VD: WVIP8899"
                maxLength={8}
                value={voucherCode}
                onChange={(e) => setVoucherCode(e.target.value.toUpperCase())}
                className="w-full px-3.5 py-2 rounded-xl border border-slate-200 text-xs font-mono font-bold tracking-wider uppercase focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
              />
              <p className="text-[10px] text-slate-400 mt-1">
                Nhập mã voucher khách đã nhận để hệ thống đối soát giảm trừ và tính hoa hồng.
              </p>
            </div>

            {/* Giá trị HĐ & Tiền cọc */}
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <DollarSign className="w-3.5 h-3.5 text-rose-500" />
                  Tổng Giá Trị HĐ (VNĐ) <span className="text-rose-500">*</span>
                </label>
                <input
                  type="number"
                  step="500000"
                  placeholder="VD: 80000000"
                  value={contractValue}
                  onChange={(e) => setContractValue(e.target.value)}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 text-xs font-bold focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <DollarSign className="w-3.5 h-3.5 text-emerald-500" />
                  Tiền Đặt Cọc Thực Tế <span className="text-rose-500">*</span>
                </label>
                <input
                  type="number"
                  step="500000"
                  placeholder="VD: 20000000"
                  value={depositAmount}
                  onChange={(e) => setDepositAmount(e.target.value)}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 text-xs font-bold focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                  required
                />
              </div>
            </div>

            {/* Ngày cưới & Ảnh phiếu cọc */}
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <Calendar className="w-3.5 h-3.5 text-rose-500" />
                  Ngày Cưới
                </label>
                <input
                  type="date"
                  value={weddingDate}
                  onChange={(e) => setWeddingDate(e.target.value)}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 text-xs focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <ImageIcon className="w-3.5 h-3.5 text-blue-500" />
                  Ảnh Phiếu Thu / HĐ
                </label>
                <input
                  type="url"
                  placeholder="https://..."
                  value={contractImageUrl}
                  onChange={(e) => setContractImageUrl(e.target.value)}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 text-xs focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                />
              </div>
            </div>

            {/* Quy định BR-005 */}
            <div className="p-3 rounded-xl bg-purple-50/70 border border-purple-100 text-purple-900 text-[11px] space-y-1">
              <div className="font-bold flex items-center gap-1">
                ⏱️ Quy định Xác Thực 2 Chiều (BR-005):
              </div>
              <p className="opacity-90">
                Hợp đồng sẽ ở trạng thái <strong>Chờ Xác Thực</strong>. Khách hàng có <strong>72 giờ</strong> để bấm duyệt. Sau khi duyệt, hợp đồng chuyển <strong>Confirmed</strong> và tự động sinh bản ghi hoa hồng Kỳ 1.
              </p>
            </div>
          </div>

          <div className="flex gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="w-1/3 py-2.5 px-4 rounded-xl border border-slate-200 text-slate-600 font-bold text-xs hover:bg-slate-50 transition cursor-pointer"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={isCreating}
              className="w-2/3 py-2.5 px-4 rounded-xl bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 text-white font-bold text-xs shadow-md transition disabled:opacity-50 cursor-pointer"
            >
              {isCreating ? 'Đang tạo hợp đồng...' : '📝 Khởi Tạo & Gửi Khách Duyệt'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
