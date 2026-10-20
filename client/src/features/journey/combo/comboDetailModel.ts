import type {
  CatalogBuilder,
  CatalogOption,
  CatalogTemplate,
} from "../comboApi";
import { quantityFor, type ComboCart } from "./cartModel.ts";
import { comboMediaSources } from "./comboMedia.ts";
import { fixedComboOptions, fixedComboSummary } from "./fixedComboModel.ts";

export interface DetailVendor {
  id: string;
  name: string;
  slug: string;
  logoUrl?: string | null;
  coverImageUrl?: string | null;
  city: string;
  address?: string | null;
  bio?: string | null;
  isVerified: boolean;
  ratingAvg: number | null;
  totalReviews: number;
}
export type DetailOption = CatalogOption & {
  categoryId: string;
  galleryUrls?: string[];
  description?: string;
};
export type DetailCatalog = CatalogBuilder & {
  vendors?: DetailVendor[];
  previewGuestCount?: number;
  previewOnly?: boolean;
};
export interface DetailSlot {
  categoryId: string;
  categoryName: string;
  optional: boolean;
  item?: DetailOption;
  valid: boolean;
}
export interface ComboDetailModel {
  template: CatalogTemplate;
  vendor?: DetailVendor;
  vendorId: string | null;
  vendorName: string;
  required: DetailSlot[];
  optional: DetailSlot[];
  photos: string[];
  canChoose: boolean;
  readOnly?: boolean;
}
/** Private preview options come only from the authorized endpoint, never from fabricated public fallbacks. */
export function previewDetailCatalog(data: CatalogBuilder | undefined, template: CatalogTemplate): DetailCatalog {
  const categories = (data?.categories ?? []).map(category => ({ ...category, options: [] as CatalogOption[] }));
  for (const slot of template.items) {
    let category = categories.find(category => category.categoryId === slot.categoryId);
    if (!category) { category = { categoryId: slot.categoryId, name: "Hạng mục dịch vụ", slug: "", options: [] }; categories.push(category); }
    const ids = new Set([slot.serviceItemId, ...(slot.allowedItemIds ?? [])]);
    category.options.push(...(template.previewOptions ?? []).filter(option => ids.has(option.serviceItemId) && !category!.options.some(current => current.serviceItemId === option.serviceItemId)));
  }
  return { categories, templates: [template], discountTiers: data?.discountTiers ?? [], vendors: data?.vendors ?? [], previewGuestCount: data?.previewGuestCount ?? 200, previewOnly: true };
}
export function mediaUrls(urls: (string | null | undefined)[]): string[] {
  return comboMediaSources(urls);
}
export function comboDetailModel(
  data: DetailCatalog,
  id: string,
): ComboDetailModel | null {
  const template = data.templates.find((item) => item.id === id);
  if (!template) return null;
  const vendorId = template.ownerVendorId || null;
  const fixed = template.pricingMode === "FixedPrice";
  const options = new Map<string, DetailOption>(
    data.categories.flatMap((category) =>
      category.options.map(
        (item) =>
          [
            item.serviceItemId,
            { ...item, categoryId: category.categoryId },
          ] as const,
      ),
    ),
  );
  if (fixed) {
    options.clear();
    for (const item of fixedComboOptions(template)) options.set(item.serviceItemId, item);
  }
  const slots = template.items.map((slot) => {
    const item = slot.serviceItemId
      ? options.get(slot.serviceItemId)
      : undefined;
    return {
      categoryId: slot.categoryId,
      categoryName:
        data.categories.find(
          (category) => category.categoryId === slot.categoryId,
        )?.name || "Dịch vụ",
      optional: slot.isOptional,
      item,
      valid:
        !!item &&
        !!vendorId &&
        item.vendorId === vendorId &&
        item.categoryId === slot.categoryId,
    };
  });
  const required = slots.filter((slot) => !slot.optional);
  const optional = slots.filter((slot) => slot.optional);
  const vendor = data.vendors?.find((item) => item.id === vendorId);
  const photos = mediaUrls([
    template.imageUrl,
    ...(template.content?.galleryUrls ?? []),
    vendor?.coverImageUrl,
    ...slots
      .filter((slot) => slot.valid)
      .flatMap((slot) => [
        slot.item?.imageUrl,
        ...(slot.item?.galleryUrls ?? []),
      ]),
  ]);
  return {
    template,
    vendor,
    vendorId,
    vendorName:
      vendor?.name ||
      template.ownerVendorName ||
      slots.find((slot) => slot.valid)?.item?.vendorName ||
      "Nhà cung cấp",
    required,
    optional,
    photos,
    readOnly: data.previewOnly ?? false,
    canChoose:
      !!vendorId &&
      required.length > 0 &&
      slots.every((slot) => slot.valid) &&
      (fixed
        ? required.length >= 2 && optional.length === 0 && !template.requiresComponentReview && !!fixedComboSummary(template) &&
          (template.components?.length ?? 0) === slots.length && new Set(slots.map(slot => slot.item?.serviceItemId)).size === slots.length
        : new Set(slots.map((slot) => slot.categoryId)).size === slots.length),
  };
}
/** This is the base-price preview only; discounts, availability and the final RFQ price remain server-authoritative. */
export function detailBasePrice(
  model: ComboDetailModel,
  guests: number,
): number {
  if (model.template.pricingMode === "FixedPrice") return fixedComboSummary(model.template)?.original ?? 0;
  return model.required.reduce(
    (total, slot) =>
      total +
      (slot.valid && slot.item
        ? slot.item.price * quantityFor(slot.item.unit, guests, 1)
        : 0),
    0,
  );
}
export function chooseDetailTemplate(
  cart: ComboCart,
  model: ComboDetailModel,
  profile?: {
    city?: string;
    weddingDate?: string | null;
    guestCount?: number;
  } | null,
): ComboCart {
  if (model.readOnly || !model.canChoose || !model.vendorId)
    throw new Error(
      "Combo cần đầy đủ dịch vụ khả dụng của cùng một nhà cung cấp.",
    );
  return {
    ...cart,
    region: cart.region || profile?.city || "",
    weddingDate: cart.weddingDate || profile?.weddingDate?.slice(0, 10) || "",
    guestCount: cart.guestCount || profile?.guestCount || 0,
    selections: model.required.map((slot) => ({
      categoryId: slot.categoryId,
      serviceItemId: slot.item!.serviceItemId,
      variantIds: [],
      addOnIds: [],
      hours: 1,
    })),
    templateId: model.template.id,
    templateName: model.template.name,
    pricingMode: model.template.pricingMode,
    templateVendorId: model.vendorId,
    vendorId: model.vendorId,
    draftId: undefined,
    revisionOfId: undefined,
    revisionReason: undefined,
    note: undefined,
  };
}
