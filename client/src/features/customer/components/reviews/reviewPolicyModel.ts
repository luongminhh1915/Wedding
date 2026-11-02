import type { EligibleContract } from '../../../../types/review.types';

/** The endpoint supplies only completed, unused, own-customer transactions; never use arbitrary contract ids. */
export function matchingReviewContracts(contracts: EligibleContract[], listingId: string, vendorId?: string) {
  return contracts.filter(contract => !!contract.contractId && contract.listingId === listingId && (!vendorId || contract.vendorId === vendorId));
}

export function selectedReviewContract(contracts: EligibleContract[], selectedId: string) {
  return contracts.find(contract => contract.contractId === selectedId) ?? (contracts.length === 1 ? contracts[0] : undefined);
}

export function reviewHistoryLabel(isVerifiedBuyer: boolean) {
  return isVerifiedBuyer ? 'Đã xác thực giao dịch' : 'Đánh giá tư vấn cũ · Chưa xác thực giao dịch';
}
