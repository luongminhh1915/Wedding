import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import axiosClient from '../../../services/axiosClient';
import { useAuthStore } from '../../../store/authStore';
import type {
  ListingSummaryDto,
  ListingDto,
  CreateListingRequest,
  UpdateListingRequest,
  VendorListingsPage,
  VendorListingsPageParams,
} from '../../../types/listing.types';

// ─── KEYS ─────────────────────────────────────────────────────────────────────
export const listingKeys = {
  myListings: ['my-listings'] as const,
  detail: (id: string) => ['listing', id] as const,
  pending: ['pending-listings'] as const,
  adminAll: (status?: string) => ['admin-listings', status] as const,
};

// ─── VENDOR QUERIES ───────────────────────────────────────────────────────────

/** Lấy danh sách bài đăng của Vendor đang đăng nhập */
export const useMyListings = () => {
  const user = useAuthStore(state => state.user);
  const token = useAuthStore(state => state.token);
  return useQuery<ListingSummaryDto[]>({
    queryKey: [...listingKeys.myListings, 'all', user?.id],
    enabled: !!token && user?.role === 'VendorOwner',
    queryFn: async () => {
      const res = await axiosClient.get('/api/listings/my-listings');
      return res.data;
    },
  });
};

/** Server-owned filtering and paging; never reuse another vendor's cached private rows. */
export const useMyListingsPage = (params: VendorListingsPageParams) => {
  const user = useAuthStore(state => state.user);
  const token = useAuthStore(state => state.token);
  return useQuery<VendorListingsPage>({
    queryKey: [...listingKeys.myListings, 'page', user?.id, params],
    enabled: !!token && user?.role === 'VendorOwner',
    queryFn: async () => (await axiosClient.get<VendorListingsPage>('/api/listings/my-listings/page', { params })).data,
  });
};

/** Tạo bài đăng mới */
export const useCreateListing = () => {
  const qc = useQueryClient();
  return useMutation<{ listingId: string }, Error, CreateListingRequest>({
    mutationFn: async (data) => {
      const res = await axiosClient.post('/api/listings', data);
      return res.data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: listingKeys.myListings });
      qc.invalidateQueries({ queryKey: ['public-listings'] });
      qc.invalidateQueries({ queryKey: ['combo-catalog'] });
    },
  });
};

/** Cập nhật bài đăng */
export const useUpdateListing = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string } & UpdateListingRequest>({
    mutationFn: async ({ id, ...data }) => {
      await axiosClient.put(`/api/listings/${id}`, data);
    },
    onSuccess: (_, variables) => {
      qc.invalidateQueries({ queryKey: listingKeys.myListings });
      qc.invalidateQueries({ queryKey: listingKeys.detail(variables.id) });
      qc.invalidateQueries({ queryKey: ['listing-detail', variables.id] });
      qc.invalidateQueries({ queryKey: ['public-listings'] });
      qc.invalidateQueries({ queryKey: ['admin-listings'] });
      qc.invalidateQueries({ queryKey: ['combo-builder'] });
      qc.invalidateQueries({ queryKey: ['combo-catalog'] });
    },
  });
};

/** Nộp bài lên hàng chờ kiểm duyệt */
export const useSubmitListing = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, string>({
    mutationFn: async (id) => {
      await axiosClient.post(`/api/listings/${id}/submit`);
    },
    onSuccess: () => { qc.invalidateQueries({ queryKey: listingKeys.myListings }); qc.invalidateQueries({ queryKey: ['combo-catalog'] }); },
  });
};

// ─── MODERATOR QUERIES ────────────────────────────────────────────────────────

/** Lấy danh sách bài chờ duyệt */
export const usePendingListings = (enabled = true) =>
  useQuery<ListingDto[]>({
    queryKey: listingKeys.pending,
    enabled,
    queryFn: async () => {
      const res = await axiosClient.get('/api/listings/pending');
      return res.data;
    },
  });

/** Chi tiết bài đăng */
export const useListingDetail = (id: string, editorSession?: string) => {
  const user = useAuthStore(state => state.user);
  return useQuery<ListingDto>({
    queryKey: [...listingKeys.detail(id), 'editor', user?.id, user?.role, editorSession],
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    queryFn: async () => {
      const res = await axiosClient.get(`/api/listings/${id}/edit`);
      return res.data;
    },
    enabled: !!id && ['VendorOwner', 'Moderator', 'SuperAdmin'].includes(user?.role ?? ''),
  });
};

export const useSetListingActivity = () => {
  const qc = useQueryClient();
  return useMutation({ mutationFn: async ({ id, isActive }: { id: string; isActive: boolean }) => {
    await axiosClient.post(`/api/listings/${id}/activity`, { isActive });
  }, onSuccess: () => qc.invalidateQueries() });
};

/** Duyệt bài */
export const useApproveListing = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string; revisionId?: string }>({
    mutationFn: async ({ id, revisionId }) => {
      await axiosClient.post(`/api/listings/${id}/approve`, null, { params: { revisionId } });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: listingKeys.pending }),
  });
};

/** Từ chối bài */
export const useRejectListing = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string; reason: string; revisionId?: string }>({
    mutationFn: async ({ id, reason, revisionId }) => {
      await axiosClient.post(`/api/listings/${id}/reject`, { reason, revisionId });
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: listingKeys.pending }),
  });
};

/** [Admin] Lấy toàn bộ bài đăng (có thể lọc theo trạng thái) */
export const useAdminListings = (status?: string) =>
  useQuery<ListingDto[]>({
    queryKey: listingKeys.adminAll(status),
    queryFn: async () => {
      const params = status && status !== 'ALL' ? { status } : {};
      const res = await axiosClient.get('/api/listings/admin-all', { params });
      return res.data;
    },
  });

/** [Admin] Đổi trạng thái bài đăng (Ẩn/Hiện) */
export const useUpdateListingStatus = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string; status: string }>({
    mutationFn: async ({ id, status }) => {
      await axiosClient.put(`/api/listings/${id}/status`, { status });
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin-listings'] });
      qc.invalidateQueries({ queryKey: listingKeys.pending });
    },
  });
};

