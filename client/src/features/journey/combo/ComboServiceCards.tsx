import { Link } from "react-router-dom";
import { BadgeCheck, Check, SlidersHorizontal } from "lucide-react";
import type { CatalogOption } from "../comboApi";
import { vnd } from "../format";
import { unitName, type CartSelection } from "./cartModel";
import { ComboPhoto } from "./ComboPhoto";

export interface ComboServiceCardsProps {
  items: CatalogOption[];
  categoryId: string;
  selections: CartSelection[];
  guestCount: number;
  fixedCart: boolean;
  onConfigure: (item: CatalogOption, categoryId: string) => void;
}

/** Shared service cards for discovery previews and the paged category catalog. */
export function ComboServiceCards({
  items,
  categoryId,
  selections,
  guestCount,
  fixedCart,
  onConfigure,
}: ComboServiceCardsProps) {
  const current = selections.some(selection => selection.categoryId === categoryId);
  return <div className="combo-product-grid">
    {items.map(item => {
      const selected = selections.some(selection => selection.serviceItemId === item.serviceItemId);
      return <article key={item.serviceItemId} className={`combo-product-card ${selected ? "is-selected" : ""}`}>
        <div className="combo-product-image">
          <ComboPhoto sources={[item.imageUrl, ...(item.galleryUrls ?? [])]} alt={`${item.title} — ${item.vendorName}`} />
          {selected && <span><Check size={13} />Trong giỏ</span>}
        </div>
        <div className="combo-product-body">
          <p className="combo-vendor-name"><BadgeCheck size={13} />{item.vendorName}</p>
          <h3>{item.title}</h3>
          <p className="combo-product-capacity">Phục vụ {item.minGuests}–{item.maxGuests} khách</p>
          <p className="combo-product-price"><strong>{vnd(item.price)}</strong>/{unitName[item.unit]}</p>
          {item.unit === "Table" && guestCount > 0 && <p className="combo-product-capacity">
            {guestCount} khách → {Math.ceil(guestCount / 10)} bàn
          </p>}
          <button className={selected ? "combo-secondary" : "combo-primary"} disabled={fixedCart}
            onClick={() => { if (!fixedCart) onConfigure(item, categoryId); }}>
            <SlidersHorizontal size={14} />
            {fixedCart ? "Giữ nguyên gói đã chọn" : selected ? "Tùy chỉnh / đổi" : current ? "Chọn để thay dịch vụ" : "Chọn dịch vụ"}
          </button>
          <Link to={`/listings/${item.listingId}`} className="combo-product-link">Xem nhà cung cấp</Link>
        </div>
      </article>;
    })}
  </div>;
}
