import type { ListingPricingDto } from "../../../types/listing.types.ts";

export interface ListingPricingEditor extends Omit<ListingPricingDto, "basePrice" | "variants" | "addOns"> {
  basePrice: number | null;
  variants: NonNullable<ListingPricingDto["variants"]>;
  addOns: NonNullable<ListingPricingDto["addOns"]>;
}
export function listingPricingValues(value?: ListingPricingDto | null): ListingPricingEditor {
  return { serviceItemId: value?.serviceItemId, basePrice: value?.basePrice ?? null, unit: value?.unit ?? "Package",
    minGuests: value?.minGuests ?? 1, maxGuests: value?.maxGuests ?? 2000, regions: value?.regions ?? "",
    cancellationPolicy: value?.cancellationPolicy ?? "", maxBookingsPerDay: value?.maxBookingsPerDay ?? 1,
    variants: (value?.variants ?? []).map(item => ({ ...item })), addOns: (value?.addOns ?? []).map(item => ({ ...item })) };
}
export function listingPricingPayload(value: ListingPricingEditor): ListingPricingDto {
  if (value.basePrice == null || !Number.isFinite(value.basePrice) || value.basePrice < 0 || !Number.isInteger(value.basePrice)) throw new Error("Nhập giá bán lẻ chính xác bằng VNĐ.");
  if (!["Package", "Table", "Guest", "Hour"].includes(value.unit)) throw new Error("Chọn đơn vị giá hợp lệ.");
  if (!Number.isInteger(value.minGuests) || !Number.isInteger(value.maxGuests) || value.minGuests < 1 || value.maxGuests < value.minGuests) throw new Error("Số khách tối đa cần lớn hơn hoặc bằng tối thiểu.");
  if (!value.regions.trim() || value.regions.trim().length > 1000) throw new Error("Nhập khu vực phục vụ, tối đa 1000 ký tự.");
  if (!Number.isInteger(value.maxBookingsPerDay) || value.maxBookingsPerDay < 1 || value.maxBookingsPerDay > 100) throw new Error("Số đơn mỗi ngày cần từ 1 đến 100.");
  if ((value.cancellationPolicy?.length ?? 0) > 4000) throw new Error("Chính sách hủy tối đa 4000 ký tự.");
  if (value.variants.length > 20 || value.addOns.length > 30) throw new Error("Tối đa 20 biến thể và 30 món kèm.");
  if (value.variants.some(item => !item.name.trim() || item.name.trim().length > 200 || !Number.isFinite(item.priceDelta) || !Number.isInteger(item.priceDelta) || value.basePrice! + item.priceDelta < 0)) throw new Error("Kiểm tra tên và giá biến thể; giá sau biến thể không được âm.");
  if (value.addOns.some(item => !item.name.trim() || item.name.trim().length > 200 || !Number.isFinite(item.price) || !Number.isInteger(item.price) || item.price < 0)) throw new Error("Kiểm tra tên và giá món kèm.");
  return { ...value, basePrice: value.basePrice, regions: value.regions.trim(), cancellationPolicy: value.cancellationPolicy?.trim() || null,
    variants: value.variants.map(item => ({ ...item, name: item.name.trim() })), addOns: value.addOns.map(item => ({ ...item, name: item.name.trim() })) };
}
