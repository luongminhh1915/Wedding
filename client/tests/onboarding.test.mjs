import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

const source = await readFile(new URL("../src/features/journey/planner/onboardingDecision.ts", import.meta.url), "utf8");
const javascript = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 } }).outputText;
const { afterLoginDestination, safeIntent, isJoinIntent } = await import(`data:text/javascript;base64,${Buffer.from(javascript).toString("base64")}`);

test("new customers always answer ten questions before any dashboard or purchase intent", () => {
  for (const intent of [null, "/", "/browse", "/combo?step=4", "/thiep/create?template=navy-cream", "/customer/dashboard"])
    assert.equal(afterLoginDestination("Customer", false, intent), "/planner/setup");
});
test("existing completed customers retain a safe return destination", () => {
  assert.equal(afterLoginDestination("Customer", true, "/thiep/create?template=navy-cream"), "/thiep/create?template=navy-cream");
  assert.equal(afterLoginDestination("Customer", true), "/customer/dashboard");
  assert.equal(afterLoginDestination("Customer", true, "/planner/setup"), "/customer/dashboard");
});
test("vendors and every back-office role bypass customer onboarding", () => {
  assert.equal(afterLoginDestination("VendorOwner", false, "/planner/setup"), "/vendor/dashboard");
  for (const role of ["SuperAdmin", "Moderator", "Finance", "CustomerCare"])
    assert.equal(afterLoginDestination(role, false, "/combo"), "/admin/dashboard");
});
test("join intent is the only incomplete-customer escape and cannot be an external redirect", () => {
  assert.equal(isJoinIntent("/planner?join=secure-token"), true);
  assert.equal(afterLoginDestination("Customer", false, "/planner?join=secure-token"), "/planner?join=secure-token");
  assert.equal(isJoinIntent("/browse?join=token"), false);
  for (const bad of ["https://example.com", "//example.com", "/\\example.com", "/login", "/register?from=/combo", "/auth/continue"])
    assert.equal(safeIntent(bad), null);
});
