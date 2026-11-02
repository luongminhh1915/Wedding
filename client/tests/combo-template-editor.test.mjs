import { test } from "node:test";
import assert from "node:assert/strict";
import {
  canPreviewCombo,
  comboDate,
  comboEditorPayload,
  comboEditorValues,
  comboPreviewPath,
  comboReviewPayload,
  emptyComboContent,
  isComboImage,
  newComboEditor,
} from "../src/features/journey/combo/comboTemplateEditorModel.ts";

const uploaded = "/uploads/listings/0123456789abcdefABCDEF0123456789.webp";
const item = (id, extra = {}) => ({
  id, listingId: `listing-${id}`, categoryId: "photo", vendorId: "shop",
  vendorName: "Shop", name: id, basePrice: 1234567, unit: "Package",
  moderationStatus: "Approved", isActive: true, variants: [], addOns: [], ...extra,
});
const catalog = () => [item("photo-1"), item("photo-2"), item("venue-1", { categoryId: "venue" })];
const editor = () => ({
  ...newComboEditor("shop"), pricingMode: "Dynamic", name: "  Combo của Shop  ",
  items: [{ categoryId: "photo", serviceItemId: "photo-1", listingId: "stale-listing", isOptional: false, allowedItemIds: ["photo-2"] }],
});
const template = () => ({
  ...editor(), id: "combo", name: "Published", estimatedTotal: 1234567,
  moderationStatus: "Approved", content: { ...emptyComboContent(), terms: "Published terms", galleryUrls: ["https://shop.example/published.jpg"] },
  latestRevision: { id: "revision", sequence: 2, status: "Pending", createdAt: "2026-10-19T10:00:00Z", proposal: {
    ...editor(), name: "Proposed", imageUrl: uploaded,
    content: { ...emptyComboContent(), terms: "Proposed terms", galleryUrls: [uploaded] },
  } },
});

test("new combo editor has independent content and no invented service, vendor, price or commission", () => {
  const first = newComboEditor("shop"), second = newComboEditor("other");
  first.content.galleryUrls.push(uploaded);
  assert.deepEqual(second.content.galleryUrls, []);
  assert.deepEqual(first.items, []);
  assert.equal(first.ownerVendorId, "shop");
  for (const field of ["fixedPrice", "commissionRate", "commissionBase", "paidAmount"]) assert.equal(Object.hasOwn(first, field), false);
});

test("draft, pending and rejected editors hydrate proposal without mutating published baseline or query cache", () => {
  for (const status of ["Draft", "Pending", "Rejected"]) {
    const data = template(); data.latestRevision.status = status;
    const before = structuredClone(data), value = comboEditorValues(data);
    assert.equal(value.name, "Proposed"); assert.equal(value.content.terms, "Proposed terms");
    assert.equal(value.id, data.id);
    value.items[0].allowedItemIds.push("changed"); value.content.galleryUrls.push("https://shop.example/new.jpg");
    assert.deepEqual(data, before);
  }
});

test("approved or absent revisions edit published baseline; legacy missing content is empty, not fabricated", () => {
  const data = template(); data.latestRevision.status = "Approved";
  assert.equal(comboEditorValues(data).name, "Published");
  assert.equal(comboEditorValues(data).content.terms, "Published terms");
  delete data.latestRevision; delete data.content;
  assert.deepEqual(comboEditorValues(data).content, emptyComboContent());
});

test("unavailable old component references are retained for explicit correction rather than silently dropped", () => {
  const data = template(); data.latestRevision.proposal.items[0].serviceItemId = "missing";
  data.latestRevision.proposal.items[0].allowedItemIds.push("old-hidden");
  const value = comboEditorValues(data);
  assert.equal(value.items[0].serviceItemId, "missing");
  assert.ok(value.items[0].allowedItemIds.includes("old-hidden"));
  assert.throws(() => comboEditorPayload(value, catalog(), false), /chưa duyệt/);
});

test("save explicitly distinguishes draft and submitted revisions, derives listing identity from actual service", () => {
  const value = editor();
  const before = structuredClone(value);
  const draft = comboEditorPayload(value, catalog(), false);
  const pending = comboEditorPayload(value, catalog(), true);
  assert.equal(draft.submitForApproval, false); assert.equal(pending.submitForApproval, true);
  assert.equal(draft.items[0].listingId, "listing-photo-1");
  assert.equal(draft.items[0].serviceItemId, "photo-1");
  assert.equal(draft.name, "Combo của Shop");
  assert.deepEqual(value, before);
});

test("payload whitelist preserves existing discount but excludes trusted owner, revision and financial snapshots", () => {
  const value = { ...editor(), singleVendorDiscount: .075, ownerVendorId: "shop", commissionRate: .01, estimatedTotal: 99, latestRevision: { id: "forged" }, paidAmount: 100 };
  const result = comboEditorPayload(value, catalog(), true);
  assert.equal(result.singleVendorDiscount, .075);
  for (const field of ["ownerVendorId", "commissionRate", "estimatedTotal", "latestRevision", "paidAmount"]) assert.equal(Object.hasOwn(result, field), false, field);
  assert.equal(Object.hasOwn(result, "price"), false);
});

test("default and alternate services must be active, approved, same category and one vendor", () => {
  for (const replacement of [item("photo-1", { vendorId: "foreign" }), item("photo-1", { moderationStatus: "Pending" }), item("photo-1", { isActive: false }), item("photo-1", { categoryId: "venue" })])
    assert.throws(() => comboEditorPayload(editor(), [replacement, item("photo-2")], true), /chưa duyệt/);
  for (const extra of [item("photo-2", { vendorId: "foreign" }), item("photo-2", { isActive: false }), item("photo-2", { moderationStatus: "Draft" }), item("photo-2", { categoryId: "venue" })])
    assert.throws(() => comboEditorPayload(editor(), [item("photo-1"), extra], true), /chưa duyệt/);
  assert.throws(() => comboEditorPayload({ ...editor(), ownerVendorId: "" }, catalog(), true));
});

test("combo cardinality is one to twelve unique categories, allowed choices remain bounded and deduplicated", () => {
  assert.throws(() => comboEditorPayload({ ...editor(), items: [] }, catalog(), false));
  assert.throws(() => comboEditorPayload({ ...editor(), items: [...editor().items, ...editor().items] }, catalog(), true));
  const value = editor(); value.items[0].allowedItemIds = ["photo-2", "photo-2"];
  assert.deepEqual(comboEditorPayload(value, catalog(), false).items[0].allowedItemIds, ["photo-2"]);
  value.items[0].allowedItemIds = Array(101).fill("photo-2");
  assert.throws(() => comboEditorPayload(value, catalog(), false), /100/);
  const many = Array.from({ length: 13 }, (_, index) => item(`item-${index}`, { categoryId: `cat-${index}` }));
  assert.throws(() => comboEditorPayload({ ...editor(), items: many.map(item => ({ categoryId: item.categoryId, serviceItemId: item.id, isOptional: false })) }, many, false));
});

test("cover and gallery accept only safe HTTP(S) or exact server-generated image references", () => {
  for (const reference of [uploaded, "https://shop.example/photo.jpg", "https://shop.example/Wedding%20Gallery/photo.jpg", "http://shop.example:8080/a.jpg"]) assert.equal(isComboImage(reference), true, reference);
  for (const reference of ["javascript:alert(1)", "data:image/png;base64,abc", "//shop.example/a.jpg", "https://user:pass@shop.example/a.jpg", "https://shop.example/%252e%252e/secret", "https://shop.example/a\\b", "https://shop.example/%0a", "/uploads/listings/../secret.jpg", "/uploads/listings/not-a-uuid.jpg", "/uploads/listings/0123456789abcdefABCDEF0123456789.svg", uploaded.replace("uploads", "UPLOADS"), uploaded.replace(".webp", ".WEBP")]) assert.equal(isComboImage(reference), false, reference);
  const value = editor(); value.imageUrl = uploaded; value.content.galleryUrls = [uploaded, " https://shop.example/another.jpg ", ""];
  const result = comboEditorPayload(value, catalog(), false);
  assert.deepEqual(result.content.galleryUrls, [uploaded, "https://shop.example/another.jpg"]);
  assert.equal(result.imageUrl, uploaded);
});

test("metadata limits and validity are checked before saving without evaluating money or commission terms", () => {
  const value = editor(); value.content.terms = "Mô tả thanh toán, không tự thu tiền";
  value.content.validFromUtc = "2027-01-01T00:00:00Z"; value.content.validUntilUtc = "2027-02-01T00:00:00Z";
  assert.deepEqual(comboEditorPayload(value, catalog(), true).content, value.content);
  assert.throws(() => comboEditorPayload({ ...value, content: { ...value.content, terms: "a".repeat(4001) } }, catalog(), false), /4000/);
  assert.throws(() => comboEditorPayload({ ...value, content: { ...value.content, galleryUrls: Array(11).fill(uploaded) } }, catalog(), false), /10/);
  for (const [from, until] of [["invalid", null], [null, "invalid"], ["2027-02-01T00:00:00Z", "2027-01-01T00:00:00Z"], ["2027-01-01T00:00:00Z", "2027-01-01T00:00:00Z"]])
    assert.throws(() => comboEditorPayload({ ...value, content: { ...value.content, validFromUtc: from, validUntilUtc: until } }, catalog(), false), /Thời hạn/);
});

test("client validates existing descriptive and discount boundaries, with server still authoritative", () => {
  for (const change of [{ name: "" }, { name: "a".repeat(151) }, { style: "" }, { style: "a".repeat(51) }, { tier: "Unknown" }, { description: "a".repeat(4001) }, { region: "a".repeat(1001) }, { sortOrder: 1.5 }]) assert.throws(() => comboEditorPayload({ ...editor(), ...change }, catalog(), true));
  for (const discount of [-.01, .31, NaN, Infinity]) assert.throws(() => comboEditorPayload({ ...editor(), singleVendorDiscount: discount }, catalog(), true));
  assert.equal(comboEditorPayload({ ...editor(), singleVendorDiscount: .30 }, catalog(), true).singleVendorDiscount, .30);
});

test("private preview requires explicit role and query flag and encodes independent revision identity", () => {
  for (const role of ["VendorOwner", "Moderator", "SuperAdmin"]) {
    assert.equal(canPreviewCombo(role, "vendor"), true);
    assert.equal(canPreviewCombo(role, null), false);
    assert.equal(canPreviewCombo(role, "true"), false);
  }
  for (const role of [null, undefined, "Customer", "Finance", "Support"]) assert.equal(canPreviewCombo(role, "vendor"), false);
  assert.equal(comboPreviewPath("combo/id", "revision&id"), "/combo/combo%2Fid?preview=vendor&revision=revision%26id");
  assert.equal(comboPreviewPath("combo"), "/combo/combo?preview=vendor");
});

test("review payload targets latest pending revision and requires a reason without accepting arbitrary snapshot fields", () => {
  const data = template();
  assert.deepEqual(comboReviewPayload(data, true, "  Reviewed  "), { approve: true, note: "Reviewed", revisionId: "revision" });
  assert.deepEqual(comboReviewPayload(data, false, "  Cần bổ sung ảnh  "), { approve: false, note: "Cần bổ sung ảnh", revisionId: "revision" });
  assert.throws(() => comboReviewPayload(data, false, "  "), /Lý do/);
  assert.throws(() => comboReviewPayload(data, true, "a".repeat(2001)), /2000/);
  data.latestRevision = { ...data.latestRevision, id: "new-revision" };
  assert.equal(comboReviewPayload(data, true, "").revisionId, "new-revision");
  for (const status of ["Draft", "Rejected", "Approved", "Superseded"]) {
    data.latestRevision.status = status;
    assert.throws(() => comboReviewPayload(data, true, ""), /chờ duyệt/);
  }
  data.latestRevision = null; data.moderationStatus = "Pending";
  assert.deepEqual(comboReviewPayload(data, false, "Legacy reason"), { approve: false, note: "Legacy reason" });
});

test("UTC timestamps with or without SQL kind and explicit offsets display consistently in Vietnam", () => {
  assert.equal(comboDate("2026-10-19T10:00:00"), comboDate("2026-10-19T10:00:00Z"));
  assert.equal(comboDate("2026-10-19T17:00:00+07:00"), comboDate("2026-10-19T10:00:00Z"));
  assert.equal(comboDate("invalid"), "Chưa xác định");
  assert.equal(comboDate(null), "Chưa xác định");
});
