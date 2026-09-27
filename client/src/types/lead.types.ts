export type LeadStatus = 'New' | 'Accepted' | 'Contacted' | 'Contracted' | 'Cancelled' | 'Expired';

export interface VoucherSummary {
  id: string;
  code: string;
  discountValue?: number;
  discountPercent?: number;
  status: string;
  expiresAt: string;
}

export interface Lead {
  id: string;
  customerId: string;
  customerName: string;
  vendorId: string;
  vendorBrandName: string;
  listingId?: string;
  listingTitle?: string;
  phoneNumber: string; // BR-002: Bị mask dạng 0987***123 trừ khi khách mở khóa
  isPhoneUnlocked: boolean;
  weddingDate?: string;
  estimatedGuests?: number;
  estimatedBudget?: number;
  notes?: string;
  status: LeadStatus;
  slaDeadline: string;
  acceptedAt?: string;
  createdAt: string;
  voucher?: VoucherSummary;
}

export interface SendLeadRequest {
  listingId?: string;
  vendorId?: string;
  phoneNumber?: string;
  weddingDate?: string;
  estimatedGuests?: number;
  estimatedBudget?: number;
  notes?: string;
}

export interface SendLeadResponse {
  leadId: string;
  status: string;
  slaDeadline: string;
  voucher: VoucherSummary;
  message: string;
}

export interface VoucherDto {
  id: string;
  code: string;
  leadId: string;
  customerId: string;
  customerName: string;
  vendorId: string;
  vendorBrandName: string;
  discountValue?: number;
  discountPercent?: number;
  status: string;
  issuedAt: string;
  expiresAt: string;
  redeemedAt?: string;
  isExpired: boolean;
}
