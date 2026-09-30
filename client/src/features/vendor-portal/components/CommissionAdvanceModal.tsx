import React, { useState, useEffect } from 'react';
import {
  X, Banknote, Building2, User, CreditCard,
  AlertCircle, Sparkles, Send, FileText, Percent
} from 'lucide-react';
import type { CommissionItem } from '../../../types/commission.types';

export interface GroupedSettlementContract {
  contractCode: string;
  contractId: string;
  customerName: string;
  contractValue: number;
  commissionRate: number;
  totalCommissionAmount: number;
  totalPaidAmount: number;
  totalPendingAmount: number;
  periods: string[];
  dueDate: string;
  status: 'Paid' | 'PartiallyPaid' | 'Pending' | 'Overdue';
  items: CommissionItem[];
}

export interface CommissionAdvanceRecord {
  contractId: string;
  contractCode: string;
  amount: number;
  reason: string;
  bankName: string;
  bankAccountNumber: string;
  bankAccountName: string;
  note?: string;
  status: 'PendingApproval' | 'Approved' | 'Rejected';
  requestedAt: string;
}

interface CommissionAdvanceModalProps {
  isOpen: boolean;
  onClose: () => void;
  contract: GroupedSettlementContract | null;
  onSuccess: (record: CommissionAdvanceRecord) => void;
}

const COMMON_BANKS = [
  'Vietcombank (VCB)',
  'MB Bank (Quân Đội)',
  'Techcombank (TCB)',
  'ACB (Á Châu)',
  'VPBank (Việt Nam Thịnh Vượng)',
  'BIDV (Đầu tư & Phát triển)',
  'VietinBank (Công Thương)',
  'TPBank (Tiên Phong)',
  'HDBank',
  'Sacombank',
];

const COMMON_REASONS = [
  'Yêu cầu thanh toán trước một phần hoa hồng',
  'Tạm ứng thanh toán hoa hồng trước hạn để quyết toán chi phí',
  'Hợp đồng giá trị lớn - đề nghị tạm ứng trước một phần hoa hồng',
  'Đối soát sớm cho sự kiện cưới sắp diễn ra',
  'Khác',
];

export function CommissionAdvanceModal({
  isOpen,
  onClose,
  contract,
  onSuccess,
}: CommissionAdvanceModalProps) {
  const [amountRaw, setAmountRaw] = useState<string>('');
  const [reason, setReason] = useState<string>(COMMON_REASONS[0]);
  const [customReason, setCustomReason] = useState<string>('');
  const [bankName, setBankName] = useState<string>('Vietcombank (VCB)');
  const [bankAccountNumber, setBankAccountNumber] = useState<string>('');
  const [bankAccountName, setBankAccountName] = useState<string>('');
  const [note, setNote] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const fullContractCommission = contract
    ? contract.totalCommissionAmount
    : 0;

  useEffect(() => {
    if (isOpen && contract) {
      setError(null);
      // Pre-fill with pending amount or total contract commission
      const defaultAmount = contract.totalPendingAmount > 0
        ? contract.totalPendingAmount
        : contract.totalCommissionAmount;

      setAmountRaw(defaultAmount > 0 ? defaultAmount.toLocaleString('vi-VN') : '');

      const savedBank = localStorage.getItem('vendor_default_bank');
      if (savedBank) {
        try {
          const parsed = JSON.parse(savedBank);
          if (parsed.bankName) setBankName(parsed.bankName);
          if (parsed.bankAccountNumber) setBankAccountNumber(parsed.bankAccountNumber);
          if (parsed.bankAccountName) setBankAccountName(parsed.bankAccountName);
        } catch {
          // ignore
        }
      }
    }
  }, [isOpen, contract]);

  if (!isOpen || !contract) return null;

  const parseAmount = (val: string): number => {
    const clean = val.replace(/\D/g, '');
    return clean ? parseInt(clean, 10) : 0;
  };

  const handleAmountChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const num = parseAmount(e.target.value);
    setAmountRaw(num > 0 ? num.toLocaleString('vi-VN') : '');
    setError(null);
  };

  const setPresetPercentage = (pct: number) => {
    const calculated = Math.round((fullContractCommission * pct) / 100);
    setAmountRaw(calculated.toLocaleString('vi-VN'));
    setError(null);
  };

  const setFullPendingPreset = () => {
    if (contract.totalPendingAmount > 0) {
      setAmountRaw(contract.totalPendingAmount.toLocaleString('vi-VN'));
    } else {
      setAmountRaw(Math.round(fullContractCommission * 0.5).toLocaleString('vi-VN'));
    }
    setError(null);
  };

  const currentAmount = parseAmount(amountRaw);
  const percentageOfCommission = fullContractCommission > 0
    ? Math.round((currentAmount / fullContractCommission) * 100)
    : 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (currentAmount <= 0) {
      setError('Vui lòng nhập số tiền hoa hồng muốn yêu cầu thanh toán trước.');
      return;
    }

    if (currentAmount > fullContractCommission) {
      setError(`Số tiền yêu cầu không thể vượt quá tổng hoa hồng của hợp đồng (${fullContractCommission.toLocaleString('vi-VN')} đ).`);
      return;
    }

    const finalReason = reason === 'Khác' ? customReason.trim() : reason;
    if (!finalReason) {
      setError('Vui lòng chọn hoặc nhập lý do yêu cầu thanh toán trước.');
      return;
    }

    if (!bankAccountNumber.trim() || !bankAccountName.trim()) {
      setError('Vui lòng nhập đầy đủ Số tài khoản và Tên chủ tài khoản nhận tiền.');
      return;
    }

    setIsSubmitting(true);

    try {
      const token = localStorage.getItem('token');
      const payload = {
        amount: currentAmount,
        reason: finalReason,
        bankName,
        bankAccountNumber: bankAccountNumber.trim(),
        bankAccountName: bankAccountName.trim().toUpperCase(),
        note: note.trim() || undefined,
      };

      // Call API if contractId exists
      if (contract.contractId) {
        await fetch(`/api/contracts/${contract.contractId}/advance-payment`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            ...(token ? { Authorization: `Bearer ${token}` } : {}),
          },
          body: JSON.stringify(payload),
        }).catch(() => {});
      }

      // Remember bank info for future
      localStorage.setItem('vendor_default_bank', JSON.stringify({
        bankName,
        bankAccountNumber: bankAccountNumber.trim(),
        bankAccountName: bankAccountName.trim().toUpperCase(),
      }));

      const record: CommissionAdvanceRecord = {
        contractId: contract.contractId || contract.contractCode,
        contractCode: contract.contractCode,
        amount: currentAmount,
        reason: finalReason,
        bankName,
        bankAccountNumber: bankAccountNumber.trim(),
        bankAccountName: bankAccountName.trim().toUpperCase(),
        note: note.trim() || undefined,
        status: 'PendingApproval',
        requestedAt: new Date().toISOString(),
      };

      onSuccess(record);
      onClose();
    } catch (err: any) {
      setError(err.message || 'Có lỗi xảy ra khi gửi yêu cầu.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-950/60 backdrop-blur-xs animate-in fade-in duration-200">
      <div className="bg-white rounded-3xl max-w-xl w-full overflow-hidden shadow-2xl border border-rose-100 flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="px-6 py-5 bg-gradient-to-r from-rose-600 via-rose-500 to-amber-500 text-white flex items-center justify-between relative shrink-0">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-white/20 backdrop-blur-md flex items-center justify-center text-white">
              <Banknote className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="font-bold text-lg leading-tight">Yêu Cầu Thanh Toán Trước Hoa Hồng</h3>
                <span className="text-[10px] uppercase font-extrabold px-2 py-0.5 rounded-full bg-white/25">
                  Đối Soát
                </span>
              </div>
              <p className="text-xs text-rose-100 mt-0.5">
                Đề nghị giải ngân / thanh toán trước một phần hoa hồng của hợp đồng
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full bg-black/10 hover:bg-black/20 flex items-center justify-center text-white transition cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Scrollable Form Body */}
        <form onSubmit={handleSubmit} noValidate className="p-6 overflow-y-auto space-y-5 text-xs text-slate-700">
          {/* Contract Overview Box */}
          <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200/80 space-y-2.5">
            <div className="flex items-center justify-between border-b border-slate-200/60 pb-2">
              <div className="flex items-center gap-2">
                <FileText className="w-4 h-4 text-rose-500" />
                <span className="font-mono font-bold text-slate-900 text-sm">{contract.contractCode}</span>
              </div>
              <div className="flex items-center gap-1.5 text-slate-500">
                <User className="w-3.5 h-3.5" />
                <span className="font-medium text-slate-700">{contract.customerName}</span>
              </div>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-1 text-[11px]">
              <div>
                <span className="text-slate-400 block">Giá trị HĐ</span>
                <span className="text-xs font-extrabold text-slate-900">
                  {contract.contractValue.toLocaleString('vi-VN')} đ
                </span>
              </div>
              <div>
                <span className="text-slate-400 block">Tỷ lệ HH</span>
                <span className="text-xs font-bold text-slate-700 flex items-center gap-0.5">
                  <Percent className="w-3 h-3 text-rose-500" />
                  {(contract.commissionRate * 100).toFixed(0)}%
                </span>
              </div>
              <div>
                <span className="text-slate-400 block">Tổng Hoa Hồng Phải Trả</span>
                <span className="text-xs font-extrabold text-slate-900">
                  {fullContractCommission.toLocaleString('vi-VN')} đ
                </span>
              </div>
              <div>
                <span className="text-slate-400 block">Chưa Thanh Toán</span>
                <span className="text-xs font-extrabold text-rose-600">
                  {contract.totalPendingAmount.toLocaleString('vi-VN')} đ
                </span>
              </div>
            </div>

            <div className="pt-2 border-t border-slate-200/60 flex flex-wrap items-center justify-between text-[11px] text-slate-500 gap-1">
              <span>
                Đã thanh toán: <strong className="text-emerald-600 font-bold">{contract.totalPaidAmount.toLocaleString('vi-VN')} đ</strong>
              </span>
              <span>
                Còn lại chưa thanh toán: <strong className="text-rose-600 font-bold">{contract.totalPendingAmount.toLocaleString('vi-VN')} đ</strong>
              </span>
            </div>
          </div>

          {/* Input Amount */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <label className="font-bold text-slate-900 text-xs flex items-center gap-1.5">
                <Sparkles className="w-3.5 h-3.5 text-amber-500" />
                Số tiền hoa hồng yêu cầu thanh toán trước (VNĐ) <span className="text-rose-500">*</span>
              </label>
              {percentageOfCommission > 0 && (
                <span className="text-[11px] font-bold text-rose-600 bg-rose-50 px-2 py-0.5 rounded-full border border-rose-200">
                  Chiếm {percentageOfCommission}% tổng hoa hồng ({fullContractCommission.toLocaleString('vi-VN')} đ)
                </span>
              )}
            </div>

            <div className="relative">
              <input
                type="text"
                inputMode="numeric"
                value={amountRaw}
                onChange={handleAmountChange}
                placeholder="Nhập số tiền hoa hồng muốn thanh toán trước, VD: 25.000.000"
                className="w-full px-4 py-3 rounded-xl border border-slate-300 text-base font-bold text-slate-900 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 transition"
              />
              <span className="absolute right-4 top-1/2 -translate-y-1/2 font-bold text-slate-400 text-sm">
                VNĐ
              </span>
            </div>

            {/* Quick Presets */}
            <div className="flex flex-wrap items-center gap-1.5 pt-1">
              <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider mr-1">
                Chọn nhanh:
              </span>
              <button
                type="button"
                onClick={() => setPresetPercentage(30)}
                className="px-2.5 py-1 rounded-lg border border-slate-200 bg-white hover:bg-rose-50 hover:border-rose-200 text-slate-700 hover:text-rose-600 font-semibold text-[11px] transition cursor-pointer"
              >
                30% ({Math.round(fullContractCommission * 0.3).toLocaleString('vi-VN')} đ)
              </button>
              <button
                type="button"
                onClick={() => setPresetPercentage(50)}
                className="px-2.5 py-1 rounded-lg border border-slate-200 bg-white hover:bg-rose-50 hover:border-rose-200 text-slate-700 hover:text-rose-600 font-semibold text-[11px] transition cursor-pointer"
              >
                50% - Đợt 1 ({Math.round(fullContractCommission * 0.5).toLocaleString('vi-VN')} đ)
              </button>
              {contract.totalPendingAmount > 0 && (
                <button
                  type="button"
                  onClick={setFullPendingPreset}
                  className="px-2.5 py-1 rounded-lg border border-amber-200 bg-amber-50 hover:bg-amber-100 text-amber-800 font-semibold text-[11px] transition cursor-pointer"
                >
                  Toàn bộ nợ kỳ này ({contract.totalPendingAmount.toLocaleString('vi-VN')} đ)
                </button>
              )}
            </div>
          </div>

          {/* Reason */}
          <div className="space-y-1.5">
            <label className="font-bold text-slate-900 text-xs block">
              Mục đích & Lý do yêu cầu thanh toán trước <span className="text-rose-500">*</span>
            </label>
            <select
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              className="w-full px-3 py-2.5 rounded-xl border border-slate-300 bg-white text-xs font-semibold text-slate-800 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
            >
              {COMMON_REASONS.map((r) => (
                <option key={r} value={r}>
                  {r}
                </option>
              ))}
            </select>

            {reason === 'Khác' && (
              <input
                type="text"
                value={customReason}
                onChange={(e) => setCustomReason(e.target.value)}
                placeholder="Nhập chi tiết lý do yêu cầu thanh toán trước hoa hồng..."
                className="w-full mt-2 px-3 py-2 rounded-xl border border-slate-300 text-xs text-slate-800 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
              />
            )}
          </div>

          {/* Bank Account Info */}
          <div className="p-4 rounded-2xl bg-amber-50/50 border border-amber-200/80 space-y-3">
            <div className="flex items-center gap-2 text-amber-900 font-bold text-xs">
              <Building2 className="w-4 h-4 text-amber-600" />
              <span>Tài Khoản Ngân Hàng Nhận Tiền / Đối Soát Của Vendor</span>
            </div>

            <div className="space-y-2">
              <div>
                <label className="text-[11px] text-slate-600 block mb-1">Ngân hàng thụ hưởng</label>
                <select
                  value={bankName}
                  onChange={(e) => setBankName(e.target.value)}
                  className="w-full px-3 py-2 rounded-xl border border-slate-300 bg-white text-xs font-semibold text-slate-800 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                >
                  {COMMON_BANKS.map((b) => (
                    <option key={b} value={b}>{b}</option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                <div>
                  <label className="text-[11px] text-slate-600 block mb-1">
                    Số tài khoản <span className="text-rose-500">*</span>
                  </label>
                  <div className="relative">
                    <input
                      type="text"
                      value={bankAccountNumber}
                      onChange={(e) => setBankAccountNumber(e.target.value)}
                      placeholder="VD: 0123456789"
                      className="w-full px-3 py-2 rounded-xl border border-slate-300 bg-white font-mono font-bold text-xs text-slate-900 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                    />
                    <CreditCard className="w-3.5 h-3.5 text-slate-400 absolute right-3 top-1/2 -translate-y-1/2" />
                  </div>
                </div>

                <div>
                  <label className="text-[11px] text-slate-600 block mb-1">
                    Tên chủ tài khoản <span className="text-rose-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={bankAccountName}
                    onChange={(e) => setBankAccountName(e.target.value.toUpperCase())}
                    placeholder="VD: NGUYEN VAN A"
                    className="w-full px-3 py-2 rounded-xl border border-slate-300 bg-white font-bold text-xs text-slate-900 uppercase focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                  />
                </div>
              </div>
            </div>
          </div>

          {/* Additional Notes */}
          <div className="space-y-1.5">
            <label className="font-bold text-slate-900 text-xs block">
              Ghi chú thêm gửi Quản Trị Viên (Admin)
            </label>
            <textarea
              rows={2}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="VD: Đề nghị duyệt đối soát sớm để phục vụ triển khai sự kiện..."
              className="w-full px-3 py-2 rounded-xl border border-slate-300 text-xs text-slate-800 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 resize-none"
            />
          </div>

          {/* Error Message */}
          {error && (
            <div className="p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0 text-rose-500" />
              <span>{error}</span>
            </div>
          )}

          {/* Footer Actions */}
          <div className="pt-2 flex items-center justify-end gap-3 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              disabled={isSubmitting}
              className="px-4 py-2.5 rounded-xl border border-slate-200 text-slate-600 hover:bg-slate-50 font-bold transition cursor-pointer"
            >
              Hủy Bỏ
            </button>

            <button
              type="submit"
              disabled={isSubmitting}
              className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-rose-600 to-amber-600 hover:from-rose-500 hover:to-amber-500 text-white font-bold shadow-md shadow-rose-600/20 flex items-center gap-2 transition transform hover:-translate-y-0.5 active:translate-y-0 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isSubmitting ? (
                <>
                  <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                  <span>Đang Gửi Yêu Cầu...</span>
                </>
              ) : (
                <>
                  <Send className="w-4 h-4" />
                  <span>Xác Nhận Yêu Cầu Thanh Toán Trước</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
