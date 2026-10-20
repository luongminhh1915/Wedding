import test from 'node:test';
import assert from 'node:assert/strict';
import { moderationFilters, moderationSelection, updateModerationFilters, selectModerationRow, moderationStatus, moderationDecision } from '../src/features/admin/moderationQueueModel.ts';

const id = '01234567-89ab-cdef-0123-456789abcdef';
const revisionId = '11234567-89ab-cdef-0123-456789abcdef';
const version = 'A'.repeat(64);
const listing = () => ({ type: 'listing', id, listing: { id, status: 'Active', minPrice: 999999, commissionRate: .12, latestRevision: { id: revisionId, status: 'Pending' } } });
const item = () => ({ type: 'service-item', id, serviceItem: { id, moderationStatus: 'Pending', reviewVersion: version, basePrice: 999999, commissionRate: .12 } });
const combo = () => ({ type: 'combo', id, combo: { id, moderationStatus: 'Approved', latestRevision: { id: revisionId, status: 'Pending' }, estimatedTotal: 999999 } });

test('moderation query defaults and unsupported/malformed filter values remain bounded', () => {
  assert.deepEqual(moderationFilters(new URLSearchParams()), { type: 'all', status: 'Pending', search: '', page: 1, pageSize: 10 });
  const params = new URLSearchParams({ moderationType: 'contracts', moderationStatus: 'Paid', moderationSearch: ' a '.repeat(100), moderationPage: '-2', moderationSize: '100' });
  const parsed = moderationFilters(params);
  assert.equal(parsed.type, 'all'); assert.equal(parsed.status, 'Pending'); assert.equal(parsed.page, 1);
  assert.equal(parsed.pageSize, 20); assert.equal(parsed.search.length, 150);
  assert.equal(moderationFilters(new URLSearchParams('moderationPage=1e8&moderationSize=abc')).pageSize, 10);
});

test('filter/page/selection URL updates preserve tabs and unrelated query state without mutating history', () => {
  const before = new URLSearchParams({ tab: 'moderation', keep: 'value', moderationType: 'combo', moderationStatus: 'Rejected', moderationPage: '4', moderationSearch: 'áo cưới' });
  const original = before.toString();
  const selected = selectModerationRow(before, { id, type: 'combo', revisionId });
  assert.equal(before.toString(), original);
  assert.deepEqual(moderationSelection(selected), { type: 'combo', id, revisionId });
  assert.deepEqual(moderationFilters(selected), moderationFilters(before));
  const next = updateModerationFilters(selected, { status: 'Pending' });
  assert.equal(next.get('tab'), 'moderation'); assert.equal(next.get('keep'), 'value');
  assert.equal(moderationFilters(next).page, 1); assert.equal(moderationFilters(next).search, 'áo cưới');
  assert.equal(moderationSelection(next), null);
  assert.equal(moderationFilters(updateModerationFilters(before, { page: 3 })).page, 3);
  assert.equal(moderationFilters(new URLSearchParams(original)).page, 4);
});

test('selection permits only discriminated UUID resource identities and no revision for service items', () => {
  for (const params of [
    { reviewType: 'all', reviewId: id }, { reviewType: 'contracts', reviewId: id },
    { reviewType: 'listing', reviewId: '../private' }, { reviewType: 'listing', reviewId: id, reviewRevision: 'invalid' },
    { reviewType: 'service-item', reviewId: id, reviewRevision: revisionId },
    { reviewType: 'listing', reviewId: '00000000-0000-0000-0000-000000000000' },
  ]) assert.equal(moderationSelection(new URLSearchParams(params)), null);
  assert.deepEqual(moderationSelection(new URLSearchParams({ reviewType: 'service-item', reviewId: id })), { type: 'service-item', id });
  assert.equal(selectModerationRow(new URLSearchParams('reviewRevision=old'), { id, type: 'service-item' }).has('reviewRevision'), false);
});

test('listing approve/reject preserve exact revision and whitelist request fields', () => {
  assert.equal(moderationStatus(listing()), 'Pending');
  assert.deepEqual(moderationDecision(listing(), true, ''), { url: `/api/listings/${id}/approve`, body: null, params: { revisionId } });
  assert.deepEqual(moderationDecision(listing(), false, '  Cần bổ sung ảnh  '), { url: `/api/listings/${id}/reject`, body: { reason: 'Cần bổ sung ảnh', revisionId }, params: undefined });
  assert.throws(() => moderationDecision(listing(), false, ' '));
  assert.throws(() => moderationDecision(listing(), true, 'x'.repeat(1001)));
});

test('service-item decisions require opaque version and never carry price/rate mutations', () => {
  assert.deepEqual(moderationDecision(item(), true, ''), { url: `/api/combos/items/${id}/review`, body: { approve: true, note: null, reviewVersion: version }, params: undefined });
  assert.deepEqual(moderationDecision(item(), false, '  Không đủ thông tin '), { url: `/api/combos/items/${id}/review`, body: { approve: false, note: 'Không đủ thông tin', reviewVersion: version }, params: undefined });
  for (const token of [undefined, '', 'a'.repeat(63), 'z'.repeat(64)]) {
    const detail = item(); detail.serviceItem.reviewVersion = token;
    assert.throws(() => moderationDecision(detail, true, ''));
  }
});

test('combo moderation uses latest revision, not approved baseline or pricing object', () => {
  assert.deepEqual(moderationDecision(combo(), true, ''), { url: `/api/combos/templates/${id}/review`, body: { approve: true, note: null, revisionId }, params: undefined });
  assert.throws(() => moderationDecision(combo(), false, ' '));
  assert.throws(() => moderationDecision(combo(), true, 'x'.repeat(2001)));
  const legacy = combo(); delete legacy.combo.latestRevision; legacy.combo.moderationStatus = 'Pending';
  assert.deepEqual(moderationDecision(legacy, false, 'Legacy reason').body, { approve: false, note: 'Legacy reason' });
});

test('nonpending/mismatched/invalid identities cannot be reviewed even from a retained browser snapshot', () => {
  for (const detail of [listing(), item(), combo()]) {
    if (detail.listing) detail.listing.latestRevision.status = 'Superseded';
    if (detail.serviceItem) detail.serviceItem.moderationStatus = 'Approved';
    if (detail.combo) detail.combo.latestRevision.status = 'Draft';
    assert.throws(() => moderationDecision(detail, true, ''));
  }
  for (const detail of [listing(), item(), combo()]) {
    detail.id = 'invalid'; assert.throws(() => moderationDecision(detail, true, ''));
  }
  const other = item(); other.serviceItem.id = revisionId; assert.throws(() => moderationDecision(other, true, ''));
});

test('legacy listing keeps bounded supported decision contract without inventing revision or financial metadata', () => {
  const detail = listing(); delete detail.listing.latestRevision; detail.listing.status = 'PendingApproval';
  assert.deepEqual(moderationDecision(detail, false, '  Legacy reason '), { url: `/api/listings/${id}/reject`, body: { reason: 'Legacy reason' }, params: undefined });
  assert.deepEqual(moderationDecision(detail, true, ''), { url: `/api/listings/${id}/approve`, body: null, params: undefined });
});
