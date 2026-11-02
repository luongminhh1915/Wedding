import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = async path => readFile(new URL(path, import.meta.url), "utf8");

test("Google and Facebook controls remain visible while unavailable providers stay disabled", async () => {
  const quickLogin = await source("../src/features/journey/planner/QuickLogin.tsx");
  assert.match(quickLogin, /Tiếp tục với Google/);
  assert.match(quickLogin, /Tiếp tục với Facebook/);
  assert.match(quickLogin, /disabled/);
  assert.match(quickLogin, /facebookReady/);
  assert.match(quickLogin, /providers\.isError/);
  assert.match(quickLogin, /googleSdkError/);
});

test("Facebook login sends a token to the API and requests only public profile without email permission", async () => {
  const quickLogin = await source("../src/features/journey/planner/QuickLogin.tsx");
  const api = await source("../src/features/journey/api.ts");
  assert.match(quickLogin, /scope: "public_profile"/);
  assert.doesNotMatch(quickLogin, /public_profile,email/);
  assert.match(quickLogin, /authApi\.facebook\(accessToken\)/);
  assert.match(api, /\/api\/auth\/facebook/);
  assert.match(api, /facebookGraphApiVersion/);
});

test("Facebook account linking is Customer-only, session-bound, and uses explicit link endpoints", async () => {
  const accountPage = await source("../src/features/account/AccountPage.tsx");
  const component = await source("../src/features/account/FacebookAccountLink.tsx");
  const api = await source("../src/features/journey/api.ts");
  assert.match(accountPage, /FacebookAccountLink/);
  assert.match(component, /role !== "Customer"/);
  assert.match(component, /sessionRevision/);
  assert.match(api, /\/api\/auth\/facebook\/link-status/);
  assert.match(api, /\/api\/auth\/facebook\/link/);
});
