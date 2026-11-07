import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  Gift,
  Minus,
  Plus,
  ShieldCheck,
  ShoppingBag,
  Sparkles,
  Store,
  Trash2,
} from "lucide-react";
import { useWeddingCartStore, type WeddingCartItem } from "../../store/weddingCartStore";
import { vnd } from "../journey/format";
import { SendLeadModal } from "./components/SendLeadModal";

export default function CartPage() {
  const navigate = useNavigate();
  const {
    items,
    removeItem,
    updateQuantity,
    updateNotes,
    clearCart,
    getItemCount,
    getTotalAmount,
  } = useWeddingCartStore();

  const [isSubmitModalOpen, setIsSubmitModalOpen] = useState(false);
  const [selectedLeadItem, setSelectedLeadItem] = useState<WeddingCartItem | null>(null);

  const itemCount = getItemCount();
  const totalAmount = getTotalAmount();

  const handleCheckout = () => {
    if (items.length === 0) return;
    // Mở modal gửi tư vấn cho mục đầu tiên hoặc mục được chọn
    setSelectedLeadItem(items[0]);
    setIsSubmitModalOpen(true);
  };

  return (
    <main className="min-h-screen bg-[#faf8f5] py-8 sm:py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-6xl mx-auto">
        {/* Navigation Breadcrumb */}
        <div className="mb-6 flex items-center justify-between">
          <button
            type="button"
            onClick={() => navigate(-1)}
            className="inline-flex items-center gap-1.5 text-xs sm:text-sm font-semibold text-slate-600 hover:text-rose-600 transition cursor-pointer"
          >
            <ArrowLeft size={16} />
            <span>Quay lại</span>
          </button>
          <Link
            to="/browse"
            className="text-xs sm:text-sm font-semibold text-rose-600 hover:text-rose-700 transition"
          >
            + Chọn thêm dịch vụ khác
          </Link>
        </div>

        {/* Page Title */}
        <div className="mb-8">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-rose-50 text-rose-600 text-xs font-bold mb-2">
            <ShoppingBag size={14} /> Giỏ hàng dịch vụ cưới
          </div>
          <h1
            className="text-3xl sm:text-4xl font-bold text-slate-900 leading-tight"
            style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
          >
            Dịch vụ cưới hai bạn đã chọn
          </h1>
          <p className="mt-2 text-sm text-slate-600">
            Xem lại các gói dịch vụ và gửi yêu cầu tư vấn trực tiếp đến từng nhà cung cấp. Chưa thanh toán ngay.
          </p>
        </div>

        {items.length === 0 ? (
          /* Empty State */
          <div className="bg-white rounded-3xl p-10 sm:p-16 border border-slate-200/80 shadow-xs text-center max-w-2xl mx-auto">
            <div className="w-16 h-16 rounded-3xl bg-rose-50 border border-rose-100 flex items-center justify-center text-rose-400 mx-auto mb-5">
              <ShoppingBag size={32} />
            </div>
            <h2
              className="text-2xl font-bold text-slate-900"
              style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
            >
              Giỏ hàng của hai bạn đang trống
            </h2>
            <p className="mt-2 text-sm text-slate-500 leading-relaxed max-w-md mx-auto">
              Khám phá 66+ gói dịch vụ cưới chất lượng trên sàn và bấm "Thêm vào giỏ" để lưu lại những lựa chọn ưng ý nhất.
            </p>
            <div className="mt-8 flex flex-wrap justify-center gap-3">
              <Link
                to="/browse"
                className="py-3 px-6 rounded-2xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 text-white font-bold text-sm shadow-md shadow-rose-200 transition inline-flex items-center gap-2"
              >
                <span>Khám phá dịch vụ ngay</span>
                <ArrowRight size={16} />
              </Link>
              <Link
                to="/browse?keyword=combo"
                className="py-3 px-5 rounded-2xl bg-white hover:bg-slate-50 border border-slate-200 text-slate-700 font-bold text-sm transition"
              >
                Xem các gói Combo (NCC)
              </Link>
            </div>
          </div>
        ) : (
          /* Active Cart with items */
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 items-start">
            {/* Left: Items list */}
            <div className="lg:col-span-2 space-y-4">
              <div className="flex items-center justify-between pb-3 border-b border-slate-200">
                <span className="text-sm font-bold text-slate-800">
                  {itemCount} dịch vụ đã chọn
                </span>
                <button
                  type="button"
                  onClick={clearCart}
                  className="text-xs font-semibold text-slate-500 hover:text-rose-600 transition cursor-pointer"
                >
                  Xóa tất cả
                </button>
              </div>

              {items.map((item) => (
                <article
                  key={item.id}
                  className="bg-white rounded-3xl p-5 border border-slate-200/80 shadow-2xs hover:shadow-xs transition space-y-4"
                >
                  <div className="flex gap-4">
                    {/* Thumbnail */}
                    <div className="w-24 h-24 sm:w-28 sm:h-28 rounded-2xl overflow-hidden bg-slate-100 shrink-0 border border-slate-100">
                      {item.imageUrl ? (
                        <img
                          src={item.imageUrl}
                          alt={item.title}
                          className="w-full h-full object-cover"
                          loading="lazy"
                        />
                      ) : (
                        <div className="w-full h-full flex items-center justify-center text-xs text-slate-400">
                          Wedding Match
                        </div>
                      )}
                    </div>

                    {/* Details */}
                    <div className="flex-1 min-w-0 flex flex-col justify-between">
                      <div>
                        <div className="flex items-center gap-2 text-xs text-slate-500 mb-1">
                          <span className="font-semibold text-slate-700 flex items-center gap-1">
                            <Store size={12} className="text-rose-500" />
                            {item.vendorBrandName}
                          </span>
                          <span>·</span>
                          <span className="px-2 py-0.5 rounded-full bg-slate-100 text-[10px] font-medium">
                            {item.categoryName}
                          </span>
                        </div>

                        <h3 className="font-bold text-slate-900 text-sm sm:text-base leading-snug line-clamp-2">
                          <Link
                            to={`/listings/${item.listingId}`}
                            className="hover:text-rose-600 transition"
                          >
                            {item.title}
                          </Link>
                        </h3>

                        {/* If combo, show sub-services pills */}
                        {item.subServices && item.subServices.length > 0 && (
                          <div className="mt-2 flex flex-wrap gap-1">
                            {item.subServices.slice(0, 3).map((sub, idx) => (
                              <span
                                key={idx}
                                className="text-[10px] px-2 py-0.5 rounded-md bg-rose-50 text-rose-700 border border-rose-100"
                              >
                                ✓ {sub}
                              </span>
                            ))}
                            {item.subServices.length > 3 && (
                              <span className="text-[10px] text-slate-400">
                                +{item.subServices.length - 3} dịch vụ khác
                              </span>
                            )}
                          </div>
                        )}
                      </div>

                      <div className="mt-2 flex flex-wrap items-baseline justify-between gap-2">
                        <div className="text-sm sm:text-base font-bold text-rose-600">
                          {item.price > 0 ? (
                            <>
                              {vnd(item.price * item.quantity)}
                              {item.quantity > 1 && (
                                <span className="text-xs text-slate-400 font-normal ml-1">
                                  ({vnd(item.price)} × {item.quantity})
                                </span>
                              )}
                            </>
                          ) : (
                            item.priceFormatted
                          )}
                        </div>

                        {/* Quantity Controls & Delete */}
                        <div className="flex items-center gap-3">
                          <div className="flex items-center border border-slate-200 rounded-xl overflow-hidden bg-slate-50">
                            <button
                              type="button"
                              onClick={() => updateQuantity(item.id, item.quantity - 1)}
                              aria-label="Giảm số lượng"
                              className="w-7 h-7 flex items-center justify-center text-slate-600 hover:bg-slate-200 transition cursor-pointer"
                            >
                              <Minus size={13} />
                            </button>
                            <span className="w-8 text-center text-xs font-bold text-slate-800">
                              {item.quantity}
                            </span>
                            <button
                              type="button"
                              onClick={() => updateQuantity(item.id, item.quantity + 1)}
                              aria-label="Tăng số lượng"
                              className="w-7 h-7 flex items-center justify-center text-slate-600 hover:bg-slate-200 transition cursor-pointer"
                            >
                              <Plus size={13} />
                            </button>
                          </div>

                          <button
                            type="button"
                            onClick={() => removeItem(item.id)}
                            className="p-1.5 text-slate-400 hover:text-rose-600 transition cursor-pointer rounded-lg hover:bg-rose-50"
                            title="Xóa dịch vụ khỏi giỏ"
                            aria-label={`Xóa ${item.title}`}
                          >
                            <Trash2 size={16} />
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Customer Note input for this item & Individual Lead CTA */}
                  <div className="pt-2 border-t border-slate-100 flex flex-col sm:flex-row gap-2 items-center">
                    <input
                      type="text"
                      placeholder="Ghi chú yêu cầu riêng cho dịch vụ này..."
                      value={item.notes || ""}
                      onChange={(e) => updateNotes(item.id, e.target.value)}
                      className="w-full sm:flex-1 px-3 py-1.5 text-xs rounded-xl border border-slate-200/80 bg-slate-50/50 focus:bg-white focus:outline-none focus:ring-1 focus:ring-rose-400 text-slate-700"
                    />
                    <button
                      type="button"
                      onClick={() => {
                        setSelectedLeadItem(item);
                        setIsSubmitModalOpen(true);
                      }}
                      className="w-full sm:w-auto shrink-0 text-xs font-bold text-rose-600 hover:text-rose-700 bg-rose-50 hover:bg-rose-100 px-3 py-1.5 rounded-xl border border-rose-200/70 transition cursor-pointer flex items-center justify-center gap-1.5"
                    >
                      💌 Tư vấn gói này ({item.categoryName})
                    </button>
                  </div>
                </article>
              ))}
            </div>

            {/* Right: Summary Card */}
            <aside className="lg:col-span-1 space-y-4 sticky top-20">
              <div className="bg-white rounded-3xl p-6 border border-slate-200/80 shadow-xs space-y-5">
                <h3
                  className="text-lg font-bold text-slate-900 pb-3 border-b border-slate-100"
                  style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                >
                  Tổng quan giỏ hàng
                </h3>

                <div className="space-y-3 text-xs sm:text-sm">
                  <div className="flex justify-between text-slate-600">
                    <span>Số lượng dịch vụ đã chọn</span>
                    <span className="font-semibold text-slate-800">{itemCount} gói</span>
                  </div>

                  <div className="flex justify-between text-slate-600">
                    <span>Số nhà cung cấp</span>
                    <span className="font-semibold text-slate-800">
                      {new Set(items.map((i) => i.vendorBrandName)).size} NCC
                    </span>
                  </div>

                  <div className="pt-3 border-t border-slate-100 flex items-baseline justify-between">
                    <div>
                      <span className="font-bold text-slate-900 text-base">Tổng dự kiến</span>
                      <p className="text-[10px] text-slate-400">Giá chính thức sẽ được NCC chốt qua tư vấn</p>
                    </div>
                    <span className="text-xl font-extrabold text-rose-600">
                      {vnd(totalAmount)}
                    </span>
                  </div>
                </div>

                {/* Voucher Highlight */}
                <div className="p-3.5 rounded-2xl bg-gradient-to-r from-amber-50 to-rose-50 border border-rose-100 flex items-center gap-3">
                  <div className="w-8 h-8 rounded-xl bg-rose-100 flex items-center justify-center text-rose-600 shrink-0">
                    <Gift size={16} />
                  </div>
                  <div>
                    <p className="text-xs font-bold text-rose-800 flex items-center gap-1">
                      <Sparkles size={12} /> Tặng Voucher đến 500.000đ
                    </p>
                    <p className="text-[10px] text-slate-500 mt-0.5">
                      Tự động cấp mã khi gửi yêu cầu tư vấn thành công
                    </p>
                  </div>
                </div>

                {/* CTAs */}
                <div className="space-y-2.5 pt-2">
                  <button
                    type="button"
                    onClick={handleCheckout}
                    className="w-full py-3.5 px-4 rounded-2xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 text-white font-bold text-sm shadow-md shadow-rose-200 transition cursor-pointer flex items-center justify-center gap-2"
                  >
                    <span>💌 Gửi Yêu Cầu Báo Giá & Tư Vấn</span>
                  </button>

                  <Link
                    to="/browse"
                    className="w-full py-3 px-4 rounded-2xl bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold text-xs text-center transition block"
                  >
                    + Chọn thêm dịch vụ khác trên sàn
                  </Link>
                </div>

                <div className="pt-3 border-t border-slate-100 flex items-center gap-2 text-[11px] text-slate-400">
                  <ShieldCheck size={14} className="text-emerald-500 shrink-0" />
                  <span>Miễn phí tư vấn · Không ràng buộc · Smart Privacy bảo mật số điện thoại</span>
                </div>
              </div>
            </aside>
          </div>
        )}

        {/* Modal gửi tư vấn khi bấm Gửi Yêu Cầu từ giỏ hàng */}
        {isSubmitModalOpen && selectedLeadItem && (
          <SendLeadModal
            isOpen={isSubmitModalOpen}
            onClose={() => setIsSubmitModalOpen(false)}
            listingId={selectedLeadItem.listingId}
            vendorId={selectedLeadItem.vendorId}
            vendorName={selectedLeadItem.vendorBrandName}
            listingTitle={selectedLeadItem.title}
            categoryName={selectedLeadItem.categoryName}
            subServices={selectedLeadItem.subServices}
            initialNotes={selectedLeadItem.notes}
          />
        )}
      </div>
    </main>
  );
}

