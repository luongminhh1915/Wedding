import type { CatalogBuilder, PublicComboVendor } from "../comboApi";

export const COMBO_FILTER_KEYS = ["category", "listing", "search", "sort", "page", "minPrice", "maxPrice", "city", "region", "minServices", "maxServices", "minSavings", "minRating", "templateCategoryId", "filterVendorId"] as const;
export const COMBO_PRICE_LIMIT = 1_000_000_000_000_000;
export const COMBO_SORTS = ["recommended", "price-asc", "price-desc", "newest", "rating", "savings"] as const;
export interface ComboFilters {
  category?: string; listing?: string; search?: string; sort: typeof COMBO_SORTS[number]; page: number;
  minPrice?: number; maxPrice?: number; city?: string; region?: string; minServices?: number; maxServices?: number;
  minSavings?: number; minRating?: number; templateCategoryId?: string; filterVendorId?: string;
}
const isGuid = (value: string | null): value is string => !!value && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
const amount = (value: string | null, max: number, integer = true) => {
  if (!value || !(integer ? /^\d+$/ : /^\d+(?:\.\d+)?$/).test(value)) return undefined;
  const number = Number(value);
  return Number.isFinite(number) && number >= 0 && number <= max && (!integer || Number.isSafeInteger(number)) ? number : undefined;
};
export function readComboFilters(search: string): ComboFilters {
  const p = new URLSearchParams(search);
  const min = amount(p.get("minPrice"), COMBO_PRICE_LIMIT), max = amount(p.get("maxPrice"), COMBO_PRICE_LIMIT);
  const validPrice = min == null || max == null || min <= max;
  const minServices = amount(p.get("minServices"), 12), maxServices = amount(p.get("maxServices"), 12);
  const validCount = !minServices || !maxServices || minServices <= maxServices;
  return {
    category: p.get("category") === "templates" ? "templates" : isGuid(p.get("category")) ? p.get("category")!.toLowerCase() : undefined,
    listing: isGuid(p.get("listing")) ? p.get("listing")!.toLowerCase() : undefined,
    search: p.get("search")?.trim().slice(0, 150) || undefined,
    sort: COMBO_SORTS.includes(p.get("sort") as ComboFilters["sort"]) ? p.get("sort") as ComboFilters["sort"] : "recommended",
    page: amount(p.get("page"), 1_000_000) || 1,
    minPrice: validPrice ? min : undefined, maxPrice: validPrice ? max : undefined,
    city: p.get("city")?.trim().slice(0, 100) || undefined, region: p.get("region")?.trim().slice(0, 100) || undefined,
    minServices: validCount ? minServices || undefined : undefined, maxServices: validCount ? maxServices || undefined : undefined,
    minSavings: amount(p.get("minSavings"), COMBO_PRICE_LIMIT), minRating: amount(p.get("minRating"), 5, false) || undefined,
    templateCategoryId: isGuid(p.get("templateCategoryId")) ? p.get("templateCategoryId")!.toLowerCase() : undefined,
    filterVendorId: isGuid(p.get("filterVendorId")) ? p.get("filterVendorId")!.toLowerCase() : undefined,
  };
}
export function comboFilterSearch(filters: ComboFilters, previousSearch = ""): string {
  const p = new URLSearchParams(previousSearch);
  COMBO_FILTER_KEYS.forEach(key => p.delete(key));
  for (const key of COMBO_FILTER_KEYS) {
    const value = filters[key];
    if (value == null || value === "" || key === "sort" && value === "recommended" || key === "page" && value === 1 || key === "category" && value === "templates") continue;
    p.set(key, String(value));
  }
  return p.size ? `?${p}` : "";
}
export const clearComboFilterSearch = (search: string) => comboFilterSearch({ sort: "recommended", page: 1 }, search);
export function comboCatalogReturn(value: unknown): string {
  if (typeof value !== "string" || value.length > 4000 || !/^\/combo(?:\?[^#\\]*)?$/.test(value)) return "/combo";
  return `/combo${comboFilterSearch(readComboFilters(value.slice(6)))}`;
}
export interface ComboFilterChip { key: string; label: string; clear: Partial<ComboFilters> }
export function comboFilterChips(filter: ComboFilters, categories: CatalogBuilder["categories"], vendors: PublicComboVendor[]): ComboFilterChip[] {
  const chips: ComboFilterChip[] = [];
  const add = (key: keyof ComboFilters, label: string) => chips.push({ key, label, clear: { [key]: undefined } });
  if (filter.search) add("search", `Tìm: ${filter.search}`);
  if (filter.listing) add("listing", "Theo bài dịch vụ đã chọn");
  if (filter.filterVendorId) add("filterVendorId", `Shop: ${vendors.find(v => v.id === filter.filterVendorId)?.name ?? "đã chọn"}`);
  if (filter.templateCategoryId) add("templateCategoryId", `Có: ${categories.find(c => c.categoryId === filter.templateCategoryId)?.name ?? "danh mục đã chọn"}`);
  if (filter.city) add("city", `Địa điểm shop: ${filter.city}`);
  if (filter.region) add("region", `Phục vụ: ${filter.region}`);
  if (filter.minPrice != null || filter.maxPrice != null) chips.push({ key: "price", label: `Giá: ${filter.minPrice?.toLocaleString("vi-VN") ?? "0"} – ${filter.maxPrice?.toLocaleString("vi-VN") ?? "không giới hạn"} đ`, clear: { minPrice: undefined, maxPrice: undefined } });
  if (filter.minServices || filter.maxServices) chips.push({ key: "services", label: `${filter.minServices ?? 1}–${filter.maxServices ?? 12} dịch vụ có sẵn`, clear: { minServices: undefined, maxServices: undefined } });
  if (filter.minSavings != null) add("minSavings", `Tiết kiệm từ ${filter.minSavings.toLocaleString("vi-VN")} đ`);
  if (filter.minRating) add("minRating", `Shop từ ${filter.minRating.toLocaleString("vi-VN")} sao`);
  return chips;
}
