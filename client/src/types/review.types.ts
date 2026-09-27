export interface Review {
  id: string;
  bookingContractId?: string;
  contractCode?: string;
  listingId: string;
  listingTitle: string;
  customerId: string;
  customerName: string;
  vendorId: string;
  vendorBrandName: string;
  rating: number; // 1 - 5
  content: string;
  photosJson?: string;
  isVerifiedBuyer: boolean; // BR-009
  status: 'Pending' | 'Approved' | 'Rejected';
  vendorReply?: string;
  vendorRepliedAt?: string;
  createdAt: string;
}

export interface SubmitReviewRequest {
  bookingContractId?: string;
  listingId: string;
  rating: number;
  content: string;
  photosJson?: string;
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
