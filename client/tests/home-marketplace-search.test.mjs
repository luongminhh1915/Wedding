import { test } from "node:test";
import assert from "node:assert/strict";
import { marketplaceSearchFilters } from "../src/features/home/marketplaceSearchModel.ts";
import { readServiceFilters, serviceFilterSearch } from "../src/features/customer/serviceFilterModel.ts";

const categoryId = "11111111-2222-4333-8444-555555555555";

test("homepage marketplace search opens the existing URL-backed service filters", () => {
  const filters = marketplaceSearchFilters({ categoryId, keyword: "  chụp ảnh cưới  ", location: " Hà Nội ", maxPrice: "30000000" });
  const restored = readServiceFilters(serviceFilterSearch(filters));

  assert.equal(restored.categoryId, categoryId);
  assert.equal(restored.keyword, "chụp ảnh cưới");
  assert.equal(restored.location, "Hà Nội");
  assert.equal(restored.maxPrice, 30_000_000);
  assert.equal(restored.sortBy, "popular");
  assert.equal(restored.page, 1);
  assert.equal(restored.pageSize, 12);
});

test("homepage search bounds text and ignores a budget outside its visible options", () => {
  const filters = marketplaceSearchFilters({ categoryId: "", keyword: ` ${"x".repeat(200)} `, location: ` ${"y".repeat(120)} `, maxPrice: "99999999" });

  assert.equal(filters.categoryId, undefined);
  assert.equal(filters.keyword?.length, 150);
  assert.equal(filters.location?.length, 100);
  assert.equal(filters.maxPrice, undefined);
});
