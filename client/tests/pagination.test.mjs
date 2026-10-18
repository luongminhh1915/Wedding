import { test } from "node:test";
import assert from "node:assert/strict";
import { paginationWindow } from "../src/components/paginationModel.ts";

test("desktop pages show six records and preserve the final partial range", () => {
  assert.deepEqual(paginationWindow(2, 6, 13), { pages: 3, current: 2, first: 7, last: 12, hasPrevious: true, hasNext: true });
  assert.deepEqual(paginationWindow(3, 6, 13), { pages: 3, current: 3, first: 13, last: 13, hasPrevious: true, hasNext: false });
});
test("phone pages contain fewer records with correct total navigation", () => {
  assert.deepEqual(paginationWindow(2, 4, 13), { pages: 4, current: 2, first: 5, last: 8, hasPrevious: true, hasNext: true });
});
test("empty results and out-of-range pages remain bounded after filters or deletion", () => {
  assert.deepEqual(paginationWindow(4, 6, 0), { pages: 1, current: 1, first: 0, last: 0, hasPrevious: false, hasNext: false });
  assert.deepEqual(paginationWindow(99, 6, 7), { pages: 2, current: 2, first: 7, last: 7, hasPrevious: true, hasNext: false });
  assert.equal(paginationWindow(-2, 6, 7).current, 1);
});
