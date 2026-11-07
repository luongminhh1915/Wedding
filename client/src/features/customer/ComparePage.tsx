import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ArrowLeft, CheckCircle2, Star, X } from "lucide-react";
import { useCompareStore, type CompareItem } from "./compareStore";
import { SendLeadModal } from "./components/SendLeadModal";

// Mẫu dịch vụ mặc định theo đúng hình minh họa nếu giỏ so sánh trống
const DEFAULT_COMPARE_DEMO: CompareItem[] = [
  {
    id: "demo-lumiere-studio",
    title: "Gói Phóng Sự Cưới Hoàng Gia Lumiere",
    vendorBrandName: "Lumiere Studio",
    priceAmount: 12000000,
    priceFormatted: "Từ 12.000.000đ",
    rating: 4.9,
    totalReviews: 120,
    location: "Hà Nội, TP.HCM",
    style: "Hiện đại",
    categoryName: "Quay phim & Chụp ảnh cưới",
    imageUrl: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=600&q=80",
    prepTime: "2-3 tháng",
    isVerified: true,
  },
  {
    id: "demo-the-little-flower",
    title: "Trang Trí Tiệc Cưới Phong Cách Vườn Lãng Mạn",
    vendorBrandName: "The Little Flower",
    priceAmount: 8000000,
    priceFormatted: "Từ 8.000.000đ",
    rating: 4.8,
    totalReviews: 86,
    location: "Hà Nội, Đà Nẵng",
    style: "Lãng mạn",
    categoryName: "Trang trí tiệc cưới & hoa tươi",
    imageUrl: "https://images.unsplash.com/photo-1511285560929-80b456fea0bc?auto=format&fit=crop&w=600&q=80",
    prepTime: "1-2 tháng",
    isVerified: true,
  },
  {
    id: "demo-amour-decoration",
    title: "Thiết Kế Không Gian Cưới Tối Giản Tinh Tế",
    vendorBrandName: "Amour Decoration",
    priceAmount: 15000000,
    priceFormatted: "Từ 15.000.000đ",
    rating: 4.6,
    totalReviews: 58,
    location: "Hà Nội, TP.HCM",
    style: "Tối giản",
    categoryName: "Thiết kế & Trang trí tiệc",
    imageUrl: "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=600&q=80",
    prepTime: "2-4 tháng",
    isVerified: true,
  },
];

export default function ComparePage() {
  const navigate = useNavigate();
  const { items: storedItems, removeItem, clear } = useCompareStore();
  const [selectedLeadItem, setSelectedLeadItem] = useState<CompareItem | null>(null);

  // Nếu người dùng chưa chọn món nào từ catalog, hiển thị 3 dịch vụ mẫu chuẩn đúng hình minh họa của người dùng
  const items = storedItems.length > 0 ? storedItems : DEFAULT_COMPARE_DEMO;
  const isDemo = storedItems.length === 0;

  return (
    <main className="min-h-screen bg-[#faf8f5] py-8 sm:py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-7xl mx-auto">
        {/* Back navigation */}
        <div className="mb-6">
          <button
            type="button"
            onClick={() => navigate(-1)}
            className="inline-flex items-center gap-1.5 text-xs sm:text-sm font-semibold text-slate-600 hover:text-rose-600 transition cursor-pointer"
          >
            <ArrowLeft size={16} />
            <span>Quay lại</span>
          </button>
        </div>

        {/* Page Header */}
        <div className="mb-8">
          <h1
            className="text-3xl sm:text-4xl font-bold text-slate-900 leading-tight"
            style={{ fontFamily: "'Playfair Display', serif" }}
          >
            So sánh dịch vụ
          </h1>
          <p className="mt-2 text-sm text-slate-600 font-serif italic">
            Nhìn rõ sự khác biệt, chọn đúng dịch vụ phù hợp nhất.
          </p>
        </div>

        {/* Subheader bar: item count & clear */}
        <div className="flex items-center justify-between gap-4 pb-4 mb-6 border-b border-slate-200">
          <p className="text-xs sm:text-sm font-semibold text-slate-800">
            {items.length} dịch vụ được chọn {isDemo && <span className="text-xs text-rose-600 font-normal italic">(Mẫu minh họa)</span>}
          </p>
          {!isDemo && (
            <button
              type="button"
              onClick={clear}
              className="text-xs font-semibold text-slate-500 hover:text-rose-600 transition cursor-pointer"
            >
              Xóa tất cả
            </button>
          )}
        </div>

        {/* Comparison Table / Grid */}
        <div className="overflow-x-auto pb-6">
          <div className="min-w-[760px] grid grid-cols-4 gap-4 sm:gap-6 bg-white rounded-3xl p-6 sm:p-8 border border-slate-200/80 shadow-xs">
            {/* Criteria Column */}
            <div className="col-span-1 pt-64 space-y-6 text-xs sm:text-sm font-bold text-slate-600 border-r border-slate-100 pr-4">
              <div className="h-10 flex items-center">Phong cách</div>
              <div className="h-10 flex items-center">Giá gói cơ bản</div>
              <div className="h-10 flex items-center">Thời gian chuẩn bị</div>
              <div className="h-10 flex items-center">Địa điểm</div>
              <div className="h-10 flex items-center">Đánh giá từ khách hàng</div>
              <div className="h-10 flex items-center">Đã xác minh thông tin</div>
              <div className="h-10 flex items-center">Hỗ trợ tư vấn</div>
            </div>

            {/* Compared Services Columns */}
            <div className={`col-span-3 grid grid-cols-${Math.min(items.length, 3)} gap-4 sm:gap-6`}>
              {items.slice(0, 3).map((item) => (
                <div key={item.id} className="flex flex-col">
                  {/* Top Card Box */}
                  <div className="relative mb-6">
                    {!isDemo && (
                      <button
                        type="button"
                        onClick={() => removeItem(item.id)}
                        className="absolute top-2 right-2 z-10 w-6 h-6 rounded-full bg-black/60 hover:bg-black/80 text-white flex items-center justify-center transition"
                        title="Xóa khỏi so sánh"
                      >
                        <X size={12} />
                      </button>
                    )}
                    <div className="aspect-[16/10] rounded-2xl overflow-hidden bg-slate-100 mb-3 border border-slate-100">
                      {item.imageUrl ? (
                        <img
                          src={item.imageUrl}
                          alt={item.title}
                          className="w-full h-full object-cover"
                          loading="lazy"
                        />
                      ) : (
                        <div className="w-full h-full flex items-center justify-center text-slate-300 text-xs">
                          Wedding Match
                        </div>
                      )}
                    </div>
                    <h3 className="font-bold text-slate-900 text-sm sm:text-base leading-snug line-clamp-1">
                      {item.vendorBrandName}
                    </h3>
                    <div className="flex items-center gap-1.5 mt-1 text-xs text-slate-500">
                      <Star size={13} className="text-amber-400 fill-amber-400" />
                      <span className="font-bold text-slate-800">{item.rating}</span>
                      <span>({item.totalReviews} đánh giá)</span>
                    </div>
                    <p className="mt-2 text-sm font-bold text-rose-600">
                      {item.priceFormatted}
                    </p>
                    <Link
                      to={item.id.startsWith("demo-") ? "/browse" : `/listings/${item.id}`}
                      className="mt-3 block w-full py-2.5 px-3 rounded-xl bg-[#1c2d37] hover:bg-[#253d4b] text-white font-bold text-xs text-center transition shadow-2xs"
                    >
                      Xem chi tiết
                    </Link>
                  </div>

                  {/* Criteria Values Rows */}
                  <div className="space-y-6 text-xs sm:text-sm text-slate-700">
                    <div className="h-10 flex items-center font-medium">{item.style}</div>
                    <div className="h-10 flex items-center font-bold text-rose-600">
                      {item.priceAmount.toLocaleString("vi-VN")}đ
                    </div>
                    <div className="h-10 flex items-center">{item.prepTime || "2-3 tháng"}</div>
                    <div className="h-10 flex items-center truncate" title={item.location}>
                      {item.location}
                    </div>
                    <div className="h-10 flex items-center font-medium">
                      {item.rating}/5 ({item.totalReviews})
                    </div>
                    <div className="h-10 flex items-center">
                      <span className="inline-flex items-center gap-1 text-emerald-700 font-semibold text-xs">
                        <CheckCircle2 size={16} className="text-emerald-500" />
                        <span>Đã xác minh</span>
                      </span>
                    </div>
                    <div className="h-10 flex items-center">
                      <span className="inline-flex items-center gap-1 text-emerald-700 font-semibold text-xs">
                        <CheckCircle2 size={16} className="text-emerald-500" />
                        <span>Hỗ trợ 24/7</span>
                      </span>
                    </div>
                  </div>

                  {/* Bottom Action Button */}
                  <div className="mt-8 pt-4 border-t border-slate-100">
                    <button
                      type="button"
                      onClick={() => setSelectedLeadItem(item)}
                      className="w-full py-3 px-4 rounded-xl bg-[#e5a999] hover:bg-[#d89786] text-slate-900 font-bold text-xs transition cursor-pointer shadow-xs text-center"
                    >
                      Đặt lịch tư vấn
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Modal đặt lịch tư vấn khi bấm Đặt lịch tư vấn từ bảng so sánh */}
        {selectedLeadItem && (
          <SendLeadModal
            isOpen={!!selectedLeadItem}
            onClose={() => setSelectedLeadItem(null)}
            listingId={selectedLeadItem.id.startsWith("demo-") ? "00000000-0000-0000-0000-000000000000" : selectedLeadItem.id}
            listingTitle={selectedLeadItem.title}
            vendorName={selectedLeadItem.vendorBrandName}
          />
        )}
      </div>
    </main>
  );
}
