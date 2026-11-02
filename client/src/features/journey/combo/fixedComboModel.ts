import type { CatalogOption, CatalogTemplate, ComboComponent } from "../comboApi";

export type FrozenOption = CatalogOption & { categoryId: string };
export function componentOption(component: ComboComponent, vendorName: string): FrozenOption {
  return { serviceItemId: component.serviceItemId, listingId: component.listingId, vendorId: component.vendorId,
    vendorName, categoryId: component.categoryId, vendorVerified: false, ratingAvg: 0, title: component.name,
    price: component.basePrice, maxPrice: component.basePrice, unit: component.unit, minGuests: component.minGuests,
    maxGuests: component.maxGuests, location: component.regions, cancellationPolicy: component.cancellationPolicy,
    description: component.description, imageUrl: component.imageUrl, galleryUrls: component.galleryUrls ?? [],
    variants: component.variants ?? [], addOns: component.addOns ?? [] };
}
/** Fixed packages render their published snapshot, never the current catalog's mutable prices. */
export function fixedComboOptions(template: CatalogTemplate): FrozenOption[] {
  if (template.pricingMode !== "FixedPrice") return [];
  return (template.components ?? []).map(component => componentOption(component, template.ownerVendorName ?? "Nhà cung cấp"));
}
export function fixedComboSummary(template: CatalogTemplate) {
  if (template.pricingMode !== "FixedPrice") return null;
  const price = template.comboPrice, original = template.originalPrice;
  if (typeof price !== "number" || !Number.isFinite(price) || price <= 0 || typeof original !== "number" || !Number.isFinite(original) || original < 0) return null;
  const saving = Math.max(0, original - price);
  return { price, original, saving, percentage: original > 0 ? saving / original * 100 : 0 };
}
