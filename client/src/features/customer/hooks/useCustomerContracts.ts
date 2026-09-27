import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { Contract } from '../../../types/contract.types';

export function useCustomerContracts(pageNumber = 1, pageSize = 20) {
  const queryClient = useQueryClient();

  const contractsQuery = useQuery({
    queryKey: ['customer-contracts', pageNumber, pageSize],
    queryFn: async (): Promise<Contract[]> => {
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/contracts/my-contracts?pageNumber=${pageNumber}&pageSize=${pageSize}`, {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể tải danh sách hợp đồng của bạn.');
      }

      return res.json();
    }
  });

  const confirmMutation = useMutation({
    mutationFn: async (contractId: string) => {
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/contracts/${contractId}/confirm`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể xác nhận hợp đồng.');
      }

      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customer-contracts'] });
    }
  });

  const rejectMutation = useMutation({
    mutationFn: async ({ contractId, reason }: { contractId: string; reason: string }) => {
      const token = localStorage.getItem('token');
      const res = await fetch(`/api/contracts/${contractId}/reject`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify({ reason })
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể gửi báo cáo sai lệch hợp đồng.');
      }

      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['customer-contracts'] });
    }
  });

  return {
    contracts: contractsQuery.data ?? [],
    isLoading: contractsQuery.isLoading,
    isError: contractsQuery.isError,
    error: contractsQuery.error,
    refetch: contractsQuery.refetch,
    confirmContract: confirmMutation.mutateAsync,
    isConfirming: confirmMutation.isPending,
    rejectContract: rejectMutation.mutateAsync,
    isRejecting: rejectMutation.isPending
  };
}
