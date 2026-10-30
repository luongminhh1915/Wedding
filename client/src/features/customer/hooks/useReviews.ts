import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { SubmitComboReviewRequest, SubmitReviewRequest, SubmitShopReviewRequest } from '../../../types/review.types';
import { useAuthStore } from '../../../store/authStore';
import axiosClient from '../../../services/axiosClient';
import { canRequestReviews, createReviewTransport, reviewQueryKey, type ReviewSession } from '../reviewTransport';
import { invalidateReviewQueries } from '../reviewCacheModel';

function currentSession(): ReviewSession {
  const state = useAuthStore.getState();
  return { userId: state.user?.id ?? null, role: state.user?.role ?? null, token: state.token, revision: state.sessionRevision };
}
function useReviewSession(): ReviewSession {
  const userId = useAuthStore(state => state.user?.id ?? null);
  const role = useAuthStore(state => state.user?.role ?? null);
  const token = useAuthStore(state => state.token);
  const revision = useAuthStore(state => state.sessionRevision);
  return { userId, role, token, revision };
}
const transport = createReviewTransport(axiosClient, currentSession);

export function useListingReviews(listingId?: string) {
  const session = useReviewSession();
  return useQuery({ queryKey: reviewQueryKey('listing-reviews', session, listingId ?? ''),
    queryFn: ({ signal }) => transport.listing(session, listingId!, signal), enabled: !!listingId, retry: false });
}
export function useVendorReviews(vendorId?: string) {
  const session = useReviewSession();
  return useQuery({ queryKey: reviewQueryKey('vendor-reviews', session, vendorId ?? ''),
    queryFn: ({ signal }) => transport.vendor(session, vendorId!, signal), enabled: !!vendorId, retry: false });
}
export function useEligibleContracts(enabled = true) {
  const session = useReviewSession();
  return useQuery({ queryKey: reviewQueryKey('eligible-contracts', session),
    queryFn: ({ signal }) => transport.eligible(session, signal), enabled: enabled && canRequestReviews('customer', session), retry: false });
}
export function useEligibleReviewTargets(enabled = true) {
  const session = useReviewSession();
  return useQuery({ queryKey: reviewQueryKey('eligible-targets', session),
    queryFn: ({ signal }) => transport.eligibleTargets(session, signal),
    enabled: enabled && canRequestReviews('customer', session), retry: false });
}
export function useMyReviews(enabled = true) {
  const session = useReviewSession();
  return useQuery({ queryKey: reviewQueryKey('my-reviews', session),
    queryFn: ({ signal }) => transport.mine(session, signal),
    enabled: enabled && canRequestReviews('customer', session), retry: false });
}
export function useIndependentReviews(targetType?: string, targetId?: string, options?: {
  page?: number; pageSize?: number; rating?: number | null; verifiedOnly?: boolean | null; sort?: string;
}) {
  const session = useReviewSession();
  const page = options?.page ?? 1; const pageSize = options?.pageSize ?? 10;
  const rating = options?.rating ?? null; const verifiedOnly = options?.verifiedOnly ?? null;
  const sort = options?.sort ?? 'newest';
  return useQuery({ queryKey: [...reviewQueryKey('target-reviews', session, `${targetType ?? ''}:${targetId ?? ''}`),
    page, pageSize, rating, verifiedOnly, sort],
    queryFn: ({ signal }) => transport.target(session, targetType!, targetId!, page, pageSize, rating, verifiedOnly, sort, signal),
    enabled: !!targetType && !!targetId, retry: false });
}
export function useModerationReviews() {
  const session = useReviewSession();
  return useQuery({ queryKey: reviewQueryKey('moderation-reviews', session),
    queryFn: ({ signal }) => transport.moderation(session, signal), enabled: canRequestReviews('moderator', session), retry: false });
}
export function usePendingReviewEdits() {
  const session = useReviewSession();
  return useQuery({ queryKey: reviewQueryKey('moderation-edits', session),
    queryFn: ({ signal }) => transport.pendingEdits(session, signal),
    enabled: canRequestReviews('moderator', session), retry: false });
}
export function useSubmitReview() {
  const queryClient = useQueryClient(); const session = useReviewSession();
  return useMutation({ mutationKey: [...reviewQueryKey('eligible-contracts', session), 'submit'],
    mutationFn: (payload: SubmitReviewRequest) => transport.submit(session, payload),
    onSuccess: () => invalidateReviewQueries(queryClient, 'submit') });
}
export function useSubmitShopReview() {
  const queryClient = useQueryClient(); const session = useReviewSession();
  return useMutation({ mutationKey: [...reviewQueryKey('eligible-targets', session), 'shop-submit'],
    mutationFn: (payload: SubmitShopReviewRequest) => transport.submitShop(session, payload),
    onSuccess: () => invalidateReviewQueries(queryClient, 'submit') });
}
export function useSubmitComboReview() {
  const queryClient = useQueryClient(); const session = useReviewSession();
  return useMutation({ mutationKey: [...reviewQueryKey('eligible-targets', session), 'combo-submit'],
    mutationFn: (payload: SubmitComboReviewRequest) => transport.submitCombo(session, payload),
    onSuccess: () => invalidateReviewQueries(queryClient, 'submit') });
}
export function useSubmitReviewEdit() {
  const queryClient = useQueryClient(); const session = useReviewSession();
  return useMutation({ mutationKey: [...reviewQueryKey('my-reviews', session), 'edit'],
    mutationFn: ({ reviewId, payload }: { reviewId: string; payload: Omit<SubmitReviewRequest, 'listingId' | 'bookingContractId'> }) =>
      transport.submitEdit(session, reviewId, payload),
    onSuccess: () => invalidateReviewQueries(queryClient, 'edit') });
}
export function useApproveReview() {
  const queryClient = useQueryClient(); const session = useReviewSession();
  return useMutation({ mutationKey: [...reviewQueryKey('moderation-reviews', session), 'approve'],
    mutationFn: (reviewId: string) => transport.approve(session, reviewId),
    onSuccess: () => invalidateReviewQueries(queryClient, 'approve') });
}
export function useRejectReview() {
  const queryClient = useQueryClient(); const session = useReviewSession();
  return useMutation({ mutationKey: [...reviewQueryKey('moderation-reviews', session), 'reject'],
    mutationFn: ({ reviewId, reason }: { reviewId: string; reason?: string }) => transport.reject(session, reviewId, reason),
    onSuccess: () => invalidateReviewQueries(queryClient, 'reject') });
}
export function useModerateReviewEdit() {
  const queryClient = useQueryClient(); const session = useReviewSession();
  return useMutation({ mutationKey: [...reviewQueryKey('moderation-edits', session), 'decision'],
    mutationFn: ({ editRequestId, approve, reason }: { editRequestId: string; approve: boolean; reason?: string }) =>
      approve ? transport.approveEdit(session, editRequestId) : transport.rejectEdit(session, editRequestId, reason ?? ''),
    onSuccess: () => invalidateReviewQueries(queryClient, 'moderate-edit') });
}
export function useVendorReplyReview() {
  const queryClient = useQueryClient(); const session = useReviewSession();
  return useMutation({ mutationKey: [...reviewQueryKey('vendor-reviews', session), 'reply'],
    mutationFn: ({ reviewId, reply }: { reviewId: string; reply: string }) => transport.reply(session, reviewId, reply),
    onSuccess: () => invalidateReviewQueries(queryClient, 'reply') });
}
