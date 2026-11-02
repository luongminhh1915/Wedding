import { test } from 'node:test';
import assert from 'node:assert/strict';
import { notificationTarget, notificationTime, requestedNotificationFocus } from '../src/features/notifications/notificationModel.ts';

const id = 'e6013f41-ef1d-4711-b55d-3542f3d61492';
const link = `/customer/dashboard?tab=contracts&section=quotes&focus=${id}&focusKind=quote`;
test('notification destinations are local typed resource links appropriate for the signed-in role', () => {
  assert.equal(notificationTarget(link, 'Customer'), link);
  assert.equal(notificationTarget(link, 'VendorOwner'), null);
  const vendor = `/vendor/dashboard?tab=appointments&focus=${id}&focusKind=appointment`;
  assert.equal(notificationTarget(vendor, 'VendorOwner'), vendor);
  assert.equal(notificationTarget(vendor, 'Customer'), null);
  const finance = `/admin/dashboard?tab=change-requests&focus=${id}&focusKind=order-request`;
  assert.equal(notificationTarget(finance, 'Finance'), finance);
  assert.equal(notificationTarget(finance, 'SuperAdmin'), finance);
  assert.equal(notificationTarget(finance, 'Moderator'), null);
  assert.equal(notificationTarget(finance, 'CustomerCare'), null);
});
test('notification destinations reject open redirects, encoded tricks, foreign portals and malformed context', () => {
  for (const target of [null, '', '//evil.example', 'https://evil.example', `\\evil.example${link}`, `/customer/dashboard%3ftab=contracts`, `${link}&returnTo=https://evil.example`, `${link}&tab=combos`, `${link}\n`, link.replace(id, 'arbitrary-id'), link.replace('quote', 'javascript:alert(1)'), link.replace('tab=contracts', 'tab=users')])
    assert.equal(notificationTarget(target, 'Customer'), null, `must reject ${target}`);
  assert.equal(notificationTarget(link, undefined), null);
});
test('notification timestamp treats datetime2 without timezone as UTC and handles invalid values', () => {
  assert.equal(notificationTime('2026-10-18T01:00:00'), notificationTime('2026-10-18T01:00:00Z'));
  assert.match(notificationTime('2026-10-18T01:00:00Z'), /08:00/);
  assert.equal(notificationTime('not-a-date'), '');
});
test('resource focus accepts only exact typed GUID context and normalizes its case', () => {
  assert.equal(requestedNotificationFocus(new URLSearchParams(`focusKind=lead&focus=${id.toUpperCase()}`), 'lead'), id);
  assert.equal(requestedNotificationFocus(new URLSearchParams(`focusKind=lead&focus=${id}`), 'contract'), null);
  assert.equal(requestedNotificationFocus(new URLSearchParams('focusKind=lead&focus=../../../admin'), 'lead'), null);
  assert.equal(requestedNotificationFocus(new URLSearchParams('focusKind=lead'), 'lead'), null);
});
