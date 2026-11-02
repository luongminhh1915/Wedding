import { test } from "node:test";
import assert from "node:assert/strict";
import { comboEditorPayload, comboEditorValues, fixedComboTotals, isFixedComboService, newComboEditor } from "../src/features/journey/combo/comboTemplateEditorModel.ts";
import { vendorComboPage } from "../src/features/journey/vendor/vendorComboListModel.ts";

const item = (id, extra = {}) => ({ id, listingId: `listing-${id}`, categoryId: "photo", vendorId: "shop", vendorName: "Shop", name: id, basePrice: 5000000, unit: "Package", moderationStatus: "Approved", isActive: true, isCanonical: true, listingStatus: "Active", listingIsActive: true, variants: [], addOns: [], ...extra });
const catalog = [item("one"), item("two", { basePrice: 2000000 }), item("three", { categoryId: "dress", basePrice: 3000000 })];
const editor = () => ({ ...newComboEditor("shop"), name: "Gói cưới", comboPrice: 8500000, items: catalog.map(item => ({ categoryId: item.categoryId, listingId: item.listingId, serviceItemId: item.id, isOptional: false, allowedItemIds: [] })) });

test("fixed combos use multiple services in the same category and derive identity from source", () => {
  const value = editor(); value.items[0].listingId = "stale";
  const payload = comboEditorPayload(value, catalog, true);
  assert.equal(payload.pricingMode, "FixedPrice"); assert.equal(payload.comboPrice, 8500000);
  assert.equal(payload.items.length, 3); assert.equal(payload.items[0].listingId, "listing-one");
  for (const key of ["originalPrice", "savingsAmount", "components", "commissionRate", "ownerVendorId"]) assert.equal(Object.hasOwn(payload, key), false);
});

test("fixed combo eligibility requires owner, source and facet approval/activity and canonical package price", () => {
  assert.equal(isFixedComboService(catalog[0], "shop"), true);
  for (const change of [{ vendorId: "foreign" }, { moderationStatus: "Pending" }, { isActive: false }, { isCanonical: false }, { isCanonical: undefined }, { listingStatus: "Draft" }, { listingIsActive: false }, { unit: "Table" }, { unit: "Guest" }, { unit: "Hour" }]) {
    assert.equal(isFixedComboService(item("one", change), "shop"), false);
    assert.throws(() => comboEditorPayload(editor(), [item("one", change), ...catalog.slice(1)], false), /chưa duyệt/);
  }
});

test("fixed combos reject too few, duplicate facet or duplicate source and optional/replacement items", () => {
  assert.throws(() => comboEditorPayload({ ...editor(), items: editor().items.slice(0, 1) }, catalog, true), /ít nhất 2/);
  assert.throws(() => comboEditorPayload({ ...editor(), items: [editor().items[0], editor().items[0]] }, catalog, true), /trùng/);
  const sameSource = [catalog[0], { ...catalog[1], listingId: catalog[0].listingId }, catalog[2]];
  assert.throws(() => comboEditorPayload(editor(), sameSource, true), /trùng/);
  for (const change of [{ isOptional: true }, { allowedItemIds: ["two"] }]) {
    const value = editor(); value.items[0] = { ...value.items[0], ...change };
    assert.throws(() => comboEditorPayload(value, catalog, true), /cố định/);
  }
  for (const comboPrice of [null, NaN, Infinity, -1, 0, 1.5]) assert.throws(() => comboEditorPayload({ ...editor(), comboPrice }, catalog, false), /giá bán combo/);
});

test("retail, fixed price and savings use real values; premiums and zero retail show no savings", () => {
  assert.deepEqual(fixedComboTotals(catalog, 8500000), { originalPrice: 10000000, comboPrice: 8500000, savingsAmount: 1500000, savingsPercentage: 15 });
  for (const price of [10000000, 12000000, null]) assert.equal(fixedComboTotals(catalog, price).savingsAmount, 0);
  assert.equal(fixedComboTotals([], 100).savingsPercentage, 0);
});

test("existing legacy combos keep dynamic mode while fixed pending proposal restores typed price", () => {
  const legacy = { ...editor(), id: "legacy", pricingMode: undefined, moderationStatus: "Approved" };
  assert.equal(comboEditorValues(legacy).pricingMode, "Dynamic");
  const fixed = { ...legacy, pricingMode: "FixedPrice", latestRevision: { status: "Pending", proposal: { ...editor(), comboPrice: 8000000 } } };
  const before = structuredClone(fixed);
  assert.equal(comboEditorValues(fixed).comboPrice, 8000000); assert.deepEqual(fixed, before);
  assert.equal(newComboEditor("shop").comboPrice, null);
});

test("vendor combo search/status/paging operate on real proposals and safely clamp removed pages", () => {
  const templates = Array.from({ length: 15 }, (_, index) => ({ ...editor(), id: String(index), name: `Combo ${index}`, moderationStatus: "Approved", isActive: index !== 0, region: "Huế", latestRevision: index === 1 ? { status: "Pending", proposal: { name: "Bản sửa" } } : null }));
  assert.equal(vendorComboPage(templates, "Huế", "", 3, 6).items.length, 3);
  assert.equal(vendorComboPage(templates, "Bản sửa", "Pending", 9, 6).page, 1);
  assert.equal(vendorComboPage(templates, "", "Inactive", 1, 6).totalCount, 1);
  assert.equal(vendorComboPage(templates, "absent", "", 5, 6).items.length, 0);
});
