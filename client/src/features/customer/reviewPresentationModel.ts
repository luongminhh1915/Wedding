import type { PendingReviewEdit, Review } from "../../types/review.types";

type ReviewContext = Pick<Review, "targetType" | "listingTitle" | "comboTemplateName" | "vendorBrandName" |
  "contractCode" | "bookingContractId" | "comboOrderId">;
const nonblank = (...values: (string | null | undefined)[]) => values.find(value => typeof value === "string" && value.trim())?.trim();

export function reviewTargetLabel(targetType: Review["targetType"]): string {
  return targetType === "Shop" ? "Shop" : targetType === "Combo" ? "Combo" : "Dịch vụ";
}

/** Empty legacy ListingTitle must not hide the actual Shop/Combo identity. */
export function reviewTargetName(review: ReviewContext): string {
  if (review.targetType === "Shop") return nonblank(review.vendorBrandName) ?? "Shop chưa có tên";
  if (review.targetType === "Combo") return nonblank(review.comboTemplateName, review.listingTitle) ?? "Combo chưa có tên";
  return nonblank(review.listingTitle) ?? "Dịch vụ chưa có tên";
}

export function reviewTransactionLabel(review: ReviewContext): string {
  if (nonblank(review.comboOrderId)) return `Đơn combo ${review.comboOrderId!.trim()}`;
  const contract = nonblank(review.contractCode, review.bookingContractId);
  return contract ? `Hợp đồng ${contract}` : "Đánh giá lịch sử";
}

/** Omitted optional fields preserve the stored photos and detailed scores on the edit API. */
export const reviewTextEditPayload = (rating: number, content: string) => ({ rating, content: content.trim() });

export function reviewPhotos(photosJson: string | null | undefined) {
  if (photosJson === undefined) return { state: "unavailable" as const, photos: [] };
  if (photosJson === null || photosJson.trim() === "") return { state: "ready" as const, photos: [] };
  try {
    const parsed: unknown = JSON.parse(photosJson);
    if (!Array.isArray(parsed) || parsed.some(photo => typeof photo !== "string")) throw new Error("Invalid photos");
    return { state: "ready" as const, photos: (parsed as string[]).map(reference => {
      const value = reference.trim();
      let href: string | null = null;
      if (value.startsWith("/uploads/") && !/[\\\s?#]/.test(value) && !decodeURIComponent(value).includes("..")) href = value;
      else {
        try {
          const url = new URL(value);
          if (url.protocol === "https:" && !url.username && !url.password) href = value;
        } catch { /* Keep the reference visible without offering an unsafe link. */ }
      }
      return { reference, href };
    }) };
  } catch { return { state: "invalid" as const, photos: [] }; }
}

const criteria = [
  ["QualityRating", "Chất lượng"], ["ServiceRating", "Phục vụ"],
  ["PunctualityRating", "Đúng hẹn"], ["ValueRating", "Chi phí"],
] as const;

/** Use each persisted snapshot, never infer a proposed value from the current Review. */
export function reviewEditSnapshots(edit: PendingReviewEdit) {
  return (["before", "after"] as const).map(side => ({
    side,
    rating: edit[`${side}Rating`],
    content: edit[`${side}Content`],
    photos: reviewPhotos(edit[`${side}PhotosJson`]),
    criteria: criteria.map(([key, label]) => ({ key, label, value: edit[`${side}${key}`] })),
  }));
}

export function reviewEditComparisonComplete(edit: PendingReviewEdit): boolean {
  return reviewEditSnapshots(edit).every(snapshot => snapshot.photos.state === "ready" &&
    snapshot.criteria.every(criterion => criterion.value !== undefined));
}
