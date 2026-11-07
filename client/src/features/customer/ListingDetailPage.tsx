import { useState } from "react";
import { useParams, Link, useNavigate, useSearchParams, useLocation } from "react-router-dom";
import {
  ArrowLeft,
  MapPin,
  Star,
  Eye,
  Building2,
  ImageIcon,
  ChevronLeft,
  ChevronRight,
  Gift,
  Share2,
  CalendarDays,
  ShoppingBag,
  Check,
  Sparkles,
} from "lucide-react";
import { usePublicListingDetail } from "./hooks/useBrowse";
import { SendLeadModal } from "./components/SendLeadModal";
import { BookAppointmentModal } from "../journey/appointments/BookAppointmentModal";
import { ReviewModal } from "./components/reviews/ReviewModal";
import { IndependentReviewPanel } from "./components/reviews/IndependentReviewPanel";
import { ServiceDetails } from "./components/ServiceDetails";
import { useIndependentReviews } from "./hooks/useReviews";
import { useAuthStore } from "../../store/authStore";
import { isListingPreview, listingPreviewReturn } from "./listingPreview";
import { serviceBrowseReturn } from "./serviceFilterModel";
import { listingPricePresentation } from "./listingPriceModel";
import { useWeddingCartStore } from "../../store/weddingCartStore";
import { vnd } from "../journey/format";

export default function ListingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const browseReturn = serviceBrowseReturn((location.state as { browseReturn?: unknown } | null)?.browseReturn);
  const [searchParams] = useSearchParams();
  const role = useAuthStore((state) => state.user?.role);
  const isPreview = isListingPreview(role, searchParams.get("preview"));
  const previewReturn = listingPreviewReturn(role);
  const {
    data: listing,
    isLoading,
    isError,
  } = usePublicListingDetail(id ?? "", isPreview ? searchParams.get("revision") : null);
  const [activeImg, setActiveImg] = useState(0);
  const [isLeadModalOpen, setIsLeadModalOpen] = useState(false);
  const [isReviewModalOpen, setIsReviewModalOpen] = useState(false);
  const [isBookingOpen, setIsBookingOpen] = useState(false);
  const [shareStatus, setShareStatus] = useState("");
  const reviewTargetId = isPreview && listing?.status !== "Active" ? undefined : id;
  const reviewsQuery = useIndependentReviews("Service", reviewTargetId);
  const shopReviewsQuery = useIndependentReviews("Shop", reviewTargetId ? listing?.vendorId : undefined);
  const refetchReviews = () => {
    void reviewsQuery.refetch();
    void shopReviewsQuery.refetch();
  };

  const { hasItem: hasCartItem, addItem: addToCart } = useWeddingCartStore();
  const inCart = id ? hasCartItem(id) : false;

  if (isLoading) return <LoadingState />;
  if (isError || !listing)
    return <ErrorState onBack={() => navigate(isPreview ? previewReturn : browseReturn)} />;

  const media = listing.media ?? [];
  const images = media.map((m) => m.mediaUrl);
  const displayImages = images.length > 0 ? images : [null];
  const price = listingPricePresentation(listing);

  // Packages data
  const rawPrice = listing.pricing?.basePrice ?? listing.minPrice ?? 15000000;
  const premiumPrice = Math.max(25000000, Math.round(rawPrice * 1.6));

  const handleAddToCart = () => {
    addToCart({
      id: listing.id,
      listingId: listing.id,
      title: listing.title,
      slug: listing.slug,
      vendorBrandName: listing.vendorBrandName,
      vendorId: listing.vendorId,
      categoryName: listing.categoryName,
      price: rawPrice,
      priceFormatted: price.amount,
      unit: price.unit ?? undefined,
      imageUrl: displayImages[0],
      isCombo: listing.title.toLowerCase().includes("combo"),
    });
  };

  return (
    <main className="feature-content detail-page service-detail-page">
      {isPreview && (
        <section role="note" className="rounded-2xl border border-[#d4c894] bg-[#f5f1e6] p-4 mb-5 text-[#233a42]">
          <h1 className="font-semibold">Xem trước như khách hàng</h1>
          <p className="text-sm mt-1">
            Đây là bản xem trước chỉ đọc ({listing.status}). Xem trước không xuất bản dịch vụ; khách hàng chỉ thấy nội dung đã được duyệt và đang hoạt động.
          </p>
          {listing.latestRevision && (
            <p className="text-sm mt-2">
              Phiên bản #{listing.latestRevision.sequence} · {listing.latestRevision.status}. Nội dung này chưa thay thế bản công khai nếu chưa được duyệt.
            </p>
          )}
          <Link to={previewReturn} className="inline-block mt-2 text-sm underline">
            {role === "VendorOwner" ? "Về dịch vụ của tôi" : "Về hàng chờ kiểm duyệt"}
          </Link>
        </section>
      )}

      {/* Breadcrumb header */}
      <div>
        <div className="detail-breadcrumb">
          <button
            onClick={() => navigate(-1)}
            className="flex items-center gap-1.5 text-sm text-slate-600 hover:text-rose-600 transition font-medium cursor-pointer"
          >
            <ArrowLeft className="w-4 h-4" />
            Quay lại
          </button>
          <span className="text-slate-300">/</span>
          <Link
            to={isPreview ? previewReturn : browseReturn}
            className="text-sm text-slate-500 hover:text-rose-500 transition"
          >
            {isPreview ? (role === "VendorOwner" ? "Dịch vụ của tôi" : "Kiểm duyệt") : "Khám phá"}
          </Link>
          <span className="text-slate-300">/</span>
          <span className="text-sm text-slate-700 font-medium truncate max-w-xs">
            {listing.title}
          </span>

          {/* Actions */}
          {!isPreview && (
            <div className="ml-auto flex items-center gap-2">
              <button
                aria-label="Sao chép đường dẫn dịch vụ"
                onClick={async () => {
                  try {
                    await navigator.clipboard.writeText(window.location.href);
                    setShareStatus("Đã sao chép đường dẫn");
                    setTimeout(() => setShareStatus(""), 3000);
                  } catch {
                    setShareStatus("Chọn đường dẫn trên trình duyệt để chia sẻ");
                  }
                }}
                className="p-2 rounded-xl hover:bg-slate-100 text-slate-500 transition cursor-pointer"
              >
                <Share2 className="w-4 h-4" />
              </button>
              <span role="status" className="text-[10px]">
                {shareStatus}
              </span>
            </div>
          )}
        </div>
      </div>

      <nav className="service-detail-tabs" aria-label="Mục thông tin dịch vụ">
        <a href="#service-gallery">Hình ảnh</a>
        <a href="#service-overview">Tổng quan</a>
        {(listing.serviceDetails?.portfolioUrls?.length || listing.serviceDetails?.videoUrl) ? (
          <a href="#service-portfolio">Portfolio</a>
        ) : null}
        <a href="#service-reviews">Đánh giá</a>
        {!isPreview && <a href="#service-contact">Liên hệ</a>}
      </nav>

      <div>
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 service-detail-layout">
          {/* ── Left: Images + Info ─────────────────────────────────────────── */}
          <div className="lg:col-span-2 space-y-6">
            {/* Image gallery */}
            <div id="service-gallery" className="relative rounded-3xl overflow-hidden bg-slate-100 aspect-[16/9] service-detail-gallery">
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
                    onClick={() => setActiveImg((i) => Math.max(0, i - 1))}
                    disabled={activeImg === 0}
                    aria-label="Ảnh trước"
                    className="absolute left-4 top-1/2 -translate-y-1/2 p-2 rounded-full bg-white/90 shadow-md hover:bg-white transition disabled:opacity-40 cursor-pointer"
                  >
                    <ChevronLeft className="w-5 h-5" />
                  </button>
                  <button
                    onClick={() => setActiveImg((i) => Math.min(images.length - 1, i + 1))}
                    disabled={activeImg === images.length - 1}
                    aria-label="Ảnh sau"
                    className="absolute right-4 top-1/2 -translate-y-1/2 p-2 rounded-full bg-white/90 shadow-md hover:bg-white transition disabled:opacity-40 cursor-pointer"
                  >
                    <ChevronRight className="w-5 h-5" />
                  </button>
                  <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-1.5">
                    {images.map((_, i) => (
                      <button
                        key={i}
                        onClick={() => setActiveImg(i)}
                        aria-label={`Xem ảnh ${i + 1}`}
                        className={`w-2 h-2 rounded-full transition-all ${
                          i === activeImg ? "bg-white w-4" : "bg-white/50"
                        }`}
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
                    className={`flex-shrink-0 w-16 h-16 rounded-xl overflow-hidden border-2 transition cursor-pointer ${
                      i === activeImg
                        ? "border-rose-500"
                        : "border-transparent opacity-60 hover:opacity-100"
                    }`}
                  >
                    <img
                      src={url}
                      alt=""
                      className="w-full h-full object-cover"
                    />
                  </button>
                ))}
              </div>
            )}

            {/* Info section */}
            <div id="service-overview" className="bg-white rounded-2xl border border-rose-50 p-6 space-y-5 service-detail-info">
              {/* Title */}
              <div id="service-description">
                <h1
                  className="text-2xl font-bold text-slate-900 leading-tight"
                  style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                >
                  {listing.title}
                </h1>
                <div className="flex flex-wrap items-center gap-4 mt-3 text-sm text-slate-500">
                  <span className="flex items-center gap-1 font-semibold text-slate-700">
                    <Building2 className="w-4 h-4 text-rose-500" />
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
                    <strong className="text-slate-700">
                      {reviewsQuery.isError
                        ? "Chưa tải được đánh giá"
                        : reviewsQuery.isLoading
                          ? "Đang tải đánh giá"
                          : `${reviewsQuery.data?.summary.totalReviews ?? 0} đánh giá`}
                    </strong>
                  </span>
                </div>
              </div>

              {/* Description */}
              <div>
                <h2 className="text-base font-bold text-slate-800 mb-2">
                  Mô Tả Chi Tiết
                </h2>
                <p className="text-slate-600 text-sm leading-relaxed whitespace-pre-wrap">
                  {listing.description || "Nhà cung cấp chưa cập nhật mô tả chi tiết."}
                </p>
              </div>

              {/* ── Gói Dịch Vụ Nổi Bật (Basic & Premium) ──────────────────── */}
              <div className="pt-5 border-t border-slate-100">
                <h2 className="text-base font-bold text-slate-900 mb-3 flex items-center gap-2">
                  <Sparkles size={16} className="text-amber-500" />
                  Gói Dịch Vụ Nổi Bật
                </h2>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="p-4 rounded-2xl bg-gradient-to-br from-slate-50 to-white border border-slate-200/80 shadow-2xs space-y-2.5">
                    <div className="flex justify-between items-baseline">
                      <h3 className="font-bold text-slate-900 text-sm">Gói Tiêu Chuẩn (Basic)</h3>
                      <span className="text-xs font-bold text-rose-600">{price.amount}</span>
                    </div>
                    <ul className="text-xs text-slate-600 space-y-1.5">
                      <li className="flex items-center gap-1.5">
                        <Check size={13} className="text-emerald-600 shrink-0" />
                        <span>Hạng mục chuẩn bị và hoàn thiện cơ bản</span>
                      </li>
                      <li className="flex items-center gap-1.5">
                        <Check size={13} className="text-emerald-600 shrink-0" />
                        <span>Đội ngũ chuyên nghiệp phụ trách thực hiện</span>
                      </li>
                      <li className="flex items-center gap-1.5">
                        <Check size={13} className="text-emerald-600 shrink-0" />
                        <span>Hỗ trợ tư vấn chi tiết trước ngày cưới</span>
                      </li>
                    </ul>
                  </div>

                  <div className="p-4 rounded-2xl bg-gradient-to-br from-rose-50/40 via-white to-amber-50/30 border border-rose-200/80 shadow-2xs space-y-2.5">
                    <div className="flex justify-between items-baseline">
                      <div className="flex items-center gap-1.5">
                        <h3 className="font-bold text-slate-900 text-sm">Gói Cao Cấp (Premium)</h3>
                        <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-rose-100 text-rose-700 font-bold">Hot</span>
                      </div>
                      <span className="text-xs font-bold text-rose-600">{vnd(premiumPrice)}</span>
                    </div>
                    <ul className="text-xs text-slate-600 space-y-1.5">
                      <li className="flex items-center gap-1.5">
                        <Check size={13} className="text-emerald-600 shrink-0" />
                        <span>Trọn gói toàn diện &amp; vật liệu cao cấp</span>
                      </li>
                      <li className="flex items-center gap-1.5">
                        <Check size={13} className="text-emerald-600 shrink-0" />
                        <span>Thiết kế concept riêng biệt theo gu của cặp đôi</span>
                      </li>
                      <li className="flex items-center gap-1.5">
                        <Check size={13} className="text-emerald-600 shrink-0" />
                        <span>Hỗ trợ điều phối viên riêng trong suốt buổi lễ</span>
                      </li>
                    </ul>
                  </div>
                </div>
              </div>

              <ServiceDetails details={listing.serviceDetails} cancellationPolicy={listing.cancellationPolicy} />

              {/* ── Reviews Section (Verified Buyer - BR-009 & FM-005) ──────── */}
              <div id="service-reviews" className="pt-6 border-t border-slate-100">
                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-6">
                  <div>
                    <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
                      <Star className="w-5 h-5 text-amber-400 fill-current" />
                      Đánh giá dịch vụ{reviewsQuery.isSuccess ? ` (${reviewsQuery.data.summary.totalReviews})` : ""}
                    </h2>
                    <p className="text-xs text-slate-500 mt-0.5">
                      Chỉ những khách hàng có hợp đồng hoàn tất mới được tạo đánh giá mới. Đánh giá tư vấn cũ được giữ với nhãn riêng.
                    </p>
                  </div>

                  {!isPreview && (
                    <button
                      onClick={() => setIsReviewModalOpen(true)}
                      className="px-4 py-2 rounded-xl bg-gradient-to-r from-amber-500 to-rose-500 text-white font-bold text-xs hover:from-amber-600 hover:to-rose-600 transition shadow-xs flex items-center gap-1.5 self-start sm:self-auto cursor-pointer"
                    >
                      <span>✍️ Viết Đánh Giá</span>
                    </button>
                  )}
                </div>

                {!isPreview && (
                  <div className="space-y-5">
                    <IndependentReviewPanel targetType="Service" targetId={listing.id} targetName={listing.title} />
                    <IndependentReviewPanel targetType="Shop" targetId={listing.vendorId} targetName={listing.vendorBrandName} />
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* ── Right: Pricing + Actions ────────────────────────────────────── */}
          <div className="space-y-4">
            {/* Price card */}
            <div id="service-contact" className="bg-white rounded-2xl border border-rose-100 p-6 shadow-sm sticky top-20 service-detail-price">
              {/* Price */}
              <div className="mb-5">
                <p className="text-xs text-slate-500 font-semibold uppercase tracking-wider mb-1">
                  {price.caption}
                </p>
                <div className="flex items-baseline gap-1">
                  <span className="text-2xl font-bold text-rose-600">
                    {price.amount}
                  </span>
                  {price.unit && <span className="text-slate-500 text-sm">/ {price.unit}</span>}
                </div>
                <p className="text-xs text-slate-400 mt-1">
                  Giá chính thức sẽ được báo sau khi tư vấn
                </p>
              </div>

              {/* Voucher highlight */}
              {!isPreview && (
                <div className="mb-5 p-3 rounded-xl bg-gradient-to-r from-rose-50 to-amber-50 border border-rose-100 flex items-center gap-3">
                  <div className="w-9 h-9 rounded-xl bg-rose-100 flex items-center justify-center flex-shrink-0">
                    <Gift className="w-5 h-5 text-rose-500" />
                  </div>
                  <div>
                    <p className="text-xs font-bold text-rose-700">
                      Nhận Mã Voucher Khi Liên Hệ
                    </p>
                    <p className="text-[11px] text-slate-500">
                      Mã 8 ký tự độc nhất · Hiệu lực 30 ngày
                    </p>
                  </div>
                </div>
              )}

              {/* CTA Action Buttons */}
              {!isPreview && (
                <div className="space-y-2.5">
                  {/* Direct Add to Cart Button */}
                  <button
                    type="button"
                    onClick={handleAddToCart}
                    className={`w-full py-3.5 px-5 rounded-xl font-bold text-sm transition transform hover:-translate-y-0.5 active:translate-y-0 cursor-pointer shadow-md flex items-center justify-center gap-2 ${
                      inCart
                        ? "bg-emerald-600 hover:bg-emerald-700 text-white shadow-emerald-200"
                        : "bg-slate-900 hover:bg-slate-800 text-white shadow-slate-300"
                    }`}
                  >
                    {inCart ? <Check className="w-4 h-4" /> : <ShoppingBag className="w-4 h-4" />}
                    <span>{inCart ? "✓ Đã Có Trong Giỏ (Thêm Tiếp)" : "🛒 Thêm Vào Giỏ Hàng"}</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => setIsLeadModalOpen(true)}
                    className="service-contact-cta w-full py-3 px-5 rounded-xl text-white font-bold text-sm transition transform hover:-translate-y-0.5 active:translate-y-0 cursor-pointer shadow-md shadow-rose-200 flex items-center justify-center gap-2"
                  >
                    <span>💌</span>
                    <span>Nhận Tư Vấn &amp; Ưu Đãi Ngay</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => setIsBookingOpen(true)}
                    className="w-full py-2.5 px-5 rounded-xl bg-white hover:bg-slate-50 text-slate-700 hover:text-slate-900 border border-slate-300 hover:border-slate-400 font-semibold text-xs transition flex items-center justify-center gap-2 cursor-pointer shadow-xs"
                  >
                    <CalendarDays className="w-4 h-4 text-rose-500" />
                    <span>Đặt lịch hẹn xem trực tiếp</span>
                  </button>

                  <p className="text-center text-[11px] text-slate-400 mt-2 flex items-center justify-center gap-1.5">
                    <span className="inline-block w-1.5 h-1.5 rounded-full bg-emerald-500" />
                    <span>Miễn phí · Không ràng buộc · Thời gian phản hồi tùy nhà cung cấp</span>
                  </p>
                </div>
              )}
              {isPreview && (
                <p className="rounded-xl bg-slate-50 p-3 text-sm text-slate-600 text-center">
                  Tư vấn, đặt lịch và viết đánh giá được tắt trong bản xem trước.
                </p>
              )}

              {/* Stats */}
              <div className="mt-5 pt-5 border-t border-slate-100 grid grid-cols-2 gap-3 text-center">
                <div>
                  <p className="text-lg font-bold text-slate-800">
                    {listing.viewCount}
                  </p>
                  <p className="text-[10px] text-slate-400 font-medium uppercase">
                    Lượt xem
                  </p>
                </div>
                <div>
                  <p className="text-lg font-bold text-slate-800">
                    {reviewsQuery.isSuccess ? reviewsQuery.data.summary.totalReviews : "—"}
                  </p>
                  <p className="text-[10px] text-slate-400 font-medium uppercase">
                    Đánh giá
                  </p>
                </div>
              </div>
            </div>

            {/* Vendor card */}
            <div className="bg-white rounded-2xl border border-rose-50 p-5">
              <h3 className="text-sm font-bold text-slate-800 mb-3">
                Về Nhà Cung Cấp
              </h3>
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-rose-100 to-amber-100 flex items-center justify-center text-lg font-bold text-rose-600 flex-shrink-0">
                  {listing.vendorBrandName.charAt(0)}
                </div>
                <div>
                  <p className="font-bold text-slate-900 text-sm">
                    {listing.vendorBrandName}
                  </p>
                  <p className="text-xs text-slate-500 flex items-center gap-1 mt-0.5">
                    <MapPin className="w-3 h-3" /> {listing.location}
                  </p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {!isPreview && isBookingOpen && (
        <BookAppointmentModal
          vendorId={listing.vendorId}
          vendorName={listing.vendorBrandName}
          onClose={() => setIsBookingOpen(false)}
        />
      )}

      {/* Modal gửi Lead & sinh Voucher 8 ký tự */}
      {!isPreview && (
        <SendLeadModal
          isOpen={isLeadModalOpen}
          onClose={() => setIsLeadModalOpen(false)}
          listingId={listing.id}
          vendorId={listing.vendorId}
          vendorName={listing.vendorBrandName}
          listingTitle={listing.title}
        />
      )}

      {/* Modal đánh giá & chấm điểm Verified Buyer */}
      {!isPreview && (
        <ReviewModal
          isOpen={isReviewModalOpen}
          onClose={() => setIsReviewModalOpen(false)}
          listingId={listing.id}
          listingTitle={listing.title}
          vendorBrandName={listing.vendorBrandName}
          onSuccess={refetchReviews}
        />
      )}
    </main>
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
      <h2 className="text-xl font-bold text-slate-800 mb-2">
        Không tìm thấy dịch vụ
      </h2>
      <p className="text-sm text-slate-500 mb-6">
        Gói dịch vụ này không tồn tại hoặc đã bị ẩn.
      </p>
      <button
        onClick={onBack}
        className="flex items-center gap-2 px-5 py-2 rounded-xl bg-rose-500 text-white text-sm font-semibold hover:bg-rose-600 transition cursor-pointer"
      >
        <ArrowLeft className="w-4 h-4" />
        Quay lại khám phá
      </button>
    </div>
  );
}
