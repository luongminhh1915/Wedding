import test from "node:test";
import assert from "node:assert/strict";
import { ACTOR_ROLES, actorDashboard, isActorRole, roleAwareReturnPath, safeInternalReturnPath } from "../src/features/auth/authNavigation.ts";

test("all six actors have an explicit workspace and unknown roles fail closed", () => {
  assert.deepEqual(ACTOR_ROLES, ["Customer", "VendorOwner", "Moderator", "Finance", "CustomerCare", "SuperAdmin"]);
  assert.equal(actorDashboard("Customer"), "/customer/dashboard");
  assert.equal(actorDashboard("VendorOwner"), "/vendor/dashboard");
  for (const role of ["Moderator", "Finance", "CustomerCare", "SuperAdmin"])
    assert.equal(actorDashboard(role), "/admin/dashboard");
  for (const role of [undefined, null, "", "Admin", "customer", "Owner", 1]) {
    assert.equal(isActorRole(role), false);
    assert.equal(actorDashboard(role), "/login");
    assert.equal(roleAwareReturnPath(role, "/browse"), null);
  }
});

test("completed customers cannot inherit a vendor or back-office return destination", () => {
  for (const path of [
    "/vendor/dashboard", "/vendor/dashboard?tab=quotes", "/vendor/dashboard/",
    "/Vendor/Dashboard", "/%76endor/dashboard", "/vendor%2fdashboard",
    "/admin/dashboard", "/ADMIN/dashboard/", "/%61dmin/dashboard",
    "/customer/../vendor/dashboard", "/browse/../admin/dashboard",
  ]) assert.equal(roleAwareReturnPath("Customer", path), null, path);
});

test("vendors and internal actors cannot inherit customer-only or another actor's workspace", () => {
  const customerPaths = [
    "/customer/dashboard", "/Customer/Dashboard?tab=contracts", "/customer%2fdashboard",
    "/planner", "/planner/setup/", "/planner?join=invitation-code", "/%70lanner/setup",
    "/thiep/create", "/invitation/manage", "/lich-hen", "/hop-dong/",
  ];
  for (const role of ACTOR_ROLES.filter(role => role !== "Customer"))
    for (const path of customerPaths)
      assert.equal(roleAwareReturnPath(role, path), null, `${role}: ${path}`);
  assert.equal(roleAwareReturnPath("VendorOwner", "/admin/dashboard"), null);
  for (const role of ["Moderator", "Finance", "CustomerCare", "SuperAdmin"])
    assert.equal(roleAwareReturnPath(role, "/vendor/dashboard"), null);
});

test("role-appropriate and public local return paths retain their complete query", () => {
  const publicPaths = ["/", "/browse?category=photo", "/combo?step=2", "/listings/sample", "/invitation", "/wiki/article"];
  for (const role of ACTOR_ROLES)
    for (const path of publicPaths)
      assert.equal(roleAwareReturnPath(role, path), path);
  assert.equal(roleAwareReturnPath("Customer", "/customer/dashboard?tab=contracts"), "/customer/dashboard?tab=contracts");
  assert.equal(roleAwareReturnPath("Customer", "/planner?join=invitation-code"), "/planner?join=invitation-code");
  assert.equal(roleAwareReturnPath("VendorOwner", "/vendor/dashboard?tab=quotes"), "/vendor/dashboard?tab=quotes");
  for (const role of ["Moderator", "Finance", "CustomerCare", "SuperAdmin"])
    assert.equal(roleAwareReturnPath(role, "/admin/dashboard?tab=overview"), "/admin/dashboard?tab=overview");
});

test("return paths reject external URLs, malformed encoding, controls and authentication loops", () => {
  for (const path of [
    null, undefined, "", "https://outside.example", "//outside.example", "///outside.example",
    "/%2foutside.example", "/%2F%2Foutside.example", "/\\outside.example", "/%5coutside.example",
    " /browse", "/browse ", "/browse\n", "/browse%0a", "/browse%00", "/browse%7f", "/%zz",
    "/%252foutside.example", "/%2576endor/dashboard", "/login", "/login?from=/combo",
    "/Login/", "/%6cogin", "/register?role=vendor", "/auth/continue", "/AUTH/continue",
    "/browse/../auth/continue",
  ]) assert.equal(safeInternalReturnPath(path), null, String(path));
});

test("malformed runtime intent values fail closed instead of crashing a successful login", () => {
  for (const path of [{ from: "/vendor/dashboard" }, {}, [], ["/browse"], 42, 0, true, false]) {
    assert.equal(safeInternalReturnPath(path), null);
    assert.equal(roleAwareReturnPath("Customer", path), null);
  }
});

test("ordinary encoded search text survives the safe local return path check", () => {
  for (const path of ["/browse?q=studio%20c%C6%B0%E1%BB%9Bi", "/combo?region=H%E1%BB%93%20Ch%C3%AD%20Minh"]) {
    assert.equal(safeInternalReturnPath(path), path);
    assert.equal(roleAwareReturnPath("Customer", path), path);
  }
});
