import { test } from 'node:test';
import assert from 'node:assert/strict';
import { matchingReviewContracts, selectedReviewContract, reviewHistoryLabel } from '../src/features/customer/components/reviews/reviewPolicyModel.ts';

const eligible = [
  { contractId: 'completed-a', listingId: 'listing-a', vendorId: 'vendor-a' },
  { contractId: 'completed-b', listingId: 'listing-b', vendorId: 'vendor-a' },
  { contractId: 'foreign', listingId: 'listing-a', vendorId: 'vendor-b' },
];
test('review form offers only backend-eligible contracts for the exact listing and vendor', () => {
  assert.deepEqual(matchingReviewContracts(eligible, 'listing-a', 'vendor-a').map(item => item.contractId), ['completed-a']);
  assert.deepEqual(matchingReviewContracts(eligible, 'missing', 'vendor-a'), []);
  assert.deepEqual(matchingReviewContracts([{ contractId: '', listingId: 'listing-a', vendorId: 'vendor-a' }], 'listing-a'), []);
});
test('a single eligible transaction is selected without permitting a stale or arbitrary contract id', () => {
  const matches = matchingReviewContracts(eligible, 'listing-a', 'vendor-a');
  assert.equal(selectedReviewContract(matches, '')?.contractId, 'completed-a');
  assert.equal(selectedReviewContract(matches, 'foreign')?.contractId, 'completed-a');
  assert.equal(selectedReviewContract([], 'completed-a'), undefined);
});
test('multiple eligible transactions require an explicit valid choice', () => {
  assert.equal(selectedReviewContract(eligible, ''), undefined);
  assert.equal(selectedReviewContract(eligible, 'arbitrary-unverified'), undefined);
  assert.equal(selectedReviewContract(eligible, 'completed-b')?.contractId, 'completed-b');
});
test('historical consultation reviews remain clearly labelled rather than silently upgraded', () => {
  assert.match(reviewHistoryLabel(false), /tư vấn cũ/); assert.match(reviewHistoryLabel(false), /Chưa xác thực/);
  assert.equal(reviewHistoryLabel(true), 'Đã xác thực giao dịch');
});
