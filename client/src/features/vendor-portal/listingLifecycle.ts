import type { ListingSummaryDto } from "../../types/listing.types.ts";

export function listingCanEdit(listing: Pick<ListingSummaryDto, "status">): boolean {
  return ['Draft', 'Rejected', 'Active', 'Hidden'].includes(listing.status);
}
export function listingCanSubmit(listing: Pick<ListingSummaryDto, "status" | "revisionStatus">): boolean {
  return ['Draft', 'Rejected'].includes(listing.status) || ['Active', 'Hidden'].includes(listing.status) && ['Draft', 'Rejected'].includes(listing.revisionStatus ?? '');
}
export function listingPreviewUrl(listing: Pick<ListingSummaryDto, 'id' | 'revisionStatus' | 'latestRevisionId'>): string {
  const params = new URLSearchParams({ preview: 'vendor' });
  if (listing.latestRevisionId && ['Draft', 'Pending', 'Rejected'].includes(listing.revisionStatus ?? '')) params.set('revision', listing.latestRevisionId);
  return `/listings/${listing.id}?${params}`;
}
