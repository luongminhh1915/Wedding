export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
  listingCount: number;
}

export interface PublicListingsResult {
  items: import('./listing.types').ListingSummaryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface PublicListingsFilter {
  categoryId?: string;
  location?: string;
  minPrice?: number;
  maxPrice?: number;
  keyword?: string;
  sortBy?: 'newest' | 'price_asc' | 'price_desc' | 'popular';
  page?: number;
  pageSize?: number;
}
