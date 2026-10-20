import { useQuery } from '@tanstack/react-query';
import axiosClient from '../../../services/axiosClient';
import type { CategoryDto, PublicListingsFilter, PublicListingsResult } from '../../../types/category.types';
import type { ListingDto } from '../../../types/listing.types';
import { useAuthStore } from '../../../store/authStore';

export const usePublicCategories = () =>
  useQuery<CategoryDto[]>({
    queryKey: ['categories'],
    queryFn: async () => {
      const res = await axiosClient.get('/api/categories');
      return res.data;
    },
    staleTime: 5 * 60 * 1000, // cache 5 phút
  });

export const usePublicListings = (filter: PublicListingsFilter) =>
  useQuery<PublicListingsResult>({
    queryKey: ['public-listings', filter],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filter.categoryId) params.set('categoryId', filter.categoryId);
      if (filter.location) params.set('location', filter.location);
      if (filter.minPrice != null) params.set('minPrice', String(filter.minPrice));
      if (filter.maxPrice != null) params.set('maxPrice', String(filter.maxPrice));
      if (filter.minRating != null) params.set('minRating', String(filter.minRating));
      if (filter.keyword) params.set('keyword', filter.keyword);
      if (filter.sortBy) params.set('sortBy', filter.sortBy);
      if (filter.page) params.set('page', String(filter.page));
      if (filter.pageSize) params.set('pageSize', String(filter.pageSize));
      const res = await axiosClient.get(`/api/listings?${params.toString()}`);
      return res.data;
    },
  });

export const usePublicListingDetail = (id: string, revisionId?: string | null) => {
  const user = useAuthStore(state => state.user);
  return useQuery<ListingDto>({
    queryKey: ['listing-detail', id, user?.id ?? null, user?.role ?? null, revisionId ?? null],
    queryFn: async () => {
      const res = revisionId
        ? await axiosClient.get(`/api/listings/${id}/edit`, { params: { revisionId } })
        : await axiosClient.get(`/api/listings/${id}`);
      return res.data;
    },
    enabled: !!id,
  });
};
