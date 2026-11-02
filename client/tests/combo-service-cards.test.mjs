import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import ts from "typescript";

async function transpile(relativeFile, aliases = {}) {
  const source = await readFile(new URL(relativeFile, import.meta.url), "utf8");
  let javascript = ts.transpileModule(source, { compilerOptions: {
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext, jsx: ts.JsxEmit.ReactJSX,
  } }).outputText;
  for (const dependency of ["react", "react/jsx-runtime", "react-router-dom", "lucide-react"])
    javascript = javascript.replaceAll(`from "${dependency}"`, `from "${import.meta.resolve(dependency)}"`);
  for (const [dependency, target] of Object.entries(aliases))
    javascript = javascript.replaceAll(`from "${dependency}"`, `from "${target}"`);
  return `data:text/javascript;base64,${Buffer.from(javascript).toString("base64")}`;
}
const comboPhoto = await transpile("../src/features/journey/combo/ComboPhoto.tsx", {
  "./comboMedia": new URL("../src/features/journey/combo/comboMedia.ts", import.meta.url).href,
});
const moduleUrl = await transpile("../src/features/journey/combo/ComboServiceCards.tsx", {
  "../format": new URL("../src/features/journey/format.ts", import.meta.url).href,
  "./cartModel": new URL("../src/features/journey/combo/cartModel.ts", import.meta.url).href,
  "./ComboPhoto": comboPhoto,
});
const { ComboServiceCards } = await import(moduleUrl);
const item = (id, unit = "Package") => ({
  serviceItemId: id, listingId: `listing-${id}`, vendorId: "shop", vendorName: "Studio QA",
  title: `Dịch vụ ${id}`, price: 1_250_000, unit, minGuests: 1, maxGuests: 500,
  imageUrl: `https://images.example.invalid/${id}.jpg`, galleryUrls: [], variants: [], addOns: [],
});
const selection = id => ({ serviceItemId: id, categoryId: "photo", variantIds: [], addOnIds: [], hours: 1 });
const props = (extra = {}) => ({ items: [item("one")], categoryId: "photo", selections: [], guestCount: 203,
  fixedCart: false, onConfigure: () => {}, ...extra });
const render = input => renderToStaticMarkup(createElement(MemoryRouter, null, createElement(ComboServiceCards, props(input))));
function elements(node, type) {
  if (node == null || typeof node !== "object") return [];
  if (Array.isArray(node)) return node.flatMap(child => elements(child, type));
  return [...(node.type === type ? [node] : []), ...elements(node.props?.children, type)];
}

test("shared service cards render supplied catalog images, names, price, capacity and detail links", () => {
  const html = render();
  assert.match(html, /combo-product-grid/);
  assert.match(html, /Dịch vụ one/);
  assert.match(html, /Studio QA/);
  assert.match(html, /1\.250\.000đ/);
  assert.match(html, /Phục vụ 1–500 khách/);
  assert.match(html, /\/gói/);
  assert.match(html, /href="\/listings\/listing-one"/);
  assert.match(html, /src="https:\/\/images\.example\.invalid\/one\.jpg"/);
  assert.match(html, /Chọn dịch vụ/);
});

test("selection and same-category replacements retain distinct labels without mutating the cart", () => {
  const selections = [selection("one")], original = structuredClone(selections);
  const html = render({ items: [item("one"), item("two")], selections });
  assert.match(html, /is-selected/);
  assert.match(html, /Trong giỏ/);
  assert.match(html, /Tùy chỉnh \/ đổi/);
  assert.match(html, /Chọn để thay dịch vụ/);
  assert.deepEqual(selections, original);
  assert.doesNotMatch(render({ selections: [{ ...selection("other"), categoryId: "decor" }] }), /Chọn để thay dịch vụ/);
});

test("Table card quantities round up while other units and zero guests do not invent a table count", () => {
  assert.match(render({ items: [item("table", "Table")] }), /203 khách → 21 bàn/);
  assert.match(render({ items: [item("table", "Table")] }), /\/bàn/);
  assert.doesNotMatch(render({ items: [item("table", "Table")], guestCount: 0 }), /khách →/);
  assert.doesNotMatch(render({ items: [item("package", "Package")] }), /khách →/);
});

test("configure callbacks retain the actual service/category and fixed package cannot invoke configuration", () => {
  const configured = [], source = item("one");
  const onConfigure = (...value) => configured.push(value);
  const unlocked = ComboServiceCards(props({ items: [source], onConfigure }));
  const button = elements(unlocked, "button")[0];
  assert.equal(button.props.disabled, false);
  button.props.onClick();
  assert.equal(configured[0][0], source);
  assert.equal(configured[0][1], "photo");
  const locked = ComboServiceCards(props({ items: [source], fixedCart: true, onConfigure }));
  const lockedButton = elements(locked, "button")[0];
  assert.equal(lockedButton.props.disabled, true);
  lockedButton.props.onClick();
  assert.equal(configured.length, 1);
  const html = render({ fixedCart: true });
  assert.match(html, /disabled=""/);
  assert.match(html, /Giữ nguyên gói đã chọn/);
});

test("empty preview is an empty grid so parent controls actual loading/error/empty states", () => {
  const html = render({ items: [] });
  assert.equal(html, '<div class="combo-product-grid"></div>');
});
