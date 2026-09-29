import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import axiosClient from '../../../services/axiosClient';
import type {
  ListingSummaryDto,
  ListingDto,
  CreateListingRequest,
  UpdateListingRequest,
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
export const useMyListings = () =>
  useQuery<ListingSummaryDto[]>({
    queryKey: listingKeys.myListings,
    queryFn: async () => {
      const res = await axiosClient.get('/api/listings/my-listings');
      return res.data;
    },
  });

/** Tạo bài đăng mới */
export const useCreateListing = () => {
  const qc = useQueryClient();
  return useMutation<{ listingId: string }, Error, CreateListingRequest>({
    mutationFn: async (data) => {
      const res = await axiosClient.post('/api/listings', data);
      return res.data;
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: listingKeys.myListings }),
  });
};

/** Cập nhật bài đăng */
export const useUpdateListing = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string } & UpdateListingRequest>({
    mutationFn: async ({ id, ...data }) => {
      await axiosClient.put(`/api/listings/${id}`, data);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: listingKeys.myListings }),
  });
};

/** Nộp bài lên hàng chờ kiểm duyệt */
export const useSubmitListing = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, string>({
    mutationFn: async (id) => {
      await axiosClient.post(`/api/listings/${id}/submit`);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: listingKeys.myListings }),
  });
};

// ─── MODERATOR QUERIES ────────────────────────────────────────────────────────

/** Lấy danh sách bài chờ duyệt */
export const usePendingListings = () =>
  useQuery<ListingDto[]>({
    queryKey: listingKeys.pending,
    queryFn: async () => {
      const res = await axiosClient.get('/api/listings/pending');
      return res.data;
    },
  });

/** Chi tiết bài đăng */
export const useListingDetail = (id: string) =>
  useQuery<ListingDto>({
    queryKey: listingKeys.detail(id),
    queryFn: async () => {
      const res = await axiosClient.get(`/api/listings/${id}`);
      return res.data;
    },
    enabled: !!id,
  });

/** Duyệt bài */
export const useApproveListing = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, string>({
    mutationFn: async (id) => {
      await axiosClient.post(`/api/listings/${id}/approve`);
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: listingKeys.pending }),
  });
};

/** Từ chối bài */
export const useRejectListing = () => {
  const qc = useQueryClient();
  return useMutation<void, Error, { id: string; reason: string }>({
    mutationFn: async ({ id, reason }) => {
      await axiosClient.post(`/api/listings/${id}/reject`, { reason });
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

