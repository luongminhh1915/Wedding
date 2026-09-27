import { useMutation, useQuery } from '@tanstack/react-query';
import axiosClient from '../../../services/axiosClient';
import { useAuthStore } from '../../../store/authStore';
import type { AuthResponse, LoginRequest, RegisterCustomerRequest, RegisterVendorRequest, User } from '../../../types/auth';

export const useAuth = () => {
  const { setAuth, logout, user, token } = useAuthStore();

  const loginMutation = useMutation({
    mutationFn: async (credentials: LoginRequest): Promise<AuthResponse> => {
      const response = await axiosClient.post<AuthResponse>('/api/auth/login', credentials);
      return response.data;
    },
    onSuccess: (data) => {
      setAuth(data.user, data.token);
    },
  });

  const registerCustomerMutation = useMutation({
    mutationFn: async (data: RegisterCustomerRequest): Promise<AuthResponse> => {
      const response = await axiosClient.post<AuthResponse>('/api/auth/register-customer', data);
      return response.data;
    },
    onSuccess: (data) => {
      setAuth(data.user, data.token);
    },
  });

  const registerVendorMutation = useMutation({
    mutationFn: async (data: RegisterVendorRequest): Promise<AuthResponse> => {
      const response = await axiosClient.post<AuthResponse>('/api/auth/register-vendor', data);
      return response.data;
    },
    onSuccess: (data) => {
      setAuth(data.user, data.token);
    },
  });

  const currentUserQuery = useQuery({
    queryKey: ['currentUser'],
    queryFn: async (): Promise<User> => {
      const response = await axiosClient.get<User>('/api/auth/me');
      return response.data;
    },
    enabled: !!token,
  });

  return {
    user,
    token,
    isAuthenticated: !!token,
    login: loginMutation.mutateAsync,
    isLoggingIn: loginMutation.isPending,
    loginError: loginMutation.error,
    registerCustomer: registerCustomerMutation.mutateAsync,
    isRegisteringCustomer: registerCustomerMutation.isPending,
    registerVendor: registerVendorMutation.mutateAsync,
    isRegisteringVendor: registerVendorMutation.isPending,
    logout,
    currentUser: currentUserQuery.data,
  };
};
