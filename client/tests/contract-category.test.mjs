import { test } from 'node:test';
import assert from 'node:assert/strict';
import { contractCategoryPresentation } from '../src/features/contracts/contractCategoryModel.ts';

test('confirmed category and source are taken only from immutable backend snapshot', () => {
  const view = contractCategoryPresentation({ status: 'Confirmed', commissionRateSnapshot: .1, categorySnapshot: {
    categoryId: 'category-id', categoryName: 'Quay chụp', businessGroupName: 'Quay chụp & Làm đẹp',
    rateSource: 'CategoryDefault', policyVersion: 'recorded-policy-v1',
  }, currentCategoryName: 'Must not use current name', currentRate: .03 });
  assert.equal(view.title, 'Quay chụp'); assert.equal(view.group, 'Quay chụp & Làm đẹp');
  assert.equal(view.rate, '10%'); assert.equal(view.policy, 'Phiên bản recorded-policy-v1');
  assert.equal(view.source, 'Tỷ lệ mặc định theo danh mục');
});

test('legacy shop snapshot is not presented as negotiated approval or a current group policy', () => {
  const view = contractCategoryPresentation({ status: 'Confirmed', commissionRateSnapshot: .12, categorySnapshot: {
    categoryId: 'category-id', categoryName: 'Trang trí', rateSource: 'LegacyVendor', policyVersion: null,
  } });
  assert.equal(view.rate, '12%'); assert.equal(view.source, 'Tỷ lệ riêng của shop được giữ nguyên');
  assert.match(view.policy, /Chưa có thông tin phiên bản/); assert.equal(view.group, null);
});

test('historical missing category stays unknown even when a current catalog label is available', () => {
  const view = contractCategoryPresentation({ status: 'Completed', commissionRateSnapshot: .08, currentCategoryName: 'New name' });
  assert.equal(view.title, 'Hợp đồng cũ chưa lưu danh mục lúc xác nhận');
  assert.equal(view.source, 'Tỷ lệ đã lưu trên hợp đồng'); assert.equal(view.policy, null);
  assert.match(view.note, /Không lấy danh mục hiện tại/);
});

test('pending contracts do not claim to have a historical category or an approved rate', () => {
  const view = contractCategoryPresentation({ status: 'PendingVerification' });
  assert.equal(view.title, 'Danh mục sẽ được chốt khi khách xác nhận'); assert.equal(view.rate, null);
  assert.equal(view.source, null); assert.equal(view.policy, null);
});

test('old cancellation still has historical context when ConfirmedAt exists', () => {
  const view = contractCategoryPresentation({ status: 'Cancelled', confirmedAt: '2026-10-19T00:00:00Z' });
  assert.match(view.title, /Hợp đồng cũ/); assert.match(view.policy, /cần Admin\/Tài chính/);
});

test('non-finite or missing rate never becomes a fabricated zero or eight percent', () => {
  for (const rate of [undefined, null, NaN, Infinity, 0, -.08, .13]) {
    const view = contractCategoryPresentation({ status: 'Confirmed', commissionRateSnapshot: rate });
    assert.equal(view.rate, null); assert.equal(view.source, null); assert.match(view.policy, /Chưa đủ snapshot/);
  }
});
