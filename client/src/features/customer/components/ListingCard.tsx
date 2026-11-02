import { Link } from "react-router-dom";
import { ArrowUpRight, Eye, Gift, ImageIcon, MapPin, ShoppingBag } from "lucide-react";
import type { ListingSummaryDto } from "../../../types/listing.types";
import { listingPricePresentation } from "../listingPriceModel";

export default function ListingCard({
  listing,
  returnPath,
}: {
  listing: ListingSummaryDto;
  returnPath?: string;
}) {
  const price = listingPricePresentation(listing);
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
      <Link to={`/combo?listing=${listing.id}`} className="listing-combo-link"><ShoppingBag size={14} /> Chọn dịch vụ để ghép combo</Link>
    </article>
  );
}
