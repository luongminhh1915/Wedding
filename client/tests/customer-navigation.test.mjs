import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import ts from "typescript";
import { CUSTOMER_TABS, customerMobileSection, customerNavigationGroups, customerTab, customerTabHref, isCustomerWorkspace } from "../src/features/customer/customerNavigationModel.ts";
import { roleAwareReturnPath } from "../src/features/auth/authNavigation.ts";

const source = await readFile(new URL("../src/features/customer/CustomerToolNavigation.tsx", import.meta.url), "utf8");
let componentJs = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX } }).outputText;
for (const dependency of ["react/jsx-runtime", "react-router-dom", "lucide-react"])
  componentJs = componentJs.replaceAll(`from "${dependency}"`, `from "${import.meta.resolve(dependency)}"`);
componentJs = componentJs.replace('from "./customerNavigationModel"', `from "${new URL("../src/features/customer/customerNavigationModel.ts", import.meta.url).href}"`);
const { CustomerNavigationGroups, CustomerToolNavigation } = await import(`data:text/javascript;base64,${Buffer.from(componentJs).toString("base64")}`);
const render = (Component, pathname, search = "") => renderToStaticMarkup(createElement(MemoryRouter, { initialEntries: [`${pathname}${search}`] }, createElement(Component, { pathname, search })));
const links = html => [...html.matchAll(/<a\b([^>]*)>([\s\S]*?)<\/a>/g)].map(match => ({
  href: /href="([^"]+)"/.exec(match[1])?.[1].replaceAll("&amp;", "&"), current: match[1].includes('aria-current="page"'),
}));

test("hierarchical customer menu retains every existing tool once and both discovery entries", () => {
  const groups = customerNavigationGroups("/customer/dashboard", "");
  assert.deepEqual(groups.map(group => group.label), ["Kế hoạch cưới", "Dịch vụ & tư vấn", "Khách mời & thiệp"]);
  const items = groups.flatMap(group => group.items);
  assert.equal(items.length, 11); assert.equal(new Set(items.map(item => item.id)).size, 11);
  assert.deepEqual(items.filter(item => CUSTOMER_TABS.includes(item.id)).map(item => item.id).sort(), [...CUSTOMER_TABS].sort());
  assert.equal(items.find(item => item.id === "discovery").to, "/combo");
  assert.equal(items.find(item => item.id === "browse").to, "/browse");
  assert.equal(items.find(item => item.id === "reviews").to, "/customer/dashboard?tab=reviews");
});

test("legacy workspace aliases activate the actual customer tool and the mobile Planner entry", () => {
  for (const [path, tab] of [["/planner", "overview"], ["/planner/setup", "overview"], ["/hop-dong", "contracts"], ["/customer/contracts", "contracts"], ["/lich-hen", "appointments"], ["/thiep/create", "invitations"], ["/invitation/manage", "invitations"], ["/HOP-DONG/", "contracts"]]) {
    assert.equal(isCustomerWorkspace(path), true, path); assert.equal(customerTab(path, ""), tab, path);
    assert.equal(customerMobileSection(path), "planner", path);
    assert.equal(customerNavigationGroups(path, "").flatMap(group => group.items).find(item => item.active)?.id, tab);
  }
  assert.equal(customerTab("/hop-dong", "?tab=budget"), "budget");
  assert.equal(customerTab("/hop-dong", "?tab=unknown"), "overview");
  assert.equal(customerTab("/hop-dong", "?tab="), "contracts");
  assert.equal(isCustomerWorkspace("/planner/setup/other"), false);
  assert.equal(isCustomerWorkspace("/invitation/synthetic-couple"), false);
});

test("tab navigation preserves wedding join, paging/filter state and compatible notification focus", () => {
  const original = new URLSearchParams({ tab: "contracts", join: "synthetic-invite", focusKind: "quote", focus: "owned-quote", section: "quotes", contractPage: "3", comboPage: "2", filter: "active", custom: "keep" });
  const contract = new URL(customerTabHref("contracts", original), "https://example.invalid");
  assert.equal(contract.searchParams.get("focus"), "owned-quote"); assert.equal(contract.searchParams.get("section"), "quotes");
  const budget = new URL(customerTabHref("budget", original), "https://example.invalid");
  for (const key of ["join", "contractPage", "comboPage", "filter", "custom"]) assert.equal(budget.searchParams.get(key), original.get(key));
  for (const key of ["focus", "focusKind", "section"]) assert.equal(budget.searchParams.has(key), false);
  assert.equal(original.get("tab"), "contracts"); assert.equal(original.get("focus"), "owned-quote");
  const previousLocation = customerTabHref("contracts", original);
  assert.equal(customerTab("/customer/dashboard", new URL(previousLocation, "https://example.invalid").search), "contracts", "Back/reload reconstructs the previous URL without component-local tab state");
  assert.equal(new URL(customerTabHref("overview", original), "https://example.invalid").searchParams.has("tab"), false);
});

test("all generated workspace links keep actor routing and onboarding authority in the existing guards", () => {
  for (const tab of CUSTOMER_TABS) {
    const path = customerTabHref(tab, "?join=synthetic&tab=budget");
    assert.equal(roleAwareReturnPath("Customer", path), path);
    for (const role of ["VendorOwner", "Moderator", "Finance", "CustomerCare", "SuperAdmin"]) assert.equal(roleAwareReturnPath(role, path), null);
    assert.equal(path.includes("/planner/setup"), false, "navigation does not reopen or rewrite onboarding");
  }
  assert.equal(roleAwareReturnPath("Customer", "/account?tab=security"), "/account?tab=security");
  assert.equal(roleAwareReturnPath("Customer", "/notifications?page=2"), "/notifications?page=2");
});

test("customer bottom navigation assigns one section to workspace, invitation, discovery and account aliases", () => {
  for (const path of ["/invitation", "/customer/dashboard", "/thiep/create", "/invitation/manage"]) assert.equal(customerMobileSection(path), "planner");
  for (const path of ["/browse", "/vendors", "/listings/service"]) assert.equal(customerMobileSection(path), "services");
  for (const path of ["/combo", "/combo/package"]) assert.equal(customerMobileSection(path), "combo");
  for (const path of ["/account", "/notifications", "/register", "/login"]) assert.equal(customerMobileSection(path), "account");
  for (const path of ["/vendor/dashboard", "/admin/dashboard", "/unknown"]) assert.equal(customerMobileSection(path), null);
});

test("actual grouped navigation renders all links, semantic groups and the exact active deep link", () => {
  const html = render(CustomerNavigationGroups, "/hop-dong", "?focusKind=contract&focus=owned&section=contracts&join=synthetic");
  const entries = links(html);
  assert.equal(entries.length, 11); assert.equal(entries.filter(item => item.current).length, 1);
  const selected = new URL(entries.find(item => item.current).href, "https://example.invalid");
  assert.equal(selected.searchParams.get("tab"), "contracts"); assert.equal(selected.searchParams.get("focus"), "owned");
  assert.match(html, /aria-label="Kế hoạch cưới"/); assert.match(html, /aria-label="Khách mời &amp; thiệp"/);
  assert.match(html, /Lịch hẹn/); assert.match(html, /Thiệp &amp; RSVP/); assert.match(html, /Khách &amp; xếp bàn/);
  assert.doesNotMatch(html, /Tiện ích song song/);
});

test("actual responsive menu uses native keyboard disclosure, visible current tool and wrapping links", () => {
  const html = render(CustomerToolNavigation, "/thiep/create", "?join=synthetic");
  assert.match(html, /<nav[^>]+aria-label="Công cụ của cặp đôi"/);
  assert.match(html, /<details[^>]*>\s*<summary/); assert.match(html, /Không gian kế hoạch/);
  assert.match(html, /Thiệp &amp; RSVP/); assert.match(html, /break-words/); assert.match(html, /focus-visible:outline/);
  assert.doesNotMatch(html, /<select|role="tablist"|planner-utility-nav/);
});
