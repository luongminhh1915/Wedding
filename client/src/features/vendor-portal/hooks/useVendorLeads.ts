import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { Lead } from '../../../types/lead.types';

export function useVendorLeads(status?: string) {
  const queryClient = useQueryClient();

  const leadsQuery = useQuery({
    queryKey: ['vendor-leads', status],
    queryFn: async (): Promise<Lead[]> => {
      const token = localStorage.getItem('token');
      const url = status 
        ? `/api/leads/vendor-inbox?status=${status}` 
        : `/api/leads/vendor-inbox`;
      
      const res = await fetch(url, {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        throw new Error('Không thể tải hộp thư Lead.');
      }

      return res.json();
    }
  });

  const acceptMutation = useMutation({
    mutationFn: async (leadId: string) => {
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/leads/${leadId}/accept`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        const errorData = await res.json().catch(() => ({}));
        throw new Error(errorData.detail || errorData.message || 'Không thể tiếp nhận Lead.');
      }

      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vendor-leads'] });
    }
  });

  return {
    ...leadsQuery,
    acceptLead: acceptMutation.mutateAsync,
    isAccepting: acceptMutation.isPending
  };
}
