import { test } from "node:test";
import assert from "node:assert/strict";
import { serviceItemReviewPayload } from "../src/features/journey/admin/serviceItemReview.ts";

const pending = () => ({ moderationStatus: "Pending", reviewVersion: "a".repeat(64) });

test("component review submits only the exact opaque version, decision and trimmed note", () => {
  const item = { ...pending(), basePrice: 1000000, commissionRate: 0.99 };
  assert.deepEqual(serviceItemReviewPayload(item, true, "  Checked  "), { approve: true, note: "Checked", reviewVersion: item.reviewVersion });
  assert.deepEqual(serviceItemReviewPayload(item, true, "  "), { approve: true, note: null, reviewVersion: item.reviewVersion });
  assert.equal(item.basePrice, 1000000);
});

test("reject requires a nonblank reason within the same backend limit", () => {
  for (const value of ["", "  ", "\n\t", "x".repeat(2001)]) assert.throws(() => serviceItemReviewPayload(pending(), false, value));
  assert.equal(serviceItemReviewPayload(pending(), false, " x ").note, "x");
  assert.equal(serviceItemReviewPayload(pending(), false, "x".repeat(2000)).note.length, 2000);
  assert.throws(() => serviceItemReviewPayload(pending(), true, "x".repeat(2001)));
});

test("missing or malformed server review version fails closed instead of approving cached content", () => {
  for (const reviewVersion of [undefined, null, "", "old-id", "g".repeat(64), "a".repeat(63), "a".repeat(65)]) {
    assert.throws(() => serviceItemReviewPayload({ ...pending(), reviewVersion }, true, ""));
  }
  assert.equal(serviceItemReviewPayload({ ...pending(), reviewVersion: "A".repeat(64) }, true, "").reviewVersion, "A".repeat(64));
});

test("nonpending components cannot be reviewed or silently resubmitted by a decision", () => {
  for (const moderationStatus of ["Draft", "Approved", "Rejected", "Unknown", ""]) assert.throws(() => serviceItemReviewPayload({ ...pending(), moderationStatus }, true, ""));
});
