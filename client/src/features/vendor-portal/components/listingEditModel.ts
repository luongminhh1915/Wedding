import type { ListingDto, ServiceDetailsDto, UpdateListingRequest, ListingRevisionDto, ListingPricingDto, ListingPricingOption } from "../../../types/listing.types";
import { normalizeServiceDetails, serviceDetailsUpdate } from "../../listings/serviceDetailsModel.ts";

export interface ListingFormValues {
  categoryId: string;
  title: string;
  minPrice: string;
  maxPrice: string;
  description: string;
  location: string;
}
export interface ListingImageItem { id: string; preview: string; url: string }
export interface ListingEditorState { form: ListingFormValues; images: ListingImageItem[]; serviceDetails: ServiceDetailsDto; cancellationPolicy: string; categoryRequiresCommissionBasisApproval: boolean; revision?: ListingRevisionDto | null; revisionHistory?: ListingRevisionDto[] | null; pricing?: ListingPricingDto | null; pricingOptions?: ListingPricingOption[]; pricingSelectionRequired?: boolean; }

export const EMPTY_LISTING_FORM: ListingFormValues = {
  categoryId: "", title: "", minPrice: "", maxPrice: "", description: "", location: "",
};

export function isListingImageReference(reference: string): boolean {
  if (typeof reference !== "string") return false;
  const value = reference.trim();
  if (!value || value.length > 500) return false;
  let decoded = value;
  for (let depth = 0; depth < 4; depth++) {
    const hasControl = Array.from(decoded).some(character => { const code = character.charCodeAt(0); return code < 32 || code >= 127 && code <= 159; });
    if (hasControl || decoded.includes("\\") || /(^|\/)\.\.(\/|[?#]|$)/.test(decoded)) return false;
    let next: string;
    try { next = decodeURIComponent(decoded); } catch { return false; }
    if (next === decoded) break;
    if (depth === 3) return false;
    decoded = next;
  }
  if (value.startsWith("/uploads/listings/")) return /^\/uploads\/listings\/[0-9a-fA-F]{32}\.(jpg|jpeg|png|webp)$/.test(value);
  if (!/^https?:\/\//i.test(value)) return false;
  try {
    const url = new URL(value);
    return !!url.hostname && !url.username && !url.password;
  } catch { return false; }
}

export function listingImagePreview(reference: string, apiBase = ""): string {
  if (!isListingImageReference(reference)) return "";
  const value = reference.trim();
  return value.startsWith("/uploads/listings/") ? `${apiBase.replace(/\/$/, "")}${value}` : value;
}

/** Called once when the editor mounts; background refetches never reinitialize local edits. */
export function hydrateListingEditor(detail: ListingDto, expectedId: string, apiBase = ""): ListingEditorState {
  if (!detail || detail.id !== expectedId || typeof detail.description !== "string" || !Array.isArray(detail.media))
    throw new Error("Chưa tải được đầy đủ nội dung bài đăng. Vui lòng thử lại.");
  const media = [...detail.media].sort((left, right) => Number(right.isFeatured) - Number(left.isFeatured) || left.displayOrder - right.displayOrder);
  return {
    form: { categoryId: detail.categoryId, title: detail.title, minPrice: String(detail.minPrice), maxPrice: String(detail.maxPrice), description: detail.description, location: detail.location },
    // Preserve the complete stored album, including older references. Only newly supplied replacements
    // are validated; price-only edits omit media so they never silently delete historical images.
    images: media.map(image => ({ id: image.id, url: image.mediaUrl, preview: listingImagePreview(image.mediaUrl, apiBase) })),
    serviceDetails: normalizeServiceDetails(detail.serviceDetails),
    cancellationPolicy: detail.cancellationPolicy ?? "",
    categoryRequiresCommissionBasisApproval: detail.categoryRequiresCommissionBasisApproval ?? false,
    revision: detail.latestRevision, revisionHistory: detail.revisionHistory,
    pricing: detail.pricing, pricingOptions: detail.pricingOptions, pricingSelectionRequired: detail.pricingSelectionRequired,
  };
}

export function moveListingCover(images: ListingImageItem[], index: number): ListingImageItem[] {
  if (!Number.isInteger(index) || index <= 0 || index >= images.length) return images;
  return [images[index], ...images.slice(0, index), ...images.slice(index + 1)];
}

export function listingUpdatePayload(form: ListingFormValues, images: ListingImageItem[], originalImages: ListingImageItem[], details?: ServiceDetailsDto, originalDetails?: ServiceDetailsDto | null): UpdateListingRequest {
  const imageUrls = images.map(image => image.url.trim());
  const changed = imageUrls.length !== originalImages.length || imageUrls.some((url, index) => url !== originalImages[index]?.url);
  if (changed && (imageUrls.length < 1 || imageUrls.length > 10 || imageUrls.some(url => !isListingImageReference(url)) || new Set(imageUrls).size !== imageUrls.length))
    throw new Error("Cần từ 1 đến 10 ảnh hợp lệ và không trùng nhau khi thay album.");
  const serviceDetails = details === undefined ? undefined : serviceDetailsUpdate(details, originalDetails);
  return {
    title: form.title, minPrice: Number(form.minPrice), maxPrice: Number(form.maxPrice), description: form.description, location: form.location,
    ...(changed ? { imageUrls } : {}),
    ...(serviceDetails === undefined ? {} : { serviceDetails }),
  };
}

export function uploadedListingImages(items: ListingImageItem[], urls: string[], apiBase = ""): ListingImageItem[] {
  if (!Array.isArray(urls) || urls.length !== items.length || urls.some(url => !isListingImageReference(url)))
    throw new Error("Máy chủ chưa trả đủ đường dẫn ảnh hợp lệ. Hãy thử tải lại ảnh.");
  return items.map((item, index) => ({ ...item, url: urls[index], preview: item.preview || listingImagePreview(urls[index], apiBase) }));
}
