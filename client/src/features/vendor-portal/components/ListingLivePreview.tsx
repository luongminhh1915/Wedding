import { useRef, useState } from 'react';
import {
  Eye, Sparkles, MapPin, Image as ImageIcon, Gift,
  Tag, CheckCircle2, Clock, ChevronLeft, ChevronRight,
  ExternalLink, Video, FileText, ShieldCheck,
  Star, XCircle, Share2, ArrowUpRight,
  MessageCircle, Calendar, LayoutGrid, Info, ArrowRight,
  FolderPlus, Plus, Pencil, Trash2
} from 'lucide-react';
import type { ServiceDetailsDto, ServiceCollectionItem, ServiceLatestUpdate } from '../../../types/listing.types';
import { isServiceDetailsLink, serviceStudioImageSrc } from '../../listings/serviceDetailsModel';
export type { ServiceCollectionItem, ServiceLatestUpdate } from '../../../types/listing.types';
import type { ListingPricingOption } from '../../../types/listing.types';

export interface ListingLivePreviewPricing {
  serviceItemId?: string | null;
  basePrice?: number | null;
  unit?: string | null;
}

export interface ListingLivePreviewProps {
  title: string;
  categoryName?: string;
  minPrice: string | number;
  maxPrice: string | number;
  location: string;
  description: string;
  images: { preview?: string; url?: string }[];
  vendorBrandName?: string;
  serviceDetails?: ServiceDetailsDto | null;
  cancellationPolicy?: string | null;
  pricing?: ListingLivePreviewPricing | null;
  pricingOptions?: ListingPricingOption[];
  collections?: ServiceCollectionItem[];
  latestUpdate?: ServiceLatestUpdate | null;
  onOpenCreateCollection?: () => void;
  onDeleteCollection?: (id: string) => void;
  onOpenUpdateEditor?: () => void;
}

export function ListingLivePreview({
  title,
  categoryName,
  minPrice,
  maxPrice,
  location,
  description,
  images,
  vendorBrandName,
  serviceDetails,
  cancellationPolicy,
  pricing,
  pricingOptions = [],
  collections = [],
  latestUpdate = null,
  onOpenCreateCollection,
  onDeleteCollection,
  onOpenUpdateEditor
}: ListingLivePreviewProps) {
  const [previewTab, setPreviewTab] = useState<'detail' | 'card'>('detail');
  const [detailNavTab, setDetailNavTab] = useState<'info' | 'reviews'>('info');
  const [activeImg, setActiveImg] = useState(0);
  const reviewSection = useRef<HTMLDivElement>(null);

  const primaryImage = images[0]?.preview || images[0]?.url;
  const currentImages = images.map(img => img.preview || img.url).filter(Boolean) as string[];
  const safeActiveIndex = Math.min(activeImg, Math.max(0, currentImages.length - 1));
  const selectedImage = currentImages[safeActiveIndex] || primaryImage;

  // Format price
  const numMin = minPrice !== '' && minPrice != null ? Number(minPrice) : null;
  const numMax = maxPrice !== '' && maxPrice != null ? Number(maxPrice) : null;

  let priceLabel = 'Chưa đặt giá';
  if (numMin != null && numMax != null && !Number.isNaN(numMin) && !Number.isNaN(numMax)) {
    if (numMin === numMax) {
      priceLabel = `${numMin.toLocaleString('vi-VN')} ₫`;
    } else {
      priceLabel = `${numMin.toLocaleString('vi-VN')} ₫ – ${numMax.toLocaleString('vi-VN')} ₫`;
    }
  } else if (numMin != null && !Number.isNaN(numMin)) {
    priceLabel = `Từ ${numMin.toLocaleString('vi-VN')} ₫`;
  }

  const displayBrand = vendorBrandName?.trim() || 'Thương hiệu của bạn';
  const displayTitle = title.trim() || 'Tiêu đề gói dịch vụ sẽ hiển thị ở đây';
  const displayLocation = location.trim() || 'Chưa cập nhật địa điểm';
  const displayCategory = categoryName?.trim() || 'Danh mục dịch vụ';

  // Portfolio & video links
  const portfolioUrls = (serviceDetails?.portfolioUrls || []).filter(isServiceDetailsLink);
  const videoUrl = serviceDetails?.videoUrl && isServiceDetailsLink(serviceDetails.videoUrl) ? serviceDetails.videoUrl : '';
  const studioImage = (reference: string) => serviceStudioImageSrc(reference, import.meta.env.VITE_API_URL || '');

  // Check if any extended details exist
  const hasInclusions = !!serviceDetails?.includedItems?.trim();
  const hasExclusions = !!serviceDetails?.excludedItems?.trim();
  const hasProcess = !!serviceDetails?.process?.trim();
  const hasTerms = !!serviceDetails?.terms?.trim();
  const hasAdditionalFees = !!serviceDetails?.additionalFees?.trim();
  const hasReschedule = !!serviceDetails?.reschedulePolicy?.trim();
  const hasRequirements = !!serviceDetails?.customerRequirements?.trim();
  const hasNotes = !!serviceDetails?.notes?.trim();
  const hasHighlights = !!serviceDetails?.highlights?.trim();
  const hasShortDesc = !!serviceDetails?.shortDescription?.trim();
  const hasOptions = !!serviceDetails?.serviceOptions?.trim();
  const hasDuration = !!serviceDetails?.duration?.trim();
  const hasFullService = !!serviceDetails?.fullService && Object.values(serviceDetails.fullService).some(v => typeof v === 'string' && v.trim().length > 0);
  const hasPortfolioOrVideo = portfolioUrls.length > 0 || !!videoUrl;

  // Linked pricing option
  const selectedPricingOption = pricing?.serviceItemId
    ? pricingOptions.find(o => o.id === pricing.serviceItemId)
    : null;

  // Check if latest update has content or image
  const hasUpdate = !!(latestUpdate?.content?.trim() || latestUpdate?.imageUrl?.trim());

  return (
    <div className="flex flex-col space-y-4">
      {/* Top Header Card */}
      <div className="flex flex-wrap gap-3 items-center justify-between px-5 py-3.5 bg-white rounded-2xl border border-slate-200/90 shadow-2xs">
        <div className="flex items-center gap-2">
          <Eye className="w-4 h-4 text-rose-500" />
          <span className="text-xs font-bold uppercase tracking-wider text-slate-800">
            Xem trước trực tiếp
          </span>
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
            Live Sync
          </span>
        </div>

        {/* View Mode Toggle: Default to 'detail' for rich full page */}
        <div className="inline-flex p-0.5 bg-slate-100 rounded-xl text-xs border border-slate-200">
          <button
            type="button"
            onClick={() => setPreviewTab('detail')}
            className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition ${
              previewTab === 'detail'
                ? 'bg-white text-slate-900 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Trang chi tiết đầy đủ
          </button>
          <button
            type="button"
            onClick={() => setPreviewTab('card')}
            className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition ${
              previewTab === 'card'
                ? 'bg-white text-slate-900 shadow-2xs'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Thẻ tìm kiếm
          </button>
        </div>
      </div>

      {/* Main Preview Content */}
      {previewTab === 'card' ? (
        /* ── 1. Thẻ danh mục preview ── */
        <div className="max-w-md mx-auto py-4">
          <div className="text-[11px] font-medium text-slate-500 mb-2.5 flex items-center justify-between">
            <span>Hiển thị trên Trang chủ & Danh sách tìm kiếm:</span>
            <span className="text-rose-600 font-semibold">{images.length}/10 ảnh</span>
          </div>

          <article className="bg-white rounded-2xl overflow-hidden border border-slate-200 shadow-sm transition hover:shadow-md">
            {/* Image banner */}
            <div className="relative aspect-[4/3] bg-slate-100 overflow-hidden">
              {primaryImage ? (
                <img
                  src={primaryImage}
                  alt={displayTitle}
                  className="w-full h-full object-cover"
                />
              ) : (
                <div className="w-full h-full flex flex-col items-center justify-center text-slate-400 gap-2 bg-rose-50/30">
                  <ImageIcon className="w-10 h-10 text-rose-300" />
                  <span className="text-xs font-medium">Chưa có ảnh bìa</span>
                </div>
              )}
              <span className="absolute top-2.5 left-2.5 px-2.5 py-1 bg-white/95 backdrop-blur-xs text-slate-800 text-xs font-bold rounded-lg shadow-2xs flex items-center gap-1">
                <Tag className="w-3 h-3 text-rose-500" />
                {displayCategory}
              </span>
              {images.length > 1 && (
                <span className="absolute bottom-2.5 right-2.5 px-2 py-0.5 bg-black/60 backdrop-blur-xs text-white text-[11px] font-medium rounded-md">
                  +{images.length} ảnh
                </span>
              )}
            </div>

            {/* Card body */}
            <div className="p-4 space-y-2">
              <div className="flex items-center gap-1 text-xs text-slate-500 font-medium">
                <span>{displayBrand}</span>

              </div>
              <h3 className="font-bold text-slate-900 text-sm leading-snug line-clamp-2">
                {displayTitle}
              </h3>
              <p className="flex items-center gap-1 text-xs text-slate-500">
                <MapPin className="w-3.5 h-3.5 text-slate-400 shrink-0" />
                <span className="truncate">{displayLocation}</span>
              </p>
              <div className="pt-2 border-t border-slate-100 flex items-baseline justify-between">
                <span className="text-xs text-slate-500">
                  {serviceDetails?.priceUnit ? `Khoảng giá (${serviceDetails.priceUnit})` : 'Khoảng giá'}
                </span>
                <span className="text-sm font-bold text-rose-600">
                  {priceLabel}
                </span>
              </div>
            </div>

            {/* Card meta */}
            <div className="px-4 py-2.5 bg-slate-50 border-t border-slate-100 flex items-center justify-between text-[11px] text-slate-500">
              <span className="flex items-center gap-1 text-amber-700">
                <Gift className="w-3.5 h-3.5" /> Trao đổi cùng nhà cung cấp
              </span>
              <span className="flex items-center gap-1">
                <Eye className="w-3 h-3" /> Lượt xem trên trang công khai
              </span>
            </div>
            <div className="p-2.5 bg-slate-50 border-t border-slate-100 text-center">
              <span className="inline-flex items-center gap-1.5 text-xs font-semibold text-slate-700">
                <Eye className="w-3.5 h-3.5 text-slate-400" /> Xem chi tiết dịch vụ
              </span>
            </div>
          </article>

          <p className="mt-3 text-[11px] text-center text-slate-400">
            💡 Khách hàng bấm vào thẻ để mở trang chi tiết dịch vụ đầy đủ
          </p>
        </div>
      ) : (
        /* ── 2. Trang chi tiết đầy đủ preview (Full live page simulation per Image 2) ── */
        <div className="space-y-4 max-w-5xl mx-auto">
          {/* Breadcrumb Simulation Bar */}
          <div className="flex items-center justify-between py-1.5 px-3 bg-white/80 rounded-xl border border-slate-200/80 text-xs text-slate-500">
            <div className="flex items-center gap-1.5 overflow-hidden truncate">
              <span>Trang chủ</span>
              <span>/</span>
              <span>Khám phá</span>
              <span>/</span>
              <span className="text-emerald-700 font-medium">{displayCategory}</span>
              <span>/</span>
              <span className="truncate text-slate-700 font-semibold">{displayTitle}</span>
            </div>
            <div className="flex items-center gap-1 text-[11px] text-slate-400 shrink-0">
              <Share2 className="w-3 h-3" />
              <span>Chia sẻ</span>
            </div>
          </div>

          {/* Top Primary Tabs (Tham khảo Ảnh 2: THÔNG TIN CHUNG & ĐÁNH GIÁ) */}
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => setDetailNavTab('info')}
              className={`inline-flex items-center gap-2 px-5 py-2 rounded-full text-xs font-bold tracking-wide transition shadow-2xs ${
                detailNavTab === 'info'
                  ? 'bg-[#5e7c3b] text-white ring-2 ring-[#5e7c3b]/20'
                  : 'bg-white text-slate-600 hover:text-slate-900 border border-slate-200'
              }`}
            >
              <Info className="w-3.5 h-3.5" />
              <span>THÔNG TIN CHUNG</span>
            </button>
            <button
              type="button"
              onClick={() => { setDetailNavTab('reviews'); reviewSection.current?.scrollIntoView({ behavior: 'smooth', block: 'center' }); }}
              className={`inline-flex items-center gap-2 px-5 py-2 rounded-full text-xs font-bold tracking-wide transition ${
                detailNavTab === 'reviews'
                  ? 'bg-[#5e7c3b] text-white ring-2 ring-[#5e7c3b]/20'
                  : 'bg-white text-slate-600 hover:text-slate-900 border border-slate-200'
              }`}
            >
              <Star className="w-3.5 h-3.5 text-amber-500" />
              <span>ĐÁNH GIÁ & PHẢN HỒI</span>
            </button>
          </div>

          {/* Main Content Layout: Grid with Left Content + Right Sticky Action Bar */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
            {/* ── Left Content Column (8 cols) ── */}
            <div className="lg:col-span-8 space-y-5">
              {/* 1. Hero Gallery */}
              <div className="bg-white rounded-2xl border border-slate-200 overflow-hidden shadow-xs p-3">
                <div className="relative aspect-[16/9] bg-slate-100 rounded-xl overflow-hidden">
                  {selectedImage ? (
                    <img
                      src={selectedImage}
                      alt={displayTitle}
                      className="w-full h-full object-cover transition duration-300"
                    />
                  ) : (
                    <div className="w-full h-full flex flex-col items-center justify-center text-slate-400 gap-2 bg-rose-50/30">
                      <ImageIcon className="w-12 h-12 text-rose-300" />
                      <span className="text-xs">Chưa có hình ảnh nào được tải lên</span>
                    </div>
                  )}

                  {/* Tag badge */}
                  <div className="absolute top-3 left-3">
                    <span className="px-3 py-1 bg-white/95 backdrop-blur-xs text-slate-800 text-xs font-bold rounded-lg shadow-2xs flex items-center gap-1.5">
                      <Tag className="w-3.5 h-3.5 text-rose-500" />
                      {displayCategory}
                    </span>
                  </div>

                  {/* Navigation arrows */}
                  {currentImages.length > 1 && (
                    <>
                      <button
                        type="button"
                        onClick={() => setActiveImg(i => Math.max(0, i - 1))}
                        disabled={activeImg === 0}
                        className="absolute left-3 top-1/2 -translate-y-1/2 p-2 rounded-full bg-white/90 text-slate-700 shadow-md hover:bg-white disabled:opacity-30 transition"
                        aria-label="Ảnh trước"
                      >
                        <ChevronLeft className="w-4 h-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => setActiveImg(i => Math.min(currentImages.length - 1, i + 1))}
                        disabled={activeImg === currentImages.length - 1}
                        className="absolute right-3 top-1/2 -translate-y-1/2 p-2 rounded-full bg-white/90 text-slate-700 shadow-md hover:bg-white disabled:opacity-30 transition"
                        aria-label="Ảnh sau"
                      >
                        <ChevronRight className="w-4 h-4" />
                      </button>
                      <div className="absolute bottom-3 right-3 px-2.5 py-1 bg-black/60 text-white text-xs font-medium rounded-lg">
                        {activeImg + 1} / {currentImages.length} ảnh
                      </div>
                    </>
                  )}
                </div>

                {/* Thumbnails strip */}
                {currentImages.length > 1 && (
                  <div className="flex gap-2 overflow-x-auto pt-3 pb-1 no-scrollbar">
                    {currentImages.map((src, idx) => (
                      <button
                        key={idx}
                        type="button"
                        onClick={() => setActiveImg(idx)}
                        className={`shrink-0 w-16 h-16 rounded-xl overflow-hidden border-2 transition ${
                          idx === activeImg ? 'border-[#5e7c3b] scale-102 shadow-2xs' : 'border-slate-200 opacity-60 hover:opacity-100'
                        }`}
                      >
                        <img src={studioImage(src) || src} alt="" className="w-full h-full object-cover" />
                      </button>
                    ))}
                  </div>
                )}
              </div>

              {/* 2. Giới thiệu dịch vụ Card (Tham khảo Ảnh 2) */}
              <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-3 shadow-xs">
                <h2 className="text-base font-bold text-slate-900">
                  Giới thiệu dịch vụ
                </h2>
                <div className="text-xs sm:text-sm text-slate-600 leading-relaxed whitespace-pre-wrap">
                  {description.trim() || `${displayBrand} chuyên cung cấp dịch vụ ${displayCategory.toLowerCase()} chuyên nghiệp. Hỗ trợ tư vấn tận tâm và cam kết chất lượng tốt nhất trong ngày vui trọng đại của bạn.`}
                </div>

                {/* Highlights & Short description if available */}
                {hasShortDesc && (
                  <div className="p-3 bg-amber-50/70 border border-amber-200/80 rounded-xl text-xs text-amber-900 leading-relaxed">
                    <b>Tóm tắt:</b> {serviceDetails?.shortDescription}
                  </div>
                )}

                {hasHighlights && (
                  <div className="space-y-1.5 pt-2 border-t border-slate-100">
                    <h3 className="text-xs font-bold text-slate-700 flex items-center gap-1.5">
                      <Sparkles className="w-3.5 h-3.5 text-amber-500" />
                      <span>Điểm nổi bật của gói dịch vụ:</span>
                    </h3>
                    <p className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap bg-slate-50 p-3 rounded-xl border border-slate-100">
                      {serviceDetails?.highlights}
                    </p>
                  </div>
                )}
              </div>

              {/* 3. BỘ SƯU TẬP (Yêu cầu mới: Để trống mặc định, NCC có thể tạo mới 1 BST rồi thêm ảnh vào) */}
              <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-4 shadow-xs">
                <div className="flex items-center justify-between">
                  <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
                    <LayoutGrid className="w-4 h-4 text-[#5e7c3b]" />
                    <span>Bộ sưu tập</span>
                  </h2>
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-slate-500">
                      {collections.length} bộ sưu tập
                    </span>
                    {onOpenCreateCollection && (
                      <button
                        type="button"
                        onClick={onOpenCreateCollection}
                        className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-semibold text-[#5e7c3b] hover:bg-emerald-50 border border-emerald-200 transition"
                      >
                        <Plus className="w-3.5 h-3.5" />
                        <span>Tạo mới BST</span>
                      </button>
                    )}
                  </div>
                </div>

                {collections.length === 0 ? (
                  /* Để trống theo yêu cầu: Hiển thị trạng thái hướng dẫn tạo bộ sưu tập mới */
                  <div className="py-8 px-4 text-center rounded-2xl border-2 border-dashed border-slate-200 bg-slate-50/50 flex flex-col items-center justify-center space-y-3">
                    <div className="w-12 h-12 rounded-2xl bg-emerald-50 text-[#5e7c3b] flex items-center justify-center">
                      <FolderPlus className="w-6 h-6" />
                    </div>
                    <div className="space-y-1 max-w-sm">
                      <h4 className="text-xs font-bold text-slate-800">
                        Chưa có bộ sưu tập nào
                      </h4>
                      <p className="text-[11px] text-slate-500 leading-relaxed">
                        Nhà cung cấp có thể tạo mới bộ sưu tập theo chủ đề và thêm các hình ảnh thực tế vào để khách hàng dễ dàng tham khảo.
                      </p>
                    </div>
                    {onOpenCreateCollection && (
                      <button
                        type="button"
                        onClick={onOpenCreateCollection}
                        className="inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-[#5e7c3b] text-white text-xs font-bold hover:bg-[#4d692e] shadow-2xs transition"
                      >
                        <Plus className="w-3.5 h-3.5" />
                        <span>Tạo mới 1 bộ sưu tập</span>
                      </button>
                    )}
                  </div>
                ) : (
                  /* Hiển thị danh sách các bộ sưu tập do NCC đã tạo */
                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    {collections.map(col => (
                      <div key={col.id} className="group rounded-2xl border border-slate-200/90 overflow-hidden bg-slate-50/50 hover:shadow-md transition">
                        {/* Photos collage */}
                        <div className="aspect-[4/3] bg-slate-200 grid grid-cols-2 grid-rows-2 gap-0.5 overflow-hidden">
                          {col.images.slice(0, 4).map((src, i) => (
                            <div key={i} className="relative w-full h-full overflow-hidden bg-slate-100">
                              <img
                                src={studioImage(src) || src}
                                alt=""
                                className="w-full h-full object-cover group-hover:scale-105 transition duration-300"
                              />
                            </div>
                          ))}
                          {Array.from({ length: Math.max(0, 4 - col.images.length) }).map((_, i) => (
                            <div key={`ph-${i}`} className="flex items-center justify-center bg-slate-100 text-slate-300">
                              <ImageIcon className="w-6 h-6" />
                            </div>
                          ))}
                        </div>

                        <div className="p-3.5 bg-white flex items-center justify-between">
                          <div className="min-w-0 pr-2">
                            <h3 className="text-xs font-bold text-slate-900 tracking-wide uppercase truncate">
                              {col.name}
                            </h3>
                            <p className="text-[11px] text-slate-500 flex items-center gap-1 mt-0.5">
                              <ImageIcon className="w-3 h-3 text-slate-400 shrink-0" />
                              <span>{col.images.length} ẢNH TRONG ALBUM</span>
                            </p>
                          </div>
                          <div className="flex items-center gap-1 shrink-0">
                            {onDeleteCollection && (
                              <button
                                type="button"
                                onClick={() => onDeleteCollection(col.id)}
                                title="Xóa bộ sưu tập này"
                                className="p-1.5 rounded-lg text-slate-400 hover:text-red-600 hover:bg-red-50 transition"
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            )}
                            <div className="w-7 h-7 rounded-full bg-slate-100 flex items-center justify-center text-slate-600 group-hover:bg-[#5e7c3b] group-hover:text-white transition">
                              <ArrowRight className="w-3.5 h-3.5" />
                            </div>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* 4. CẬP NHẬT MỚI NHẤT (Yêu cầu mới: Có thể thêm ảnh hoặc viết bài đều được) */}
              <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-4 shadow-xs">
                <div className="flex items-center justify-between">
                  <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
                    <Sparkles className="w-4 h-4 text-amber-500" />
                    <span>Cập nhật mới nhất</span>
                  </h2>
                  <div className="flex items-center gap-2">
                    {hasUpdate ? (
                      <span className="text-xs font-medium text-[#5e7c3b] bg-emerald-50 px-2.5 py-0.5 rounded-full border border-emerald-100">
                        Vừa cập nhật
                      </span>
                    ) : (
                      <span className="text-xs text-slate-400">Chưa có bản tin</span>
                    )}
                    {onOpenUpdateEditor && (
                      <button
                        type="button"
                        onClick={onOpenUpdateEditor}
                        className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-semibold text-amber-700 hover:bg-amber-50 border border-amber-200 transition"
                      >
                        <Pencil className="w-3 h-3" />
                        <span>{hasUpdate ? 'Sửa cập nhật' : 'Thêm cập nhật'}</span>
                      </button>
                    )}
                  </div>
                </div>

                {!hasUpdate ? (
                  /* Trạng thái để trống khi chưa có bài viết hay ảnh nào */
                  <div className="py-7 px-4 text-center rounded-2xl border-2 border-dashed border-slate-200 bg-slate-50/50 flex flex-col items-center justify-center space-y-2.5">
                    <div className="w-11 h-11 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center">
                      <Sparkles className="w-5 h-5" />
                    </div>
                    <div className="space-y-1 max-w-sm">
                      <h4 className="text-xs font-bold text-slate-800">
                        Chưa có cập nhật mới nào
                      </h4>
                      <p className="text-[11px] text-slate-500 leading-relaxed">
                        Nhà cung cấp có thể viết thông báo, ưu đãi hoặc tải lên hình ảnh mới nhất để giới thiệu đến các cặp đôi.
                      </p>
                    </div>
                    {onOpenUpdateEditor && (
                      <button
                        type="button"
                        onClick={onOpenUpdateEditor}
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-amber-600 text-white text-xs font-semibold hover:bg-amber-700 shadow-2xs transition"
                      >
                        <Plus className="w-3.5 h-3.5" />
                        <span>Viết cập nhật hoặc thêm ảnh</span>
                      </button>
                    )}
                  </div>
                ) : (
                  /* Hiển thị bài cập nhật: có thể chỉ có ảnh, chỉ có bài viết, hoặc cả hai */
                  <div className="rounded-2xl border border-slate-200 overflow-hidden bg-white shadow-2xs space-y-3">
                    {/* Header bài đăng của Vendor */}
                    <div className="p-4 pb-0 flex items-center justify-between">
                      <div className="flex items-center gap-2.5">
                        <div className="w-9 h-9 rounded-full bg-[#5e7c3b]/15 text-[#5e7c3b] font-bold flex items-center justify-center text-xs">
                          {displayBrand.slice(0, 2).toUpperCase()}
                        </div>
                        <div>
                          <div className="flex items-center gap-1 text-xs font-bold text-slate-800">
                            <span>{displayBrand}</span>

                          </div>
                          <p className="text-[10px] text-slate-400 flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            <span>Nội dung đang soạn</span>
                          </p>
                        </div>
                      </div>
                    </div>

                    {/* 1. Phần viết (Text content nếu có) */}
                    {latestUpdate?.content && (
                      <div className="px-4 py-1 text-xs sm:text-sm text-slate-700 leading-relaxed whitespace-pre-wrap">
                        {latestUpdate.content}
                      </div>
                    )}

                    {/* 2. Phần ảnh (Image poster nếu có) */}
                    {latestUpdate?.imageUrl && (
                      <div className="px-4 pb-2">
                        <div className="relative aspect-[16/9] sm:aspect-[21/9] rounded-xl overflow-hidden bg-slate-100 border border-slate-200">
                          <img
                            src={studioImage(latestUpdate.imageUrl)}
                            alt="Cập nhật mới nhất"
                            className="w-full h-full object-cover"
                          />
                        </div>
                      </div>
                    )}

                    {/* Footer bar */}
                    <div className="px-4 py-2.5 bg-slate-50 border-t border-slate-100 flex items-center justify-between text-[11px] text-slate-500">
                      <span className="flex items-center gap-1 text-amber-700">
                        <Sparkles className="w-3.5 h-3.5" />
                        <span>Nội dung do nhà cung cấp soạn trên Wedding Match</span>
                      </span>
                      <span className="flex items-center gap-1">
                        <ShieldCheck className="w-3.5 h-3.5 text-[#5e7c3b]" />
                        <span>Bản xem trước</span>
                      </span>
                    </div>
                  </div>
                )}
              </div>

              {/* 5. Included & Excluded Items */}
              {(hasInclusions || hasExclusions) && (
                <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-4 shadow-xs">
                  <h2 className="text-base font-bold text-slate-800">
                    Hạng mục dịch vụ
                  </h2>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {hasInclusions && (
                      <div className="p-3.5 rounded-xl bg-emerald-50/60 border border-emerald-100 space-y-2">
                        <h4 className="text-xs font-bold text-emerald-800 flex items-center gap-1.5">
                          <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                          <span>Các hạng mục bao gồm:</span>
                        </h4>
                        <p className="text-xs text-emerald-900 leading-relaxed whitespace-pre-wrap">
                          {serviceDetails?.includedItems}
                        </p>
                      </div>
                    )}
                    {hasExclusions && (
                      <div className="p-3.5 rounded-xl bg-slate-50 border border-slate-200 space-y-2">
                        <h4 className="text-xs font-bold text-slate-700 flex items-center gap-1.5">
                          <XCircle className="w-4 h-4 text-rose-500" />
                          <span>Các hạng mục không bao gồm:</span>
                        </h4>
                        <p className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap">
                          {serviceDetails?.excludedItems}
                        </p>
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* 6. Process & Duration */}
              {(hasDuration || hasProcess || hasOptions) && (
                <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-3.5 shadow-xs">
                  <h2 className="text-base font-bold text-slate-800">
                    Thời gian & Quy trình thực hiện
                  </h2>
                  {hasDuration && (
                    <div className="flex items-center gap-2 text-xs text-slate-700">
                      <Clock className="w-4 h-4 text-[#5e7c3b]" />
                      <span className="font-semibold">Thời gian thực hiện:</span>
                      <span>{serviceDetails?.duration}</span>
                    </div>
                  )}
                  {hasProcess && (
                    <div className="space-y-1.5">
                      <h4 className="text-xs font-bold text-slate-700">Quy trình cung cấp dịch vụ:</h4>
                      <p className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap bg-slate-50 p-3 rounded-xl border border-slate-100">
                        {serviceDetails?.process}
                      </p>
                    </div>
                  )}
                  {hasOptions && (
                    <div className="space-y-1.5">
                      <h4 className="text-xs font-bold text-slate-700">Tùy chọn & Gói dịch vụ mở rộng:</h4>
                      <p className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap bg-slate-50 p-3 rounded-xl border border-slate-100">
                        {serviceDetails?.serviceOptions}
                      </p>
                    </div>
                  )}
                </div>
              )}

              {/* 7. Conditions & Notes */}
              {(hasTerms || hasAdditionalFees || hasReschedule || hasRequirements || hasNotes) && (
                <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-3.5 shadow-xs">
                  <h2 className="text-base font-bold text-slate-800">
                    Điều kiện & Lưu ý khi sử dụng
                  </h2>
                  <div className="space-y-3 text-xs text-slate-600">
                    {hasTerms && (
                      <div>
                        <span className="font-bold text-slate-700 block mb-1">Điều kiện sử dụng dịch vụ:</span>
                        <p className="whitespace-pre-wrap leading-relaxed">{serviceDetails?.terms}</p>
                      </div>
                    )}
                    {hasAdditionalFees && (
                      <div>
                        <span className="font-bold text-slate-700 block mb-1">Mô tả phụ phí dự kiến (nếu có):</span>
                        <p className="whitespace-pre-wrap leading-relaxed text-amber-900 bg-amber-50/50 p-2.5 rounded-lg border border-amber-100">
                          {serviceDetails?.additionalFees}
                        </p>
                      </div>
                    )}
                    {hasReschedule && (
                      <div>
                        <span className="font-bold text-slate-700 block mb-1">Điều kiện thay đổi lịch:</span>
                        <p className="whitespace-pre-wrap leading-relaxed">{serviceDetails?.reschedulePolicy}</p>
                      </div>
                    )}
                    {hasRequirements && (
                      <div>
                        <span className="font-bold text-slate-700 block mb-1">Yêu cầu dành cho khách hàng:</span>
                        <p className="whitespace-pre-wrap leading-relaxed">{serviceDetails?.customerRequirements}</p>
                      </div>
                    )}
                    {hasNotes && (
                      <div>
                        <span className="font-bold text-slate-700 block mb-1">Ghi chú đặc biệt:</span>
                        <p className="whitespace-pre-wrap leading-relaxed">{serviceDetails?.notes}</p>
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* 8. Portfolio & Video */}
              {hasPortfolioOrVideo && (
                <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-3 shadow-xs">
                  <h2 className="text-base font-bold text-slate-800">
                    Portfolio & Video giới thiệu
                  </h2>
                  {portfolioUrls.length > 0 && (
                    <div className="space-y-1.5">
                      <span className="text-xs font-semibold text-slate-700">Dự án & Tài liệu liên quan:</span>
                      <ul className="space-y-1.5">
                        {portfolioUrls.map((url, i) => (
                          <li key={i}>
                            <a
                              href={url}
                              target="_blank"
                              rel="noreferrer"
                              className="inline-flex items-center gap-1.5 text-xs text-rose-600 hover:text-rose-700 underline truncate max-w-full"
                            >
                              <ExternalLink className="w-3.5 h-3.5 shrink-0" />
                              <span>{url}</span>
                            </a>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                  {videoUrl && (
                    <div className="pt-2">
                      <a
                        href={videoUrl}
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-2 px-3 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-xs font-semibold text-slate-800 transition"
                      >
                        <Video className="w-4 h-4 text-rose-500" />
                        <span>Xem video giới thiệu dịch vụ</span>
                        <ArrowUpRight className="w-3.5 h-3.5" />
                      </a>
                    </div>
                  )}
                </div>
              )}

              {/* 9. Full Service Package (if applicable) */}
              {hasFullService && serviceDetails?.fullService && (
                <div className="bg-amber-50/50 rounded-2xl border border-amber-200/80 p-5 space-y-3 shadow-xs">
                  <h2 className="text-base font-bold text-[#233a42]">
                    Phạm vi tổ chức cưới trọn gói A–Z
                  </h2>
                  <p className="text-xs text-slate-600 leading-relaxed">
                    Đơn vị tổ chức chịu trách nhiệm quản lý và điều phối theo phạm vi cam kết.
                  </p>
                  <div className="space-y-2 text-xs text-slate-700 pt-2 border-t border-amber-200">
                    {serviceDetails.fullService.scope && (
                      <div>
                        <b>Phạm vi tổ chức:</b> {serviceDetails.fullService.scope}
                      </div>
                    )}
                    {serviceDetails.fullService.capacity && (
                      <div>
                        <b>Quy mô phục vụ:</b> {serviceDetails.fullService.capacity}
                      </div>
                    )}
                    {serviceDetails.fullService.stages && (
                      <div>
                        <b>Các giai đoạn thực hiện:</b> {serviceDetails.fullService.stages}
                      </div>
                    )}
                    {serviceDetails.fullService.outsourcing && (
                      <div>
                        <b>Dịch vụ thuê ngoài / chi hộ:</b> {serviceDetails.fullService.outsourcing}
                      </div>
                    )}
                    {serviceDetails.fullService.managementFeeDescription && (
                      <div>
                        <b>Mô tả phí quản lý/điều phối:</b> {serviceDetails.fullService.managementFeeDescription}
                      </div>
                    )}
                    {serviceDetails.fullService.packagePriceDescription && (
                      <div>
                        <b>Mô tả cơ cấu tổng giá gói:</b> {serviceDetails.fullService.packagePriceDescription}
                      </div>
                    )}
                    {serviceDetails.fullService.extraCostTerms && (
                      <div>
                        <b>Điều kiện phát sinh chi phí:</b> {serviceDetails.fullService.extraCostTerms}
                      </div>
                    )}
                    {serviceDetails.fullService.paymentTerms && (
                      <div>
                        <b>Tiến độ & điều kiện thanh toán:</b> {serviceDetails.fullService.paymentTerms}
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* 10. Cancellation Policy */}
              <div className="bg-white rounded-2xl border border-slate-200 p-5 space-y-2 shadow-xs">
                <h2 className="text-base font-bold text-slate-800">
                  Chính sách hủy dịch vụ
                </h2>
                <p className="text-xs text-slate-600 leading-relaxed whitespace-pre-wrap">
                  {cancellationPolicy || 'Chưa cập nhật chính sách hủy. Điều kiện áp dụng cần được trao đổi và ghi nhận trong hợp đồng tương ứng.'}
                </p>
              </div>

              {/* 11. Reviews Placeholder */}
              <div ref={reviewSection} className="bg-white rounded-2xl border border-slate-200 p-5 space-y-2 shadow-xs">
                <div className="flex items-center justify-between">
                  <h2 className="text-base font-bold text-slate-800 flex items-center gap-1.5">
                    <Star className="w-4 h-4 text-amber-500 fill-amber-500" />
                    <span>Đánh giá từ các cặp đôi</span>
                  </h2>
                  <span className="text-xs text-slate-500">Bản xem trước</span>
                </div>
                <p className="text-xs text-slate-500 italic">
                  Đánh giá thật và phản hồi được hiển thị trên trang dịch vụ công khai. Bản xem trước không tải hoặc thay đổi điểm đánh giá.
                </p>
              </div>
            </div>

            {/* ── Right Column: Sticky Action & Pricing Sidebar ── */}
            <div className="lg:col-span-4 space-y-3.5 lg:sticky lg:top-2">
              {/* Main Pricing & Booking Card (Ảnh 2) */}
              <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm space-y-4">
                {/* Header Price Label */}
                <div className="text-center space-y-1 pb-3 border-b border-slate-100">
                  <span className="text-[11px] font-bold text-slate-400 tracking-wider uppercase block">
                    MỨC GIÁ THAM KHẢO
                  </span>
                  <div className="text-xl sm:text-2xl font-black text-[#5e7c3b] tracking-tight">
                    {priceLabel}
                  </div>
                </div>

                {/* Location Info Box (Ảnh 2) */}
                <div className="p-3 rounded-xl bg-slate-50 border border-slate-100 flex items-start gap-2.5 text-xs">
                  <MapPin className="w-4 h-4 text-slate-400 shrink-0 mt-0.5" />
                  <div>
                    <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">
                      ĐỊA CHỈ
                    </span>
                    <span className="font-medium text-slate-700">
                      {displayLocation}
                    </span>
                  </div>
                </div>

                {/* Working Hours Info Box (Ảnh 2) */}
                <div className="p-3 rounded-xl bg-slate-50 border border-slate-100 flex items-start gap-2.5 text-xs">
                  <Clock className="w-4 h-4 text-slate-400 shrink-0 mt-0.5" />
                  <div>
                    <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">
                      GIỜ LÀM VIỆC
                    </span>
                    <span className="font-medium text-slate-700">
                      Chưa cập nhật giờ làm việc
                    </span>
                  </div>
                </div>

                {/* Linked pricing option info if present */}
                {selectedPricingOption && (
                  <div className="p-3 bg-amber-50/60 rounded-xl border border-amber-200 text-xs text-amber-900 space-y-1">
                    <div className="font-semibold">{selectedPricingOption.name}</div>
                    <div className="text-[11px] text-amber-800">
                      Giá cơ sở: {selectedPricingOption.basePrice.toLocaleString('vi-VN')} ₫ / {selectedPricingOption.unit}
                    </div>
                  </div>
                )}

                {/* CTA Buttons (Tham khảo chính xác Ảnh 2 - Đã bỏ ghép combo) */}
                <div className="space-y-2 pt-1">
                  {/* Button 1: NHẮN TIN (Outlined) */}
                  <button
                    type="button"
                    tabIndex={-1}
                    aria-disabled="true"
                    className="w-full py-2.5 px-4 rounded-xl border-2 border-[#5e7c3b] text-[#4d692e] text-xs font-bold text-center bg-white cursor-default flex items-center justify-center gap-2 hover:bg-[#5e7c3b]/5 transition"
                  >
                    <MessageCircle className="w-4 h-4" />
                    <span>NHẮN TIN</span>
                  </button>

                  {/* Button 2: ĐẶT DỊCH VỤ (Solid Green) */}
                  <button
                    type="button"
                    tabIndex={-1}
                    aria-disabled="true"
                    className="w-full py-2.5 px-4 rounded-xl bg-[#5e7c3b] hover:bg-[#4d692e] text-white text-xs font-bold text-center shadow-xs cursor-default flex items-center justify-center gap-2 transition"
                  >
                    <Calendar className="w-4 h-4" />
                    <span>ĐẶT DỊCH VỤ</span>
                  </button>

                  {/* Button 3: XEM BẢNG GIÁ (Slate/Light) */}
                  <button
                    type="button"
                    tabIndex={-1}
                    aria-disabled="true"
                    className="w-full py-2.5 px-4 rounded-xl border border-slate-200 text-slate-700 text-xs font-bold text-center bg-slate-50 cursor-default flex items-center justify-center gap-2 hover:bg-slate-100 transition"
                  >
                    <FileText className="w-4 h-4 text-slate-500" />
                    <span>XEM BẢNG GIÁ</span>
                  </button>
                </div>

                {/* Wedding Match Trust Note */}
                <div className="p-3 rounded-xl bg-amber-50/80 border border-amber-200/70 flex items-start gap-2 text-[11px] text-amber-900 leading-snug">
                  <Sparkles className="w-4 h-4 text-amber-600 shrink-0 mt-0.5" />
                  <span>
                    Trao đổi qua <b>Wedding Match</b> để lưu lại yêu cầu tư vấn và nội dung đã thống nhất với nhà cung cấp.
                  </span>
                </div>
              </div>

              {/* Vendor Identity Card */}
              <div className="bg-white rounded-2xl border border-slate-200 p-4 space-y-3 shadow-xs">
                <div className="flex items-center gap-3">
                  <div className="w-11 h-11 rounded-2xl bg-[#5e7c3b]/15 text-[#5e7c3b] font-bold flex items-center justify-center text-sm">
                    {displayBrand.slice(0, 2).toUpperCase()}
                  </div>
                  <div className="min-w-0">
                    <h4 className="font-bold text-slate-900 text-sm flex items-center gap-1 truncate">
                      <span className="truncate">{displayBrand}</span>

                    </h4>
                    <p className="text-xs text-slate-500">Nhà cung cấp trên Wedding Match</p>
                  </div>
                </div>
                <div className="pt-2 border-t border-slate-100 text-xs text-slate-600 space-y-1.5">
                  <div className="flex items-center gap-1.5">
                    <ShieldCheck className="w-3.5 h-3.5 text-[#5e7c3b] shrink-0" />
                    <span>Thông tin giao dịch được ghi nhận trong hợp đồng</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Footer hint */}
      <div className="px-5 py-3 bg-white rounded-2xl border border-slate-200/90 text-xs text-slate-500 flex items-center justify-between shadow-2xs">
        <span className="flex items-center gap-2">
          <Sparkles className="w-4 h-4 text-[#5e7c3b] shrink-0" />
          <span>Mọi thông tin bạn điền hoặc sửa ở form bên trái đều đồng bộ ngay lập tức sang trang xem trước này</span>
        </span>
      </div>
    </div>
  );
}
