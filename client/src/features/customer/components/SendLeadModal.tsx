import React, { useState } from 'react';
import { X, Calendar, Users, DollarSign, Phone, FileText, Gift, CheckCircle, Sparkles, Copy, Check } from 'lucide-react';
import type { SendLeadRequest, SendLeadResponse } from '../../../types/lead.types';

interface SendLeadModalProps {
  isOpen: boolean;
  onClose: () => void;
  listingId?: string;
  vendorId?: string;
  vendorName: string;
  listingTitle?: string;
  defaultPhone?: string;
  onSuccess?: (res: SendLeadResponse) => void;
}

export function SendLeadModal({
  isOpen,
  onClose,
  listingId,
  vendorId,
  vendorName,
  listingTitle,
  defaultPhone = '',
  onSuccess
}: SendLeadModalProps) {
  const [formData, setFormData] = useState<SendLeadRequest>({
    listingId,
    vendorId,
    phoneNumber: defaultPhone,
    weddingDate: '',
    estimatedGuests: undefined,
    estimatedBudget: undefined,
    notes: ''
  });

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successResult, setSuccessResult] = useState<SendLeadResponse | null>(null);
  const [copied, setCopied] = useState(false);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    try {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/leads', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify(formData)
      });

      if (!res.ok) {
        const errorData = await res.json().catch(() => ({}));
        throw new Error(errorData.detail || errorData.message || 'Không thể gửi yêu cầu tư vấn.');
      }

      const data: SendLeadResponse = await res.json();
      setSuccessResult(data);
      if (onSuccess) onSuccess(data);
    } catch (err: any) {
      setError(err.message || 'Đã có lỗi xảy ra.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleCopyCode = () => {
    if (successResult?.voucher?.code) {
      navigator.clipboard.writeText(successResult.voucher.code);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/60 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-white rounded-3xl max-w-lg w-full overflow-hidden shadow-2xl border border-rose-100 relative">
        {/* Nút đóng */}
        <button
          onClick={onClose}
          className="absolute top-4 right-4 w-9 h-9 rounded-full bg-slate-100 hover:bg-slate-200 flex items-center justify-center text-slate-500 hover:text-slate-700 transition z-10"
        >
          <X className="w-5 h-5" />
        </button>

        {/* Trạng thái thành công: Pop-up Voucher 8 ký tự */}
        {successResult ? (
          <div className="p-8 text-center space-y-6">
            <div className="w-16 h-16 rounded-3xl bg-gradient-to-tr from-rose-500 to-pink-500 flex items-center justify-center text-white mx-auto shadow-lg shadow-rose-200 animate-bounce">
              <Gift className="w-8 h-8" />
            </div>

            <div>
              <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-50 text-emerald-600 text-xs font-bold mb-2">
                <CheckCircle className="w-3.5 h-3.5" /> Gửi tư vấn thành công!
              </div>
              <h3 className="text-2xl font-bold text-slate-900" style={{ fontFamily: "'Playfair Display', serif" }}>
                Chúc mừng bạn đã nhận ưu đãi!
              </h3>
              <p className="text-xs text-slate-500 mt-1 max-w-xs mx-auto">
                Nhà cung cấp <strong className="text-slate-700">{vendorName}</strong> đã nhận được yêu cầu và cam kết phản hồi trong 2 giờ.
              </p>
            </div>

            {/* Voucher Card BR-004 */}
            <div className="p-5 rounded-2xl bg-gradient-to-br from-amber-50 via-rose-50 to-pink-50 border-2 border-dashed border-rose-200 relative overflow-hidden">
              <div className="flex justify-between items-center mb-2">
                <span className="text-[11px] font-bold uppercase tracking-wider text-rose-600 flex items-center gap-1">
                  <Sparkles className="w-3.5 h-3.5" /> Mã Ưu Đãi 8 Ký Tự
                </span>
                <span className="text-[10px] font-semibold text-slate-400">
                  Hiệu lực 30 ngày (BR-004)
                </span>
              </div>

              <div className="flex items-center justify-center gap-3 my-3">
                <span className="font-mono text-3xl font-extrabold tracking-widest text-slate-900 bg-white px-4 py-2 rounded-xl shadow-sm border border-rose-100">
                  {successResult.voucher.code}
                </span>
                <button
                  type="button"
                  onClick={handleCopyCode}
                  className="p-3 rounded-xl bg-rose-500 hover:bg-rose-600 text-white font-bold text-xs shadow-md transition flex items-center gap-1"
                  title="Sao chép mã"
                >
                  {copied ? <Check className="w-4 h-4" /> : <Copy className="w-4 h-4" />}
                </button>
              </div>

              <p className="text-xs font-semibold text-rose-700">
                🎁 Trị giá {successResult.voucher.discountValue?.toLocaleString('vi-VN')}đ khấu trừ trực tiếp khi ký hợp đồng
              </p>
            </div>

            <p className="text-[11px] text-slate-400">
              🔒 <strong>Smart Privacy (BR-002):</strong> Số điện thoại của bạn đang được ẩn an toàn dạng <code className="bg-slate-100 px-1 py-0.5 rounded">0987***123</code>.
            </p>

            <button
              onClick={onClose}
              className="w-full py-3 rounded-xl bg-slate-900 hover:bg-slate-800 text-white font-bold text-sm shadow-md transition"
            >
              Đã hiểu & Hoàn tất
            </button>
          </div>
        ) : (
          /* Form nhập thông tin */
          <form onSubmit={handleSubmit} className="p-7 space-y-5">
            <div>
              <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-rose-50 text-rose-600 text-xs font-bold mb-2">
                <Gift className="w-3.5 h-3.5" /> Tặng Voucher 500.000đ
              </div>
              <h3 className="text-xl font-bold text-slate-900" style={{ fontFamily: "'Playfair Display', serif" }}>
                Nhận Báo Giá &amp; Tư Vấn Trực Tiếp
              </h3>
              <p className="text-xs text-slate-500 mt-0.5">
                Gửi yêu cầu tới <strong>{vendorName}</strong> {listingTitle && `— ${listingTitle}`}
              </p>
            </div>

            {error && (
              <div className="p-3 rounded-xl bg-rose-50 border border-rose-100 text-rose-600 text-xs font-medium">
                {error}
              </div>
            )}

            <div className="space-y-3.5">
              {/* SĐT */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <Phone className="w-3.5 h-3.5 text-rose-500" />
                  Số Điện Thoại Liên Hệ <span className="text-rose-500">*</span>
                </label>
                <input
                  type="tel"
                  placeholder="VD: 0987654321"
                  value={formData.phoneNumber || ''}
                  onChange={(e) => setFormData({ ...formData, phoneNumber: e.target.value })}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                  required
                />
                <p className="text-[10px] text-slate-400 mt-1 flex items-center gap-1">
                  🔒 Smart Privacy: NCC sẽ chỉ nhìn thấy dạng <code className="bg-slate-100 px-1 rounded">0987***123</code>
                </p>
              </div>

              {/* Ngày cưới & Khách */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                    <Calendar className="w-3.5 h-3.5 text-rose-500" />
                    Ngày Cưới Dự Kiến
                  </label>
                  <input
                    type="date"
                    value={formData.weddingDate ? formData.weddingDate.substring(0, 10) : ''}
                    onChange={(e) => setFormData({ ...formData, weddingDate: e.target.value ? new Date(e.target.value).toISOString() : '' })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                    <Users className="w-3.5 h-3.5 text-rose-500" />
                    Số Lượng Khách
                  </label>
                  <input
                    type="number"
                    placeholder="VD: 300"
                    min="1"
                    value={formData.estimatedGuests || ''}
                    onChange={(e) => setFormData({ ...formData, estimatedGuests: e.target.value ? parseInt(e.target.value) : undefined })}
                    className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                  />
                </div>
              </div>

              {/* Ngân sách */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <DollarSign className="w-3.5 h-3.5 text-rose-500" />
                  Ngân Sách Dự Kiến (VNĐ)
                </label>
                <input
                  type="number"
                  step="500000"
                  placeholder="VD: 50000000"
                  value={formData.estimatedBudget || ''}
                  onChange={(e) => setFormData({ ...formData, estimatedBudget: e.target.value ? parseFloat(e.target.value) : undefined })}
                  className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
                />
              </div>

              {/* Ghi chú */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1 flex items-center gap-1.5">
                  <FileText className="w-3.5 h-3.5 text-rose-500" />
                  Yêu Cầu Chi Tiết / Ghi Chú
                </label>
                <textarea
                  rows={2}
                  placeholder="VD: Muốn xem menu tiệc chay, sảnh tiệc ngoài trời..."
                  value={formData.notes || ''}
                  onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
                  className="w-full px-3.5 py-2 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500 resize-none"
                />
              </div>
            </div>

            {/* SLA Badge */}
            <div className="p-3 rounded-xl bg-amber-50 border border-amber-100 text-amber-800 text-[11px] flex items-center gap-2">
              <span className="font-bold">⚡ Cam kết SLA:</span>
              <span>NCC tiếp nhận trong vòng 2 giờ. Tự động sinh Voucher 8 ký tự ngay khi gửi.</span>
            </div>

            <div className="flex gap-2 pt-1">
              <button
                type="button"
                onClick={onClose}
                className="w-1/3 py-2.5 px-4 rounded-xl border border-slate-200 text-slate-600 font-bold text-sm hover:bg-slate-50 transition"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={isLoading}
                className="w-2/3 py-2.5 px-4 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 text-white font-bold text-sm shadow-md transition disabled:opacity-50"
              >
                {isLoading ? 'Đang gửi...' : '💌 Gửi & Nhận Mã Ngay'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
