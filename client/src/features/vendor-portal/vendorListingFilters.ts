import type { ListingStatus, VendorListingsPageParams } from "../../types/listing.types.ts";

export const VENDOR_LISTING_STATUSES: readonly ListingStatus[] = ["Draft", "PendingApproval", "Active", "Rejected", "Hidden"];
export const VENDOR_LISTING_PAGE_SIZES = [10, 20, 50] as const;
const names = { keyword: "serviceKeyword", categoryId: "serviceCategory", status: "serviceStatus", page: "servicePage", pageSize: "servicePageSize" } as const;

export function vendorListingFilters(params: URLSearchParams): VendorListingsPageParams {
  const status = params.get(names.status);
  const requestedSize = Number(params.get(names.pageSize));
  const pageSize = VENDOR_LISTING_PAGE_SIZES.some(size => size === requestedSize) ? requestedSize : 10;
  const requestedPage = Number(params.get(names.page));
  const page = Number.isSafeInteger(requestedPage) && requestedPage > 0 && (requestedPage - 1) * pageSize <= 2147483647 ? requestedPage : 1;
  return { keyword: (params.get(names.keyword) ?? "").trim().slice(0, 200),
    ...(params.get(names.categoryId) ? { categoryId: params.get(names.categoryId)! } : {}),
    status: VENDOR_LISTING_STATUSES.includes(status as ListingStatus) ? status as ListingStatus : undefined, page, pageSize };
}

export function changeVendorListingFilters(params: URLSearchParams, change: Partial<VendorListingsPageParams>): URLSearchParams {
  const next = new URLSearchParams(params);
  for (const [key, value] of Object.entries(change)) {
    const name = names[key as keyof typeof names];
    if (!name) continue;
    if (value == null || value === "" || key === "page" && value === 1 || key === "pageSize" && value === 10) next.delete(name);
    else next.set(name, String(value));
  }
  if ("keyword" in change || "categoryId" in change || "status" in change || "pageSize" in change) next.delete(names.page);
  return next;
}

export function listingUpdatedTime(updatedAt: string | null | undefined, createdAt: string): string {
  const value = updatedAt ?? createdAt;
  // SQL datetime2 values are UTC but may serialize without a zone suffix.
  const utcValue = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?$/i.test(value) ? `${value}Z` : value;
  const timestamp = new Date(utcValue);
  return Number.isNaN(timestamp.getTime()) ? "Chưa có thông tin" : timestamp.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh", dateStyle: "short", timeStyle: "short" });
}
