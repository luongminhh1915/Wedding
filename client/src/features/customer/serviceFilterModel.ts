import type { CategoryDto, PublicListingsFilter } from "../../types/category.types";

export const SERVICE_SORTS = ["newest", "popular", "price_asc", "price_desc"] as const;
export const SERVICE_FILTER_KEYS = ["categoryId", "keyword", "location", "minPrice", "maxPrice", "minRating", "sortBy", "page", "pageSize"] as const;
export const SERVICE_PRICE_LIMIT = 100_000_000_000;
const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const boundedNumber = (text: string | null, max: number, integer = true) => {
  if (!text || !(integer ? /^\d+$/ : /^\d+(?:\.\d+)?$/).test(text)) return undefined;
  const number = Number(text);
  return Number.isFinite(number) && number >= 0 && number <= max && (!integer || Number.isSafeInteger(number)) ? number : undefined;
};

/** URL is the applied source of truth. Invalid/huge numeric input never reaches the API. */
export function readServiceFilters(search: string): PublicListingsFilter {
  const params = new URLSearchParams(search);
  const category = params.get("categoryId") ?? "";
  const minPrice = boundedNumber(params.get("minPrice"), SERVICE_PRICE_LIMIT);
  const maxPrice = boundedNumber(params.get("maxPrice"), SERVICE_PRICE_LIMIT);
  const validRange = minPrice == null || maxPrice == null || minPrice <= maxPrice;
  const sort = params.get("sortBy");
  const page = boundedNumber(params.get("page"), 1_000_000);
  const pageSize = boundedNumber(params.get("pageSize"), 48);
  return {
    categoryId: guid.test(category) ? category.toLowerCase() : undefined,
    keyword: params.get("keyword")?.trim().slice(0, 150) || undefined,
    location: params.get("location")?.trim().slice(0, 100) || undefined,
    minPrice: validRange ? minPrice : undefined,
    maxPrice: validRange ? maxPrice : undefined,
    minRating: boundedNumber(params.get("minRating"), 5, false),
    sortBy: SERVICE_SORTS.includes(sort as typeof SERVICE_SORTS[number]) ? sort as PublicListingsFilter["sortBy"] : "newest",
    page: page && page >= 1 ? page : 1,
    pageSize: pageSize && pageSize >= 1 ? pageSize : 12,
  };
}

export function serviceFilterSearch(filter: PublicListingsFilter, previousSearch = ""): string {
  const params = new URLSearchParams(previousSearch);
  SERVICE_FILTER_KEYS.forEach(key => params.delete(key));
  const values: Record<string, unknown> = { ...filter };
  for (const key of SERVICE_FILTER_KEYS) {
    const value = values[key];
    if (value == null || value === "" || key === "sortBy" && value === "newest" || key === "page" && value === 1 || key === "pageSize" && value === 12) continue;
    params.set(key, String(value));
  }
  const result = params.toString();
  return result ? `?${result}` : "";
}

export function clearServiceFilterSearch(previousSearch: string): string {
  return serviceFilterSearch({ sortBy: "newest", page: 1, pageSize: 12 }, previousSearch);
}

const shortNames: Record<string, string> = {
  "tiem-cuoi-venues": "Địa điểm & tiệc cưới",
  "trang-tri-decor": "Trang trí cưới",
  "quay-chup-photo-video": "Quay phim & chụp ảnh",
  "vay-cuoi-vest": "Váy cưới & vest",
  "trang-diem-makeup": "Trang điểm",
  "thiep-cuoi-qua-tang": "Thiệp cưới & quà",
  "wedding-planner": "Tổ chức cưới A–Z",
  "tiec-su-kien-khac": "Tiệc & sự kiện khác",
};
export const serviceCategoryLabel = (category: Pick<CategoryDto, "slug" | "name">) => shortNames[category.slug] ?? category.name;

export interface ServiceFilterChip { key: string; label: string; clear: PublicListingsFilter }
export function serviceFilterChips(filter: PublicListingsFilter, categories: CategoryDto[]): ServiceFilterChip[] {
  const chips: ServiceFilterChip[] = [];
  const category = categories.find(item => item.id === filter.categoryId);
  if (filter.categoryId) chips.push({ key: "categoryId", label: category ? serviceCategoryLabel(category) : "Danh mục đã chọn", clear: { categoryId: undefined } });
  if (filter.keyword) chips.push({ key: "keyword", label: `Từ khóa: ${filter.keyword}`, clear: { keyword: undefined } });
  if (filter.location) chips.push({ key: "location", label: `Khu vực: ${filter.location}`, clear: { location: undefined } });
  if (filter.minPrice != null || filter.maxPrice != null) chips.push({ key: "price", label: `Giá: ${filter.minPrice?.toLocaleString("vi-VN") ?? "0"} – ${filter.maxPrice?.toLocaleString("vi-VN") ?? "không giới hạn"} đ`, clear: { minPrice: undefined, maxPrice: undefined } });
  if (filter.minRating != null) chips.push({ key: "minRating", label: `Shop từ ${filter.minRating} sao`, clear: { minRating: undefined } });
  return chips;
}

/** Only accept local browse URLs, never navigate to caller-supplied external destinations. */
export function serviceBrowseReturn(value: unknown): string {
  if (typeof value !== "string" || value.length > 3000 || !/^\/browse(?:\?[^#\\]*)?$/.test(value)) return "/browse";
  return `/browse${serviceFilterSearch(readServiceFilters(value.slice(7)))}`;
}
