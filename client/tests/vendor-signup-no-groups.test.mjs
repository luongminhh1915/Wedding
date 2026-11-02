import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import ts from "typescript";

const javascriptModule = source => `data:text/javascript;base64,${Buffer.from(source).toString("base64")}`;
const authStub = javascriptModule(`export function useAuth() { return { registerCustomer() {}, registerVendor() {}, isRegisteringCustomer: false, isRegisteringVendor: false }; }`);
const intentStub = javascriptModule(`export const safeReturnPath = value => value; export const useIntentStore = { getState: () => ({ returnTo: null }) };`);
const provinceStub = javascriptModule(`import { createElement } from "${import.meta.resolve("react")}"; export default function ProvinceSelect({id, value}) { return createElement('select', { id, value, onChange() {}, 'aria-label': 'Tỉnh / thành phố' }, createElement('option', { value: '' }, 'Chọn địa phương')); }`);
const operatingAreasStub = javascriptModule(`import { createElement } from "${import.meta.resolve("react")}"; export default function OperatingAreasSelect({id}) { return createElement('fieldset', { id }, createElement('legend', null, 'Khu vực hoạt động (tỉnh / thành phố)')); }`);
const source = await readFile(new URL("../src/features/auth/components/RegisterForm.tsx", import.meta.url), "utf8");
let javascript = ts.transpileModule(source, { compilerOptions: {
  target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
} }).outputText;
for (const dependency of ["react", "react/jsx-runtime", "react-router-dom", "lucide-react"])
  javascript = javascript.replaceAll(`from "${dependency}"`, `from "${import.meta.resolve(dependency)}"`);
for (const [dependency, url] of Object.entries({
  "../hooks/useAuth": authStub,
  "../../../store/intentStore": intentStub,
  "./registrationValidation": new URL("../src/features/auth/components/registrationValidation.ts", import.meta.url).href,
  "./ProvinceSelect": provinceStub,
  "./OperatingAreasSelect": operatingAreasStub,
  "./operatingAreasModel": new URL("../src/features/auth/components/operatingAreasModel.ts", import.meta.url).href,
})) javascript = javascript.replaceAll(`from "${dependency}"`, `from "${url}"`);
javascript = javascript.replace('import "./register-page.css";', "");
const { default: RegisterForm } = await import(javascriptModule(javascript));
const render = path => renderToStaticMarkup(createElement(MemoryRouter, { initialEntries: [path] }, createElement(RegisterForm)));

test("actual vendor signup has no group/rate selector and needs no group catalog hook", () => {
  const html = render("/register?role=vendor");
  assert.match(html, /Tên thương hiệu/);
  assert.match(html, /Hotline nhà cung cấp/);
  assert.match(html, /Khu vực hoạt động|tỉnh \/ thành phố/i);
  assert.match(html, /tự chọn loại dịch vụ khi tạo bài/);
  assert.match(html, /Nhóm và mức hoa hồng tương ứng sẽ hiển thị tại đó/);
  assert.doesNotMatch(html, /businessGroupIds|Nhóm dịch vụ kinh doanh|Tải lại chính sách|wm-business-group-option|\b(?:3|8|10|12)%/);
  assert.doesNotMatch(source, /useBusinessGroups|commissionPercent|policyReady|selectedGroups/);
  assert.match(html, /<button type="submit" class=/); // Not disabled by an unavailable group API.
});

test("vendor signup retains one generic acknowledgement, not a selected financial rate", () => {
  const html = render("/register?role=vendor");
  assert.equal((html.match(/type="checkbox"/g) || []).length, 1);
  assert.match(html, /id="register-vendor-policy"/);
  assert.match(html, /Tôi hiểu hoa hồng được xác định theo danh mục dịch vụ/);
  assert.match(html, /Việc đề xuất điều chỉnh riêng cần được nền tảng phê duyệt/);
  assert.doesNotMatch(html, /các nhóm đã chọn|đã ký|chính sách có hiệu lực/);
});

test("customer signup and actor-safe continuation remain separate from Vendor commission choices", () => {
  const html = render("/register?role=customer");
  assert.match(html, /10 câu hỏi ngắn/);
  assert.match(html, /Tạo tài khoản khách hàng/);
  assert.doesNotMatch(html, /register-vendor-policy|Tên thương hiệu|type="checkbox"|businessGroupIds/);
  assert.match(source, /navigate\("\/auth\/continue", \{ replace: true, state: \{ from: returnTo \} \}\)/);
});
