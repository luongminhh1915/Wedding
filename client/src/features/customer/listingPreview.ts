/** A URL flag only changes presentation; private data access remains enforced by the API. */
export function isListingPreview(role: string | null | undefined, preview: string | null): boolean {
  return preview === 'vendor' && ['VendorOwner', 'Moderator', 'SuperAdmin'].includes(role ?? '');
}

export function listingPreviewReturn(role: string | null | undefined): string {
  if (role === 'VendorOwner') return '/vendor/dashboard?tab=listings';
  if (role === 'Moderator' || role === 'SuperAdmin') return '/admin/dashboard?tab=moderation';
  return '/browse';
}

