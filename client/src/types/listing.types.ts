export interface ListingMediaDto {
  id: string;
  mediaUrl: string;
  thumbnailUrl?: string;
  isFeatured: boolean;
  displayOrder: number;
}

export interface ListingSummaryDto {
  id: string;
  title: string;
  slug: string;
  minPrice: number;
  maxPrice: number;
  location: string;
  status: ListingStatus;
  rejectionReason?: string;
  categoryName: string;
  vendorBrandName: string;
  viewCount: number;
  createdAt: string;
  primaryImageUrl?: string;
}

export interface ListingDto {
  id: string;
  title: string;
  slug: string;
  minPrice: number;
  maxPrice: number;
  description: string;
  location: string;
  status: ListingStatus;
  rejectionReason?: string;
  categoryId: string;
  categoryName: string;
  vendorId: string;
  vendorBrandName: string;
  viewCount: number;
  createdAt: string;
  moderatedAt?: string;
  media: ListingMediaDto[];
}

export type ListingStatus = 'Draft' | 'PendingApproval' | 'Active' | 'Rejected' | 'Hidden';

export interface CreateListingRequest {
  categoryId: string;
  title: string;
  minPrice: number;
  maxPrice: number;
  description: string;
  location: string;
  imageUrls: string[];
}

export interface UpdateListingRequest {
  title: string;
  minPrice: number;
  maxPrice: number;
  description: string;
  location: string;
}
