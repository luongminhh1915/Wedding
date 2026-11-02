import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const indexCss = await readFile(new URL("../src/index.css", import.meta.url), "utf8");
const themeCss = await readFile(new URL("../src/styles/marketplace-experience.css", import.meta.url), "utf8");
const legacyPageCss = await readFile(new URL("../src/styles/wedding-match.css", import.meta.url), "utf8");
const registrationCss = await readFile(new URL("../src/features/auth/components/register-page.css", import.meta.url), "utf8");
const layout = await readFile(new URL("../src/components/WeddingMatchLayout.tsx", import.meta.url), "utf8");
const mascot = await readFile(new URL("../public/wedding-match/be-hy-mascot.png", import.meta.url));
const mark = await readFile(new URL("../public/wedding-match/mark.svg", import.meta.url), "utf8");
const journeyCss = await readFile(new URL("../src/styles/weddy.css", import.meta.url), "utf8");

const weddingMatchPalette = ["#233a42", "#fcf6ea", "#d49587", "#e4b7a6", "#e38c82", "#f1dba2", "#98ab98"];

test("the shared app shell exposes the approved Wedding Match palette to every actor route", () => {
  assert.match(layout, /className="wm-theme"/);
  for (const color of weddingMatchPalette) assert.ok(themeCss.includes(color), `missing brand color ${color}`);
  assert.match(themeCss, /--color-slate-900:\s*#233a42/);
  assert.match(themeCss, /--color-slate-50:\s*#fffcf5/);
  assert.match(themeCss, /--color-slate-400:\s*#66758b/);
  assert.match(themeCss, /--color-slate-950:\s*#233a42/);
  assert.match(themeCss, /--color-rose-500:\s*#e38c82/);
  assert.match(themeCss, /--color-pink-500:\s*#e38c82/);
  assert.match(themeCss, /--color-indigo-600:\s*#58735e/);
  assert.match(themeCss, /--color-blue-50:\s*#f2f5f1/);
  assert.match(themeCss, /--color-purple-600:\s*#b86661/);
  assert.match(themeCss, /--color-amber-200:\s*#f1dba2/);
  assert.match(themeCss, /--color-red-400:\s*#e38c82/);
  assert.match(themeCss, /--color-gray-900:\s*#233a42/);
  assert.match(themeCss, /\[class~="bg-rose-500"\]\[class~="text-white"\][\s\S]*?color:\s*#233a42/);
  assert.equal(mascot.subarray(0, 8).toString("hex"), "89504e470d0a1a0a");
});

test("all shared screens use a blush navbar, warm canvas and coral primary actions", () => {
  assert.match(indexCss, /background:\s*#fcf6ea/);
  assert.match(themeCss, /\.wm-header\s*\{[\s\S]*?background:\s*linear-gradient\(105deg, #f1dba2 0%, #e4b7a6 52%, #e38c82 100%\)/);
  assert.match(themeCss, /\.wm-button\s*\{\s*background:\s*#e38c82;\s*border-color:\s*#e38c82;\s*color:\s*#233a42/);
  assert.match(themeCss, /--paper:\s*#fcf6ea/);
  assert.match(themeCss, /--wm-sage:\s*#98ab98/);
  assert.match(themeCss, /--wm-champagne:\s*#f1dba2/);
  assert.match(themeCss, /\.wm-header\s*\{[\s\S]*?linear-gradient\(105deg, #f1dba2 0%, #e4b7a6 52%, #e38c82 100%\)/);
  assert.match(themeCss, /\.wm-mascot-image \{ mix-blend-mode: multiply; \}/);
  assert.match(themeCss, /\.wm-header \.wm-login \{ background: #b86661; color: #fffcf5/);
  assert.match(themeCss, /\.wm-header\s*\{[\s\S]*?linear-gradient\(105deg, #f1dba2 0%, #e4b7a6 52%, #e38c82 100%\)/);
  assert.match(journeyCss, /\.wd \.btn-primary \{ background: linear-gradient\(135deg, #e38c82, #d49587\); color: #233a42/);
  assert.match(journeyCss, /\.wd-header-bar[^\n]*linear-gradient\(105deg, #f1dba2 0%, #e4b7a6 52%, #e38c82 100%\)/);
  assert.match(mark, /stroke="#233A42"/);
  assert.match(mark, /fill="#E38C82"/);
  assert.match(legacyPageCss, /--color-rose-500:\s*#e38c82/);
  assert.match(registrationCss, /background: #fffcf5; border: 1px solid #eadbd4/);
  assert.doesNotMatch(`${indexCss}\n${legacyPageCss}\n${themeCss}\n${journeyCss}\n${registrationCss}\n${mascot}\n${mark}`, /#25334c|#f2efe4|#d2ac2b|rgba\(37,51,76/i);
});
