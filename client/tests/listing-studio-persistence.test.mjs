import { test } from 'node:test';
import assert from 'node:assert/strict';
import { emptyServiceDetails, normalizeServiceDetails, validateServiceDetails, serviceStudioImageSrc } from '../src/features/listings/serviceDetailsModel.ts';
import { hydrateListingEditor, listingUpdatePayload } from '../src/features/vendor-portal/components/listingEditModel.ts';
import { listingStudioActorIsCurrent, uploadStudioImages, validateStudioUploadFiles } from '../src/features/vendor-portal/components/listingStudioUpload.ts';

const image = '/uploads/listings/0123456789abcdef0123456789abcdef.webp';
const album = () => ({ id: 'f74c7261-553b-41d4-9830-a84af2b5aa36', name: ' Ngày vui ', images: [image] });
const photo = () => new File(['synthetic image'], 'photo.jpg', { type: 'image/jpeg' });

test('Studio metadata survives save and re-open; a price-only edit does not rewrite it', () => {
  const metadata = { ...emptyServiceDetails(), notes: 'Keep original terms', collections: [album()], latestUpdate: { content: ' New announcement ', imageUrl: image }, commissionRate: .01 };
  const details = { id: 'owned', title: 'Service', categoryId: 'photo', minPrice: 100, maxPrice: 200, description: 'Original description', location: 'Hà Nội', media: [{ id: 'photo', mediaUrl: image, displayOrder: 0, isFeatured: true }], serviceDetails: metadata };
  const hydrated = hydrateListingEditor(details, 'owned');
  assert.equal(hydrated.serviceDetails.collections[0].name, 'Ngày vui');
  assert.equal(hydrated.serviceDetails.latestUpdate.content, 'New announcement');
  assert.equal(Object.hasOwn(hydrated.serviceDetails, 'commissionRate'), false);
  const priceOnly = listingUpdatePayload({ ...hydrated.form, minPrice: '120' }, hydrated.images, hydrated.images, hydrated.serviceDetails, metadata);
  assert.equal(Object.hasOwn(priceOnly, 'serviceDetails'), false);
  const edited = listingUpdatePayload(hydrated.form, hydrated.images, hydrated.images, { ...hydrated.serviceDetails, latestUpdate: { content: 'Updated', imageUrl: '' } }, metadata);
  assert.equal(edited.serviceDetails.collections[0].images[0], image);
  assert.equal(edited.serviceDetails.notes, 'Keep original terms');
  assert.deepEqual(normalizeServiceDetails({ latestUpdate: { content: ' ', imageUrl: '' } }).latestUpdate, null);
  assert.equal(serviceStudioImageSrc(image, 'https://api.example.invalid/'), `https://api.example.invalid${image}`);
});

test('Studio validates album bounds and rejects transient/unsafe photos rather than quietly omitting them', () => {
  const base = { ...emptyServiceDetails(), collections: [album()] };
  assert.deepEqual(validateServiceDetails(base), {});
  for (const url of ['blob:local-preview', 'data:image/png;base64,a', 'http://example.invalid/image.png', 'https://user:password@example.invalid/image.png', '/uploads/listings/../secret', 'https://example.invalid/%252e%252e/secret', 'https://example.invalid/a\\b']) {
    assert.ok(validateServiceDetails({ ...base, collections: [{ ...album(), images: [url] }] })['serviceDetails.collections'], url);
    assert.ok(validateServiceDetails({ ...emptyServiceDetails(), latestUpdate: { content: '', imageUrl: url } })['serviceDetails.latestUpdate'], url);
  }
  for (const invalid of [{ ...album(), images: [] }, { ...album(), images: [image, image] }, { ...album(), name: 'x'.repeat(101) }, { ...album(), id: 'not-a-guid' }, { ...album(), id: '00000000-0000-0000-0000-000000000000' }])
    assert.ok(validateServiceDetails({ ...base, collections: [invalid] })['serviceDetails.collections']);
  assert.ok(validateServiceDetails({ ...base, collections: [album(), album()] })['serviceDetails.collections']);
  assert.ok(validateServiceDetails({ ...base, latestUpdate: { content: 'x'.repeat(4001), imageUrl: '' } })['serviceDetails.latestUpdate']);
});

test('Uploads reject unsupported files, incomplete server responses and temporary blob URLs', async () => {
  for (const file of [{ name: 'bad.svg', type: 'image/svg+xml', size: 10 }, { name: 'photo.jpg', type: 'image/png', size: 10 }, { name: 'photo.jpg', type: 'image/jpeg', size: 8 * 1024 * 1024 + 1 }]) assert.throws(() => validateStudioUploadFiles([file]));
  for (const response of [[], ['blob:local-preview'], undefined]) {
    await assert.rejects(uploadStudioImages([photo()], { isCurrent: () => true, signal: new AbortController().signal, prepare: async file => file, send: async () => response }));
  }
  assert.deepEqual(await uploadStudioImages([photo()], { isCurrent: () => true, signal: new AbortController().signal, prepare: async file => file, send: async () => [image] }), [image]);
});

test('Studio upload never sends after session changes during preparation and never accepts an old-session response', async () => {
  let active = true, sent = 0;
  await assert.rejects(uploadStudioImages([photo()], { isCurrent: () => active, signal: new AbortController().signal, prepare: async file => { active = false; return file; }, send: async () => { sent++; return [image]; } }));
  assert.equal(sent, 0);
  active = true;
  await assert.rejects(uploadStudioImages([photo()], { isCurrent: () => active, signal: new AbortController().signal, prepare: async file => file, send: async () => { active = false; return [image]; } }));
  const controller = new AbortController(); controller.abort();
  await assert.rejects(uploadStudioImages([photo()], { isCurrent: () => true, signal: controller.signal, prepare: async file => file, send: async () => { sent++; return [image]; } }));
  assert.equal(sent, 0);
});

test('Studio actor identity binds account, token, role and session revision', () => {
  const actor = { id: 'vendor-a', token: 'synthetic-token', revision: 9 };
  const current = { user: { id: 'vendor-a', role: 'VendorOwner' }, token: 'synthetic-token', sessionRevision: 9 };
  assert.equal(listingStudioActorIsCurrent(actor, current), true);
  for (const changed of [{ ...current, user: { id: 'vendor-b', role: 'VendorOwner' } }, { ...current, token: 'other' }, { ...current, sessionRevision: 10 }, { ...current, user: { id: 'vendor-a', role: 'SuperAdmin' } }, { ...current, user: null, token: null }]) assert.equal(listingStudioActorIsCurrent(actor, changed), false);
});
