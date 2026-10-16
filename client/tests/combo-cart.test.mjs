import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { EMPTY_CART, estimateLine, quantityFor, removeService, selectService } from "../src/features/journey/combo/cartModel.ts";
const selection = (categoryId, serviceItemId, extra = {}) => ({ categoryId, serviceItemId, variantIds: [], addOnIds: [], hours: 1, ...extra });
describe("Customer combo cart", () => {
  it("replaces an existing category without duplicate costs and preserves other categories", () => {
    const first = selectService(EMPTY_CART, selection("photo", "p1"), "vendor-a");
    const second = selectService(first, selection("venue", "v1"), "vendor-b");
    const replaced = selectService(second, selection("photo", "p2"), "vendor-c");
    assert.equal(replaced.selections.length, 2); assert.ok(replaced.selections.some((s) => s.serviceItemId === "v1")); assert.ok(!replaced.selections.some((s) => s.serviceItemId === "p1"));
    assert.equal(first.selections[0].serviceItemId, "p1"); assert.equal(EMPTY_CART.selections.length, 0);
  });
  it("drops the single-vendor preset reference when switching to a specialist", () => {
    const preset = { ...EMPTY_CART, templateId: "t1", templateName: "All in one", templateVendorId: "vendor-a" };
    const same = selectService(preset, selection("photo", "p1"), "vendor-a"); assert.equal(same.templateId, "t1");
    const mixed = selectService(same, selection("photo", "p2"), "vendor-b");
    assert.equal(mixed.templateId, null); assert.equal(mixed.templateVendorId, null); assert.equal(mixed.templateName, "");
  });
  it("retains mixed-vendor preset metadata for ordinary catalog swaps", () => {
    const preset = { ...EMPTY_CART, templateId: "mixed", templateVendorId: null };
    assert.equal(selectService(preset, selection("photo", "p2"), "vendor-b").templateId, "mixed");
  });
  it("removes only the requested category without mutating the previous cart", () => {
    const previous = { ...EMPTY_CART, selections: [selection("photo", "p1"), selection("venue", "v1")] };
    assert.deepEqual(removeService(previous, "photo").selections.map((s) => s.serviceItemId), ["v1"]); assert.equal(previous.selections.length, 2);
  });
  it("rounds table quantities up and bounds hourly bookings", () => {
    assert.equal(quantityFor("Table", 201, 1), 21); assert.equal(quantityFor("Guest", 201, 1), 201); assert.equal(quantityFor("Package", 201, 1), 1);
    assert.equal(quantityFor("Hour", 201, 100), 24); assert.equal(quantityFor("Hour", 201, 0), 1);
  });
  it("calculates preview quantity, variant and per-unit/fixed add-ons without claiming discounts", () => {
    const item = { price: 1000000, unit: "Table", variants: [{ id: "premium", priceDelta: 100000 }], addOns: [{ id: "gifts", price: 20000, perUnit: true }, { id: "photo", price: 500000, perUnit: false }] };
    const line = selection("venue", "v1", { variantIds: ["premium"], addOnIds: ["gifts", "photo"] });
    assert.equal(estimateLine(item, line, 201), 24020000);
  });
});
