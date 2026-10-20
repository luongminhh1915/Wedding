import type { SavedCombo, CatalogTemplate } from "../comboApi";
import { restoreSelection, type ComboCart } from "./cartModel.ts";

export const comboEditIntent = (order: SavedCombo) =>
  order.status === "Draft" ? { draft: order } : { revision: order };

/** Restore a new editing session without mutating an order or reusing its submitted identity. */
export function editableComboCart(
  order: SavedCombo,
  template?: CatalogTemplate,
  revisionOfId?: string | null,
): ComboCart {
  const vendors = [...new Set(order.items.map((item) => item.vendorId))];
  const vendorId = vendors.length === 1 ? vendors[0] : null;
  const matchingTemplate =
    template?.ownerVendorId === vendorId ? template : undefined;
  return {
    selections: order.items.flatMap((item) =>
      item.serviceItemId
        ? [restoreSelection({ ...item, serviceItemId: item.serviceItemId })]
        : [],
    ),
    region: order.region,
    weddingDate: order.weddingDate?.slice(0, 10) ?? "",
    guestCount: order.guestCount ?? 0,
    draftId: order.status === "Draft" && vendorId ? order.id : undefined,
    templateId: matchingTemplate?.id ?? null,
    templateName: matchingTemplate?.name ?? "",
    pricingMode: matchingTemplate?.pricingMode,
    templateVendorId: matchingTemplate?.ownerVendorId ?? null,
    vendorId,
    revisionOfId: revisionOfId || undefined,
    revisionReason: order.revisionReason ?? "",
    note: order.note ?? "",
  };
}
