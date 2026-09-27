export type ContractStatus = 'Draft' | 'PendingVerification' | 'Confirmed' | 'Completed' | 'Cancelled' | 'Disputed';

export interface ContractCommission {
  id: string;
  period: string;
  commissionRate: number;
  commissionAmount: number;
  dueDate: string;
  status: string;
}

export interface Contract {
  id: string;
  contractCode: string;
  leadId: string;
  voucherId?: string;
  voucherCode?: string;
  voucherDiscount?: number;
  vendorId: string;
  vendorBrandName: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  contractValue: number;
  depositAmount: number;
  contractImageUrl?: string;
  weddingDate?: string;
  status: ContractStatus;
  verificationDeadline: string;
  confirmedAt?: string;
  completedAt?: string;
  cancellationReason?: string;
  createdAt: string;
  commissions: ContractCommission[];
}

export interface CreateContractRequest {
  leadId: string;
  voucherCode?: string;
  contractValue: number;
  depositAmount: number;
  contractImageUrl?: string;
  weddingDate?: string;
}
