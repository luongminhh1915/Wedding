import { test } from 'node:test';
import assert from 'node:assert/strict';
import { QueryClient } from '@tanstack/react-query';
import { canReplyToReview, invalidateReviewQueries } from '../src/features/customer/reviewCacheModel.ts';
import { reviewQueryKey } from '../src/features/customer/reviewTransport.ts';

const customer = { userId: 'customer', role: 'Customer', token: 'synthetic', revision: 1 };
const anonymous = { userId: null, role: null, token: null, revision: 2 };

test('approve, edit decisions and replies invalidate every independent target/filter/session and legacy panel', async () => {
  for (const mutation of ['approve', 'moderate-edit', 'reply']) {
    const client = new QueryClient();
    const keys = [];
    for (const session of [customer, anonymous]) {
      for (const target of ['Service:service', 'Shop:shop', 'Combo:combo']) {
        for (const page of [1, 2]) keys.push([...reviewQueryKey('target-reviews', session, target), page, 10, 5, true, 'newest']);
      }
      keys.push(reviewQueryKey('listing-reviews', session, 'service'), reviewQueryKey('vendor-reviews', session, 'shop'));
    }
    for (const key of keys) client.setQueryData(key, { unchanged: true });
    client.setQueryData(['unrelated-bookings'], ['preserve']);
    await invalidateReviewQueries(client, mutation);
    for (const key of keys) assert.equal(client.getQueryState(key).isInvalidated, true, `${mutation}: ${JSON.stringify(key)}`);
    assert.equal(client.getQueryState(['unrelated-bookings']).isInvalidated, false);
    assert.deepEqual(client.getQueryData(['unrelated-bookings']), ['preserve']);
    client.clear();
  }
});

test('pending edit submission refreshes private queues without invalidating the still-approved public version', async () => {
  const client = new QueryClient();
  for (const key of [['my-reviews', customer.userId], ['moderation-edits', 'moderator'], ['target-reviews', 'Service:service']]) client.setQueryData(key, []);
  await invalidateReviewQueries(client, 'edit');
  assert.equal(client.getQueryState(['my-reviews', customer.userId]).isInvalidated, true);
  assert.equal(client.getQueryState(['moderation-edits', 'moderator']).isInvalidated, true);
  assert.equal(client.getQueryState(['target-reviews', 'Service:service']).isInvalidated, false);
  client.clear();
});

test('all target submissions refresh eligibility and rejection refreshes own status without changing public data', async () => {
  const client = new QueryClient();
  const privateKeys = ['eligible-contracts', 'eligible-targets', 'my-reviews', 'moderation-reviews'];
  for (const key of privateKeys) client.setQueryData([key, 'actor'], []);
  await invalidateReviewQueries(client, 'submit');
  for (const key of privateKeys) assert.equal(client.getQueryState([key, 'actor']).isInvalidated, true);
  client.setQueryData(['my-reviews', 'actor'], []);
  client.setQueryData(['target-reviews', 'Combo:combo'], []);
  await invalidateReviewQueries(client, 'reject');
  assert.equal(client.getQueryState(['my-reviews', 'actor']).isInvalidated, true);
  assert.equal(client.getQueryState(['target-reviews', 'Combo:combo']).isInvalidated, false);
  client.clear();
});

test('reply affordance requires the owning Vendor and an approved review; absent ownership fails closed', () => {
  const review = { vendorId: 'shop-a', status: 'Approved' };
  const owner = { role: 'VendorOwner', vendorId: 'shop-a' };
  assert.equal(canReplyToReview(owner, review), true);
  for (const user of [null, undefined, { role: 'VendorOwner' }, { role: 'VendorOwner', vendorId: 'shop-b' },
    { role: 'Customer', vendorId: 'shop-a' }, { role: 'Moderator', vendorId: 'shop-a' }]) assert.equal(canReplyToReview(user, review), false);
  for (const status of ['Pending', 'Rejected']) assert.equal(canReplyToReview(owner, { ...review, status }), false);
});
