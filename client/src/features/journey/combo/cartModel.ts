export interface CartSelection { serviceItemId: string; categoryId: string; variantIds: string[]; addOnIds: string[]; hours: number }
export interface ComboCart {
  selections: CartSelection[]; region: string; weddingDate: string; guestCount: number;
  templateId: string | null; templateName: string; templateVendorId: string | null; draftId?: string;
}
export const EMPTY_CART: ComboCart = { selections: [], region: "", weddingDate: "", guestCount: 0, templateId: null, templateName: "", templateVendorId: null };
export function selectService(cart: ComboCart, selection: CartSelection, vendorId: string): ComboCart {
  const otherVendor = !!cart.templateVendorId && cart.templateVendorId !== vendorId;
  return { ...cart, selections: [...cart.selections.filter((line) => line.categoryId !== selection.categoryId), selection],
    ...(otherVendor ? { templateId: null, templateName: "", templateVendorId: null } : {}) };
}
export function removeService(cart: ComboCart, categoryId: string): ComboCart {
  return { ...cart, selections: cart.selections.filter((s) => s.categoryId !== categoryId) };
}
export function quantityFor(unit: string, guests: number, hours: number): number {
  if (unit === "Table") return Math.ceil(Math.max(0, guests) / 10);
  if (unit === "Guest") return Math.max(0, guests);
  if (unit === "Hour") return Math.min(24, Math.max(1, hours));
  return 1;
}
export function estimateLine(item: { price: number; unit: string; variants: { id?: string | null; priceDelta: number }[]; addOns: { id?: string | null; price: number; perUnit: boolean }[] }, selection: CartSelection, guests: number) {
  const quantity = quantityFor(item.unit, guests, selection.hours);
  const variantDelta = item.variants.filter((v) => v.id && selection.variantIds.includes(v.id)).reduce((sum, v) => sum + v.priceDelta, 0);
  const addons = item.addOns.filter((a) => a.id && selection.addOnIds.includes(a.id)).reduce((sum, a) => sum + a.price * (a.perUnit ? quantity : 1), 0);
  return Math.round((item.price + variantDelta) * quantity + addons);
}
