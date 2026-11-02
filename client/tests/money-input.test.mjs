import { test } from "node:test";
import assert from "node:assert/strict";
import { formatMoneyInput, moneyCaret, moneyDelete, moneyInputEdit, parseMoneyInput } from "../src/components/moneyInputModel.ts";

test("money display groups VND and preserves the optional decimal value", () => {
  for (const [amount, display] of [[1000000, "1.000.000"], [12000000, "12.000.000"], [150000000, "150.000.000"], [0, "0"], [1234567.25, "1.234.567,25"]]) {
    assert.equal(formatMoneyInput(amount), display);
    assert.equal(parseMoneyInput(display).value, amount);
  }
  assert.equal(formatMoneyInput(null), "");
  assert.equal(formatMoneyInput(NaN), "");
});
test("input accepts unformatted typing, supported paste and emits only a numeric value", () => {
  for (const raw of ["1000000", "1.000.000", "1 000 000", "1\u202f000\u202f000", "1.000.000 ₫", " 1.000.000 VNĐ ", "1.000.000 VND", "1.000.000 đ"]) {
    assert.deepEqual(parseMoneyInput(raw), { valid: true, value: 1000000, text: "1.000.000" });
  }
  assert.deepEqual(parseMoneyInput(""), { valid: true, value: null, text: "" });
  assert.deepEqual(parseMoneyInput("0001234,"), { valid: true, value: 1234, text: "1.234," });
  assert.deepEqual(parseMoneyInput("1234,50"), { valid: true, value: 1234.5, text: "1.234,50" });
});
test("invalid/ambiguous pasted text is rejected instead of silently changing its meaning", () => {
  for (const raw of ["1e6", "1E6", "+100", "-100", "NaN", "Infinity", "₫100", "12USD", "<script>100</script>", "1.23", "1234.50", "1,000,000", "1,234", "12 34", "1..000", "1.000,123", "0x100", "1/2"]) {
    assert.equal(parseMoneyInput(raw).valid, false, raw);
  }
});
test("bounds, integer mode and precision cannot cause overflow or round an input silently", () => {
  assert.equal(parseMoneyInput("100", { min: 101 }).valid, false);
  assert.equal(parseMoneyInput("100", { max: 99 }).valid, false);
  assert.equal(parseMoneyInput("100,5", { fractionDigits: 0 }).valid, false);
  assert.equal(parseMoneyInput("100,55", { fractionDigits: 1 }).valid, false);
  assert.equal(parseMoneyInput("9999999999999999", { max: Number.MAX_VALUE }).valid, false);
  assert.equal(parseMoneyInput("9007199254740991,01", { max: Number.MAX_VALUE }).valid, false);
  assert.equal(parseMoneyInput("100", { min: 200, max: 100 }).valid, false);
  assert.equal(parseMoneyInput("100", { max: Infinity }).valid, false);
  assert.deepEqual(parseMoneyInput("-1.250,5", { min: -2000 }), { valid: true, value: -1250.5, text: "-1.250,5" });
});
test("caret follows logical digits for typing, replacing middle digits and decimals", () => {
  assert.equal(moneyCaret("1234", 4, "1.234"), 5);
  assert.equal(moneyCaret("12345", 2, "12.345"), 2);
  assert.equal(moneyCaret("12.345", 4, "12.345"), 4);
  assert.equal(moneyCaret("1234,50", 6, "1.234,50"), 7);
  assert.equal(moneyCaret("", 0, ""), 0);
  assert.equal(moneyCaret("-1234", 1, "-1.234"), 1);
});
test("separator deletion removes the adjacent digit and leaves a valid amount", () => {
  const back = moneyDelete("12.345", 3, "Backspace");
  assert.deepEqual(back, { raw: "1345", caret: 1 });
  assert.equal(parseMoneyInput(back.raw).value, 1345);
  const del = moneyDelete("12.345", 2, "Delete");
  assert.deepEqual(del, { raw: "1245", caret: 2 });
  assert.equal(parseMoneyInput(del.raw).value, 1245);
  assert.equal(moneyDelete("1.234", 1, "Backspace"), null);
  assert.equal(moneyDelete("1.234", 3, "Delete"), null);
});
test("normalization to unchanged display still supplies a caret for the current browser edit", () => {
  for (const [raw, caret, expectedCaret] of [["01.234", 1, 1], ["1..234", 3, 1], ["12.345", 2, 2]]) {
    const edit = moneyInputEdit(raw, caret);
    assert.equal(edit.valid, true);
    assert.equal(edit.text, raw === "12.345" ? "12.345" : "1.234");
    assert.equal(edit.caret, expectedCaret);
    assert.equal(edit.complete, true);
  }
});
test("negative-sign draft is local-only until a complete allowed amount exists", () => {
  assert.deepEqual(moneyInputEdit("-", 1, { min: -2000 }), { valid: true, value: null, text: "-", caret: 1, complete: false });
  assert.equal(moneyInputEdit("-", 1).valid, false);
  assert.deepEqual(moneyInputEdit("-1234", 5, { min: -2000 }), { valid: true, value: -1234, text: "-1.234", caret: 6, complete: true });
  assert.equal(moneyInputEdit("-2001", 5, { min: -2000 }).valid, false);
  assert.equal(parseMoneyInput("-150", { min: -1000, max: 100 }).value, -150);
});
