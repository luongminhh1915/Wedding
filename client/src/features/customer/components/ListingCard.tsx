import { useNavigate } from 'react-router-dom';
import { MapPin, Star, Eye, Heart, ImageIcon } from 'lucide-react';
import type { ListingSummaryDto } from '../../../types/listing.types';

interface Props {
  listing: ListingSummaryDto;
}

export default function ListingCard({ listing }: Props) {
  const navigate = useNavigate();

  return (
    <article
      onClick={() => navigate(`/listings/${listing.id}`)}
      className="group bg-white rounded-2xl border border-rose-50 shadow-sm hover:shadow-xl hover:-translate-y-1.5 transition-all duration-300 cursor-pointer overflow-hidden flex flex-col"
      role="button"
      aria-label={`Xem chi tiết ${listing.title}`}
    >
      {/* Image */}
      <div className="relative h-52 bg-gradient-to-br from-rose-50 to-pink-50 overflow-hidden flex-shrink-0">
        {listing.primaryImageUrl ? (
          <img
            src={listing.primaryImageUrl}
            alt={listing.title}
            className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
            loading="lazy"
          />
        ) : (
          <div className="flex items-center justify-center h-full">
            <ImageIcon className="w-12 h-12 text-rose-200" />
          </div>
        )}

        {/* Category tag */}
        <span className="absolute top-3 left-3 text-xs font-bold px-2.5 py-1 rounded-full bg-white/90 backdrop-blur-sm text-slate-700 shadow-sm">
          {listing.categoryName}
        </span>

        {/* Voucher badge — placeholder cho Task AI-07 */}
        <span className="absolute bottom-3 left-3 text-xs font-bold px-2.5 py-1 rounded-lg bg-gradient-to-r from-rose-500 to-pink-500 text-white shadow-md">
          🎁 Nhận Voucher Ưu Đãi
        </span>

        {/* Favorite button */}
        <button
          onClick={e => e.stopPropagation()}
          className="absolute top-3 right-3 p-1.5 rounded-full bg-white/80 backdrop-blur-sm hover:bg-white transition shadow-sm opacity-0 group-hover:opacity-100"
          aria-label="Yêu thích"
        >
          <Heart className="w-4 h-4 text-rose-400" />
        </button>
      </div>

      {/* Body */}
      <div className="p-4 flex flex-col flex-1">
        {/* Vendor name */}
        <div className="flex items-center gap-1.5 mb-1.5">
          <div className="w-5 h-5 rounded-full bg-rose-100 flex items-center justify-center flex-shrink-0">
            <span className="text-[10px] font-bold text-rose-600">
              {listing.vendorBrandName.charAt(0)}
            </span>
          </div>
          <span className="text-xs font-semibold text-slate-500 truncate">{listing.vendorBrandName}</span>
          <span className="text-[10px] text-emerald-600 font-bold ml-auto flex-shrink-0">✓ Đã xác thực</span>
        </div>

        {/* Title */}
        <h3 className="font-bold text-slate-900 text-sm leading-snug line-clamp-2 mb-2 group-hover:text-rose-600 transition-colors">
          {listing.title}
        </h3>

        {/* Location */}
        <div className="flex items-center gap-1 text-xs text-slate-500 mb-3">
          <MapPin className="w-3.5 h-3.5 flex-shrink-0" />
          <span className="truncate">{listing.location}</span>
        </div>

        {/* Price */}
        <div className="mt-auto pt-3 border-t border-slate-100 flex items-center justify-between">
          <div>
            <p className="text-[10px] text-slate-400 font-medium uppercase tracking-wider">Khoảng giá</p>
            <p className="font-bold text-rose-600 text-sm">
              {listing.minPrice.toLocaleString('vi-VN')}đ
              <span className="font-normal text-slate-400 mx-1">–</span>
              {listing.maxPrice.toLocaleString('vi-VN')}đ
            </p>
          </div>

          <div className="flex items-center gap-3 text-xs text-slate-400">
            <span className="flex items-center gap-0.5">
              <Star className="w-3.5 h-3.5 text-amber-400 fill-amber-400" />
              <span className="font-semibold text-slate-600">4.8</span>
            </span>
            <span className="flex items-center gap-0.5">
              <Eye className="w-3.5 h-3.5" />
              {listing.viewCount}
            </span>
          </div>
        </div>
      </div>
    </article>
  );
}
