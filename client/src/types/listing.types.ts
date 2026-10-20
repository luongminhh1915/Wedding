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
  updatedAt?: string | null;
  primaryImageUrl?: string;
  isActive?: boolean;
  revisionStatus?: string | null;
  latestRevisionId?: string | null;
  revisionReviewNote?: string | null;
}

export interface VendorListingsPage {
  items: ListingSummaryDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface VendorListingsPageParams {
  keyword?: string;
  categoryId?: string;
  status?: ListingStatus;
  page: number;
  pageSize: number;
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
  serviceDetails?: ServiceDetailsDto | null;
  cancellationPolicy?: string | null;
  categoryRequiresCommissionBasisApproval?: boolean;
  isActive?: boolean;
  latestRevision?: ListingRevisionDto | null;
  revisionHistory?: ListingRevisionDto[] | null;
  pricingServiceItemId?: string | null;
  pricing?: ListingPricingDto | null;
  pricingOptions?: ListingPricingOption[];
  pricingSelectionRequired?: boolean;
}

export interface ListingPricingDto {
  serviceItemId?: string | null;
  basePrice: number;
  unit: string;
  minGuests: number;
  maxGuests: number;
  regions: string;
  cancellationPolicy?: string | null;
  maxBookingsPerDay: number;
  variants?: { id?: string | null; name: string; priceDelta: number }[];
  addOns?: { id?: string | null; name: string; price: number; perUnit: boolean }[];
}
export interface ListingPricingOption extends Omit<ListingPricingDto, 'serviceItemId'> {
  id: string;
  name: string;
  moderationStatus: string;
  isActive: boolean;
}

export interface ListingRevisionDto {
  id: string; sequence: number; status: 'Draft' | 'Pending' | 'Approved' | 'Rejected' | 'Superseded' | 'Baseline';
  createdAt: string; submittedAt?: string | null; reviewedAt?: string | null; reviewNote?: string | null;
  createdById: string; reviewedById?: string | null;
}

export interface FullServiceDetailsDto {
  scope: string;
  capacity: string;
  stages: string;
  outsourcing: string;
  managementFeeDescription: string;
  packagePriceDescription: string;
  extraCostTerms: string;
  paymentTerms: string;
}

export interface ServiceDetailsDto {
  shortDescription: string;
  priceUnit: string;
  includedItems: string;
  excludedItems: string;
  duration: string;
  process: string;
  serviceOptions: string;
  additionalFees: string;
  terms: string;
  reschedulePolicy: string;
  portfolioUrls: string[];
  videoUrl: string | null;
  highlights: string;
  customerRequirements: string;
  notes: string;
  fullService: FullServiceDetailsDto | null;
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
  serviceDetails?: ServiceDetailsDto | null;
  pricing?: ListingPricingDto;
}

export interface UpdateListingRequest {
  title: string;
  minPrice: number;
  maxPrice: number;
  description: string;
  location: string;
  imageUrls?: string[] | null;
  serviceDetails?: ServiceDetailsDto | null;
  pricing?: ListingPricingDto;
}
