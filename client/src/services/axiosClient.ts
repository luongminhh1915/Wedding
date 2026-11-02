import axios from 'axios';
import { useAuthStore } from '../store/authStore';
import { shouldInvalidateSession } from '../features/auth/sessionPolicy';

const axiosClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '',
  headers: {
    'Content-Type': 'application/json',
  },
});


// Interceptor tự động gắn JWT Bearer Token
axiosClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token && !config.headers.has('Authorization')) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
}, (error) => {
  return Promise.reject(error);
});

// Interceptor xử lý lỗi 401 Unauthorized
axiosClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (shouldInvalidateSession(error.response?.status, error.config?.url, error.config?.headers?.Authorization, useAuthStore.getState().token))
      useAuthStore.getState().logout();
    return Promise.reject(error);
  }
);

export default axiosClient;
