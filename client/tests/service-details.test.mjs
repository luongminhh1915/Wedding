import { test } from "node:test";
import assert from "node:assert/strict";
import { emptyServiceDetails, emptyFullServiceDetails, normalizeServiceDetails, validateServiceDetails, isServiceDetailsLink, serviceDetailsUpdate, SERVICE_DETAIL_SECTIONS, FULL_SERVICE_FIELDS } from "../src/features/listings/serviceDetailsModel.ts";
import { hydrateListingEditor, listingUpdatePayload } from "../src/features/vendor-portal/components/listingEditModel.ts";

test("legacy missing service metadata hydrates empty and unchanged edit omits it", () => {
  const empty = emptyServiceDetails();
  assert.deepEqual(normalizeServiceDetails(null), empty);
  assert.equal(serviceDetailsUpdate(empty, null), undefined);
  const detail = { id: "owned", title: "Original", minPrice: 1000, maxPrice: 2000, description: "Full original description", location: "Hà Nội", categoryId: "photo", media: [{ id: "image", mediaUrl: "https://example.invalid/photo.jpg", mediaType: "image", isFeatured: true, displayOrder: 0 }] };
  const editor = hydrateListingEditor(detail, "owned", "");
  const payload = listingUpdatePayload({ ...editor.form, minPrice: "1500" }, editor.images, editor.images, editor.serviceDetails, null);
  assert.equal(payload.minPrice, 1500);
  assert.equal(payload.description, detail.description);
  assert.equal(Object.hasOwn(payload, "serviceDetails"), false);
  assert.equal(Object.hasOwn(payload, "imageUrls"), false);
});
test("metadata whitelists descriptive fields and strips financial, HTML execution and arbitrary nested data", () => {
  const result = normalizeServiceDetails({ shortDescription: " Summary ", terms: " Terms ", commissionRate: .01, paidAmount: 100, cancellationPolicy: "NOT ALLOWED", portfolioUrls: [" https://example.invalid/work.jpg ", ""], fullService: { scope: " Planning ", managementFeeDescription: "Quoted separately", commissionBase: 1000 } });
  assert.equal(result.shortDescription, "Summary");
  assert.equal(result.terms, "Terms");
  assert.deepEqual(result.portfolioUrls, ["https://example.invalid/work.jpg"]);
  assert.equal(result.fullService.scope, "Planning");
  for (const key of ["commissionRate", "paidAmount", "cancellationPolicy"]) assert.equal(Object.hasOwn(result, key), false);
  assert.equal(Object.hasOwn(result.fullService, "commissionBase"), false);
});
test("all text-field boundaries agree with backend DTO limits", () => {
  for (const section of SERVICE_DETAIL_SECTIONS) for (const field of section.fields) {
    assert.deepEqual(validateServiceDetails({ ...emptyServiceDetails(), [field.key]: "a".repeat(field.maxLength) }), {});
    assert.ok(validateServiceDetails({ ...emptyServiceDetails(), [field.key]: "a".repeat(field.maxLength + 1) })[`serviceDetails.${field.key}`]);
  }
  for (const field of FULL_SERVICE_FIELDS) {
    assert.deepEqual(validateServiceDetails({ ...emptyServiceDetails(), fullService: { ...emptyFullServiceDetails(), [field.key]: "a".repeat(4000) } }), {});
    assert.ok(validateServiceDetails({ ...emptyServiceDetails(), fullService: { ...emptyFullServiceDetails(), [field.key]: "a".repeat(4001) } })[`serviceDetails.fullService.${field.key}`]);
  }
});
test("portfolio and video accept only safe absolute HTTP(S), never credentials, scripts or encoded traversal", () => {
  for (const value of ["https://example.invalid/photo.jpg", "http://example.invalid:8080/portfolio?a=b", "https://www.youtube.com/watch?v=synthetic12"]) assert.equal(isServiceDetailsLink(value), true, value);
  for (const value of ["javascript:alert(1)", "data:text/html,hello", "//example.invalid/a", "/uploads/a.jpg", "https://user:password@example.invalid/a", "https://example.invalid/a/../secret", "https://example.invalid/%252e%252e/secret", "https://example.invalid/a\\b", "https://example.invalid/%0a", "https://example.invalid/a b", "https://example.invalid/%GG"]) {
    assert.equal(isServiceDetailsLink(value), false, value);
    assert.ok(validateServiceDetails({ ...emptyServiceDetails(), videoUrl: value })["serviceDetails.videoUrl"]);
    assert.ok(validateServiceDetails({ ...emptyServiceDetails(), portfolioUrls: [value] })["serviceDetails.portfolioUrls"]);
  }
});
test("encoded ordinary URL spaces remain valid without permitting raw whitespace or encoded controls", () => {
  for (const value of ["https://example.invalid/Wedding%20Studio/My%20Work.pdf", "https://example.invalid/video?title=Our%20Wedding"]) {
    assert.equal(isServiceDetailsLink(value), true);
    assert.deepEqual(validateServiceDetails({ ...emptyServiceDetails(), portfolioUrls: [value], videoUrl: value }), {});
    assert.equal(normalizeServiceDetails({ videoUrl: value }).videoUrl, value);
  }
  for (const value of ["https://example.invalid/Wedding Studio/file", "https://example.invalid/%0a", "https://example.invalid/%250d", "https://example.invalid/%255csecret", "https://example.invalid/%252e%252e/secret"])
    assert.equal(isServiceDetailsLink(value), false, value);
});
test("portfolio count and aggregate content are bounded before transport", () => {
  const urls = Array.from({ length: 10 }, (_, i) => `https://example.invalid/${i}.jpg`);
  assert.deepEqual(validateServiceDetails({ ...emptyServiceDetails(), portfolioUrls: urls }), {});
  assert.ok(validateServiceDetails({ ...emptyServiceDetails(), portfolioUrls: [...urls, "https://example.invalid/extra.jpg"] })["serviceDetails.portfolioUrls"]);
  const huge = emptyServiceDetails();
  for (const section of SERVICE_DETAIL_SECTIONS) for (const field of section.fields) huge[field.key] = "a".repeat(field.maxLength);
  huge.fullService = emptyFullServiceDetails();
  for (const field of FULL_SERVICE_FIELDS) huge.fullService[field.key] = "a".repeat(4000);
  assert.ok(validateServiceDetails(huge).serviceDetails);
});
test("price-only edit preserves full metadata and complete album; explicit metadata change replaces only that object", () => {
  const details = { ...emptyServiceDetails(), shortDescription: "Summary", includedItems: "Preparation", fullService: { ...emptyFullServiceDetails(), scope: "Coordination" } };
  const detail = { id: "owned", title: "Original", minPrice: 1000, maxPrice: 2000, description: "Full description", location: "Hà Nội", categoryId: "planning", serviceDetails: details, categoryRequiresCommissionBasisApproval: true, cancellationPolicy: "Original cancellation agreement", media: [{ id: "image", mediaUrl: "https://example.invalid/photo.jpg", mediaType: "image", isFeatured: true, displayOrder: 0 }] };
  const editor = hydrateListingEditor(detail, "owned", "");
  assert.equal(editor.categoryRequiresCommissionBasisApproval, true);
  assert.equal(editor.cancellationPolicy, detail.cancellationPolicy);
  const unchanged = listingUpdatePayload({ ...editor.form, minPrice: "1100" }, editor.images, editor.images, editor.serviceDetails, details);
  assert.equal(Object.hasOwn(unchanged, "serviceDetails"), false);
  assert.equal(Object.hasOwn(unchanged, "imageUrls"), false);
  const changed = listingUpdatePayload(editor.form, editor.images, editor.images, { ...editor.serviceDetails, notes: "New note" }, details);
  assert.equal(changed.serviceDetails.notes, "New note");
  assert.equal(changed.serviceDetails.fullService.scope, "Coordination");
  assert.equal(Object.hasOwn(changed, "cancellationPolicy"), false);
  assert.equal(detail.serviceDetails.notes, "", "must not mutate query cache");
  assert.deepEqual(serviceDetailsUpdate(emptyServiceDetails(), details), emptyServiceDetails(), "explicit replacement can clear metadata without clearing other listing fields");
});
