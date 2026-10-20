export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
  listingCount: number;
  icon?: string | null;
  businessGroupId?: string | null;
  businessGroupName?: string | null;
  defaultCommissionRate?: number;
  requiresCommissionBasisApproval?: boolean;
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
  minRating?: number;
  keyword?: string;
  sortBy?: 'newest' | 'price_asc' | 'price_desc' | 'popular';
  page?: number;
  pageSize?: number;
}
