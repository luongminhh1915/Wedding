import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import ts from "typescript";
import { actorWorkspaceNavigation, usesCustomerNavigation } from "../src/components/actorNavigationModel.ts";
import { VENDOR_JOURNEY_TABS } from "../src/features/vendor-portal/vendorJourneyModel.ts";

const roles = ["Customer", "VendorOwner", "Moderator", "Finance", "CustomerCare", "SuperAdmin"];
const layoutSource = await readFile(new URL("../src/components/WeddingMatchLayout.tsx", import.meta.url), "utf8");
const layoutCss = await readFile(new URL("../src/styles/wedding-match.css", import.meta.url), "utf8");
const adminSource = await readFile(new URL("../src/features/admin/AdminDashboard.tsx", import.meta.url), "utf8");
const roleTabs = {
  VendorOwner: ["overview", "listings", "combos", "leads"],
  Moderator: ["overview", "moderation", "reviews", "published-listings", "cms"],
  Finance: ["overview", "finance", "settlements", "disputes"],
  CustomerCare: ["overview", "disputes", "emergency"],
  SuperAdmin: ["overview", "moderation", "users", "finance", "cms"],
};

const snippet = layoutSource.slice(layoutSource.indexOf("const actorNavigationIcons ="), layoutSource.indexOf("export function WeddingMatchLogo"));
let helperJs = ts.transpileModule(`
import { Link } from "react-router-dom";
import { BookOpen, PanelsTopLeft, Star, Package, MessageSquare, ShieldCheck, Wallet, FileCheck2, Gavel, Users, UserRound } from "lucide-react";
import { actorWorkspaceNavigation } from "./actorNavigationModel";
${snippet}`, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX } }).outputText;
for (const dependency of ["react/jsx-runtime", "react-router-dom", "lucide-react"])
  helperJs = helperJs.replaceAll(`from "${dependency}"`, `from "${import.meta.resolve(dependency)}"`);
helperJs = helperJs.replace('from "./actorNavigationModel"', `from "${new URL("../src/components/actorNavigationModel.ts", import.meta.url).href}"`);
const { ActorWorkspaceLinks } = await import(`data:text/javascript;base64,${Buffer.from(helperJs).toString("base64")}`);
const renderLinks = (role, mobile, pathname, search = "") => renderToStaticMarkup(createElement(MemoryRouter,
  { initialEntries: [pathname + search] }, createElement("nav", null, createElement(ActorWorkspaceLinks, { role, mobile, pathname, search }))));
const links = html => [...html.matchAll(/<a\b([^>]*)>([\s\S]*?)<\/a>/g)].map(match => ({
  href: /href="([^"]+)"/.exec(match[1])?.[1].replaceAll("&amp;", "&"),
  active: match[1].includes('aria-current="page"'),
}));

test("only anonymous and Customer retain public/customer primary navigation", () => {
  assert.equal(usesCustomerNavigation(null), true);
  assert.equal(usesCustomerNavigation(undefined), true);
  assert.equal(usesCustomerNavigation("Customer"), true);
  for (const role of roles.filter(role => role !== "Customer")) assert.equal(usesCustomerNavigation(role), false);
  for (const invalid of ["Superadmin", "vendor", "__proto__", {}, 1, ""]) assert.equal(usesCustomerNavigation(invalid), false);
});

test("workspace menu matrix points only to real role-authorized dashboard tabs", () => {
  const actualAdminTabs = new Map([...adminSource.matchAll(/\{\s*id:\s*"([^"]+)"[\s\S]*?roles:\s*\[([^\]]+)\]/g)]
    .map(match => [match[1], [...match[2].matchAll(/"([^"]+)"/g)].map(role => role[1])]));
  const actualVendorTabs = new Set(VENDOR_JOURNEY_TABS.map(tab => tab.id));
  assert.ok(actualAdminTabs.has("overview"));
  for (const [role, expected] of Object.entries(roleTabs)) {
    const path = role === "VendorOwner" ? "/vendor/dashboard" : "/admin/dashboard";
    const items = actorWorkspaceNavigation(role, path);
    assert.deepEqual(items.map(item => item.id), expected, role);
    assert.equal(items.filter(item => item.active).length, 1);
    assert.equal(items.find(item => item.active)?.id, "overview");
    for (const item of items) {
      const target = new URL(item.to, "https://wedding.example.invalid");
      assert.equal(target.pathname, path);
      const tab = target.searchParams.get("tab") || "overview";
      assert.equal(tab, item.id);
      if (role === "VendorOwner") assert.ok(actualVendorTabs.has(tab), tab);
      else assert.ok(actualAdminTabs.get(tab)?.includes(role), `${role} cannot access ${tab}`);
    }
    const mobile = actorWorkspaceNavigation(role, path, "", true);
    assert.deepEqual(mobile.slice(0, -1).map(item => item.to), items.map(item => item.to));
    assert.equal(mobile.at(-1).to, "/account");
    assert.ok(mobile.length <= 6);
    assert.equal(new Set(mobile.map(item => item.id)).size, mobile.length);
  }
  assert.deepEqual(actorWorkspaceNavigation("Customer", "/customer/dashboard"), []);
  for (const unknown of [null, undefined, "Admin", "unknown", "__proto__", {}]) {
    assert.deepEqual(actorWorkspaceNavigation(unknown, "/admin/dashboard"), []);
    assert.ok(actorWorkspaceNavigation(unknown, "/admin/dashboard", "", true).every(item => item.to === "/account"));
  }
});

test("query-aware active state never highlights every link sharing a dashboard pathname", () => {
  for (const [role, tabs] of Object.entries(roleTabs)) {
    const path = role === "VendorOwner" ? "/vendor/dashboard" : "/admin/dashboard";
    for (const tab of tabs) {
      const items = actorWorkspaceNavigation(role, path, `?tab=${tab}&page=2`);
      assert.deepEqual(items.filter(item => item.active).map(item => item.id), [tab], `${role}/${tab}`);
    }
    assert.equal(actorWorkspaceNavigation(role, "/notifications", "?tab=overview").filter(item => item.active).length, 0);
    assert.deepEqual(actorWorkspaceNavigation(role, path, "?tab=unknown").filter(item => item.active).map(item => item.id), ["overview"]);
    assert.deepEqual(actorWorkspaceNavigation(role, path.toUpperCase() + "/", "?tab=overview").filter(item => item.active).map(item => item.id), ["overview"]);
  }
  assert.deepEqual(actorWorkspaceNavigation("VendorOwner", "/vendor/dashboard", "?servicePage=2&serviceStatus=Pending").filter(item => item.active).map(item => item.id), ["listings"]);
  assert.deepEqual(actorWorkspaceNavigation("Moderator", "/admin/dashboard", "?tab=finance").filter(item => item.active).map(item => item.id), ["overview"]);
  assert.deepEqual(actorWorkspaceNavigation("Finance", "/admin/dashboard", "?tab=moderation").filter(item => item.active).map(item => item.id), ["overview"]);
  assert.deepEqual(actorWorkspaceNavigation("CustomerCare", "/admin/dashboard", "?tab=users").filter(item => item.active).map(item => item.id), ["overview"]);
  assert.deepEqual(actorWorkspaceNavigation("Moderator", "/admin/dashboard", "?tab=cms").filter(item => item.active).map(item => item.id), ["cms"]);
  assert.deepEqual(actorWorkspaceNavigation("VendorOwner", "/vendor/dashboard", "?tab=contracts-couple").filter(item => item.active), []);
});

test("real desktop/mobile link renderer excludes Customer catalog and other actors' portals", () => {
  for (const role of Object.keys(roleTabs)) {
    const path = role === "VendorOwner" ? "/vendor/dashboard" : "/admin/dashboard";
    for (const mobile of [false, true]) {
      const html = renderLinks(role, mobile, path);
      const rendered = links(html);
      const model = actorWorkspaceNavigation(role, path, "", mobile);
      assert.deepEqual(rendered.map(item => item.href), model.map(item => item.to));
      assert.equal(rendered.filter(item => item.active).length, 1);
      assert.equal((html.match(/<svg\b/g) || []).length, mobile ? model.length : 0);
      assert.ok(rendered.every(item => item.href.startsWith(path) || mobile && item.href === "/account"));
      assert.ok(rendered.every(item => !["/", "/browse", "/combo", "/cam-nang", "/invitation", "/customer/dashboard"].includes(item.href)));
    }
  }
  assert.deepEqual(links(renderLinks("unknown", false, "/admin/dashboard")), []);
  assert.deepEqual(links(renderLinks("unknown", true, "/admin/dashboard")).map(item => item.href), ["/account"]);
});

test("header and mobile primary navigation use the actor gate and mobile nav remains visible below the header", () => {
  const header = layoutSource.slice(layoutSource.indexOf("export function WeddingMatchHeader"), layoutSource.indexOf("export function WeddingMatchFooter"));
  const mobile = layoutSource.slice(layoutSource.indexOf("export function MobileNavigation"), layoutSource.indexOf("export default function WeddingMatchLayout"));
  assert.match(header, /usesCustomerNavigation\(user\?\.role\)/);
  assert.match(header, /<ActorWorkspaceLinks role=\{user\?\.role\} pathname=\{location\.pathname\} search=\{location\.search\}/);
  assert.match(header, /<CustomerNavigationGroups/);
  assert.match(header, /<NotificationBell\s*\/>/);
  assert.match(header, /<ComboCartLink\s*\/>/);
  assert.match(header, /to="\/account"/);
  assert.match(header, /logout\(\)/);
  assert.match(mobile, /if \(!usesCustomerNavigation\(user\?\.role\)\) return/);
  assert.match(mobile, /<ActorWorkspaceLinks role=\{user\?\.role\} pathname=\{pathname\} search=\{search\} \/>/);
  assert.match(mobile, /customerMobileSection\(pathname\)/);
  for (const item of ["Combo cưới", "Wedding Planner", "Cẩm nang", "Về Wedding Match", "Dành cho NCC"])
    assert.ok(mobile.includes(item), `mobile navigation is missing ${item}`);
  const layout = layoutSource.slice(layoutSource.indexOf("export default function WeddingMatchLayout"));
  assert.ok(layout.indexOf("<MobileNavigation />") < layout.indexOf("<div id=\"main-content\">"), "mobile navigation should be placed directly below the header, before page content");
  const tabletRules = layoutCss.slice(layoutCss.indexOf("@media (max-width: 900px)"), layoutCss.indexOf("@media (max-width: 600px)"));
  const mobileNavRules = tabletRules.slice(tabletRules.indexOf(".wm-mobile-nav {"), tabletRules.indexOf(".wm-mobile-nav a {"));
  assert.match(mobileNavRules, /display:\s*flex/);
  assert.match(mobileNavRules, /position:\s*sticky/, "mobile navigation must remain visible beneath the sticky header instead of hiding at the viewport bottom");
  assert.match(mobileNavRules, /overflow-x:\s*auto/, "long actor menus must remain reachable on narrow phones");
  assert.doesNotMatch(layoutSource, /dashboardPath\(user\)/);
});
