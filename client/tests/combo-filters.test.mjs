import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { readComboFilters, comboFilterSearch, clearComboFilterSearch, comboCatalogReturn, comboFilterChips, COMBO_PRICE_LIMIT } from "../src/features/journey/combo/comboFilterModel.ts";
const category = "11111111-1111-1111-1111-111111111103";
const listing = "22222222-2222-2222-2222-222222222222";
const vendor = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
const categories = [{ categoryId: category, name: "Quay phim & Chụp ảnh", options: [] }];
const vendors = [{ id: vendor, name: "Shop thật", ratingAvg: 4.7, totalReviews: 8 }];

test("combo URL roundtrip preserves all server filters AND sort/page without touching cart", () => {
  const input = { category, listing, search: "ảnh cưới", sort: "savings", page: 4, minPrice: 0, maxPrice: 50_000_000, city: "Hà Nội", region: "Hồ Chí Minh", minServices: 2, maxServices: 5, minSavings: 1_000_000, minRating: 4.5, templateCategoryId: category, filterVendorId: vendor };
  assert.deepEqual(readComboFilters(comboFilterSearch(input)), input);
});
test("combo default and template view serialize canonically without page one", () => {
  assert.equal(comboFilterSearch(readComboFilters("")), "");
  assert.equal(comboFilterSearch({ sort: "recommended", page: 1, category: "templates" }), "");
});
test("malformed and oversized price/count/rating/page are rejected before API", () => {
  for (const bad of ["Infinity", "NaN", "1e7", "-1", "0x12", "9007199254740993", "1.5"]) {
    assert.equal(readComboFilters(`?minPrice=${bad}`).minPrice, undefined);
    assert.equal(readComboFilters(`?minSavings=${bad}`).minSavings, undefined);
  }
  const bad = readComboFilters("?page=2147483647&sort=DROP&minServices=13&maxServices=0&minRating=5.1");
  assert.equal(bad.page, 1); assert.equal(bad.sort, "recommended"); assert.equal(bad.minServices, undefined); assert.equal(bad.maxServices, undefined); assert.equal(bad.minRating, undefined);
});
test("money uses whole dong with zero valid and documented guest-quantity upper limit", () => {
  assert.equal(readComboFilters(`?maxPrice=${COMBO_PRICE_LIMIT}`).maxPrice, COMBO_PRICE_LIMIT);
  assert.equal(readComboFilters(`?maxPrice=${COMBO_PRICE_LIMIT + 1}`).maxPrice, undefined);
  assert.equal(readComboFilters("?minPrice=0&minSavings=0").minPrice, 0);
  assert.equal(readComboFilters("?minPrice=0&minSavings=0").minSavings, 0);
});
test("inverted price and service count cannot become contradictory backend requests", () => {
  const bad = readComboFilters("?minPrice=100&maxPrice=1&minServices=4&maxServices=2");
  assert.equal(bad.minPrice, undefined); assert.equal(bad.maxPrice, undefined);
  assert.equal(bad.minServices, undefined); assert.equal(bad.maxServices, undefined);
});
test("text boundaries and category IDs follow authoritative endpoint limits", () => {
  const value = readComboFilters(`?search=${"x".repeat(200)}&city=${"y".repeat(150)}&region=${"z".repeat(120)}&category=made-up&listing=/private&filterVendorId=${vendor.toUpperCase()}`);
  assert.equal(value.search.length, 150); assert.equal(value.city.length, 100); assert.equal(value.region.length, 100);
  assert.equal(value.category, undefined); assert.equal(value.listing, undefined); assert.equal(value.filterVendorId, vendor);
});
test("filter reset clears query constraints and keeps unrelated navigation parameters", () => {
  const input = comboFilterSearch({ category, listing, sort: "rating", page: 4, minPrice: 1, region: "HCM", minServices: 2, minSavings: 0, filterVendorId: vendor }, "?campaign=demo");
  assert.equal(clearComboFilterSearch(input), "?campaign=demo");
});
test("replacing URL filter removes duplicate query entries and retains valid other constraints", () => {
  const applied = readComboFilters(`?category=${category}&region=HCM&page=4`);
  const search = comboFilterSearch({ ...applied, search: "Studio", page: 1 }, "?search=old&search=duplicate&campaign=demo");
  const params = new URLSearchParams(search);
  assert.deepEqual(params.getAll("search"), ["Studio"]); assert.equal(params.get("region"), "HCM"); assert.equal(params.has("page"), false); assert.equal(params.get("campaign"), "demo");
});
test("chips distinguish shop rating, shop location, service area and actual included services", () => {
  const chips = comboFilterChips({ sort: "recommended", page: 1, filterVendorId: vendor, templateCategoryId: category, city: "HN", region: "HCM", minServices: 2, maxServices: 3, minRating: 4.5, minPrice: 0 }, categories, vendors);
  assert.equal(chips.find(c => c.key === "filterVendorId").label, "Shop: Shop thật");
  assert.equal(chips.find(c => c.key === "templateCategoryId").label, "Có: Quay phim & Chụp ảnh");
  assert.equal(chips.find(c => c.key === "city").label, "Địa điểm shop: HN");
  assert.equal(chips.find(c => c.key === "region").label, "Phục vụ: HCM");
  assert.equal(chips.find(c => c.key === "services").label, "2–3 dịch vụ có sẵn");
  assert.equal(chips.find(c => c.key === "minRating").label, "Shop từ 4,5 sao");
  assert.deepEqual(chips.find(c => c.key === "price").clear, { minPrice: undefined, maxPrice: undefined });
});
test("detail return preserves applied filters and rejects unsafe or noncatalog routes", () => {
  const valid = `/combo${comboFilterSearch({ category, sort: "price-asc", page: 3, search: "chụp ảnh", region: "HCM" })}`;
  assert.equal(comboCatalogReturn(valid), valid);
  for (const bad of ["https://evil.test", "//evil.test", "/combos", "/combo/anything", "/combo?x=\\evil", "/combo#bad", {}, null, "/combo?" + "x".repeat(5000)]) assert.equal(comboCatalogReturn(bad), "/combo");
});
test("Back/reload reconstructs prior combo page and AND filters instead of resetting to discovery defaults", () => {
  const prior = comboFilterSearch({ category: "templates", templateCategoryId: category, sort: "rating", page: 3, minSavings: 1000000, minRating: 4.5 });
  const next = comboFilterSearch({ ...readComboFilters(prior), minSavings: undefined, page: 1 });
  assert.equal(readComboFilters(next).minSavings, undefined);
  assert.equal(readComboFilters(prior).page, 3); assert.equal(readComboFilters(prior).minSavings, 1000000); assert.equal(readComboFilters(prior).minRating, 4.5);
});
test("catalog filter navigation cannot remount and reset an editing cart or replay consumed draft intents", () => {
  const source = readFileSync(new URL("../src/features/journey/combo/ComboBuilderPage.tsx", import.meta.url), "utf8");
  assert.match(source, /<ComboBuilder key=\{userId\}/); assert.doesNotMatch(source, /key=\{`\$\{userId\}:\$\{location\.key\}`\}/);
  assert.match(source, /loadedDraft\.current === routeOrder\.id/);
  const reset = source.slice(source.indexOf("const resetFilters"), source.indexOf("const routeDraft"));
  assert.doesNotMatch(reset, /clearCart|updateCart/);
});
