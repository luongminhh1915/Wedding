import { useMutation } from '@tanstack/react-query';
import axiosClient from '../../../services/axiosClient';
import { useAuthStore } from '../../../store/authStore';
import type { AuthResponse, LoginRequest, RegisterCustomerRequest, RegisterVendorRequest } from '../../../types/auth';

async function authenticate(path: string, input: unknown): Promise<AuthResponse> {
  const revision = useAuthStore.getState().sessionRevision;
  const response = await axiosClient.post<AuthResponse>(path, input);
  if (useAuthStore.getState().sessionRevision !== revision) throw new Error('Phiên đã thay đổi. Vui lòng đăng nhập lại.');
  return response.data;
}

export const useAuth = () => {
  const { setAuth, logout, user, token } = useAuthStore();

  const loginMutation = useMutation({
    mutationFn: async (credentials: LoginRequest): Promise<AuthResponse> => {
      return authenticate('/api/auth/login', credentials);
    },
    onSuccess: (data) => {
      setAuth(data.user, data.token);
    },
  });

  const registerCustomerMutation = useMutation({
    mutationFn: async (data: RegisterCustomerRequest): Promise<AuthResponse> => {
      return authenticate('/api/auth/register-customer', data);
    },
    onSuccess: (data) => {
      setAuth(data.user, data.token);
    },
  });

  const registerVendorMutation = useMutation({
    mutationFn: async (data: RegisterVendorRequest): Promise<AuthResponse> => {
      return authenticate('/api/auth/register-vendor', data);
    },
    onSuccess: (data) => {
      setAuth(data.user, data.token);
    },
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
    currentUser: user,
  };
};
