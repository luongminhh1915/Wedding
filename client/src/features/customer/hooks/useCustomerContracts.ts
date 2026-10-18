import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import type { Contract } from "../../../types/contract.types";

export function useCustomerContracts(pageNumber = 1, pageSize = 50, enabled = true) {
  const queryClient = useQueryClient();
  const user = useAuthStore(state => state.user);
  const contractsQuery = useQuery({
    queryKey: ["customer-contracts", user?.id, pageNumber, pageSize],
    enabled: enabled && user?.role === "Customer",
    queryFn: async (): Promise<Contract[]> => (await axiosClient.get("/api/contracts/my-contracts", { params: { pageNumber, pageSize } })).data,
  });
  const confirmMutation = useMutation({
    mutationFn: async (contractId: string) => (await axiosClient.post(`/api/contracts/${contractId}/confirm`)).data,
    onSuccess: () => queryClient.invalidateQueries(),
  });
  const rejectMutation = useMutation({
    mutationFn: async ({ contractId, reason }: { contractId: string; reason: string }) => (await axiosClient.post(`/api/contracts/${contractId}/reject`, { reason })).data,
    onSuccess: () => queryClient.invalidateQueries(),
  });
  return {
    contracts: contractsQuery.data ?? [], isLoading: contractsQuery.isLoading,
    isError: contractsQuery.isError, error: contractsQuery.error, refetch: contractsQuery.refetch,
    confirmContract: confirmMutation.mutateAsync, isConfirming: confirmMutation.isPending,
    rejectContract: rejectMutation.mutateAsync, isRejecting: rejectMutation.isPending,
  };
}
