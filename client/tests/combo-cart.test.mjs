import { describe, it } from "node:test";
import assert from "node:assert/strict";
import {
  EMPTY_CART,
  estimateLine,
  quantityFor,
  removeService,
  restoreSelection,
  selectService,
  startVendorCart,
} from "../src/features/journey/combo/cartModel.ts";
const selection = (categoryId, serviceItemId, extra = {}) => ({
  categoryId,
  serviceItemId,
  variantIds: [],
  addOnIds: [],
  hours: 1,
  ...extra,
});
describe("Customer combo cart", () => {
  it("restores upgrades and add-ons from both current and legacy saved drafts", () => {
    for (const stored of [
      { variantIds: ["flycam"], addOnIds: ["photobooth"], hours: 3 },
      { VariantIds: ["flycam"], AddOnIds: ["photobooth"], Hours: 3 },
    ]) {
      assert.deepEqual(
        restoreSelection({
          serviceItemId: "photo-1",
          categoryId: "photo",
          selectionJson: JSON.stringify(stored),
        }),
        selection("photo", "photo-1", {
          variantIds: ["flycam"],
          addOnIds: ["photobooth"],
          hours: 3,
        }),
      );
    }
  });
  it("safely restores legacy rows without valid option JSON", () => {
    for (const selectionJson of ["bad", "null", "[]", '{"variantIds":{}}']) {
      assert.deepEqual(
        restoreSelection({
          serviceItemId: "p1",
          categoryId: "photo",
          selectionJson,
        }),
        selection("photo", "p1"),
      );
    }
  });
  it("replaces an existing category without duplicate costs and preserves other categories", () => {
    const first = selectService(
      EMPTY_CART,
      selection("photo", "p1"),
      "vendor-a",
    );
    const second = selectService(first, selection("venue", "v1"), "vendor-a");
    const replaced = selectService(
      second,
      selection("photo", "p2"),
      "vendor-a",
    );
    assert.equal(replaced.selections.length, 2);
    assert.ok(replaced.selections.some((s) => s.serviceItemId === "v1"));
    assert.ok(!replaced.selections.some((s) => s.serviceItemId === "p1"));
    assert.equal(first.selections[0].serviceItemId, "p1");
    assert.equal(EMPTY_CART.selections.length, 0);
  });
  it("rejects adding a different provider to a provider-owned preset", () => {
    const preset = {
      ...EMPTY_CART,
      templateId: "t1",
      templateName: "All in one",
      templateVendorId: "vendor-a",
    };
    const same = selectService(preset, selection("photo", "p1"), "vendor-a");
    assert.equal(same.templateId, "t1");
    assert.throws(
      () => selectService(same, selection("photo", "p2"), "vendor-b"),
      /một nhà cung cấp/,
    );
    assert.equal(same.templateId, "t1");
    assert.equal(same.selections[0].serviceItemId, "p1");
  });
  it("binds a custom cart to its first provider and does not mix later categories", () => {
    const cart = selectService(
      EMPTY_CART,
      selection("photo", "p1"),
      "vendor-a",
    );
    assert.equal(cart.vendorId, "vendor-a");
    assert.throws(
      () => selectService(cart, selection("venue", "v1"), "vendor-b"),
      /một nhà cung cấp/,
    );
  });
  it("starts a new provider cart without carrying services, a draft ID or preset discounts", () => {
    const cart = {
      ...EMPTY_CART,
      region: "Hà Nội",
      weddingDate: "2027-01-20",
      guestCount: 203,
      vendorId: "vendor-a",
      templateId: "old",
      templateName: "Old",
      templateVendorId: "vendor-a",
      draftId: "saved",
      selections: [selection("photo", "p1")],
    };
    const next = startVendorCart(cart, "vendor-b");
    assert.equal(next.vendorId, "vendor-b");
    assert.equal(next.selections.length, 0);
    assert.equal(next.draftId, undefined);
    assert.equal(next.templateId, null);
    assert.equal(next.guestCount, 203);
    assert.equal(next.region, "Hà Nội");
    assert.equal(cart.selections.length, 1);
  });
  it("removes only the requested category without mutating the previous cart", () => {
    const previous = {
      ...EMPTY_CART,
      selections: [selection("photo", "p1"), selection("venue", "v1")],
    };
    assert.deepEqual(
      removeService(previous, "photo").selections.map((s) => s.serviceItemId),
      ["v1"],
    );
    assert.equal(previous.selections.length, 2);
  });
  it("rounds table quantities up and bounds hourly bookings", () => {
    assert.equal(quantityFor("Table", 201, 1), 21);
    assert.equal(quantityFor("Guest", 201, 1), 201);
    assert.equal(quantityFor("Package", 201, 1), 1);
    assert.equal(quantityFor("Hour", 201, 100), 24);
    assert.equal(quantityFor("Hour", 201, 0), 1);
  });
  it("calculates preview quantity, variant and per-unit/fixed add-ons without claiming discounts", () => {
    const item = {
      price: 1000000,
      unit: "Table",
      variants: [{ id: "premium", priceDelta: 100000 }],
      addOns: [
        { id: "gifts", price: 20000, perUnit: true },
        { id: "photo", price: 500000, perUnit: false },
      ],
    };
    const line = selection("venue", "v1", {
      variantIds: ["premium"],
      addOnIds: ["gifts", "photo"],
    });
    assert.equal(estimateLine(item, line, 201), 24020000);
  });
});
