import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import ts from "typescript";

// Exercise real JSX and handlers without a browser, API, DB or business mutation.
// Hook adapters let us inspect the existing native dialog shell's callbacks;
// native focus, showModal and keyboard behavior still require browser QA.
const dataModule = source => `data:text/javascript;base64,${Buffer.from(source).toString("base64")}`;
const hooks = dataModule("export const useRef = () => ({ current: null }); export const useEffect = () => {};");
async function moduleUrl(file, aliases = {}) {
  const source = await readFile(new URL(file, import.meta.url), "utf8");
  let javascript = ts.transpileModule(source, { compilerOptions: {
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  for (const [dependency, target] of Object.entries({
    react: import.meta.resolve("react"), "react/jsx-runtime": import.meta.resolve("react/jsx-runtime"),
    "lucide-react": import.meta.resolve("lucide-react"), ...aliases,
  })) javascript = javascript.replaceAll(`from "${dependency}"`, `from "${target}"`);
  assert.doesNotMatch(javascript, /from\s+["']\.\.?\//, "All runtime dependencies are mapped");
  return dataModule(javascript);
}
const shellUrl = await moduleUrl("../src/features/journey/combo/ComboDialog.tsx", { react: hooks });
const { ComboDialog } = await import(shellUrl);
const lifecycle = globalThis[Symbol.for('wedding.tests.combo-dialog-lifecycle')] = {};
const lifecycleHooks = dataModule(`
  const state = globalThis[Symbol.for('wedding.tests.combo-dialog-lifecycle')];
  export const useRef = () => ({ current: state.dialog });
  export const useEffect = effect => { state.effect = effect; };
`);
const { ComboDialog: LifecycleDialog } = await import(await moduleUrl(
  '../src/features/journey/combo/ComboDialog.tsx', { react: lifecycleHooks },
));
const { ApprovalConfirmationDialog } = await import(await moduleUrl(
  "../src/features/admin/ApprovalConfirmationDialog.tsx", { "../journey/combo/ComboDialog": shellUrl },
));
const details = Object.freeze({ typeLabel: "Combo NCC", title: "Ngày vui tinh gọn", vendorName: "Shop QA", versionLabel: "Phiên bản #3" });
function setup(extra = {}) {
  const calls = { confirm: 0, cancel: 0 };
  const props = { details, onConfirm: () => { calls.confirm++; }, onCancel: () => { calls.cancel++; }, ...extra };
  return { calls, props, tree: ApprovalConfirmationDialog(props) };
}
function elements(node, type) {
  if (!node || typeof node !== "object") return [];
  if (Array.isArray(node)) return node.flatMap(child => elements(child, type));
  return [...(node.type === type ? [node] : []), ...elements(node.props?.children, type)];
}
const render = props => renderToStaticMarkup(createElement(ApprovalConfirmationDialog, props));

test("approval dialog names the exact content, provider and version without approving on render", () => {
  const { calls, props } = setup();
  const html = render(props);
  for (const value of ["<dialog", 'aria-label="Xác nhận phê duyệt"', "Bạn có chắc chắn", "Combo NCC",
    "Ngày vui tinh gọn", "Shop QA", "Phiên bản #3", "Xác nhận duyệt", "Hủy", "không thay đổi hoa hồng", "không tự bật lại"])
    assert.ok(html.includes(value), value);
  assert.deepEqual(calls, { confirm: 0, cancel: 0 });
});

test("cancel is the autofocus non-submit action and never calls approval", () => {
  const { calls, tree } = setup();
  const [cancel, confirm] = elements(tree, "button");
  assert.equal(cancel.props.type, "button"); assert.equal(cancel.props.autoFocus, true);
  assert.equal(cancel.props.children, "Hủy"); assert.equal(confirm.props.type, "button");
  cancel.props.onClick();
  assert.deepEqual(calls, { confirm: 0, cancel: 1 });
});

test("only the explicit confirm action invokes the supplied approval callback", () => {
  const { calls, tree } = setup();
  elements(tree, "button")[1].props.onClick();
  assert.deepEqual(calls, { confirm: 1, cancel: 0 });
});

test("shell close, Escape and backdrop delegate to cancel without approval", () => {
  const { calls, tree } = setup();
  tree.props.onClose();
  const shell = ComboDialog(tree.props);
  let prevented = false;
  shell.props.onCancel({ preventDefault() { prevented = true; } });
  const backdrop = {};
  shell.props.onClick({ target: backdrop, currentTarget: backdrop });
  shell.props.onClick({ target: {}, currentTarget: {} });
  assert.equal(prevented, true);
  assert.deepEqual(calls, { confirm: 0, cancel: 3 });
});

test("busy disables both buttons and blocks confirm, cancel, shell close, Escape and backdrop handlers", () => {
  const { calls, tree, props } = setup({ busy: true });
  const buttons = elements(tree, "button");
  assert.equal(buttons.length, 2);
  for (const button of buttons) { assert.equal(button.props.disabled, true); button.props.onClick(); }
  assert.equal(buttons[1].props.children, "Đang duyệt...");
  tree.props.onClose();
  const shell = ComboDialog(tree.props);
  shell.props.onCancel({ preventDefault() {} });
  const backdrop = {};
  shell.props.onClick({ target: backdrop, currentTarget: backdrop });
  elements(shell, "button")[0].props.onClick();
  assert.deepEqual(calls, { confirm: 0, cancel: 0 });
  assert.ok(render(props).includes("Đang duyệt..."));
});

test("API errors remain visible while allowing a later explicit retry or cancel", () => {
  const { calls, tree, props } = setup({ error: "Phiên bản đã thay đổi. Hãy tải lại." });
  const html = render(props);
  assert.ok(html.includes('role="alert"')); assert.ok(html.includes("Phiên bản đã thay đổi"));
  assert.equal(elements(tree, "button")[1].props.disabled, false);
  assert.deepEqual(calls, { confirm: 0, cancel: 0 });
});

test("legacy/unnamed provider fields are omitted and untrusted labels are escaped", () => {
  const { props } = setup({ details: { typeLabel: "Dịch vụ", title: '<script>alert("x")</script>', vendorName: null } });
  const html = render(props);
  assert.ok(html.includes("&lt;script&gt;")); assert.doesNotMatch(html, /<script>|Nhà cung cấp|Phiên bản đang xem/);
});

for (const connected of [true, false]) {
  test(`shared native dialog restores scroll and ${connected ? 'returns focus to its opener' : 'skips a removed opener'}`, () => {
    const previousDocument = Object.getOwnPropertyDescriptor(globalThis, 'document');
    const calls = [];
    const opener = { isConnected: connected, focus: options => calls.push(['focus', options]) };
    const body = { style: { overflow: 'scroll' } };
    Object.defineProperty(globalThis, 'document', { configurable: true, value: { activeElement: opener, body } });
    lifecycle.dialog = { showModal: () => calls.push(['show']), close: () => calls.push(['close']) };
    try {
      LifecycleDialog({ title: 'Khoản chi', onClose() {}, children: null });
      const cleanup = lifecycle.effect();
      assert.equal(body.style.overflow, 'hidden');
      assert.deepEqual(calls, [['show']]);
      cleanup();
      assert.equal(body.style.overflow, 'scroll');
      assert.deepEqual(calls, connected ? [['show'], ['close'], ['focus', { preventScroll: true }]] : [['show'], ['close']]);
    } finally {
      if (previousDocument) Object.defineProperty(globalThis, 'document', previousDocument);
      else delete globalThis.document;
    }
  });
}
