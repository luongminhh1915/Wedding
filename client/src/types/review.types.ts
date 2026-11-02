export interface Review {
  id: string;
  targetType?: "Service" | "Shop" | "Combo";
  bookingContractId?: string;
  comboOrderId?: string;
  comboTemplateId?: string;
  comboTemplateName?: string;
  contractCode?: string;
  listingId?: string | null;
  listingTitle?: string | null;
  customerId: string;
  customerName: string;
  vendorId: string;
  vendorBrandName: string;
  rating: number; // 1 - 5
  content: string;
  photosJson?: string;
  isVerifiedBuyer: boolean; // BR-009
  status: "Pending" | "Approved" | "Rejected";
  vendorReply?: string;
  vendorRepliedAt?: string;
  createdAt: string;
  qualityRating?: number;
  serviceRating?: number;
  punctualityRating?: number;
  valueRating?: number;
  averageRating?: number;
  moderationFlag?: string;
  moderationDeadline?: string;
  rejectionReason?: string | null;
  editStatus?: "Pending" | "Approved" | "Rejected";
  editAttemptCount?: number;
  canEdit?: boolean;
}

export type PublicReview = Omit<Review,
  "bookingContractId" | "contractCode" | "customerId" | "moderationFlag" | "moderationDeadline">;

export interface SubmitReviewRequest {
  bookingContractId?: string;
  listingId: string;
  rating: number;
  content: string;
  photosJson?: string;
  qualityRating?: number;
  serviceRating?: number;
  punctualityRating?: number;
  valueRating?: number;
}

export interface SubmitShopReviewRequest {
  bookingContractId?: string;
  comboOrderId?: string;
  rating: number;
  content: string;
  photosJson?: string;
}

export interface SubmitComboReviewRequest {
  comboOrderId: string;
  rating: number;
  content: string;
  photosJson?: string;
}

export interface EligibleReviewTarget {
  targetType: "Service" | "Shop" | "Combo";
  transactionId: string;
  transactionCode: string;
  targetId: string;
  targetName: string;
  bookingContractId?: string | null;
  comboOrderId?: string | null;
  listingId?: string | null;
}

export interface ReviewRatingSummary {
  averageRating: number;
  totalReviews: number;
  ratingDistribution: Record<string, number>;
}

export interface ReviewPage {
  summary: ReviewRatingSummary;
  reviews: PublicReview[];
  page: number;
  pageSize: number;
  filteredCount: number;
  totalPages: number;
}

export interface PendingReviewEdit {
  id: string;
  reviewId: string;
  attemptNumber: number;
  beforeContent: string;
  beforeRating: number;
  afterContent: string;
  afterRating: number;
  requestedAt: string;
  review: Review;
  beforePhotosJson?: string | null;
  afterPhotosJson?: string | null;
  beforeQualityRating?: number | null;
  afterQualityRating?: number | null;
  beforeServiceRating?: number | null;
  afterServiceRating?: number | null;
  beforePunctualityRating?: number | null;
  afterPunctualityRating?: number | null;
  beforeValueRating?: number | null;
  afterValueRating?: number | null;
}

export interface EligibleContract {
  contractId: string;
  contractCode: string;
  vendorId: string;
  vendorBrandName: string;
  listingId?: string;
  listingTitle?: string;
  weddingDate?: string;
}
