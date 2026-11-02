import { test } from "node:test";
import assert from "node:assert/strict";
import { listingPricePresentation } from "../src/features/customer/listingPriceModel.ts";

test("retail card and detail prefer the same canonical price and readable unit over a legacy range", () => {
  const legacyRange = { minPrice: 1000000, maxPrice: 9000000 };
  const card = listingPricePresentation({ ...legacyRange, basePrice: 5000000, priceUnit: "Package" });
  const detail = listingPricePresentation({ ...legacyRange, pricing: { basePrice: 5000000, unit: "Package" } });
  assert.deepEqual(card, detail);
  assert.equal(detail.amount, "5.000.000 VNĐ"); assert.equal(detail.unit, "gói"); assert.equal(detail.exact, true);
});
test("all supported service units and a valid zero price retain their exact meaning", () => {
  for (const [unit, label] of [["Package", "gói"], ["Table", "bàn"], ["Guest", "khách"], ["Hour", "giờ"]]) {
    const price = listingPricePresentation({ minPrice: 1, maxPrice: 2, basePrice: 0, priceUnit: unit });
    assert.equal(price.unit, label); assert.equal(price.amount, "0 VNĐ"); assert.equal(price.exact, true);
  }
});
test("legacy records and invalid canonical payloads keep their original declared range", () => {
  for (const extra of [{}, { basePrice: NaN, priceUnit: "Package" }, { basePrice: -1, priceUnit: "Hour" }, { basePrice: 5, priceUnit: "Unknown" }]) {
    const price = listingPricePresentation({ minPrice: 1000000, maxPrice: 2000000, ...extra });
    assert.equal(price.amount, "1.000.000 – 2.000.000 VNĐ"); assert.equal(price.unit, null); assert.equal(price.exact, false);
  }
  assert.equal(listingPricePresentation({ minPrice: 1000, maxPrice: 1000 }).amount, "1.000 VNĐ");
});
