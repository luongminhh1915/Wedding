import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import ts from "typescript";

const hookStateKey = Symbol.for("wedding.tests.service-category-policy");
const state = globalThis[hookStateKey] = {};
const hookStub = `data:text/javascript,${encodeURIComponent(`
  const state = globalThis[Symbol.for("wedding.tests.service-category-policy")];
  export const useBusinessGroups = () => state.references;
  export const useVendorBusinessGroupPolicy = () => state.policy;
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
const policyHelpers = new URL("../src/features/vendor-portal/businessGroupPolicy.ts", import.meta.url).href;
const { ServiceCategoryPolicySummary } = await import(await moduleUrl("../src/features/vendor-portal/components/ServiceCategoryPolicySummary.tsx", {
  "../businessGroupPolicy": policyHelpers,
}));
const { VendorPartnershipPolicy } = await import(await moduleUrl("../src/features/vendor-portal/components/VendorPartnershipPolicy.tsx", {
  "../hooks/useBusinessGroups": hookStub, "../businessGroupPolicy": policyHelpers,
  "../../journey/ui": await moduleUrl("../src/features/journey/ui.tsx"),
}));
const category = (extra = {}) => ({ id: "service-type", name: "Loại dịch vụ từ API", slug: "api-type", listingCount: 0,
  businessGroupId: "group-from-api", businessGroupName: "Nhóm từ API", defaultCommissionRate: .075, ...extra });
const policy = (extra = {}) => ({ groups: [], usesLegacyCommissionRate: false, legacyCommissionRate: null,
  policyAcceptedAt: null, policyVersion: null, ...extra });
const group = (extra = {}) => ({ id: "active-reference", code: "server-only", name: "Nhóm tham khảo từ API", description: "Mô tả chính thức từ API",
  defaultCommissionRate: .046, requiresCommissionBasisApproval: false, policyVersion: "reference-server-version", ...extra });
const query = (data, extra = {}) => ({ data, isPending: false, isError: false, isFetching: false,
  refetch: () => { state.refetches++; }, ...extra });
function reset() { state.refetches = 0; state.policy = query(policy()); state.references = query([group()]); }
const renderSummary = (extra = {}) => renderToStaticMarkup(createElement(ServiceCategoryPolicySummary, {
  category: category(), policy: policy(), pending: false, failed: false, ...extra,
}));
const renderPolicy = () => renderToStaticMarkup(createElement(VendorPartnershipPolicy));
function elements(node, type) {
  if (node == null || typeof node !== "object") return [];
  if (Array.isArray(node)) return node.flatMap(child => elements(child, type));
  return [...(node.type === type ? [node] : []), ...elements(node.props?.children, type)];
}

test("service summary renders canonical API type/group/rate read-only without fabricated defaults", () => {
  const html = renderSummary();
  for (const expected of ["Loại dịch vụ từ API", "Nhóm từ API", "7,5%", "không phải giảm giá", "không cần đăng ký nhóm trước", "snapshot ban đầu"]) assert.ok(html.includes(expected), expected);
  assert.doesNotMatch(html, /<(input|select|textarea|form)\b/);
  for (const rate of [undefined, Number.NaN, -1, 3, 0]) {
    const fallback = renderSummary({ category: category({ defaultCommissionRate: rate }) });
    assert.match(fallback, /Chưa có tỷ lệ hợp lệ/);
    assert.doesNotMatch(fallback, /7,5%|8%|10%|12%/);
  }
});

test("legacy rate stays separate from category reference and A–Z requires approved basis", () => {
  const html = renderSummary({ category: category({ defaultCommissionRate: .12, requiresCommissionBasisApproval: true }),
    policy: policy({ usesLegacyCommissionRate: true, legacyCommissionRate: .08 }) });
  for (const expected of ["12%", "8%", "không tự thay thế tỷ lệ này", "Admin", "không trên toàn bộ ngân sách cưới", "không thay thế phê duyệt"]) assert.ok(html.includes(expected), expected);
});

test("unknown/failed/pending own policy is not represented as a new approved rate", () => {
  for (const extra of [{ policy: undefined }, { failed: true }, { pending: true }]) {
    const html = renderSummary(extra);
    assert.match(html, /Chưa xác nhận được chính sách riêng/);
    assert.match(html, /chỉ là tham khảo/);
    assert.match(html, /7,5%/);
  }
});

test("policy view offers all active reference groups even when shop has zero historical memberships", () => {
  reset(); state.references = query([group(), group({ id: "second", name: "Nhóm khác từ API", defaultCommissionRate: .083 })]);
  const before = structuredClone({ shop: state.policy.data, references: state.references.data });
  const html = renderPolicy();
  for (const expected of ["Nhóm tham khảo từ API", "Nhóm khác từ API", "4,6%", "8,3%", "không cần chọn nhóm khi đăng ký", "không phải nhóm shop đã đăng ký"]) assert.ok(html.includes(expected), expected);
  assert.doesNotMatch(html, /Chưa có nhóm kinh doanh được ghi nhận|<(input|select|textarea|form)\b/);
  assert.deepEqual({ shop: state.policy.data, references: state.references.data }, before);
  assert.equal(state.refetches, 0);
});

test("reference list does not depend on a stale historical membership or legacy rate", () => {
  reset(); state.policy = query(policy({ groups: [group({ name: "Nhóm lịch sử không còn public" })], usesLegacyCommissionRate: true, legacyCommissionRate: .08 }));
  const html = renderPolicy();
  assert.match(html, /Nhóm tham khảo từ API/); assert.doesNotMatch(html, /Nhóm lịch sử không còn public/);
  assert.match(html, /Tỷ lệ cũ đang được lưu: 8%/);
  assert.match(html, /không tự chuyển tỷ lệ này/);
});

test("generic signup acknowledgement is historical metadata, not signature or all-group approval", () => {
  reset(); state.policy = query(policy({ policyAcceptedAt: "2026-10-21T00:00:00Z", policyVersion: "2026-10-21-service-category-v1" }));
  const html = renderPolicy();
  for (const expected of ["2026-10-21-service-category-v1", "Thông tin xác nhận đã lưu", "không đồng nghĩa đã ký hợp đồng", "chấp thuận mọi nhóm", "kích hoạt một chính sách riêng"]) assert.ok(html.includes(expected), expected);
  assert.doesNotMatch(html, /Chưa ghi nhận thời điểm xác nhận chính sách nhóm mới/);
});

test("own policy failure has read-only retry while public reference rates remain explicitly nonbinding", () => {
  reset(); state.policy = query(policy({ usesLegacyCommissionRate: true, legacyCommissionRate: .08 }), { isError: true });
  const html = renderPolicy();
  assert.match(html, /Chưa tải được chính sách riêng/); assert.match(html, /4,6%/);
  assert.doesNotMatch(html, /Tỷ lệ cũ đang được lưu: 8%/);
  const retry = elements(VendorPartnershipPolicy(), "button").find(button => button.props.children === "Thử lại chính sách của shop");
  assert.ok(retry); retry.props.onClick(); assert.equal(state.refetches, 1);
});

test("reference loading/error/empty are separate and retry never fabricates rates or membership", () => {
  reset(); state.references = query(undefined, { isPending: true });
  assert.match(renderPolicy(), /role="status"[^>]*>Đang tải các nhóm hoa hồng tham khảo/);
  state.references = query([group()], { isError: true });
  const error = renderPolicy();
  assert.match(error, /Chưa tải được nhóm hoa hồng tham khảo/); assert.doesNotMatch(error, /4,6%/);
  const retry = elements(VendorPartnershipPolicy(), "button").find(button => button.props.children === "Thử lại nhóm tham khảo");
  assert.ok(retry); retry.props.onClick(); assert.equal(state.refetches, 1);
  state.references = query([]);
  const empty = renderPolicy();
  assert.match(empty, /Chưa có nhóm hoa hồng đang hoạt động được cấu hình/); assert.doesNotMatch(empty, /role="alert"/);
});

test("own-policy loading never hides references or invents a policy acknowledgement", () => {
  reset(); state.policy = query(undefined, { isPending: true });
  const html = renderPolicy();
  assert.match(html, /Đang kiểm tra chính sách riêng/); assert.match(html, /4,6%/);
  assert.doesNotMatch(html, /Thông tin xác nhận đã lưu|Chưa có thời điểm xác nhận điều khoản được ghi nhận/);
});
