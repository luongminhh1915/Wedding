import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import ts from "typescript";

// Render actual panels with controllable read hooks; no network, DB or business mutation.
const stateKey = Symbol.for("wedding.tests.admin-read-query");
const state = globalThis[stateKey] = {};
const apiStub = `data:text/javascript,${encodeURIComponent(`
  const state = globalThis[Symbol.for("wedding.tests.admin-read-query")];
  export const apiError = e => e?.response?.data?.message || "Không thể kết nối API.";
  export const useAdminKpi = () => state.kpi;
  export const useAuditLogs = enabled => { state.auditEnabled = enabled; return state.audit; };
  export const useVerifications = () => state.verifications;
  export const useDisputes = () => state.disputes;
  export const useSettlements = () => state.settlements;
  const mutation = () => ({ isError: false, isPending: false, mutate: () => { state.mutations++; } });
  export const usePaySettlement = mutation, useRunSettlement = mutation,
    useResolveDispute = mutation, useReviewVerification = mutation;
`)}`;
const authStub = `data:text/javascript,${encodeURIComponent(`
  export const useAuthStore = selector => selector({ user: { role: globalThis[Symbol.for("wedding.tests.admin-read-query")].role } });
`)}`;
async function moduleUrl(file, aliases = {}) {
  const source = await readFile(new URL(file, import.meta.url), "utf8");
  let javascript = ts.transpileModule(source, { compilerOptions: {
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  for (const dependency of ["react", "react/jsx-runtime", "lucide-react"])
    javascript = javascript.replaceAll(`from "${dependency}"`, `from "${import.meta.resolve(dependency)}"`);
  for (const [dependency, target] of Object.entries(aliases))
    javascript = javascript.replaceAll(`from "${dependency}"`, `from "${target}"`);
  return `data:text/javascript;base64,${Buffer.from(javascript).toString("base64")}`;
}
const statusUrl = await moduleUrl("../src/features/journey/admin/AdminQueryReadState.tsx", { "../api": apiStub });
const uiUrl = await moduleUrl("../src/features/journey/ui.tsx");
const { AdminQueryReadState } = await import(statusUrl);
const panels = await import(await moduleUrl("../src/features/journey/admin/AdminJourneyPanels.tsx", {
  "../api": apiStub, "../ui": uiUrl, "./AdminQueryReadState": statusUrl,
  "../format": new URL("../src/features/journey/format.ts", import.meta.url).href,
  "../../../store/authStore": authStub,
}));
const query = (extra = {}) => ({ data: [], isPending: false, isError: false, isFetching: false,
  error: null, refetch: () => { state.readRetries++; }, ...extra });
function reset() {
  Object.assign(state, { role: "SuperAdmin", mutations: 0, readRetries: 0,
    kpi: query({ data: undefined }), audit: query(), verifications: query(), disputes: query(), settlements: query() });
}
const render = component => renderToStaticMarkup(createElement(component));
function elements(node, type) {
  if (node == null || typeof node !== "object") return [];
  if (Array.isArray(node)) return node.flatMap(child => elements(child, type));
  return [...(node.type === type ? [node] : []), ...elements(node.props?.children, type)];
}
const kpi = () => ({ customers: 2, vendors: 3, verifiedVendors: 1, pendingVerifications: 0,
  leadsThisMonth: 4, leadsSlaBreached: 0, contractsConfirmed: 1, gmv: 1000000,
  commissionPending: 100000, commissionPaid: 0, openDisputes: 1, comboOrders: 1,
  upcomingAppointments: 0, leadToContractRate: 25 });

test("KPI request failure renders an actionable error, not perpetual loading or invented zeroes", () => {
  reset(); state.kpi = query({ data: undefined, isError: true, error: { response: { data: { message: "API tạm ngừng" } } } });
  const html = render(panels.AdminKpiPanel);
  for (const value of ['role="alert"', "Chưa tải được tổng quan vận hành", "API tạm ngừng", "Thử lại"]) assert.ok(html.includes(value), value);
  assert.doesNotMatch(html, /Đang tải|GMV hợp đồng|HĐ hiệu lực/);
});

test("pending queries render a live loading state, never successful empty results", () => {
  for (const [component, key, empty] of [
    [panels.AdminKpiPanel, "kpi", "Chưa có dữ liệu tổng quan"],
    [panels.VerificationQueue, "verifications", "Không có hồ sơ"],
    [panels.DisputeDesk, "disputes", "Không có tranh chấp"],
    [panels.SettlementsPanel, "settlements", "Chưa có bảng kê"],
  ]) {
    reset(); state[key] = query({ data: undefined, isPending: true, isFetching: true });
    const html = render(component);
    assert.ok(html.includes('role="status"'), key); assert.ok(html.includes("Đang tải"), key);
    assert.ok(!html.includes(empty), key); assert.ok(!html.includes('role="alert"'), key);
  }
});

test("verification, dispute and settlement read errors are not empty success or stale actionable rows", () => {
  for (const [component, key, empty] of [
    [panels.VerificationQueue, "verifications", "Không có hồ sơ"],
    [panels.DisputeDesk, "disputes", "Không có tranh chấp"],
    [panels.SettlementsPanel, "settlements", "Chưa có bảng kê"],
  ]) {
    reset(); state[key] = query({ data: [{ id: "stale-row" }], isError: true });
    const html = render(component);
    assert.ok(html.includes('role="alert"'), key); assert.ok(html.includes("Thử lại"), key);
    assert.ok(!html.includes(empty), key); assert.ok(!html.includes("stale-row"), key);
    assert.equal(state.mutations, 0);
  }
});

test("successful empty results remain distinct and no retry control is fabricated", () => {
  for (const [component, empty] of [
    [panels.AdminKpiPanel, "Chưa có dữ liệu tổng quan vận hành"],
    [panels.VerificationQueue, "Không có hồ sơ"],
    [panels.DisputeDesk, "Không có tranh chấp"],
    [panels.SettlementsPanel, "Chưa có bảng kê"],
  ]) {
    reset(); const html = render(component);
    assert.ok(html.includes(empty), empty); assert.doesNotMatch(html, /role="alert"|role="status"|Thử lại/);
  }
});

test("successful KPI and list reads preserve existing values, status and permission-based actions", () => {
  reset(); state.kpi = query({ data: kpi() });
  assert.match(render(panels.AdminKpiPanel), /GMV hợp đồng|1 triệu/);
  state.verifications = query({ data: [{ id: "verification", vendorName: "Shop QA", businessLicenseNumber: "QA-only",
    documentUrls: [], createdAt: "2026-10-21T00:00:00Z", status: "Submitted" }] });
  assert.match(render(panels.VerificationQueue), /Shop QA/);
  assert.match(render(panels.VerificationQueue), /Yêu cầu bổ sung/);
  state.disputes = query({ data: [{ id: "dispute", code: "TC-QA", contractCode: "HD-QA", openedByRole: "Customer",
    status: "Open", reason: "QA reason", slaDeadline: "2026-10-24T00:00:00Z" }] });
  assert.match(render(panels.DisputeDesk), /TC-QA/);
  assert.match(render(panels.DisputeDesk), /Hủy tài chính/);
  state.role = "CustomerCare";
  assert.doesNotMatch(render(panels.DisputeDesk), /Hủy tài chính/);
  assert.match(render(panels.DisputeDesk), /Giữ hợp đồng/);
  assert.equal(state.mutations, 0);
});

test("retry only calls the failed read refetch and is disabled while fetching", () => {
  reset();
  const current = query({ isError: true });
  const button = elements(AdminQueryReadState({ query: current, label: "hồ sơ xác minh" }), "button")[0];
  assert.equal(button.props.type, "button"); assert.equal(button.props.disabled, false);
  button.props.onClick(); assert.equal(state.readRetries, 1); assert.equal(state.mutations, 0);
  const pending = elements(AdminQueryReadState({ query: { ...current, isFetching: true }, label: "hồ sơ xác minh" }), "button")[0];
  assert.equal(pending.props.disabled, true); assert.equal(pending.props.children, "Đang thử lại...");
  assert.equal(AdminQueryReadState({ query: query(), label: "hồ sơ" }), null);
});

test("audit read failure is visible independently without hiding successful KPI or exposing staff-only logs", () => {
  reset(); state.kpi = query({ data: kpi() }); state.audit = query({ isError: true });
  const html = render(panels.AdminKpiPanel);
  assert.match(html, /GMV hợp đồng/); assert.match(html, /Chưa tải được nhật ký thao tác/);
  assert.equal(state.auditEnabled, true);
  state.role = "Moderator";
  const moderator = render(panels.AdminKpiPanel);
  assert.match(moderator, /GMV hợp đồng/); assert.doesNotMatch(moderator, /Nhật ký thao tác|Thử lại/);
  assert.equal(state.auditEnabled, false);
});
