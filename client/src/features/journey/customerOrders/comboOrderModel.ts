import type { SavedCombo } from "../comboApi";
import type { OrderJourneyItem } from "./customerOrderApi";

export type ComboListFilter =
  "all" | "draft" | "active" | "closed" | "archived";
export type ComboCardAction = "edit" | "withdraw" | "archive" | "restore";
const TERMINAL = new Set([
  "Cancelled",
  "Completed",
  "Expired",
  "Superseded",
  "Withdrawn",
]);
export function comboMatchesFilter(
  order: Pick<SavedCombo, "status">,
  metadata: OrderJourneyItem | undefined,
  filter: ComboListFilter,
): boolean {
  if (filter === "archived") return metadata?.isArchived === true;
  if (metadata?.isArchived) return false;
  if (filter === "all") return true;
  if (filter === "draft") return order.status === "Draft";
  const closed = TERMINAL.has(order.status) || !!metadata?.supersededById;
  return filter === "closed" ? closed : order.status !== "Draft" && !closed;
}
/** Never infer mutation rights from the old snapshot status; only the server knows quote/contract locks. */
export function comboCardActions(
  metadata?: OrderJourneyItem,
): ComboCardAction[] {
  if (!metadata || metadata.kind !== "combo") return [];
  const caps = metadata.capabilities;
  if (metadata.isArchived) return caps.canRestore ? ["restore"] : [];
  return [
    ...(caps.canEdit ? ["edit" as const] : []),
    ...(caps.canWithdraw ? ["withdraw" as const] : []),
    ...(caps.canArchive ? ["archive" as const] : []),
  ];
}
export function comboCardModel(order: SavedCombo, metadata?: OrderJourneyItem) {
  const media = metadata?.media;
  const vendorNames = [
    ...new Set(order.items.map((item) => item.vendorName).filter(Boolean)),
  ];
  const vendorName =
    media?.vendorName ||
    (vendorNames.length === 1
      ? vendorNames[0]
      : vendorNames.length > 1
        ? "Combo cũ · nhiều nhà cung cấp"
        : "Nhà cung cấp");
  const photos = [
    ...new Set(
      [
        ...order.items.flatMap(item => item.component ? [item.component.imageUrl, ...item.component.galleryUrls] : []),
        media?.imageUrl,
        media?.vendorCoverImageUrl,
        ...(media?.galleryUrls ?? []),
        ...(media?.items ?? []).flatMap((item) => [
          item.imageUrl,
          ...item.galleryUrls,
        ]),
      ].filter((url): url is string => !!url),
    ),
  ];
  return {
    title: !media?.title ? `Combo của ${vendorName}` : media.title !== order.code
      ? media.title
      : order.status === "Draft" ? "Combo đang chuẩn bị" : "Combo ngày vui của hai bạn",
    vendorName,
    photos,
    actions: comboCardActions(metadata),
    displayStatus: metadata?.isArchived
      ? "Archived"
      : metadata?.supersededById
        ? "Superseded"
        : order.status,
    items: order.items.map((item) => {
      const image =
        media?.items?.find(
          (source) =>
            item.serviceItemId && source.serviceItemId === item.serviceItemId,
        ) ??
        media?.items?.find((source) => source.listingId === item.listingId);
      return {
        ...item,
        photos: [
          ...new Set(
            [item.component?.imageUrl, ...(item.component?.galleryUrls ?? []), image?.imageUrl, ...(image?.galleryUrls ?? [])].filter(
              (url): url is string => !!url,
            ),
          ),
        ],
      };
    }),
  };
}
