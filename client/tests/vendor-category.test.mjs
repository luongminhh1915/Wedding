import test from 'node:test';
import assert from 'node:assert/strict';
import { vendorCategoryError, vendorCategoryGroups } from '../src/features/vendor-portal/vendorCategoryModel.ts';
import { vendorMembershipPayload } from '../src/features/admin/vendorMembershipModel.ts';

test('category validation uses only allowed server categories and fails closed before loading', () => {
  const allowed = [{ id: 'photo' }];
  assert.equal(vendorCategoryError('photo', allowed, false, false), null);
  for (const args of [['', allowed, false, false], ['decor', allowed, false, false], ['photo', undefined, false, false], ['photo', allowed, true, false], ['photo', allowed, false, true]])
    assert.ok(vendorCategoryError(...args));
});

test('categories group by canonical business group and never derive scope from rate', () => {
  const items = [
    { id: 'photo', name: 'Ảnh', businessGroupId: 'media', businessGroupName: 'Ảnh, Váy & Makeup', defaultCommissionRate: .1 },
    { id: 'makeup', name: 'Makeup', businessGroupId: 'media', businessGroupName: 'Ảnh, Váy & Makeup', defaultCommissionRate: .1 },
    { id: 'decor', name: 'Decor', businessGroupId: 'decor', businessGroupName: 'Trang trí & Thiệp', defaultCommissionRate: .08 },
  ];
  const before = structuredClone(items);
  const groups = vendorCategoryGroups(items);
  assert.deepEqual(groups.map(group => [group.id, group.categories.map(item => item.id)]), [['media', ['photo', 'makeup']], ['decor', ['decor']]]);
  assert.deepEqual(items, before);
  assert.equal(vendorCategoryGroups([{ id: 'old', name: 'Cũ' }])[0].id, 'unmapped');
});

test('Admin membership payload is add-only and cannot carry commission or acknowledgment fields', () => {
  assert.deepEqual(vendorMembershipPayload(['photo', 'photo'], ['photo', 'decor'], ['decor'], '  Đã đối chiếu hồ sơ  '), { businessGroupIds: ['photo'], reason: 'Đã đối chiếu hồ sơ' });
  for (const args of [[[], ['photo'], [], 'reason'], [['foreign'], ['photo'], [], 'reason'], [['photo'], ['photo'], ['photo'], 'reason'], [['photo'], ['photo'], [], ' '], [['photo'], ['photo'], [], 'x'.repeat(2001)]])
    assert.throws(() => vendorMembershipPayload(...args));
  const ids = Array.from({ length: 21 }, (_, index) => String(index));
  assert.throws(() => vendorMembershipPayload(ids, ids, [], 'reason'));
});
