import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const readSource = path => readFile(new URL(path, import.meta.url), "utf8");
const [layout, login, routes, styles] = await Promise.all([
  readSource("../src/components/WeddingMatchLayout.tsx"),
  readSource("../src/features/auth/components/LoginForm.tsx"),
  readSource("../src/routes/AppRoutes.tsx"),
  readSource("../src/styles/wedding-match.css"),
]);

test("header login navigates to the standalone login route and preserves the current destination", () => {
  const header = layout.slice(layout.indexOf("export function WeddingMatchHeader"), layout.indexOf("export function WeddingMatchFooter"));
  assert.match(header, /navigate\("\/login", \{ state: \{ from: location\.pathname \+ location\.search \} \}\)/);
  assert.match(header, /useIntentStore\.getState\(\)\.remember\(location\.pathname \+ location\.search\)/);
  assert.doesNotMatch(header, /AuthModal|authOpen|wm-auth-dialog/);
  assert.match(routes, /path="\/login" element={<LoginForm \/>}/);
});

test("login page no longer exposes development test accounts or preset credentials", () => {
  assert.doesNotMatch(login, /Quick Test Accounts Box|Tài khoản thử nghiệm|demo-login|customer@wedding\.com|vendor\.studio@wedding\.com|123456/);
  assert.doesNotMatch(styles, /\.demo-login/);
  assert.match(login, /<QuickLogin onComplete=\{onComplete\} \/>/);
});

test("forgot-password action is always visible but cannot submit until email recovery is configured", () => {
  assert.match(login, /<button[^>]*onClick=\{\(\) => \{ setRecovering\(true\)/);
  assert.match(login, /Quên mật khẩu\?/);
  assert.match(login, /Khôi phục mật khẩu hiện chưa được cấu hình gửi email/);
  assert.match(login, /disabled=\{!passwordRecoveryReady \|\| !email\.trim\(\) \|\| recoveryBusy\}/);
  assert.match(login, /providers\.isError[\s\S]*Kiểm tra lại/);
});
