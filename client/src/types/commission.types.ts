export interface VietQrInfo {
  qrImageUrl: string;
  bankId: string;
  bankName: string;
  accountNumber: string;
  accountName: string;
  amount: number;
  description: string;
}

export interface CommissionItem {
  id: string;
  contractId: string;
  contractCode: string;
  customerName: string;
  contractValue: number;
  period: string; // "Kỳ 1 (50% lúc cọc)" | "Kỳ 2 (50% sau cưới)"
  commissionRate: number;
  commissionAmount: number;
  dueDate: string;
  status: 'Pending' | 'Paid' | 'Overdue';
  paidAt?: string;
  paymentReferenceCode?: string;
  qrImageUrl?: string;
}

export interface MonthlySettlementStatement {
  vendorId: string;
  vendorBrandName: string;
  month: number;
  year: number;
  settlementDate: string; // Ngày 25
  dueDate: string;        // Ngày cuối tháng
  totalContractValue: number;
  totalCommissionAmount: number;
  totalPendingAmount: number;
  totalPaidAmount: number;
  totalOverdueAmount: number;
  paymentStatus: 'Pending' | 'Paid' | 'PartiallyPaid' | 'Overdue';
  transferContent: string; // HH <VendorCode> T<Month>
  vietQr?: VietQrInfo;
  items: CommissionItem[];
}

export interface BankWebhookRequest {
  gateway: string;
  amount: number;
  content: string;
  referenceCode?: string;
  accountNumber?: string;
}

export interface BankWebhookResponse {
  success: boolean;
  message: string;
  paidCount: number;
  totalPaidAmount: number;
  vendorId?: string;
}
