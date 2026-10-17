export interface CartSelection {
  serviceItemId: string;
  categoryId: string;
  variantIds: string[];
  addOnIds: string[];
  hours: number;
}
export interface ComboCart {
  selections: CartSelection[];
  region: string;
  weddingDate: string;
  guestCount: number;
  templateId: string | null;
  templateName: string;
  templateVendorId: string | null;
  vendorId: string | null;
  draftId?: string;
  revisionOfId?: string;
  revisionReason?: string;
  note?: string;
}
export const EMPTY_CART: ComboCart = {
  selections: [],
  region: "",
  weddingDate: "",
  guestCount: 0,
  templateId: null,
  templateName: "",
  templateVendorId: null,
  vendorId: null,
};
export const unitName: Record<string, string> = {
  Package: "gói",
  Table: "bàn",
  Guest: "khách",
  Hour: "giờ",
};
export function restoreSelection(item: {
  serviceItemId: string;
  categoryId: string;
  selectionJson: string;
}): CartSelection {
  let stored: Record<string, unknown> = {};
  try {
    const parsed: unknown = JSON.parse(item.selectionJson);
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed))
      stored = parsed as Record<string, unknown>;
  } catch {
    // Legacy rows without configuration still restore the service itself.
  }
  const ids = (value: unknown): string[] =>
    Array.isArray(value)
      ? [...new Set(value.filter((id): id is string => typeof id === "string"))]
      : [];
  const hours = stored.hours ?? stored.Hours;
  return {
    serviceItemId: item.serviceItemId,
    categoryId: item.categoryId,
    // Early saved orders used .NET's PascalCase JSON rather than web defaults.
    variantIds: ids(stored.variantIds ?? stored.VariantIds),
    addOnIds: ids(stored.addOnIds ?? stored.AddOnIds),
    hours:
      typeof hours === "number" && Number.isFinite(hours)
        ? Math.min(24, Math.max(1, hours))
        : 1,
  };
}
export function selectService(
  cart: ComboCart,
  selection: CartSelection,
  vendorId: string,
): ComboCart {
  const boundVendor = cart.vendorId ?? cart.templateVendorId;
  if (boundVendor && boundVendor !== vendorId)
    throw new Error("Mỗi combo chỉ gồm dịch vụ của một nhà cung cấp.");
  return {
    ...cart,
    vendorId,
    selections: [
      ...cart.selections.filter(
        (line) => line.categoryId !== selection.categoryId,
      ),
      selection,
    ],
  };
}
export function startVendorCart(
  cart: ComboCart,
  vendorId: string | null,
): ComboCart {
  return {
    ...EMPTY_CART,
    region: cart.region,
    weddingDate: cart.weddingDate,
    guestCount: cart.guestCount,
    vendorId,
  };
}
export function removeService(cart: ComboCart, categoryId: string): ComboCart {
  return {
    ...cart,
    selections: cart.selections.filter((s) => s.categoryId !== categoryId),
  };
}
export function quantityFor(
  unit: string,
  guests: number,
  hours: number,
): number {
  if (unit === "Table") return Math.ceil(Math.max(0, guests) / 10);
  if (unit === "Guest") return Math.max(0, guests);
  if (unit === "Hour") return Math.min(24, Math.max(1, hours));
  return 1;
}
export function estimateLine(
  item: {
    price: number;
    unit: string;
    variants: { id?: string | null; priceDelta: number }[];
    addOns: { id?: string | null; price: number; perUnit: boolean }[];
  },
  selection: CartSelection,
  guests: number,
) {
  const quantity = quantityFor(item.unit, guests, selection.hours);
  const variantDelta = item.variants
    .filter((v) => v.id && selection.variantIds.includes(v.id))
    .reduce((sum, v) => sum + v.priceDelta, 0);
  const addons = item.addOns
    .filter((a) => a.id && selection.addOnIds.includes(a.id))
    .reduce((sum, a) => sum + a.price * (a.perUnit ? quantity : 1), 0);
  return Math.round((item.price + variantDelta) * quantity + addons);
}
