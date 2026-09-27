import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { Review, SubmitReviewRequest, EligibleContract } from '../../../types/review.types';

export function useListingReviews(listingId?: string) {
  return useQuery({
    queryKey: ['listing-reviews', listingId],
    queryFn: async (): Promise<Review[]> => {
      if (!listingId) return [];
      const res = await fetch(`/api/reviews/listing/${listingId}`);
      if (!res.ok) return [];
      return res.json();
    },
    enabled: !!listingId
  });
}

export function useVendorReviews(vendorId?: string) {
  return useQuery({
    queryKey: ['vendor-reviews', vendorId],
    queryFn: async (): Promise<Review[]> => {
      if (!vendorId) return [];
      const res = await fetch(`/api/reviews/vendor/${vendorId}`);
      if (!res.ok) return [];
      return res.json();
    },
    enabled: !!vendorId
  });
}

export function useEligibleContracts() {
  return useQuery({
    queryKey: ['eligible-contracts'],
    queryFn: async (): Promise<EligibleContract[]> => {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/reviews/eligible-contracts', {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });
      if (!res.ok) return [];
      return res.json();
    }
  });
}

export function useSubmitReview() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (payload: SubmitReviewRequest): Promise<Review> => {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/reviews', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể gửi đánh giá.');
      }

      return res.json();
    },
    onSuccess: (saved) => {
      queryClient.invalidateQueries({ queryKey: ['listing-reviews', saved.listingId] });
      queryClient.invalidateQueries({ queryKey: ['vendor-reviews', saved.vendorId] });
      queryClient.invalidateQueries({ queryKey: ['moderation-reviews'] });
    }
  });
}

export function useModerationReviews() {
  return useQuery({
    queryKey: ['moderation-reviews'],
    queryFn: async (): Promise<Review[]> => {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/reviews/moderation/pending', {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });
      if (!res.ok) return [];
      return res.json();
    }
  });
}

export function useApproveReview() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (reviewId: string) => {
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/reviews/${reviewId}/approve`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        throw new Error('Không thể duyệt đánh giá.');
      }
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['moderation-reviews'] });
      queryClient.invalidateQueries({ queryKey: ['listing-reviews'] });
      queryClient.invalidateQueries({ queryKey: ['vendor-reviews'] });
    }
  });
}

export function useRejectReview() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ reviewId, reason }: { reviewId: string; reason?: string }) => {
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/reviews/${reviewId}/reject`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ reason })
      });

      if (!res.ok) {
        throw new Error('Không thể từ chối đánh giá.');
      }
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['moderation-reviews'] });
    }
  });
}

export function useVendorReplyReview() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ reviewId, reply }: { reviewId: string; reply: string }) => {
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/reviews/${reviewId}/reply`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ reply })
      });

      if (!res.ok) {
        throw new Error('Không thể gửi phản hồi.');
      }
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['listing-reviews'] });
      queryClient.invalidateQueries({ queryKey: ['vendor-reviews'] });
    }
  });
}
