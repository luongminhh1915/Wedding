import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { QueryClient, QueryObserver } from '@tanstack/react-query';
import { invalidateReviewQueries } from '../src/features/customer/reviewCacheModel.ts';

// Mirror the rating-bearing keys in useBrowse.ts, comboApi.ts and journey/api.ts.
// The source contract below makes a future key rename fail visibly.
const ratingKeys = [
  ['public-listings', { page: 1, minRating: 4, sortBy: 'rating' }],
  ['public-listings', { page: 2, categoryId: 'media', minRating: 5 }],
  ['listing-detail', 'service-a', null, null, null],
  ['listing-detail', 'service-b', 'customer-a', 'Customer', null],
  ['combo-builder'],
  ['combo-catalog-page', { page: 1, pageSize: 12, sort: 'rating', minRating: 4 }],
  ['combo-catalog-page', { categoryId: 'media', page: 2, pageSize: 12, minRating: 5 }],
];
const preservedKeys = [
  ['my-combos', 'customer-a', 'page', 1],
  ['combo-price', { templateId: 'package-a', selections: ['service-a'] }],
  ['private-service-agreements', 'customer-a'],
  ['customer-contracts', 'customer-a'],
  ['vendor-statement', 10, 2026],
  ['commission-credits'], ['settlements', '2026-10'],
  ['combo-catalog', false, 'vendor-a'],
  ['categories'], ['wedding-budget'], ['cart', 'customer-a'],
];

test('review publication refreshes every rating-bearing catalog page/filter without clearing unrelated data', async () => {
  for (const mutation of ['approve', 'moderate-edit']) {
    const client = new QueryClient();
    try {
      for (const key of [...ratingKeys, ...preservedKeys]) client.setQueryData(key, { original: key });
      await invalidateReviewQueries(client, mutation);
      for (const key of ratingKeys) {
        assert.equal(client.getQueryState(key).isInvalidated, true, `${mutation}: ${JSON.stringify(key)}`);
        assert.deepEqual(client.getQueryData(key), { original: key });
      }
      for (const key of preservedKeys) {
        assert.equal(client.getQueryState(key).isInvalidated, false, `${mutation}: ${JSON.stringify(key)}`);
        assert.deepEqual(client.getQueryData(key), { original: key });
      }
    } finally { client.clear(); }
  }
});

test('pending/rejected review work and Vendor replies do not disturb published card aggregates', async () => {
  for (const mutation of ['submit', 'edit', 'reject', 'reply']) {
    const client = new QueryClient();
    try {
      for (const key of ratingKeys) client.setQueryData(key, { ratingAvg: 4.5, totalReviews: 3 });
      await invalidateReviewQueries(client, mutation);
      for (const key of ratingKeys) {
        assert.equal(client.getQueryState(key).isInvalidated, false, `${mutation}: ${JSON.stringify(key)}`);
        assert.deepEqual(client.getQueryData(key), { ratingAvg: 4.5, totalReviews: 3 });
      }
    } finally { client.clear(); }
  }
});

test('active filtered catalog observers refetch after moderation without requiring a reload', async () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity } } });
  const stops = [];
  try {
    const reads = new Map();
    for (const key of ratingKeys) {
      client.setQueryData(key, { ratingAvg: 3, totalReviews: 3 });
      const observer = new QueryObserver(client, {
        queryKey: key,
        queryFn: async () => {
          reads.set(JSON.stringify(key), (reads.get(JSON.stringify(key)) ?? 0) + 1);
          return { ratingAvg: 4, totalReviews: 4 };
        },
      });
      stops.push(observer.subscribe(() => {}));
    }
    assert.equal(reads.size, 0, 'fresh observers should not refetch before the decision');
    await invalidateReviewQueries(client, 'approve');
    for (const key of ratingKeys) {
      assert.equal(reads.get(JSON.stringify(key)), 1);
      assert.deepEqual(client.getQueryData(key), { ratingAvg: 4, totalReviews: 4 });
      assert.equal(client.getQueryState(key).isInvalidated, false);
    }
  } finally {
    for (const stop of stops) stop();
    client.clear();
  }
});

test('rating cache regression keys remain aligned with real catalog query callers', () => {
  const source = path => readFileSync(new URL(path, import.meta.url), 'utf8');
  const browse = source('../src/features/customer/hooks/useBrowse.ts');
  const combo = source('../src/features/journey/comboApi.ts');
  const legacy = source('../src/features/journey/api.ts');
  assert.match(browse, /queryKey:\s*\['public-listings',\s*filter\]/);
  assert.match(browse, /queryKey:\s*\['listing-detail',\s*id,/);
  assert.match(combo, /queryKey:\s*\["combo-catalog-page",\s*input\]/);
  assert.match(combo, /queryKey:\s*\["combo-builder"\]/);
  assert.match(legacy, /queryKey:\s*\["combo-builder"\]/);
});
