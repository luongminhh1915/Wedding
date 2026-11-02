import test from 'node:test';
import assert from 'node:assert/strict';
import {
  VENDOR_JOURNEY_TABS, VENDOR_JOURNEY_STAGES, VENDOR_GUIDE_FAQS,
  vendorJourneyTab, vendorDashboardTab, vendorJourneyHref, vendorJourneyStageStatus, vendorJourneyNext,
} from '../src/features/vendor-portal/vendorJourneyModel.ts';
import { roleAwareReturnPath } from '../src/features/auth/authNavigation.ts';

const summary = overrides => ({
  vendorName: 'Shop thử nghiệm', isVerified: false, latestVerificationStatus: null,
  taxProfileStatus: null, policyAccepted: false, allowInCombos: false,
  listingCount: 0, publicListingCount: 0, pendingListingCount: 0, rejectedListingCount: 0,
  serviceItemCount: 0, approvedServiceItemCount: 0, comboCount: 0,
  approvedComboCount: 0, pendingComboCount: 0, newLeadCount: 0, activeLeadCount: 0,
  quoteCount: 0, contractCount: 0, pendingOrderChangeCount: 0, upcomingAppointmentCount: 0,
  ...overrides,
});
const shop = overrides => summary({
  isVerified: true, latestVerificationStatus: 'Approved', taxProfileStatus: 'Approved',
  policyAccepted: true, listingCount: 1, publicListingCount: 1, ...overrides,
});

test('vendor URLs permit only supported tabs and guide steps, never arbitrary return paths', () => {
  for (const bad of [null, undefined, '', 'admin', '/admin/dashboard', 'https://evil.test', '//evil.test', 'guide&next=https://evil.test']) {
    assert.equal(vendorJourneyTab(bad), 'overview');
    assert.equal(vendorJourneyHref(bad), '/vendor/dashboard');
  }
  for (const { id } of VENDOR_JOURNEY_TABS) {
    assert.equal(vendorJourneyTab(id), id);
    assert.equal(roleAwareReturnPath('VendorOwner', vendorJourneyHref(id)), vendorJourneyHref(id));
  }
  assert.equal(vendorJourneyHref('guide', 'verification'), '/vendor/dashboard?tab=guide&guideStep=verification');
  for (const bad of ['unknown', 'verification&tab=admin', 'https://evil.test'])
    assert.equal(vendorJourneyHref('guide', bad), '/vendor/dashboard?tab=guide');
  assert.equal(vendorJourneyHref('leads', 'verification'), '/vendor/dashboard?tab=leads');
});

test('legacy listing-filter bookmarks still open listings without mutating query or overriding explicit tabs', () => {
  assert.equal(vendorDashboardTab(new URLSearchParams()), 'overview');
  assert.equal(vendorDashboardTab(new URLSearchParams('guideStep=listings&focus=abc')), 'overview');
  for (const filter of ['serviceKeyword', 'serviceStatus', 'servicePage', 'servicePageSize']) {
    for (const value of ['', '2', 'áo cưới', 'untrusted&tab=admin']) {
      const params = new URLSearchParams([[filter, value], ['focus', 'abc']]);
      const before = params.toString();
      assert.equal(vendorDashboardTab(params), 'listings');
      assert.equal(params.toString(), before);
      for (const { id } of VENDOR_JOURNEY_TABS) {
        const explicit = new URLSearchParams(params);
        explicit.set('tab', id);
        assert.equal(vendorDashboardTab(explicit), id);
      }
      for (const tab of ['', 'unknown', 'https://evil.test']) {
        const explicit = new URLSearchParams(params);
        explicit.set('tab', tab);
        const explicitBefore = explicit.toString();
        assert.equal(vendorDashboardTab(explicit), 'overview');
        assert.equal(explicit.toString(), explicitBefore);
      }
    }
  }
});

test('all ten detailed stages link to real tabs and distinguish optional tools from business completion', () => {
  assert.equal(VENDOR_JOURNEY_STAGES.length, 10);
  assert.equal(new Set(VENDOR_JOURNEY_STAGES.map(stage => stage.id)).size, 10);
  for (const stage of VENDOR_JOURNEY_STAGES) {
    assert.ok(VENDOR_JOURNEY_TABS.some(tab => tab.id === stage.tab));
    for (const field of ['title', 'shortTitle', 'summary', 'outcome', 'nextActor', 'caution']) assert.ok(stage[field].trim());
    assert.ok(stage.steps.length >= 3 && stage.steps.every(step => step.trim().length > 30));
    for (const link of stage.relatedTabs) {
      assert.ok(VENDOR_JOURNEY_TABS.some(tab => tab.id === link.tab));
      assert.ok(link.label.trim());
    }
  }
  assert.deepEqual(VENDOR_JOURNEY_STAGES.filter(stage => stage.optional).map(stage => stage.id), ['combos', 'appointments']);
  assert.ok(VENDOR_GUIDE_FAQS.length >= 6);
  for (const faq of VENDOR_GUIDE_FAQS) assert.ok(faq.question.trim() && faq.answer.trim());
});

test('loading or failed summary is unknown, never a zero-count completed shop', () => {
  for (const data of [undefined, null]) {
    assert.equal(vendorJourneyNext(data), null);
    for (const stage of VENDOR_JOURNEY_STAGES) assert.equal(vendorJourneyStageStatus(stage.id, data).label, 'Chưa tải dữ liệu');
  }
  assert.equal(vendorJourneyStageStatus('unknown', shop()).label, 'Chưa tải dữ liệu');
});

test('new shops start with verification, pending and rejected documents remain distinct', () => {
  assert.equal(vendorJourneyNext(summary()).tab, 'verification');
  assert.equal(vendorJourneyStageStatus('verification', summary()).label, 'Cần chuẩn bị');
  const pending = summary({ latestVerificationStatus: 'Submitted', taxProfileStatus: 'Submitted' });
  assert.equal(vendorJourneyStageStatus('verification', pending).label, 'Chờ duyệt');
  assert.match(vendorJourneyNext(pending).label, /chờ duyệt/);
  const rejected = summary({ latestVerificationStatus: 'Rejected' });
  assert.equal(vendorJourneyStageStatus('verification', rejected).label, 'Cần xử lý');
  assert.match(vendorJourneyNext(rejected).label, /cần xử lý/);
  assert.equal(vendorJourneyStageStatus('verification', shop({ taxProfileStatus: 'Submitted' })).label, 'Chờ duyệt');
  assert.equal(vendorJourneyStageStatus('verification', shop({ taxProfileStatus: 'Suspended' })).label, 'Cần xử lý');
  assert.equal(vendorJourneyStageStatus('verification', shop()).label, 'Có dữ liệu');
});

test('urgent customer requests take priority over setup and optional combo creation', () => {
  const both = summary({ pendingOrderChangeCount: 1, newLeadCount: 2, upcomingAppointmentCount: 3 });
  assert.equal(vendorJourneyNext(both).tab, 'change-requests');
  assert.equal(vendorJourneyNext(summary({ newLeadCount: 1 })).tab, 'leads');
  assert.equal(vendorJourneyNext(summary({ activeLeadCount: 1 })).tab, 'leads');
  assert.equal(vendorJourneyNext(summary({ upcomingAppointmentCount: 1 })).tab, 'appointments');
  assert.equal(vendorJourneyStageStatus('changes', both).label, 'Cần xử lý');
});

test('publication guidance handles rejected and pending revisions without treating created drafts as public', () => {
  assert.match(vendorJourneyNext(shop({ rejectedListingCount: 1 })).label, /bị từ chối/);
  assert.equal(vendorJourneyStageStatus('listings', shop({ rejectedListingCount: 1 })).label, 'Cần xử lý');
  assert.match(vendorJourneyNext(shop({ pendingListingCount: 1 })).label, /chờ duyệt/);
  assert.equal(vendorJourneyStageStatus('listings', shop({ pendingListingCount: 1 })).label, 'Chờ duyệt');
  const draft = shop({ listingCount: 3, publicListingCount: 0 });
  assert.equal(vendorJourneyNext(draft).tab, 'listings');
  assert.equal(vendorJourneyStageStatus('listings', draft).label, 'Cần chuẩn bị');
  assert.match(vendorJourneyStageStatus('listings', draft).note, /chưa có bài đã duyệt/);
});

test('legacy policy and optional combo guidance do not activate commission or assert public availability', () => {
  const legacy = shop({ policyAccepted: false });
  assert.equal(vendorJourneyNext(legacy).tab, 'leads');
  assert.equal(vendorJourneyNext(shop({ policyAccepted: false, contractCount: 1 })).tab, 'contracts-couple');
  const beforeFirstListing = shop({ policyAccepted: false, listingCount: 0, publicListingCount: 0 });
  assert.equal(vendorJourneyNext(beforeFirstListing).tab, 'partnership-policy');
  assert.match(vendorJourneyNext(beforeFirstListing).note, /không phải bước khóa/);
  assert.match(vendorJourneyStageStatus('policy', legacy).note, /giữ chính sách hiện tại/);
  assert.equal(vendorJourneyStageStatus('combos', shop()).label, 'Tùy chọn');
  assert.equal(vendorJourneyNext(shop({ allowInCombos: true })).tab, 'combos');
  assert.match(vendorJourneyNext(shop({ allowInCombos: true, pendingComboCount: 1 })).label, /chờ duyệt/);
  const approved = shop({ allowInCombos: true, comboCount: 2, approvedComboCount: 2 });
  assert.equal(vendorJourneyStageStatus('combos', approved).label, 'Có dữ liệu');
  assert.match(vendorJourneyStageStatus('combos', approved).note, /không khẳng định/);
});

test('an active shop always has a follow-up action, not fake overall completion or paid settlement', () => {
  assert.equal(vendorJourneyNext(shop()).tab, 'leads');
  assert.equal(vendorJourneyNext(shop({ quoteCount: 2 })).tab, 'quotes');
  assert.equal(vendorJourneyNext(shop({ quoteCount: 2, contractCount: 1 })).tab, 'contracts-couple');
  for (const stage of VENDOR_JOURNEY_STAGES) {
    const status = vendorJourneyStageStatus(stage.id, shop({ quoteCount: 2, contractCount: 2 }));
    assert.doesNotMatch(status.label, /Hoàn thành|Đã thanh toán|100%/);
    assert.equal('progress' in status, false);
    assert.equal('percent' in status, false);
  }
  assert.equal(vendorJourneyStageStatus('settlement', shop({ contractCount: 2 })).label, 'Cần kiểm tra');
  assert.equal(vendorJourneyStageStatus('changes', shop()).label, 'Chưa phát sinh');
  assert.match(vendorJourneyStageStatus('settlement', shop({ contractCount: 2 })).note, /không có dữ liệu để kết luận/);
  const data = shop({ quoteCount: 1, contractCount: 1, allowInCombos: true, approvedComboCount: 2 });
  const before = structuredClone(data);
  for (const stage of VENDOR_JOURNEY_STAGES) vendorJourneyStageStatus(stage.id, data);
  const next = vendorJourneyNext(data);
  assert.equal(next.path, vendorJourneyHref(next.tab));
  assert.ok(VENDOR_JOURNEY_STAGES.some(stage => stage.id === next.stageId && stage.tab === next.tab));
  assert.deepEqual(data, before);
});
