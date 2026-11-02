import type { ListingDto } from "../../types/listing.types";
import type { CatalogItem, CatalogTemplate } from "../journey/comboApi";

export const MODERATION_TYPES = [["all", "Tất cả nội dung"], ["listing", "Bài giới thiệu"], ["service-item", "Dịch vụ thành phần"], ["combo", "Combo NCC"]] as const;
export const MODERATION_STATUSES = [["Pending", "Chờ kiểm duyệt"], ["Draft", "Bản nháp"], ["Approved", "Đã duyệt"], ["Rejected", "Bị từ chối"], ["all", "Tất cả trạng thái"]] as const;
export type ModerationType = "listing" | "service-item" | "combo";
export interface ModerationFilters { type: string; status: string; search: string; page: number; pageSize: number }
export interface ModerationRow { id: string; type: ModerationType; title: string; vendorId?: string | null; vendorName: string; status: string; revisionId?: string | null; revisionSequence?: number | null; changedAt: string; isActive: boolean }
export interface ModerationDetail { type: ModerationType; id: string; listing?: ListingDto | null; serviceItem?: CatalogItem | null; combo?: CatalogTemplate | null }
export interface ModerationPage { items: ModerationRow[]; page: number; pageSize: number; totalCount: number; pendingCount: number; type: string; status: string; search: string }
const guid = (value: string | null | undefined): value is string => !!value && /^[\da-f]{8}-(?:[\da-f]{4}-){3}[\da-f]{12}$/i.test(value) && value !== "00000000-0000-0000-0000-000000000000";
const boundedInt = (value: string | null, fallback: number, maximum: number) => value && /^\d+$/.test(value) ? Math.max(1, Math.min(maximum, Number(value))) : fallback;
export function moderationFilters(params: URLSearchParams): ModerationFilters {
  return { type: MODERATION_TYPES.some(([value]) => value === params.get("moderationType")) ? params.get("moderationType")! : "all",
    status: MODERATION_STATUSES.some(([value]) => value === params.get("moderationStatus")) ? params.get("moderationStatus")! : "Pending",
    search: (params.get("moderationSearch") ?? "").trim().slice(0, 150), page: boundedInt(params.get("moderationPage"), 1, 1000000),
    pageSize: boundedInt(params.get("moderationSize"), 10, 20) };
}
export function moderationSelection(params: URLSearchParams): { type: ModerationType; id: string; revisionId?: string } | null {
  const type = params.get("reviewType"), id = params.get("reviewId"), revision = params.get("reviewRevision");
  if (!MODERATION_TYPES.some(([value]) => value !== "all" && value === type) || !guid(id) || revision !== null && !guid(revision) || type === "service-item" && revision) return null;
  return { type: type as ModerationType, id, ...(revision ? { revisionId: revision } : {}) };
}
export function updateModerationFilters(params: URLSearchParams, patch: Partial<ModerationFilters>): URLSearchParams {
  const next = new URLSearchParams(params); next.set("tab", "moderation");
  for (const key of ["reviewType", "reviewId", "reviewRevision"]) next.delete(key);
  const filters = { ...moderationFilters(params), ...patch, page: patch.page ?? 1 };
  for (const [key, value, fallback] of [["moderationType", filters.type, "all"], ["moderationStatus", filters.status, "Pending"],
    ["moderationSearch", filters.search.trim().slice(0, 150), ""], ["moderationPage", String(filters.page), "1"], ["moderationSize", String(filters.pageSize), "10"]]) {
    if (value === fallback) next.delete(key); else next.set(key, value);
  }
  return next;
}
export function selectModerationRow(params: URLSearchParams, row: ModerationRow): URLSearchParams {
  const next = new URLSearchParams(params); next.set("reviewType", row.type); next.set("reviewId", row.id);
  if (row.revisionId) next.set("reviewRevision", row.revisionId); else next.delete("reviewRevision");
  return next;
}
export function moderationStatus(detail: ModerationDetail): string {
  if (detail.type === "listing") return detail.listing?.latestRevision?.status ?? (detail.listing?.status === "PendingApproval" ? "Pending" : detail.listing?.status ?? "");
  if (detail.type === "service-item") return detail.serviceItem?.moderationStatus ?? "";
  return detail.combo?.latestRevision?.status ?? detail.combo?.moderationStatus ?? "";
}
export function moderationDecision(detail: ModerationDetail, approve: boolean, note: string) {
  if (!guid(detail.id) || moderationStatus(detail) !== "Pending") throw new Error("Nội dung không còn chờ kiểm duyệt. Hãy tải lại đúng phiên bản.");
  const reason = note.trim(); const limit = detail.type === "listing" ? 1000 : 2000;
  if ((!approve && !reason) || reason.length > limit) throw new Error(`Lý do từ chối là bắt buộc; ghi chú tối đa ${limit} ký tự.`);
  if (detail.type === "listing") {
    if (detail.listing?.id !== detail.id) throw new Error("Nội dung dịch vụ chưa hợp lệ.");
    const revisionId = detail.listing.latestRevision?.id;
    if (revisionId && !guid(revisionId)) throw new Error("Thiếu phiên bản dịch vụ hợp lệ.");
    return { url: `/api/listings/${detail.id}/${approve ? "approve" : "reject"}`, body: approve ? null : { reason, ...(revisionId ? { revisionId } : {}) }, params: approve && revisionId ? { revisionId } : undefined };
  }
  if (detail.type === "service-item") {
    const version = detail.serviceItem?.reviewVersion;
    if (detail.serviceItem?.id !== detail.id || !version || !/^[\da-f]{64}$/i.test(version)) throw new Error("Thiếu phiên bản nội dung dịch vụ. Hãy tải lại trước khi duyệt.");
    return { url: `/api/combos/items/${detail.id}/review`, body: { approve, note: reason || null, reviewVersion: version }, params: undefined };
  }
  if (detail.combo?.id !== detail.id) throw new Error("Nội dung combo chưa hợp lệ.");
  const revisionId = detail.combo.latestRevision?.id;
  if (revisionId && !guid(revisionId)) throw new Error("Thiếu phiên bản combo hợp lệ.");
  return { url: `/api/combos/templates/${detail.id}/review`, body: { approve, note: reason || null, ...(revisionId ? { revisionId } : {}) }, params: undefined };
}
