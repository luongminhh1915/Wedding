import { useState } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import {
  ArrowLeft, MapPin, Star, Eye, Building2,
  ImageIcon, ChevronLeft, ChevronRight, Gift, Heart, Share2
} from 'lucide-react';
import { usePublicListingDetail } from './hooks/useBrowse';
import { SendLeadModal } from './components/SendLeadModal';

export default function ListingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: listing, isLoading, isError } = usePublicListingDetail(id ?? '');
  const [activeImg, setActiveImg] = useState(0);
  const [isLeadModalOpen, setIsLeadModalOpen] = useState(false);

  if (isLoading) return <LoadingState />;
  if (isError || !listing) return <ErrorState onBack={() => navigate('/browse')} />;

  const media = listing.media ?? [];
  const images = media.map(m => m.mediaUrl);
  const displayImages = images.length > 0 ? images : [null];

  return (
    <div className="min-h-screen bg-[#fcfbf9]">
      {/* Breadcrumb header */}
      <div className="bg-white/95 backdrop-blur-md sticky top-0 z-50 border-b border-rose-100">
        <div className="max-w-7xl mx-auto px-5 h-14 flex items-center gap-4">
          <button
            onClick={() => navigate(-1)}
            className="flex items-center gap-1.5 text-sm text-slate-600 hover:text-rose-600 transition font-medium"
          >
            <ArrowLeft className="w-4 h-4" />
            Quay lại
          </button>
          <span className="text-slate-300">/</span>
          <Link to="/browse" className="text-sm text-slate-500 hover:text-rose-500 transition">Khám phá</Link>
          <span className="text-slate-300">/</span>
          <span className="text-sm text-slate-700 font-medium truncate max-w-xs">{listing.title}</span>

          {/* Actions */}
          <div className="ml-auto flex items-center gap-2">
            <button className="p-2 rounded-xl hover:bg-slate-100 text-slate-500 transition">
              <Share2 className="w-4 h-4" />
            </button>
            <button className="p-2 rounded-xl hover:bg-rose-50 text-slate-500 hover:text-rose-500 transition">
              <Heart className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-5 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          {/* ── Left: Images + Info ─────────────────────────────────────────── */}
          <div className="lg:col-span-2 space-y-6">
            {/* Image gallery */}
            <div className="relative rounded-3xl overflow-hidden bg-slate-100 aspect-[16/9]">
              {displayImages[activeImg] ? (
                <img
                  src={displayImages[activeImg]!}
                  alt={listing.title}
                  className="w-full h-full object-cover"
                />
              ) : (
                <div className="w-full h-full flex items-center justify-center">
                  <ImageIcon className="w-16 h-16 text-slate-300" />
                </div>
              )}

              {/* Nav arrows */}
              {images.length > 1 && (
                <>
                  <button
                    onClick={() => setActiveImg(i => Math.max(0, i - 1))}
                    disabled={activeImg === 0}
                    className="absolute left-4 top-1/2 -translate-y-1/2 p-2 rounded-full bg-white/90 shadow-md hover:bg-white transition disabled:opacity-40"
                  >
                    <ChevronLeft className="w-5 h-5" />
                  </button>
                  <button
                    onClick={() => setActiveImg(i => Math.min(images.length - 1, i + 1))}
                    disabled={activeImg === images.length - 1}
                    className="absolute right-4 top-1/2 -translate-y-1/2 p-2 rounded-full bg-white/90 shadow-md hover:bg-white transition disabled:opacity-40"
                  >
                    <ChevronRight className="w-5 h-5" />
                  </button>
                  <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-1.5">
                    {images.map((_, i) => (
                      <button
                        key={i}
                        onClick={() => setActiveImg(i)}
                        className={`w-2 h-2 rounded-full transition-all ${i === activeImg ? 'bg-white w-4' : 'bg-white/50'}`}
                      />
                    ))}
                  </div>
                </>
              )}

              {/* Overlay category tag */}
              <div className="absolute top-4 left-4">
                <span className="text-xs font-bold px-3 py-1 rounded-full bg-white/90 backdrop-blur-sm text-slate-700 shadow-sm">
                  {listing.categoryName}
                </span>
              </div>
            </div>

            {/* Thumbnail strip */}
            {images.length > 1 && (
              <div className="flex gap-2 overflow-x-auto pb-1">
                {images.map((url, i) => (
                  <button
                    key={i}
                    onClick={() => setActiveImg(i)}
                    className={`flex-shrink-0 w-16 h-16 rounded-xl overflow-hidden border-2 transition ${
                      i === activeImg ? 'border-rose-500' : 'border-transparent opacity-60 hover:opacity-100'
                    }`}
                  >
                    <img src={url} alt="" className="w-full h-full object-cover" />
                  </button>
                ))}
              </div>
            )}

            {/* Info section */}
            <div className="bg-white rounded-2xl border border-rose-50 p-6 space-y-5">
              {/* Title */}
              <div>
                <h1 className="text-2xl font-bold text-slate-900 leading-tight" style={{ fontFamily: "'Playfair Display', serif" }}>
                  {listing.title}
                </h1>
                <div className="flex flex-wrap items-center gap-4 mt-3 text-sm text-slate-500">
                  <span className="flex items-center gap-1">
                    <Building2 className="w-4 h-4" />
                    {listing.vendorBrandName}
                  </span>
                  <span className="flex items-center gap-1">
                    <MapPin className="w-4 h-4" />
                    {listing.location}
                  </span>
                  <span className="flex items-center gap-1">
                    <Eye className="w-4 h-4" />
                    {listing.viewCount} lượt xem
                  </span>
                  <span className="flex items-center gap-1">
                    <Star className="w-4 h-4 text-amber-400 fill-amber-400" />
                    <strong className="text-slate-700">4.8</strong>
                    <span className="text-slate-400">(--)</span>
                  </span>
                </div>
              </div>

              {/* Description */}
              <div>
                <h2 className="text-base font-bold text-slate-800 mb-2">Mô Tả Chi Tiết</h2>
                <p className="text-slate-600 text-sm leading-relaxed whitespace-pre-wrap">
                  {listing.description || 'Nhà cung cấp chưa cập nhật mô tả chi tiết.'}
                </p>
              </div>
            </div>
          </div>

          {/* ── Right: Pricing + CTA ────────────────────────────────────────── */}
          <div className="space-y-4">
            {/* Price card */}
            <div className="bg-white rounded-2xl border border-rose-100 p-6 shadow-sm sticky top-20">
              {/* Price */}
              <div className="mb-5">
                <p className="text-xs text-slate-500 font-semibold uppercase tracking-wider mb-1">Khoảng giá niêm yết</p>
                <div className="flex items-baseline gap-1">
                  <span className="text-2xl font-bold text-rose-600">
                    {listing.minPrice.toLocaleString('vi-VN')}đ
                  </span>
                  <span className="text-slate-400 text-sm">—</span>
                  <span className="text-lg font-semibold text-slate-700">
                    {listing.maxPrice.toLocaleString('vi-VN')}đ
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-1">Giá chính thức sẽ được báo sau khi tư vấn</p>
              </div>

              {/* Voucher highlight */}
              <div className="mb-5 p-3 rounded-xl bg-gradient-to-r from-rose-50 to-amber-50 border border-rose-100 flex items-center gap-3">
                <div className="w-9 h-9 rounded-xl bg-rose-100 flex items-center justify-center flex-shrink-0">
                  <Gift className="w-5 h-5 text-rose-500" />
                </div>
                <div>
                  <p className="text-xs font-bold text-rose-700">Nhận Mã Voucher Khi Liên Hệ</p>
                  <p className="text-[11px] text-slate-500">Mã 8 ký tự độc nhất · Hiệu lực 30 ngày</p>
                </div>
              </div>

              {/* CTA Button */}
              <button
                onClick={() => setIsLeadModalOpen(true)}
                className="w-full py-3.5 px-5 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 text-white font-bold text-sm shadow-lg shadow-rose-200 transition transform hover:-translate-y-0.5 active:translate-y-0 cursor-pointer"
              >
                💌 Nhận Tư Vấn &amp; Ưu Đãi Ngay
              </button>
              <p className="text-center text-[11px] text-slate-400 mt-2">
                Miễn phí · Không ràng buộc · Phản hồi trong 2h
              </p>

              {/* Stats */}
              <div className="mt-5 pt-5 border-t border-slate-100 grid grid-cols-3 gap-3 text-center">
                <div>
                  <p className="text-lg font-bold text-slate-800">{listing.viewCount}</p>
                  <p className="text-[10px] text-slate-400 font-medium uppercase">Lượt xem</p>
                </div>
                <div>
                  <p className="text-lg font-bold text-slate-800">4.8★</p>
                  <p className="text-[10px] text-slate-400 font-medium uppercase">Điểm đánh giá</p>
                </div>
                <div>
                  <p className="text-lg font-bold text-slate-800">24h</p>
                  <p className="text-[10px] text-slate-400 font-medium uppercase">Phản hồi</p>
                </div>
              </div>
            </div>

            {/* Vendor card */}
            <div className="bg-white rounded-2xl border border-rose-50 p-5">
              <h3 className="text-sm font-bold text-slate-800 mb-3">Về Nhà Cung Cấp</h3>
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-rose-100 to-amber-100 flex items-center justify-center text-lg font-bold text-rose-600 flex-shrink-0">
                  {listing.vendorBrandName.charAt(0)}
                </div>
                <div>
                  <p className="font-bold text-slate-900 text-sm">{listing.vendorBrandName}</p>
                  <p className="text-xs text-slate-500 flex items-center gap-1 mt-0.5">
                    <MapPin className="w-3 h-3" /> {listing.location}
                  </p>
                  <span className="inline-flex items-center gap-1 text-[10px] text-emerald-600 font-bold mt-1">
                    ✓ Đối tác được xác thực
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Modal gửi Lead & sinh Voucher 8 ký tự */}
      <SendLeadModal
        isOpen={isLeadModalOpen}
        onClose={() => setIsLeadModalOpen(false)}
        listingId={listing.id}
        vendorId={listing.vendorId}
        vendorName={listing.vendorBrandName}
        listingTitle={listing.title}
      />
    </div>
  );
}

function LoadingState() {
  return (
    <div className="min-h-screen bg-[#fcfbf9] flex items-center justify-center">
      <div className="text-center">
        <div className="w-12 h-12 border-4 border-rose-200 border-t-rose-500 rounded-full animate-spin mx-auto mb-4" />
        <p className="text-slate-500 text-sm">Đang tải thông tin dịch vụ...</p>
      </div>
    </div>
  );
}

function ErrorState({ onBack }: { onBack: () => void }) {
  return (
    <div className="min-h-screen bg-[#fcfbf9] flex flex-col items-center justify-center text-center px-4">
      <div className="text-5xl mb-4">😔</div>
      <h2 className="text-xl font-bold text-slate-800 mb-2">Không tìm thấy dịch vụ</h2>
      <p className="text-sm text-slate-500 mb-6">Gói dịch vụ này không tồn tại hoặc đã bị ẩn.</p>
      <button
        onClick={onBack}
        className="flex items-center gap-2 px-5 py-2 rounded-xl bg-rose-500 text-white text-sm font-semibold hover:bg-rose-600 transition"
      >
        <ArrowLeft className="w-4 h-4" />
        Quay lại khám phá
      </button>
    </div>
  );
}
