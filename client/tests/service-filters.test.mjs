import { test } from "node:test";
import assert from "node:assert/strict";
import { clearServiceFilterSearch, readServiceFilters, serviceBrowseReturn, serviceCategoryLabel, serviceFilterChips, serviceFilterSearch, SERVICE_PRICE_LIMIT } from "../src/features/customer/serviceFilterModel.ts";

const categoryId = "11111111-1111-1111-1111-111111111103";
const categories = [{ id: categoryId, name: "Quay phim & Chụp ảnh (Photo / Video)", slug: "quay-chup-photo-video", listingCount: 2, icon: "Camera" }];

test("service filters persist category AND keyword, location, price, shop rating, sort and page", () => {
  const filter = { categoryId, keyword: "studio hạnh phúc", location: "Hà Nội", minPrice: 10_000_000, maxPrice: 30_000_000, minRating: 4.5, sortBy: "price_asc", page: 3, pageSize: 24 };
  assert.deepEqual(readServiceFilters(serviceFilterSearch(filter)), filter);
});
test("default browse is canonical and clears optional fields", () => {
  const defaults = readServiceFilters("");
  assert.equal(defaults.sortBy, "newest"); assert.equal(defaults.page, 1); assert.equal(defaults.pageSize, 12);
  assert.equal(serviceFilterSearch(defaults), "");
});
test("numeric URL bounds reject Infinity, exponent notation, negatives, fractional pages and unsafe amounts", () => {
  for (const value of ["Infinity", "NaN", "1e10", "-1", "0x123", "9007199254740993", "100000000001", "1.5"]) {
    const filter = readServiceFilters(`?minPrice=${value}&maxPrice=${value}`);
    assert.equal(filter.minPrice, undefined); assert.equal(filter.maxPrice, undefined);
  }
  const filter = readServiceFilters("?page=1.5&pageSize=999&minRating=6&sortBy=untrusted");
  assert.equal(filter.page, 1); assert.equal(filter.pageSize, 12); assert.equal(filter.minRating, undefined); assert.equal(filter.sortBy, "newest");
});
test("zero-valued money is valid and inverted range does not reach API", () => {
  assert.equal(readServiceFilters("?minPrice=0&maxPrice=0").maxPrice, 0);
  const invalid = readServiceFilters("?minPrice=30000000&maxPrice=10000000");
  assert.equal(invalid.minPrice, undefined); assert.equal(invalid.maxPrice, undefined);
  assert.equal(readServiceFilters(`?maxPrice=${SERVICE_PRICE_LIMIT}`).maxPrice, SERVICE_PRICE_LIMIT);
});
test("text is bounded, whitespace trimmed and category IDs are validated", () => {
  const filter = readServiceFilters(`?keyword=${"x".repeat(200)}&location=${"y".repeat(200)}&categoryId=arbitrary`);
  assert.equal(filter.keyword.length, 150); assert.equal(filter.location.length, 100); assert.equal(filter.categoryId, undefined);
  assert.equal(readServiceFilters("?keyword=%20hoa%20c%C6%B0%E1%BB%9Bi%20").keyword, "hoa cưới");
});
test("query updates preserve unrelated navigation values and remove stale duplicates", () => {
  const filter = { ...readServiceFilters(`?categoryId=${categoryId}&keyword=old&page=4`), keyword: "new", page: 1 };
  const query = serviceFilterSearch(filter, "?keyword=old&keyword=duplicate&page=4&campaign=wedding");
  const params = new URLSearchParams(query);
  assert.deepEqual(params.getAll("keyword"), ["new"]); assert.equal(params.get("campaign"), "wedding"); assert.equal(params.has("page"), false);
});
test("reset removes every filter including category and rating without clearing unrelated navigation", () => {
  assert.equal(clearServiceFilterSearch(`?categoryId=${categoryId}&keyword=photo&location=HCM&minPrice=0&maxPrice=2&minRating=4.5&page=3&pageSize=24&sortBy=popular&campaign=wedding`), "?campaign=wedding");
});
test("chips describe actual applied AND constraints and can clear zero prices", () => {
  const filter = { categoryId, keyword: "photo", location: "HCM", minPrice: 0, maxPrice: 10000000, minRating: 4.5 };
  const chips = serviceFilterChips(filter, categories);
  assert.equal(chips.length, 5); assert.equal(chips[0].label, "Quay phim & chụp ảnh");
  assert.equal(chips.find(chip => chip.key === "minRating").label, "Shop từ 4.5 sao");
  assert.deepEqual(chips.find(chip => chip.key === "price").clear, { minPrice: undefined, maxPrice: undefined });
});
test("the eight short display labels do not replace API identities or invent categories", () => {
  assert.equal(serviceCategoryLabel({ slug: "wedding-planner", name: "Full name" }), "Tổ chức cưới A–Z");
  assert.equal(serviceCategoryLabel({ slug: "future-db-category", name: "Tên thật từ máy chủ" }), "Tên thật từ máy chủ");
  assert.equal(categories[0].id, categoryId);
});
test("detail return route keeps valid filters but never redirects outside local browse", () => {
  const safe = `/browse${serviceFilterSearch({ categoryId, keyword: "ảnh cưới", minRating: 4.5, page: 2 })}`;
  assert.equal(serviceBrowseReturn(safe), safe);
  for (const unsafe of ["https://evil.test", "//evil.test", "/browse/other", "/browser?x=1", "/browse#fragment", "/browse?x=\\evil", null, {}, "/browse" + "x".repeat(4000)]) assert.equal(serviceBrowseReturn(unsafe), "/browse");
});
test("Back/reload parsed URL reconstructs previous applied filter instead of defaulting", () => {
  const first = serviceFilterSearch({ categoryId, keyword: "hoa", sortBy: "popular", page: 3 });
  const next = serviceFilterSearch({ ...readServiceFilters(first), keyword: "ảnh", page: 1 });
  assert.equal(readServiceFilters(next).page, 1);
  assert.equal(readServiceFilters(first).keyword, "hoa"); assert.equal(readServiceFilters(first).page, 3);
});
