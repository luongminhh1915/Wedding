import { useState } from 'react';
import {
  CheckCircle2,
  X, Loader2, Sparkles, Image
} from 'lucide-react';
import { useEligibleContracts, useSubmitReview } from '../../hooks/useReviews';
import type { SubmitReviewRequest } from '../../../../types/review.types';

interface ReviewModalProps {
  isOpen: boolean;
  onClose: () => void;
  listingId: string;
  listingTitle?: string;
  vendorId?: string;
  vendorBrandName?: string;
  onSuccess?: () => void;
}

export function ReviewModal({
  isOpen,
  onClose,
  listingId,
  listingTitle,
  vendorBrandName,
  onSuccess
}: ReviewModalProps) {
  const { data: eligibleContracts = [] } = useEligibleContracts();
  const submitMutation = useSubmitReview();

  // 4 Tiêu chí chấm sao theo Prototype SCR-05-06
  const [c1, setC1] = useState(5); // Chất lượng thẩm mỹ
  const [c2, setC2] = useState(5); // Thái độ phục vụ
  const [c3, setC3] = useState(5); // Đúng hẹn & cam kết
  const [c4, setC4] = useState(5); // Xứng đáng chi phí

  const [selectedContractId, setSelectedContractId] = useState<string>('');
  const [content, setContent] = useState('');
  const [photoUrl, setPhotoUrl] = useState('');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  // Tính sao tổng thể
  const overallRating = Math.round((c1 + c2 + c3 + c4) / 4);

  // Kiểm tra HĐ đã chọn có thuộc dạng Verified không
  const matchedContract = eligibleContracts.find(c => c.contractId === selectedContractId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!content.trim()) {
      setErrorMsg('Vui lòng viết lời nhận xét chi tiết.');
      return;
    }

    setErrorMsg(null);
    try {
      const payload: SubmitReviewRequest = {
        listingId,
        bookingContractId: selectedContractId ? selectedContractId : undefined,
        rating: overallRating,
        content: content.trim(),
        photosJson: photoUrl.trim() ? JSON.stringify([photoUrl.trim()]) : undefined
      };

      await submitMutation.mutateAsync(payload);
      if (onSuccess) onSuccess();
      onClose();
    } catch (err: unknown) {
      const e = err as Error;
      setErrorMsg(e.message || 'Không thể gửi đánh giá.');
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-lg w-full shadow-2xl overflow-hidden border border-slate-100 flex flex-col max-h-[90vh]">
        {/* Modal Head */}
        <div className="bg-gradient-to-r from-slate-900 via-zinc-800 to-rose-950 text-white px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-xl bg-amber-400/20 text-amber-300 flex items-center justify-center font-bold">
              ★
            </div>
            <div>
              <h3 className="font-bold text-sm leading-tight">Đánh Giá Dịch Vụ Cưới</h3>
              <p className="text-[11px] text-slate-300">
                {listingTitle || vendorBrandName || 'Trải nghiệm dịch vụ thực tế'}
              </p>
            </div>
          </div>
          <button onClick={onClose} className="text-white/70 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Modal Body */}
        <form onSubmit={handleSubmit} className="p-6 overflow-y-auto space-y-4 text-xs">
          {/* Hợp đồng dịch vụ cưới (BR-009) */}
          <div>
            <label className="block font-bold text-slate-700 mb-1">
              Chọn Hợp Đồng Dịch Vụ Đã Thực Hiện (Để Nhận Huy Hiệu Verified Buyer):
            </label>
            <select
              value={selectedContractId}
              onChange={(e) => setSelectedContractId(e.target.value)}
              className="w-full p-2.5 rounded-xl border border-slate-300 bg-slate-50 focus:outline-none focus:ring-2 focus:ring-rose-500 font-semibold"
            >
              <option value="">-- Đánh giá trải nghiệm tư vấn thông thường (Chưa có HĐ) --</option>
              {eligibleContracts.map(c => (
                <option key={c.contractId} value={c.contractId}>
                  ✓ Hợp đồng {c.contractCode} - {c.vendorBrandName} ({c.weddingDate ? new Date(c.weddingDate).toLocaleDateString('vi-VN') : 'Đã ký'})
                </option>
              ))}
            </select>

            {matchedContract ? (
              <div className="mt-1.5 flex items-center gap-1.5 text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded-lg border border-emerald-200 text-[11px] font-bold">
                <CheckCircle2 className="w-3.5 h-3.5 shrink-0" />
                <span>Đủ điều kiện cấp nhãn: [✓ Verified Buyer] theo quy tắc BR-009!</span>
              </div>
            ) : (
              <p className="text-[10px] text-slate-400 mt-1">
                * Khách hàng đã hoàn tất hợp đồng trên sàn sẽ được gắn nhãn Verified Buyer và tính trọng số cao (W=1.0) theo FM-005.
              </p>
            )}
          </div>

          {/* 4 Tiêu Chí Chấm Sao */}
          <div className="bg-slate-50 p-4 rounded-2xl border border-slate-200/80 space-y-3">
            <div className="flex items-center justify-between pb-1 border-b border-slate-200">
              <span className="font-bold text-slate-800">1. Chất lượng thẩm mỹ & sản phẩm:</span>
              <StarPicker value={c1} onChange={setC1} />
            </div>

            <div className="flex items-center justify-between pb-1 border-b border-slate-200">
              <span className="font-bold text-slate-800">2. Thái độ tư vấn & phục vụ:</span>
              <StarPicker value={c2} onChange={setC2} />
            </div>

            <div className="flex items-center justify-between pb-1 border-b border-slate-200">
              <span className="font-bold text-slate-800">3. Đúng hẹn & cam kết hợp đồng:</span>
              <StarPicker value={c3} onChange={setC3} />
            </div>

            <div className="flex items-center justify-between">
              <span className="font-bold text-slate-800">4. Xứng đáng với chi phí bỏ ra:</span>
              <StarPicker value={c4} onChange={setC4} />
            </div>

            <div className="pt-2 border-t border-slate-200 flex items-center justify-between text-xs">
              <span className="font-bold text-rose-600 uppercase tracking-wider">Điểm Đánh Giá Tổng Thể:</span>
              <span className="font-black text-amber-500 text-sm flex items-center gap-1">
                {'★'.repeat(overallRating)}{'☆'.repeat(5 - overallRating)}
                <span className="text-slate-800 ml-1">({overallRating}.0 / 5.0)</span>
              </span>
            </div>
          </div>

          {/* Lời nhận xét chi tiết */}
          <div>
            <label className="block font-bold text-slate-700 mb-1">
              Lời Nhận Xét Chi Tiết Của Cặp Đôi: <span className="text-rose-500">*</span>
            </label>
            <textarea
              required
              rows={4}
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder="Chia sẻ trải nghiệm thực tế về ngày cưới, hoa tươi, phong cách trang điểm, thái độ ekip phục vụ..."
              className="w-full p-3 rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500 text-xs leading-relaxed"
            />
          </div>

          {/* Link hình ảnh thực tế */}
          <div>
            <label className="block font-bold text-slate-700 mb-1 flex items-center gap-1">
              <Image className="w-3.5 h-3.5 text-slate-400" />
              Link Ảnh Cưới Thực Tế (Tùy chọn):
            </label>
            <input
              type="text"
              value={photoUrl}
              onChange={(e) => setPhotoUrl(e.target.value)}
              placeholder="https://images.unsplash.com/... hoặc link ảnh sản phẩm"
              className="w-full px-3 py-2 rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500 text-xs"
            />
          </div>

          {errorMsg && (
            <div className="p-3 bg-red-50 text-red-700 rounded-xl text-xs font-semibold">
              {errorMsg}
            </div>
          )}

          {/* Actions */}
          <div className="pt-3 border-t border-slate-100 flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2.5 rounded-xl text-slate-500 hover:bg-slate-100 font-bold text-xs"
            >
              Hủy Bỏ
            </button>
            <button
              type="submit"
              disabled={submitMutation.isPending}
              className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-rose-600 to-amber-600 hover:from-rose-500 hover:to-amber-500 text-white font-bold text-xs shadow-md shadow-rose-600/20 flex items-center gap-1.5 transition disabled:opacity-50"
            >
              {submitMutation.isPending ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Sparkles className="w-3.5 h-3.5" />}
              <span>Gửi Đánh Giá Ngay</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

function StarPicker({ value, onChange }: { value: number; onChange: (v: number) => void }) {
  return (
    <div className="flex items-center gap-1">
      {[1, 2, 3, 4, 5].map((star) => (
        <button
          key={star}
          type="button"
          onClick={() => onChange(star)}
          className={`text-lg transition transform hover:scale-110 ${
            star <= value ? 'text-amber-400' : 'text-slate-200'
          }`}
        >
          ★
        </button>
      ))}
    </div>
  );
}

export default ReviewModal;
