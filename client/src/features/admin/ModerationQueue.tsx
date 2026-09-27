import { useState } from 'react';
import {
  CheckCircle2, XCircle, Eye, Clock, Building2, MapPin,
  ImageIcon, ChevronRight, AlertTriangle, Loader2
} from 'lucide-react';
import { usePendingListings, useApproveListing, useRejectListing } from '../vendor-portal/hooks/useListings';
import type { ListingDto } from '../../types/listing.types';

export default function ModerationQueue() {
  const { data: listings = [], isLoading } = usePendingListings();
  const approveMutation = useApproveListing();
  const rejectMutation = useRejectListing();

  const [selectedListing, setSelectedListing] = useState<ListingDto | null>(null);
  const [showRejectModal, setShowRejectModal] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [rejectTarget, setRejectTarget] = useState<string | null>(null);

  const handleApprove = async (id: string, title: string) => {
    if (!confirm(`Duyệt bài đăng "${title}"?\n\nBài sẽ chuyển sang trạng thái Active và hiển thị công khai.`)) return;
    await approveMutation.mutateAsync(id);
    if (selectedListing?.id === id) setSelectedListing(null);
  };

  const openRejectModal = (id: string) => {
    setRejectTarget(id);
    setRejectReason('');
    setShowRejectModal(true);
  };

  const handleReject = async () => {
    if (!rejectTarget || !rejectReason.trim()) return;
    await rejectMutation.mutateAsync({ id: rejectTarget, reason: rejectReason });
    setShowRejectModal(false);
    setRejectTarget(null);
    if (selectedListing?.id === rejectTarget) setSelectedListing(null);
  };

  // Tính giờ còn lại (SLA 24h từ lúc nộp)
  const getSlaInfo = (listing: ListingDto) => {
    const submittedAt = new Date(listing.createdAt).getTime();
    const deadline = submittedAt + 24 * 60 * 60 * 1000;
    const now = Date.now();
    const hoursLeft = Math.max(0, Math.floor((deadline - now) / (1000 * 60 * 60)));
    const isUrgent = hoursLeft <= 4;
    return { hoursLeft, isUrgent };
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-48 text-slate-500">
        <Loader2 className="w-5 h-5 animate-spin mr-2 text-rose-400" />
        Đang tải hàng đợi kiểm duyệt...
      </div>
    );
  }

  return (
    <div className="flex gap-6 h-full">
      {/* Left panel — list */}
      <div className="w-full max-w-sm flex-shrink-0 space-y-3">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-bold text-slate-900">Hàng Đợi Kiểm Duyệt</h2>
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-amber-100 text-amber-700 text-xs font-bold">
            {listings.length} bài chờ
          </span>
        </div>

        {listings.length === 0 && (
          <div className="text-center py-16 bg-emerald-50 rounded-2xl border border-emerald-200">
            <CheckCircle2 className="w-10 h-10 text-emerald-400 mx-auto mb-3" />
            <p className="font-semibold text-emerald-700">Hàng đợi trống!</p>
            <p className="text-sm text-emerald-600 mt-1">Tất cả bài đăng đã được xử lý.</p>
          </div>
        )}

        {listings.map((listing: ListingDto) => {
          const { hoursLeft, isUrgent } = getSlaInfo(listing);
          return (
            <button
              key={listing.id}
              onClick={() => setSelectedListing(listing)}
              className={`w-full text-left p-4 rounded-xl border transition ${
                selectedListing?.id === listing.id
                  ? 'border-rose-300 bg-rose-50 shadow-sm'
                  : 'border-slate-200 bg-white hover:border-rose-200 hover:shadow-sm'
              }`}
            >
              <div className="flex items-start justify-between gap-2">
                <div className="flex-1 min-w-0">
                  <p className="font-semibold text-sm text-slate-900 line-clamp-2">{listing.title}</p>
                  <div className="flex items-center gap-1.5 mt-1.5 text-xs text-slate-500">
                    <Building2 className="w-3 h-3" />
                    <span>{listing.vendorBrandName}</span>
                  </div>
                  <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
                    <MapPin className="w-3 h-3" />
                    <span>{listing.location}</span>
                  </div>
                </div>
                <ChevronRight className="w-4 h-4 text-slate-400 flex-shrink-0 mt-1" />
              </div>

              {/* SLA indicator */}
              <div className={`mt-3 flex items-center gap-1.5 text-xs font-semibold ${isUrgent ? 'text-red-600' : 'text-amber-600'}`}>
                {isUrgent ? <AlertTriangle className="w-3 h-3" /> : <Clock className="w-3 h-3" />}
                <span>Còn {hoursLeft}h để xử lý (SLA 24h)</span>
              </div>
            </button>
          );
        })}
      </div>

      {/* Right panel — detail */}
      <div className="flex-1 min-w-0">
        {!selectedListing ? (
          <div className="flex flex-col items-center justify-center h-full text-slate-400 py-24 bg-slate-50 rounded-2xl border border-dashed border-slate-200">
            <Eye className="w-10 h-10 mb-3 text-slate-300" />
            <p className="font-medium">Chọn một bài đăng để xem chi tiết</p>
            <p className="text-sm mt-1">Kiểm tra nội dung trước khi duyệt hoặc từ chối</p>
          </div>
        ) : (
          <ListingDetailPanel
            listing={selectedListing}
            onApprove={() => handleApprove(selectedListing.id, selectedListing.title)}
            onReject={() => openRejectModal(selectedListing.id)}
            isApproving={approveMutation.isPending}
            isRejecting={rejectMutation.isPending}
          />
        )}
      </div>

      {/* Reject modal */}
      {showRejectModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg p-6">
            <div className="flex items-center gap-3 mb-5">
              <div className="w-10 h-10 rounded-xl bg-red-100 flex items-center justify-center">
                <XCircle className="w-5 h-5 text-red-600" />
              </div>
              <div>
                <h3 className="font-bold text-slate-900">Từ Chối Bài Đăng</h3>
                <p className="text-xs text-slate-500">Lý do sẽ được thông báo đến nhà cung cấp (BR-008)</p>
              </div>
            </div>

            <label className="block text-sm font-semibold text-slate-700 mb-2">
              Lý do từ chối <span className="text-red-500">*</span>
            </label>
            <textarea
              value={rejectReason}
              onChange={e => setRejectReason(e.target.value)}
              rows={4}
              placeholder="Vd: Hình ảnh bị mờ, thiếu thông tin giá, nội dung không phù hợp..."
              className="w-full px-3 py-2.5 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 resize-none"
            />
            <p className="text-xs text-slate-400 mt-1">{rejectReason.length}/1000 ký tự</p>

            <div className="flex justify-end gap-3 mt-5">
              <button
                onClick={() => setShowRejectModal(false)}
                className="px-4 py-2 text-sm text-slate-600 rounded-xl hover:bg-slate-100 transition"
              >
                Hủy
              </button>
              <button
                onClick={handleReject}
                disabled={!rejectReason.trim() || rejectMutation.isPending}
                className="inline-flex items-center gap-2 px-5 py-2 rounded-xl bg-red-500 hover:bg-red-600 text-white text-sm font-semibold transition disabled:opacity-60"
              >
                {rejectMutation.isPending ? <Loader2 className="w-4 h-4 animate-spin" /> : <XCircle className="w-4 h-4" />}
                Xác Nhận Từ Chối
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ─── Detail Panel ─────────────────────────────────────────────────────────────

interface DetailPanelProps {
  listing: ListingDto;
  onApprove: () => void;
  onReject: () => void;
  isApproving: boolean;
  isRejecting: boolean;
}

function ListingDetailPanel({ listing, onApprove, onReject, isApproving, isRejecting }: DetailPanelProps) {
  const submittedAt = new Date(listing.createdAt);

  return (
    <div className="bg-white rounded-2xl border border-slate-200 overflow-hidden h-full flex flex-col">
      {/* Header */}
      <div className="p-6 border-b border-slate-100">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h3 className="text-lg font-bold text-slate-900">{listing.title}</h3>
            <div className="flex flex-wrap items-center gap-3 mt-2 text-sm text-slate-600">
              <span className="flex items-center gap-1">
                <Building2 className="w-3.5 h-3.5" /> {listing.vendorBrandName}
              </span>
              <span className="flex items-center gap-1">
                <MapPin className="w-3.5 h-3.5" /> {listing.location}
              </span>
              <span className="text-xs text-slate-400">
                Nộp lúc: {submittedAt.toLocaleString('vi-VN')}
              </span>
            </div>
          </div>
          <div className="flex-shrink-0">
            <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-amber-100 text-amber-700 border border-amber-200">
              ⏳ Chờ Duyệt
            </span>
          </div>
        </div>
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto p-6 space-y-5">
        {/* Thông tin giá */}
        <div className="grid grid-cols-3 gap-4">
          <InfoBox label="Ngành Hàng" value={listing.categoryName} />
          <InfoBox
            label="Giá Tối Thiểu"
            value={`${listing.minPrice.toLocaleString('vi-VN')}đ`}
            highlight
          />
          <InfoBox
            label="Giá Tối Đa"
            value={`${listing.maxPrice.toLocaleString('vi-VN')}đ`}
            highlight
          />
        </div>

        {/* Mô tả */}
        <div>
          <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">Mô Tả Chi Tiết</p>
          <div className="bg-slate-50 rounded-xl p-4 text-sm text-slate-700 leading-relaxed whitespace-pre-wrap">
            {listing.description || '(Không có mô tả)'}
          </div>
        </div>

        {/* Ảnh portfolio */}
        <div>
          <p className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-2">
            Album Portfolio ({listing.media.length} ảnh)
          </p>
          {listing.media.length === 0 ? (
            <div className="flex items-center gap-2 text-sm text-slate-400 bg-slate-50 rounded-xl p-4">
              <ImageIcon className="w-4 h-4" />
              <span>Chưa có ảnh nào được tải lên</span>
            </div>
          ) : (
            <div className="grid grid-cols-4 gap-2">
              {listing.media.map((m: import('../../types/listing.types').ListingMediaDto) => (
                <div key={m.id} className="relative aspect-square rounded-xl overflow-hidden bg-slate-100 group">
                  <img src={m.mediaUrl} alt="" className="w-full h-full object-cover" />
                  {m.isFeatured && (
                    <span className="absolute top-1 left-1 text-[10px] bg-rose-500 text-white px-1.5 py-0.5 rounded font-bold">
                      Featured
                    </span>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Action footer */}
      <div className="p-6 border-t border-slate-100 bg-slate-50/50 flex items-center justify-between gap-4">
        <p className="text-xs text-slate-500">
          Xem xét kỹ nội dung trước khi duyệt. Lý do từ chối sẽ được gửi ngay cho NCC.
        </p>
        <div className="flex items-center gap-3 flex-shrink-0">
          <button
            onClick={onReject}
            disabled={isRejecting || isApproving}
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl border border-red-200 bg-white hover:bg-red-50 text-red-600 text-sm font-semibold transition disabled:opacity-60"
          >
            {isRejecting ? <Loader2 className="w-4 h-4 animate-spin" /> : <XCircle className="w-4 h-4" />}
            Từ Chối
          </button>
          <button
            onClick={onApprove}
            disabled={isApproving || isRejecting}
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-emerald-500 hover:bg-emerald-600 text-white text-sm font-semibold shadow-sm transition disabled:opacity-60"
          >
            {isApproving ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />}
            Phê Duyệt Bài Đăng
          </button>
        </div>
      </div>
    </div>
  );
}

function InfoBox({ label, value, highlight = false }: { label: string; value: string; highlight?: boolean }) {
  return (
    <div className="bg-slate-50 rounded-xl p-3">
      <p className="text-xs text-slate-500 font-semibold mb-1">{label}</p>
      <p className={`text-sm font-bold ${highlight ? 'text-rose-700' : 'text-slate-900'}`}>{value}</p>
    </div>
  );
}
