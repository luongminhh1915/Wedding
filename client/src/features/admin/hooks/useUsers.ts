import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import axiosClient from '../../../services/axiosClient';
import type { UserManagementDto } from '../../../types/auth';

export const userKeys = {
  all: ['users'] as const,
};

export const useUsers = () =>
  useQuery<UserManagementDto[]>({
    queryKey: userKeys.all,
    queryFn: async () => {
      const res = await axiosClient.get('/api/users');
      return res.data;
    },
  });

export const useUpdateUserStatus = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string; isActive: boolean }>({
    mutationFn: async ({ id, isActive }) => {
      await axiosClient.put(`/api/users/${id}/status`, { isActive });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: userKeys.all });
    },
  });
};
