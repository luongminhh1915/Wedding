import { Link } from "react-router-dom";
import { ArrowUpRight, Check, Eye, Gift, ImageIcon, MapPin, Scale, ShoppingBag } from "lucide-react";
import type { ListingSummaryDto } from "../../../types/listing.types";
import { listingPricePresentation } from "../listingPriceModel";
import { useCompareStore, listingToCompareItem } from "../compareStore";
import { useWeddingCartStore } from "../../../store/weddingCartStore";

export default function ListingCard({
  listing,
  returnPath,
}: {
  listing: ListingSummaryDto;
  returnPath?: string;
}) {
  const price = listingPricePresentation(listing);
  const { hasItem: hasCompare, addItem: addCompare, removeItem: removeCompare } = useCompareStore();
  const isCompared = hasCompare(listing.id);

  const { hasItem: hasCartItem, addItem: addToCart } = useWeddingCartStore();
  const inCart = hasCartItem(listing.id);

  const toggleCompare = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (isCompared) {
      removeCompare(listing.id);
    } else {
      addCompare(listingToCompareItem(listing));
    }
  };

  const handleAddToCart = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    const rawPrice = listing.basePrice ?? listing.minPrice ?? 0;
    addToCart({
      id: listing.id,
      listingId: listing.id,
      title: listing.title,
      slug: listing.slug,
      vendorBrandName: listing.vendorBrandName,
      vendorId: "",
      categoryName: listing.categoryName,
      price: rawPrice,
      priceFormatted: price.amount,
      unit: price.unit ?? undefined,
      imageUrl: listing.primaryImageUrl,
      isCombo: listing.title.toLowerCase().includes("combo"),
    });
  };

  return (
    <article className="listing-card">
      <Link
        to={`/listings/${listing.id}`}
        state={returnPath ? { browseReturn: returnPath } : undefined}
        aria-label={`Xem chi tiết ${listing.title}`}
      >
        <div className="listing-card-image">
          {listing.primaryImageUrl ? (
            <img
              src={listing.primaryImageUrl}
              alt={listing.title}
              loading="lazy"
            />
          ) : (
            <div className="h-full grid place-items-center">
              <ImageIcon size={40} className="text-rose-200" />
            </div>
          )}
          <span>{listing.categoryName}</span>
        </div>
        <div className="listing-card-body">
          <div className="listing-card-vendor">{listing.vendorBrandName}</div>
          <h3>{listing.title}</h3>
          <p className="listing-card-location">
            <MapPin size={12} />
            {listing.location}
          </p>
          <div className="listing-card-price">
            <span>{price.caption}</span>
            <b>
              {price.amount}{price.unit && <> / {price.unit}</>}
            </b>
          </div>
        </div>
      </Link>
      <div className="listing-card-meta">
        <span>
          <Gift size={12} /> Ưu đãi khi gửi yêu cầu
        </span>
        <span>
          <Eye size={12} />
          {listing.viewCount}
          <ArrowUpRight size={15} />
        </span>
      </div>
      <div className="px-2.5 pb-2.5 pt-1 flex items-center gap-1.5 listing-card-actions">
        <button
          type="button"
          onClick={handleAddToCart}
          className={`flex-1 py-1.5 px-2 rounded-xl font-bold text-[11px] sm:text-xs flex items-center justify-center gap-1 transition cursor-pointer ${
            inCart
              ? "bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs"
              : "bg-slate-900 hover:bg-slate-800 text-white shadow-xs"
          }`}
          title={inCart ? "Bấm để thêm tiếp số lượng vào giỏ" : "Thêm dịch vụ này vào giỏ hàng"}
        >
          {inCart ? <Check size={12} strokeWidth={2.5} /> : <ShoppingBag size={12} />}
          <span className="truncate">{inCart ? "Trong giỏ (+1)" : "Thêm vào giỏ"}</span>
        </button>
        <button
          type="button"
          onClick={toggleCompare}
          className={`py-1.5 px-2 rounded-xl font-bold text-[11px] sm:text-xs flex items-center justify-center gap-1 transition cursor-pointer border ${
            isCompared
              ? "bg-rose-50 text-rose-700 border-rose-200 hover:bg-rose-100"
              : "bg-white text-slate-700 border-slate-200 hover:border-slate-300 hover:bg-slate-50"
          }`}
          title={isCompared ? "Bỏ so sánh" : "Thêm vào danh sách so sánh"}
        >
          {isCompared ? <Check size={11} className="text-rose-600" /> : <Scale size={11} />}
          <span className="truncate">{isCompared ? "Đã chọn" : "So sánh"}</span>
        </button>
      </div>
    </article>
  );
}
