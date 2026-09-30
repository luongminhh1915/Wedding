import React, { useState, useEffect } from 'react';
import {
  X, Banknote, Building2, User, CreditCard,
  AlertCircle, Sparkles, Send, FileText, Calendar
} from 'lucide-react';
import type { Contract } from '../../../types/contract.types';

export interface AdvancePaymentRecord {
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

interface AdvancePaymentModalProps {
  isOpen: boolean;
  onClose: () => void;
  contract: Contract | null;
  onSuccess: (record: AdvancePaymentRecord) => void;
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
  'Đặt cọc hoa tươi & concept trang trí tiệc cưới',
  'Tạm ứng chi phí sảnh tiệc / nhà hàng',
  'Chi trả tạm ứng cho ekip chụp ảnh, phóng sự & makeup',
  'May đo, chuẩn bị trang phục cưới & đạo cụ',
  'Tạm ứng thanh toán âm thanh ánh sáng & ban nhạc',
  'Khác',
];

export function AdvancePaymentModal({
  isOpen,
  onClose,
  contract,
  onSuccess,
}: AdvancePaymentModalProps) {
  const [amountRaw, setAmountRaw] = useState<string>('');
  const [reason, setReason] = useState<string>(COMMON_REASONS[0]);
  const [customReason, setCustomReason] = useState<string>('');
  const [bankName, setBankName] = useState<string>('Vietcombank (VCB)');
  const [bankAccountNumber, setBankAccountNumber] = useState<string>('');
  const [bankAccountName, setBankAccountName] = useState<string>('');
  const [note, setNote] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Load saved bank account if any
  useEffect(() => {
    if (isOpen && contract) {
      setError(null);
      // Pre-fill 30% by default
      const defaultAmount = Math.round(contract.contractValue * 0.3);
      setAmountRaw(defaultAmount.toLocaleString('vi-VN'));

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

  // Format currency helpers
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
    const calculated = Math.round((contract.contractValue * pct) / 100);
    setAmountRaw(calculated.toLocaleString('vi-VN'));
    setError(null);
  };

  const setDepositPreset = () => {
    if (contract.depositAmount > 0) {
      setAmountRaw(contract.depositAmount.toLocaleString('vi-VN'));
    } else {
      setPresetPercentage(30);
    }
    setError(null);
  };

  const currentAmount = parseAmount(amountRaw);
  const percentageOfContract = contract.contractValue > 0
    ? Math.round((currentAmount / contract.contractValue) * 100)
    : 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (currentAmount <= 0) {
      setError('Vui lòng nhập số tiền muốn xin thanh toán trước.');
      return;
    }

    if (currentAmount > contract.contractValue) {
      setError(`Số tiền xin thanh toán trước không thể lớn hơn giá trị hợp đồng (${contract.contractValue.toLocaleString('vi-VN')} đ).`);
      return;
    }

    const finalReason = reason === 'Khác' ? customReason.trim() : reason;
    if (!finalReason) {
      setError('Vui lòng chọn hoặc nhập lý do xin thanh toán trước.');
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

      // Call API
      const res = await fetch(`/api/contracts/${contract.id}/advance-payment`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.message || 'Không thể gửi yêu cầu xin thanh toán trước.');
      }

      // Remember bank info for future requests
      localStorage.setItem('vendor_default_bank', JSON.stringify({
        bankName,
        bankAccountNumber: bankAccountNumber.trim(),
        bankAccountName: bankAccountName.trim().toUpperCase(),
      }));

      const record: AdvancePaymentRecord = {
        contractId: contract.id,
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
        <div className="px-6 py-5 bg-gradient-to-r from-rose-500 via-rose-600 to-amber-500 text-white flex items-center justify-between relative shrink-0">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-white/20 backdrop-blur-md flex items-center justify-center text-white">
              <Banknote className="w-6 h-6" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="font-bold text-lg leading-tight">Xin Thanh Toán Trước Hợp Đồng</h3>
                <span className="text-[10px] uppercase font-extrabold px-2 py-0.5 rounded-full bg-white/25">
                  Tạm Ứng
                </span>
              </div>
              <p className="text-xs text-rose-100 mt-0.5">
                Đề nghị giải ngân một phần giá trị hợp đồng để chuẩn bị dịch vụ cưới
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
                {contract.customerPhone && <span className="text-slate-400">({contract.customerPhone})</span>}
              </div>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 gap-2 pt-1 text-[11px]">
              <div>
                <span className="text-slate-400 block">Tổng giá trị HĐ</span>
                <span className="text-sm font-extrabold text-slate-900">
                  {contract.contractValue.toLocaleString('vi-VN')} đ
                </span>
              </div>
              <div>
                <span className="text-slate-400 block">Tiền đặt cọc</span>
                <span className="text-sm font-extrabold text-emerald-600">
                  {contract.depositAmount.toLocaleString('vi-VN')} đ
                </span>
              </div>
              <div>
                <span className="text-slate-400 block">Ngày cưới dự kiến</span>
                <span className="text-xs font-bold text-slate-700 flex items-center gap-1">
                  <Calendar className="w-3 h-3 text-rose-500" />
                  {contract.weddingDate ? new Date(contract.weddingDate).toLocaleDateString('vi-VN') : 'Chưa định ngày'}
                </span>
              </div>
            </div>
          </div>

          {/* Input Amount */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <label className="font-bold text-slate-900 text-xs flex items-center gap-1.5">
                <Sparkles className="w-3.5 h-3.5 text-amber-500" />
                Số tiền xin thanh toán trước (VNĐ) <span className="text-rose-500">*</span>
              </label>
              {percentageOfContract > 0 && (
                <span className="text-[11px] font-bold text-rose-600 bg-rose-50 px-2 py-0.5 rounded-full border border-rose-200">
                  Bằng {percentageOfContract}% giá trị HĐ
                </span>
              )}
            </div>

            <div className="relative">
              <input
                type="text"
                inputMode="numeric"
                value={amountRaw}
                onChange={handleAmountChange}
                placeholder="Nhập số tiền muốn xin ứng, VD: 15.000.000"
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
                30% ({Math.round(contract.contractValue * 0.3).toLocaleString('vi-VN')} đ)
              </button>
              <button
                type="button"
                onClick={() => setPresetPercentage(50)}
                className="px-2.5 py-1 rounded-lg border border-slate-200 bg-white hover:bg-rose-50 hover:border-rose-200 text-slate-700 hover:text-rose-600 font-semibold text-[11px] transition cursor-pointer"
              >
                50% ({Math.round(contract.contractValue * 0.5).toLocaleString('vi-VN')} đ)
              </button>
              {contract.depositAmount > 0 && (
                <button
                  type="button"
                  onClick={setDepositPreset}
                  className="px-2.5 py-1 rounded-lg border border-emerald-200 bg-emerald-50 hover:bg-emerald-100 text-emerald-800 font-semibold text-[11px] transition cursor-pointer"
                >
                  Toàn bộ tiền cọc ({contract.depositAmount.toLocaleString('vi-VN')} đ)
                </button>
              )}
              <button
                type="button"
                onClick={() => setPresetPercentage(70)}
                className="px-2.5 py-1 rounded-lg border border-slate-200 bg-white hover:bg-rose-50 hover:border-rose-200 text-slate-700 hover:text-rose-600 font-semibold text-[11px] transition cursor-pointer"
              >
                70% ({Math.round(contract.contractValue * 0.7).toLocaleString('vi-VN')} đ)
              </button>
            </div>
          </div>

          {/* Reason */}
          <div className="space-y-1.5">
            <label className="font-bold text-slate-900 text-xs block">
              Mục đích & Lý do xin thanh toán trước <span className="text-rose-500">*</span>
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
                placeholder="Nhập chi tiết mục đích tạm ứng..."
                className="w-full mt-2 px-3 py-2 rounded-xl border border-slate-300 text-xs text-slate-800 focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
              />
            )}
          </div>

          {/* Bank Account Info */}
          <div className="p-4 rounded-2xl bg-amber-50/50 border border-amber-200/80 space-y-3">
            <div className="flex items-center gap-2 text-amber-900 font-bold text-xs">
              <Building2 className="w-4 h-4 text-amber-600" />
              <span>Tài Khoản Nhận Tiền Tạm Ứng Của Nhà Cung Cấp</span>
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
              Ghi chú thêm cho Khách hàng & Quản trị viên (Không bắt buộc)
            </label>
            <textarea
              rows={2}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="VD: Nhà cung cấp cam kết sử dụng khoản thanh toán trước đúng hạng mục chuẩn bị dịch vụ..."
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
                  <span>Xác Nhận Xin Thanh Toán Trước</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
