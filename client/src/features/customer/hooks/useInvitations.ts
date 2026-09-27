import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type {
  WeddingInvitation,
  SaveInvitationRequest,
  GuestRsvp,
  PublicInvitation,
  SubmitRsvpRequest,
  RsvpResponse
} from '../../../types/invitation.types';

export function useMyInvitation() {
  const queryClient = useQueryClient();

  const invitationQuery = useQuery({
    queryKey: ['my-invitation'],
    queryFn: async (): Promise<WeddingInvitation | null> => {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/invitations/my-invitation', {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        if (res.status === 404) return null;
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể tải thiệp cưới của bạn.');
      }

      return res.json();
    }
  });

  const saveMutation = useMutation({
    mutationFn: async (payload: SaveInvitationRequest): Promise<WeddingInvitation> => {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/invitations', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể lưu thiệp cưới.');
      }

      return res.json();
    },
    onSuccess: (saved) => {
      queryClient.setQueryData(['my-invitation'], saved);
      queryClient.invalidateQueries({ queryKey: ['public-invitation', saved.slug] });
    }
  });

  return {
    invitation: invitationQuery.data,
    isLoading: invitationQuery.isLoading,
    isError: invitationQuery.isError,
    error: invitationQuery.error,
    refetch: invitationQuery.refetch,
    saveInvitation: saveMutation.mutateAsync,
    isSaving: saveMutation.isPending
  };
}

export function useInvitationRsvps(status?: string) {
  return useQuery({
    queryKey: ['my-rsvps', status],
    queryFn: async (): Promise<GuestRsvp[]> => {
      const token = localStorage.getItem('token');
      const url = status ? `/api/invitations/my-rsvps?status=${status}` : '/api/invitations/my-rsvps';
      const res = await fetch(url, {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        return [];
      }

      return res.json();
    }
  });
}

export function usePublicInvitation(slug: string) {
  return useQuery({
    queryKey: ['public-invitation', slug],
    queryFn: async (): Promise<PublicInvitation> => {
      const res = await fetch(`/api/invitations/${slug}`);
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Thiệp cưới không tồn tại hoặc đã hết hạn.');
      }
      return res.json();
    },
    enabled: !!slug
  });
}

export function useSubmitRsvp(slug: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (payload: SubmitRsvpRequest): Promise<RsvpResponse> => {
      const res = await fetch(`/api/invitations/${slug}/rsvp`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể gửi phản hồi RSVP.');
      }

      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['public-invitation', slug] });
      queryClient.invalidateQueries({ queryKey: ['my-invitation'] });
      queryClient.invalidateQueries({ queryKey: ['my-rsvps'] });
    }
  });
}
