import { test } from "node:test";
import assert from "node:assert/strict";
import { listingPricingPayload, listingPricingValues } from "../src/features/vendor-portal/components/listingPricingModel.ts";
import { changeVendorListingFilters, vendorListingFilters } from "../src/features/vendor-portal/vendorListingFilters.ts";

test("new pricing requires explicit retail price and region without guessing from a listing range", () => {
  const value = listingPricingValues();
  assert.equal(value.basePrice, null); assert.equal(value.regions, "");
  assert.throws(() => listingPricingPayload(value), /giá bán lẻ/);
  assert.throws(() => listingPricingPayload({ ...value, basePrice: 5000000 }), /khu vực/);
});
test("service pricing editing retains selected legacy identity and child IDs without mutating input", () => {
  const source = { serviceItemId: "legacy-choice", basePrice: 5000000, unit: "Package", minGuests: 1, maxGuests: 500, regions: " Huế ", maxBookingsPerDay: 2, variants: [{ id: "old-variant", name: " Deluxe ", priceDelta: 1000000 }], addOns: [{ id: "old-addon", name: " Album ", price: 200000, perUnit: false }] };
  const before = structuredClone(source), value = listingPricingValues(source), payload = listingPricingPayload(value);
  assert.equal(payload.serviceItemId, "legacy-choice"); assert.equal(payload.variants[0].id, "old-variant"); assert.equal(payload.addOns[0].id, "old-addon");
  assert.equal(payload.regions, "Huế"); assert.deepEqual(source, before);
  value.variants[0].name = "changed"; assert.equal(source.variants[0].name, " Deluxe ");
});
test("category filter resets page and preserves vendor navigation", () => {
  const params = new URLSearchParams({ tab: "listings", servicePage: "3" });
  const next = changeVendorListingFilters(params, { categoryId: "photo" });
  assert.equal(vendorListingFilters(next).categoryId, "photo"); assert.equal(vendorListingFilters(next).page, 1); assert.equal(next.get("tab"), "listings");
  assert.equal(vendorListingFilters(changeVendorListingFilters(next, { categoryId: undefined })).categoryId, undefined);
});
