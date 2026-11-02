import { test } from "node:test";
import assert from "node:assert/strict";
import { groupCustomerOrders, matchesOrderFilter, orderCapability } from "../src/features/journey/customerOrders/orderPresentation.ts";

test("order groups never mix suppliers or different combos", () => {
  const groups = groupCustomerOrders([
    { id: "a", vendorId: "one", comboOrderId: "combo-a" },
    { id: "b", vendorId: "one", comboOrderId: "combo-a" },
    { id: "c", vendorId: "two", comboOrderId: "combo-a" },
    { id: "d", vendorId: "one", comboOrderId: "combo-b" },
    { id: "e", vendorId: "one" },
  ]);
  assert.equal(groups.length, 4);
  assert.deepEqual(groups[0].items.map((row) => row.id), ["a", "b"]);
});

test("archived orders are separate from ongoing and history, with a recovery view", () => {
  assert.equal(matchesOrderFilter({ status: "New", isArchived: true }, "all"), false);
  assert.equal(matchesOrderFilter({ status: "New", isArchived: true }, "archived"), true);
  assert.equal(matchesOrderFilter({ status: "Cancelled" }, "closed"), true);
  assert.equal(matchesOrderFilter({ status: "Confirmed" }, "active"), true);
  assert.equal(matchesOrderFilter({ status: "Completed" }, "active"), false);
  assert.equal(matchesOrderFilter({ status: "Rejected" }, "closed"), true);
  assert.equal(matchesOrderFilter({ status: "Superseded" }, "closed"), true);
  assert.equal(matchesOrderFilter({ status: "Confirmed" }, "confirmed"), true);
  assert.equal(matchesOrderFilter({ status: "Disputed" }, "confirmed"), true);
  assert.equal(matchesOrderFilter({ status: "PendingVerification" }, "confirmed"), false);
  assert.equal(matchesOrderFilter({ status: "Draft" }, "confirmed"), false);
});

test("missing server capabilities never enable withdrawal or cancellation", () => {
  assert.equal(orderCapability(undefined, "canWithdraw"), false);
  assert.equal(orderCapability({ canWithdraw: false }, "canWithdraw"), false);
  assert.equal(orderCapability({ canWithdraw: true }, "canWithdraw"), true);
  assert.equal(orderCapability(undefined, "canConfirm"), false);
  assert.equal(orderCapability(undefined, "canRespondQuote"), false);
});
