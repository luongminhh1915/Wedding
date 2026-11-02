import type { CatalogItem, CatalogTemplate, ComboTemplateContent, ComboTemplateProposal } from "../comboApi.ts";
import { isServiceDetailsLink } from "../../listings/serviceDetailsModel.ts";

export interface ComboEditorValues extends Omit<ComboTemplateProposal, "content"> { id: string | null; content: ComboTemplateContent }
export const emptyComboContent = (): ComboTemplateContent => ({ galleryUrls: [], terms: "", validFromUtc: null, validUntilUtc: null });
export function newComboEditor(ownerVendorId = ""): ComboEditorValues {
  return { id: null, ownerVendorId, name: "", tier: "Popular", style: "Hiện đại", description: "", imageUrl: "", isActive: true, sortOrder: 0, region: "*", singleVendorDiscount: null, pricingMode: "FixedPrice", comboPrice: null, items: [], content: emptyComboContent() };
}
export function comboEditorValues(template: CatalogTemplate): ComboEditorValues {
  const latest = template.latestRevision;
  const source = latest && ["Draft", "Pending", "Rejected"].includes(latest.status) && latest.proposal ? latest.proposal : template;
  return { ...newComboEditor(template.ownerVendorId), id: template.id, name: source.name, tier: source.tier, style: source.style,
    description: source.description ?? "", imageUrl: source.imageUrl ?? "", isActive: source.isActive, sortOrder: source.sortOrder ?? 0,
    ownerVendorId: source.ownerVendorId ?? template.ownerVendorId, region: source.region ?? "*", singleVendorDiscount: source.singleVendorDiscount ?? null,
    pricingMode: source.pricingMode ?? template.pricingMode ?? "Dynamic", comboPrice: source.comboPrice ?? null,
    items: source.items.map(item => ({ ...item, allowedItemIds: [...(item.allowedItemIds ?? [])] })),
    content: { ...emptyComboContent(), ...source.content, galleryUrls: [...(source.content?.galleryUrls ?? [])] } };
}
export function isComboImage(value: string): boolean {
  return /^\/uploads\/listings\/[a-fA-F\d]{32}\.(?:jpe?g|png|webp)$/.test(value.trim()) || isServiceDetailsLink(value);
}
export function comboEditorPayload(value: ComboEditorValues, catalog: CatalogItem[], submitForApproval: boolean) {
  if (!value.name.trim() || value.name.trim().length > 150) throw new Error("Tên combo cần từ 1 đến 150 ký tự.");
  if (!value.style.trim() || value.style.trim().length > 50 || !["Saving", "Popular", "Luxury"].includes(value.tier)) throw new Error("Chọn phân khúc và nhập phong cách từ 1 đến 50 ký tự.");
  if ((value.description?.trim().length ?? 0) > 4000 || (value.region?.trim().length ?? 0) > 1000 || !Number.isInteger(value.sortOrder)) throw new Error("Mô tả tối đa 4000 ký tự, khu vực tối đa 1000 ký tự; thứ tự cần là số nguyên.");
  const fixed = value.pricingMode === "FixedPrice";
  if (!value.ownerVendorId || value.items.length < (fixed ? 2 : 1) || value.items.length > 12 || !fixed && new Set(value.items.map(item => item.categoryId)).size !== value.items.length)
    throw new Error(fixed ? "Chọn ít nhất 2 dịch vụ độc lập, tối đa 12 dịch vụ của cùng shop." : "Chọn ít nhất một dịch vụ, mỗi danh mục một hạng mục của cùng NCC.");
  const eligible = catalog.filter(item => item.vendorId === value.ownerVendorId && item.moderationStatus === "Approved" && item.isActive && (!fixed || isFixedComboService(item, value.ownerVendorId)));
  const items = value.items.map(slot => {
    const item = eligible.find(item => item.id === slot.serviceItemId && item.categoryId === slot.categoryId);
    if (!item || (slot.allowedItemIds?.length ?? 0) > 100 || (slot.allowedItemIds ?? []).some(id => !eligible.some(option => option.id === id && option.categoryId === slot.categoryId))) throw new Error("Có dịch vụ hoặc lựa chọn thay thế chưa duyệt, đã tắt hoặc không thuộc NCC này. Hãy chọn lại (tối đa 100 lựa chọn mỗi hạng mục).");
    if (fixed && (slot.isOptional || slot.allowedItemIds?.length)) throw new Error("Combo cố định cần giữ đúng dịch vụ đã chọn, không có lựa chọn thay thế hoặc hạng mục tùy chọn.");
    return { categoryId: item.categoryId, listingId: item.listingId, serviceItemId: item.id, isOptional: slot.isOptional, allowedItemIds: [...new Set(slot.allowedItemIds ?? [])] };
  });
  if (fixed && (new Set(items.map(item => item.serviceItemId)).size !== items.length || new Set(items.map(item => item.listingId)).size !== items.length)) throw new Error("Không chọn trùng dịch vụ; mỗi dịch vụ gốc chỉ xuất hiện một lần trong combo.");
  if (fixed && (value.comboPrice == null || !Number.isFinite(value.comboPrice) || value.comboPrice <= 0 || !Number.isInteger(value.comboPrice))) throw new Error("Nhập giá bán combo dương, chính xác bằng VNĐ.");
  const galleryUrls = value.content.galleryUrls.map(url => url.trim()).filter(Boolean);
  const imageUrl = value.imageUrl?.trim() || null;
  if (galleryUrls.length > 10 || galleryUrls.some(url => !isComboImage(url)) || imageUrl && !isComboImage(imageUrl)) throw new Error("Ảnh cần HTTP(S) an toàn hoặc ảnh đã tải lên; gallery tối đa 10 ảnh.");
  if (value.content.terms.trim().length > 4000) throw new Error("Điều khoản combo tối đa 4000 ký tự.");
  if ((value.content.detailedDescription?.trim().length ?? 0) > 4000) throw new Error("Mô tả chi tiết combo tối đa 4000 ký tự.");
  const from = value.content.validFromUtc, until = value.content.validUntilUtc;
  if (from && Number.isNaN(Date.parse(from)) || until && Number.isNaN(Date.parse(until)) || from && until && Date.parse(until) <= Date.parse(from)) throw new Error("Thời hạn áp dụng chưa hợp lệ; thời điểm kết thúc phải sau bắt đầu.");
  if (value.singleVendorDiscount != null && (!Number.isFinite(value.singleVendorDiscount) || value.singleVendorDiscount < 0 || value.singleVendorDiscount > .30)) throw new Error("Mức giảm combo phải nằm trong giới hạn hiện hành.");
  return { id: value.id, name: value.name.trim(), tier: value.tier, style: value.style.trim(), description: value.description?.trim() ?? "", imageUrl,
    isActive: value.isActive, sortOrder: value.sortOrder, region: value.region?.trim() || "*", singleVendorDiscount: value.singleVendorDiscount,
    ...(fixed ? { pricingMode: "FixedPrice", comboPrice: value.comboPrice } : { pricingMode: "Dynamic" }),
    items, submitForApproval, content: { galleryUrls, terms: value.content.terms.trim(), ...(value.content.detailedDescription != null ? { detailedDescription: value.content.detailedDescription.trim() } : {}), validFromUtc: from, validUntilUtc: until } };
}
export function isFixedComboService(item: CatalogItem, ownerVendorId?: string | null): boolean {
  return !!ownerVendorId && item.vendorId === ownerVendorId && item.moderationStatus === "Approved" && item.isActive && item.isCanonical === true && item.listingStatus === "Active" && item.listingIsActive === true && item.unit === "Package";
}
export function fixedComboTotals(items: Pick<CatalogItem, "basePrice">[], comboPrice?: number | null) {
  const originalPrice = items.reduce((total, item) => total + item.basePrice, 0);
  const savingsAmount = comboPrice != null && Number.isFinite(comboPrice) && comboPrice < originalPrice ? originalPrice - comboPrice : 0;
  return { originalPrice, comboPrice: comboPrice ?? null, savingsAmount, savingsPercentage: originalPrice > 0 ? savingsAmount / originalPrice * 100 : 0 };
}
export function comboPreviewPath(id: string, revisionId?: string | null): string {
  return `/combo/${encodeURIComponent(id)}?preview=vendor${revisionId ? `&revision=${encodeURIComponent(revisionId)}` : ""}`;
}
export function comboReviewPayload(template: CatalogTemplate, approve: boolean, note: string) {
  const revision = template.latestRevision;
  if ((revision?.status ?? template.moderationStatus) !== "Pending") throw new Error("Combo không còn phiên bản chờ duyệt. Hãy tải lại danh sách.");
  const reason = note.trim();
  if ((!approve && !reason) || reason.length > 2000) throw new Error("Lý do từ chối cần từ 1 đến 2000 ký tự; ghi chú tối đa 2000 ký tự.");
  return { approve, note: reason || null, ...(revision ? { revisionId: revision.id } : {}) };
}
export function canPreviewCombo(role: string | null | undefined, flag: string | null): boolean {
  return flag === "vendor" && ["VendorOwner", "Moderator", "SuperAdmin"].includes(role ?? "");
}
export function comboDate(value?: string | null): string {
  if (!value) return "Chưa xác định";
  const utc = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?$/i.test(value) ? `${value}Z` : value;
  const date = new Date(utc);
  return Number.isNaN(date.getTime()) ? "Chưa xác định" : date.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh", dateStyle: "short", timeStyle: "short" });
}
