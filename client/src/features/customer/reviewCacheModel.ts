import type { QueryClient } from '@tanstack/react-query';

type ReviewMutation = 'submit' | 'edit' | 'approve' | 'reject' | 'moderate-edit' | 'reply';

// Prefixes deliberately cover every target, page/filter and session. A mutation may
// affect a Shop aggregate as well as a Service/Combo panel, not just legacy lists.
const mutationScopes: Record<ReviewMutation, readonly string[]> = {
  submit: ['eligible-contracts', 'eligible-targets', 'my-reviews', 'moderation-reviews', 'target-reviews', 'listing-reviews', 'vendor-reviews'],
  edit: ['my-reviews', 'moderation-edits'],
  approve: ['moderation-reviews', 'my-reviews', 'target-reviews', 'listing-reviews', 'vendor-reviews'],
  reject: ['moderation-reviews', 'my-reviews'],
  'moderate-edit': ['moderation-edits', 'my-reviews', 'target-reviews', 'listing-reviews', 'vendor-reviews'],
  reply: ['my-reviews', 'target-reviews', 'listing-reviews', 'vendor-reviews'],
};

// These responses embed Shop aggregates used by cards, detail headers, rating
// filters and sorting. Prefix invalidation also refreshes every cached page and
// filter, without replacing the customer's cart or historical order snapshots.
const ratingCatalogScopes = ['public-listings', 'listing-detail', 'combo-builder', 'combo-catalog-page'] as const;

export async function invalidateReviewQueries(client: QueryClient, mutation: ReviewMutation) {
  // Pending edits keep the published review intact; replies do not change its
  // score. Only moderation decisions can publish/change an aggregate. The edit
  // decision hook covers both approval and rejection, so it safely refreshes on
  // either result rather than guessing the decision from a local cache.
  const scopes = mutation === 'approve' || mutation === 'moderate-edit'
    ? [...mutationScopes[mutation], ...ratingCatalogScopes]
    : mutationScopes[mutation];
  await Promise.all(scopes.map(scope => client.invalidateQueries({ queryKey: [scope] })));
}

/** A UI affordance only: the API remains authoritative for active ownership. */
export function canReplyToReview(
  user: { role: string; vendorId?: string | null } | null | undefined,
  review: { vendorId: string; status: string },
) {
  return user?.role === 'VendorOwner' && !!user.vendorId &&
    user.vendorId === review.vendorId && review.status === 'Approved';
}
