import test from 'node:test';
import assert from 'node:assert/strict';
import { isListingPreview, listingPreviewReturn } from '../src/features/customer/listingPreview.ts';

test('preview presentation requires an explicit flag and an authorized actor role', () => {
  for (const role of ['VendorOwner', 'Moderator', 'SuperAdmin']) assert.equal(isListingPreview(role, 'vendor'), true);
  for (const role of [null, undefined, 'Customer', 'Finance', 'CustomerCare', 'vendorowner']) assert.equal(isListingPreview(role, 'vendor'), false);
  assert.equal(isListingPreview('VendorOwner', null), false);
  assert.equal(isListingPreview('VendorOwner', 'true'), false);
});

test('preview return destination stays within the actor workspace', () => {
  assert.equal(listingPreviewReturn('VendorOwner'), '/vendor/dashboard?tab=listings');
  for (const role of ['Moderator', 'SuperAdmin']) assert.equal(listingPreviewReturn(role), '/admin/dashboard?tab=moderation');
  assert.equal(listingPreviewReturn('Customer'), '/browse');
});

