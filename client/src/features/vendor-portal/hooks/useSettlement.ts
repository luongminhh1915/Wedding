import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { MonthlySettlementStatement, BankWebhookRequest, BankWebhookResponse } from '../../../types/commission.types';

export function useSettlement(month?: number, year?: number) {
  const queryClient = useQueryClient();

  const statementQuery = useQuery({
    queryKey: ['vendor-statement', month, year],
    queryFn: async (): Promise<MonthlySettlementStatement> => {
      const token = localStorage.getItem('token');
      const params = new URLSearchParams();
      if (month) params.append('month', month.toString());
      if (year) params.append('year', year.toString());

      const url = `/api/commissions/statement${params.toString() ? `?${params.toString()}` : ''}`;
      const res = await fetch(url, {
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { Authorization: `Bearer ${token}` } : {})
        }
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Không thể tải bảng kê đối soát hoa hồng.');
      }

      return res.json();
    }
  });

  const simulatePaymentMutation = useMutation({
    mutationFn: async (payload: BankWebhookRequest): Promise<BankWebhookResponse> => {
      const res = await fetch('/api/commissions/webhook/bank-transfer', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || 'Giao dịch chuyển khoản không hợp lệ hoặc không tìm thấy khoản nợ.');
      }

      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vendor-statement'] });
      queryClient.invalidateQueries({ queryKey: ['vendor-contracts'] });
    }
  });

  return {
    statement: statementQuery.data,
    isLoading: statementQuery.isLoading,
    isError: statementQuery.isError,
    error: statementQuery.error,
    refetch: statementQuery.refetch,
    simulatePayment: simulatePaymentMutation.mutateAsync,
    isSimulatingPayment: simulatePaymentMutation.isPending
  };
}
