import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { Contract, CreateContractRequest } from '../../../types/contract.types';

export function useVendorContracts(status?: string) {
  const queryClient = useQueryClient();

  const contractsQuery = useQuery({
    queryKey: ['vendor-contracts', status],
    queryFn: async (): Promise<Contract[]> => {
      const token = localStorage.getItem('token');
      const url = status ? `/api/contracts/vendor?status=${status}` : `/api/contracts/vendor`;
      const res = await fetch(url, {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        throw new Error('Không thể tải danh sách hợp đồng.');
      }

      return res.json();
    }
  });

  const createContractMutation = useMutation({
    mutationFn: async (payload: CreateContractRequest) => {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/contracts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể tạo hợp đồng.');
      }

      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vendor-contracts'] });
      queryClient.invalidateQueries({ queryKey: ['vendor-leads'] });
    }
  });

  return {
    ...contractsQuery,
    createContract: createContractMutation.mutateAsync,
    isCreating: createContractMutation.isPending
  };
}
