import { test } from "node:test";
import assert from "node:assert/strict";
import { contractAmounts } from "../src/features/vendor-portal/components/contractMoneyModel.ts";
import { formatMoneyInput, parseMoneyInput } from "../src/components/moneyInputModel.ts";

test("grouped contract/deposit input emits numbers with the exact existing decimal amounts", () => {
  const value = parseMoneyInput("80.000.000,75").value;
  const deposit = parseMoneyInput("20.000.000,25").value;
  const amounts = contractAmounts(value, deposit);
  assert.deepEqual(amounts, { valid: true, contractValue: 80000000.75, depositAmount: 20000000.25 });
  assert.equal(typeof amounts.contractValue, "number");
  assert.equal(typeof amounts.depositAmount, "number");
  assert.equal(formatMoneyInput(amounts.contractValue), "80.000.000,75");
});
test("missing, non-finite, non-positive and negative contract values never reach the API", () => {
  for (const value of [null, NaN, Infinity, -Infinity, 0, -1])
    assert.equal(contractAmounts(value, 0).valid, false);
});
test("deposits reject non-finite, missing, negative and greater-than-contract values", () => {
  for (const deposit of [null, NaN, Infinity, -Infinity, -0.01, 100.01])
    assert.equal(contractAmounts(100, deposit).valid, false);
  assert.deepEqual(contractAmounts(100.25, 100.25), { valid: true, contractValue: 100.25, depositAmount: 100.25 });
  assert.deepEqual(contractAmounts(100, 0), { valid: true, contractValue: 100, depositAmount: 0 });
});
test("only the existing create form permits an empty deposit as zero; revise requires an explicit amount", () => {
  assert.deepEqual(contractAmounts(100, null, true), { valid: true, contractValue: 100, depositAmount: 0 });
  assert.equal(contractAmounts(100, null).valid, false);
});
test("display round trips do not alter old fractional values or add financial fields", () => {
  for (const value of [0.01, 1.25, 1234567.89, 999999999999.99]) {
    const parsed = parseMoneyInput(formatMoneyInput(value), { max: 1_000_000_000_000 });
    assert.equal(parsed.valid, true);
    assert.equal(parsed.value, value);
    const result = contractAmounts(parsed.value, 0);
    assert.deepEqual(Object.keys(result), ["valid", "contractValue", "depositAmount"]);
    assert.equal(result.contractValue, value);
  }
});
