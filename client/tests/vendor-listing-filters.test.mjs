import { test } from "node:test";
import assert from "node:assert/strict";
import { changeVendorListingFilters, listingUpdatedTime, vendorListingFilters, VENDOR_LISTING_STATUSES } from "../src/features/vendor-portal/vendorListingFilters.ts";

test("vendor listing filters use bounded defaults and retain approved URL query state on back navigation", () => {
  assert.deepEqual(vendorListingFilters(new URLSearchParams()), { keyword: "", status: undefined, page: 1, pageSize: 10 });
  const params = new URLSearchParams({ tab: "listings", serviceKeyword: "  Chụp ảnh  ", serviceStatus: "Rejected", servicePage: "3", servicePageSize: "20" });
  assert.deepEqual(vendorListingFilters(params), { keyword: "Chụp ảnh", status: "Rejected", page: 3, pageSize: 20 });
  assert.deepEqual(vendorListingFilters(new URLSearchParams(params.toString())), vendorListingFilters(params));
});

test("only current moderation statuses enter the API filter and keyword is limited to the endpoint contract", () => {
  for (const status of VENDOR_LISTING_STATUSES)
    assert.equal(vendorListingFilters(new URLSearchParams({ serviceStatus: status })).status, status);
  for (const status of ["ALL", "Approved", "draft", "999", "VendorOwner", "<script>"])
    assert.equal(vendorListingFilters(new URLSearchParams({ serviceStatus: status })).status, undefined);
  assert.equal(vendorListingFilters(new URLSearchParams({ serviceKeyword: "x".repeat(250) })).keyword.length, 200);
});

test("malformed page sizes, unsafe pages and offset overflow cannot produce invalid API requests", () => {
  for (const value of ["0", "-1", "1.5", "NaN", "Infinity", "9007199254740992", "2147483648"])
    assert.equal(vendorListingFilters(new URLSearchParams({ servicePage: value })).page, 1, value);
  for (const value of ["", "0", "-1", "2", "51", "1000000", "invalid"])
    assert.equal(vendorListingFilters(new URLSearchParams({ servicePageSize: value })).pageSize, 10, value);
  assert.equal(vendorListingFilters(new URLSearchParams({ servicePage: "42949674", servicePageSize: "50" })).page, 1);
  for (const size of [10, 20, 50])
    assert.equal(vendorListingFilters(new URLSearchParams({ servicePageSize: String(size) })).pageSize, size);
});

test("filter and page-size changes reset paging while preserving vendor tab and unrelated query parameters", () => {
  const original = new URLSearchParams({ tab: "listings", other: "keep", serviceKeyword: "Photo", serviceStatus: "Draft", servicePage: "8", servicePageSize: "20" });
  for (const change of [{ keyword: "Venue" }, { status: "PendingApproval" }, { pageSize: 50 }]) {
    const changed = changeVendorListingFilters(original, change);
    assert.equal(changed.has("servicePage"), false);
    assert.equal(changed.get("tab"), "listings");
    assert.equal(changed.get("other"), "keep");
  }
  assert.equal(original.get("servicePage"), "8", "must not mutate current router parameters");
  const nextPage = changeVendorListingFilters(original, { page: 2 });
  assert.equal(nextPage.get("serviceKeyword"), "Photo");
  assert.equal(nextPage.get("serviceStatus"), "Draft");
  assert.equal(nextPage.get("servicePage"), "2");
});

test("reset removes filter/page URL keys without discarding selected page size or tab", () => {
  const next = changeVendorListingFilters(new URLSearchParams({ tab: "listings", serviceKeyword: "Photo", serviceStatus: "Hidden", servicePage: "4", servicePageSize: "50" }), { keyword: "", status: undefined, page: 1 });
  assert.deepEqual(vendorListingFilters(next), { keyword: "", status: undefined, page: 1, pageSize: 50 });
  assert.equal(next.get("tab"), "listings");
  assert.equal(changeVendorListingFilters(next, { pageSize: 10 }).has("servicePageSize"), false);
});

test("updated time prefers actual updatedAt and falls back to createdAt in Vietnam timezone", () => {
  const created = "2026-10-18T17:00:00Z";
  const updated = "2026-10-19T03:30:00Z";
  const format = value => new Date(value).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh", dateStyle: "short", timeStyle: "short" });
  assert.equal(listingUpdatedTime(updated, created), format(updated));
  assert.equal(listingUpdatedTime(null, created), format(created));
  assert.equal(listingUpdatedTime(undefined, created), format(created));
  assert.equal(listingUpdatedTime(null, "invalid"), "Chưa có thông tin");
});

test("SQL datetime without a zone is UTC while explicit offsets retain their own meaning", () => {
  const created = "2026-10-18T17:00:00Z";
  assert.equal(listingUpdatedTime("2026-10-18T17:00:00", created), listingUpdatedTime(created, created));
  assert.equal(listingUpdatedTime(null, "2026-10-18T17:00:00"), listingUpdatedTime(null, created));
  assert.equal(listingUpdatedTime("2026-10-18T17:00:00.1234567", created), listingUpdatedTime("2026-10-18T17:00:00.1234567Z", created));
  assert.equal(listingUpdatedTime("2026-10-19T00:00:00+07:00", created), listingUpdatedTime(created, created));
  assert.equal(listingUpdatedTime("2026-10-18T13:00:00-04:00", created), listingUpdatedTime(created, created));
  assert.equal(listingUpdatedTime("invalid", created), "Chưa có thông tin");
});
