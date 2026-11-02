type PriceSource = {
  minPrice: number;
  maxPrice: number;
  basePrice?: number | null;
  priceUnit?: string | null;
  pricing?: { basePrice: number; unit: string } | null;
};
const units: Record<string, string> = { Package: "gói", Table: "bàn", Guest: "khách", Hour: "giờ" };
/** One approved price/unit is shared by retail cards and details; old listings retain their declared range. */
export function listingPricePresentation(source: PriceSource) {
  const price = source.pricing?.basePrice ?? source.basePrice;
  const unit = source.pricing?.unit ?? source.priceUnit;
  if (typeof price === "number" && Number.isFinite(price) && price >= 0 && unit && units[unit])
    return { caption: "Giá niêm yết", amount: `${price.toLocaleString("vi-VN")} VNĐ`, unit: units[unit], exact: true };
  const min = source.minPrice.toLocaleString("vi-VN"), max = source.maxPrice.toLocaleString("vi-VN");
  return { caption: "Khoảng giá tham khảo", amount: source.minPrice === source.maxPrice ? `${min} VNĐ` : `${min} – ${max} VNĐ`, unit: null, exact: false };
}
