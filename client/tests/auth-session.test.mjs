import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import ts from "typescript";

const asModule = source => `data:text/javascript;base64,${Buffer.from(ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 },
}).outputText).toString("base64")}`;
const navigationUrl = asModule(await readFile(new URL("../src/features/auth/authNavigation.ts", import.meta.url), "utf8"));
const sessionSource = (await readFile(new URL("../src/features/auth/sessionPolicy.ts", import.meta.url), "utf8"))
  .replace('from "./authNavigation"', `from "${navigationUrl}"`);
const sessionUrl = asModule(sessionSource);
const { tokenExpiresAt, isSessionUser, readStoredSession, shouldInvalidateSession } = await import(sessionUrl);

const now = Date.UTC(2026, 9, 17, 12);
const user = { id: "qa-customer", fullName: "Khách QA", email: "qa@example.invalid", phoneNumber: "0900000001", role: "Customer" };
const jwt = payload => `${Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url")}.${Buffer.from(JSON.stringify(payload)).toString("base64url")}.signature`;
const validToken = jwt({ sub: user.id, exp: now / 1000 + 3600 });

test("stored sessions require a well-formed unexpired token and a known actor identity", () => {
  assert.equal(tokenExpiresAt(validToken), now + 3600000);
  const restored = readStoredSession(JSON.stringify(user), validToken, now);
  assert.deepEqual(restored, { user, token: validToken, expiresAt: now + 3600000 });
  for (const role of ["Customer", "VendorOwner", "Moderator", "Finance", "CustomerCare", "SuperAdmin"])
    assert.equal(isSessionUser({ ...user, role }), true);
});

test("malformed storage or incomplete users never crash startup or grant an authenticated session", () => {
  for (const raw of [null, "", "{bad json", "null", "false", "[]", "{}", JSON.stringify({ ...user, role: "Owner" }), JSON.stringify({ ...user, id: "" })])
    assert.equal(readStoredSession(raw, validToken, now), null, String(raw));
  for (const field of ["id", "fullName", "email", "phoneNumber", "role"]) {
    const incomplete = { ...user };
    delete incomplete[field];
    assert.equal(isSessionUser(incomplete), false, field);
    assert.equal(readStoredSession(JSON.stringify(incomplete), validToken, now), null, field);
  }
  for (const value of [null, undefined, "Customer", 42, false, []])
    assert.equal(isSessionUser(value), false);
});

test("expired, missing or unreadable JWTs fail closed, including the exact expiration boundary", () => {
  for (const token of [
    null, "", "not-a-jwt", "a.b", "a.b.c.d", "a..c", "a.invalid-json.c",
    jwt({}), jwt({ exp: "1792238400" }), jwt({ exp: null }), jwt({ exp: -1 }),
    jwt({ exp: now / 1000 - 1 }), jwt({ exp: now / 1000 }),
  ]) assert.equal(readStoredSession(JSON.stringify(user), token, now), null, String(token));
  assert.equal(readStoredSession(JSON.stringify(user), jwt({ exp: now / 1000 + 1 }), now)?.expiresAt, now + 1000);
});

test("only an authenticated 401 for the current token invalidates a session", () => {
  assert.equal(shouldInvalidateSession(401, "/api/auth/me", "Bearer current-token", "current-token"), true);
  assert.equal(shouldInvalidateSession(401, "/api/customer-orders", "Bearer current-token", "current-token"), true);
  assert.equal(shouldInvalidateSession(401, "/api/auth/google/link", "Bearer current-token", "current-token"), true);
  assert.equal(shouldInvalidateSession(401, "/api/auth/me", "Bearer old-token", "new-token"), false);
  assert.equal(shouldInvalidateSession(401, "/api/auth/me", undefined, "current-token"), false);
  assert.equal(shouldInvalidateSession(401, "/api/auth/me", "Bearer current-token", null), false);
  for (const status of [undefined, 400, 403, 404, 409, 429, 500])
    assert.equal(shouldInvalidateSession(status, "/api/auth/me", "Bearer current-token", "current-token"), false);
});

test("failed public login, registration and quick-login attempts do not log out another valid session", () => {
  for (const path of [
    "/api/auth/login", "/api/auth/login?from=/combo", "/api/auth/register-customer", "/api/auth/register-vendor/",
    "/api/auth/providers", "/api/auth/otp/request", "/api/auth/otp/verify", "/api/auth/google", "/api/auth/facebook",
    "https://api.example.invalid/api/auth/login", "/API/AUTH/LOGIN",
  ]) assert.equal(shouldInvalidateSession(401, path, "Bearer current-token", "current-token"), false, path);
});

test("actual auth store clears private cache and intent on logout and ignores old-account verification", async () => {
  const previousStorage = Object.getOwnPropertyDescriptor(globalThis, "localStorage");
  const values = new Map();
  const storage = {
    getItem: key => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, String(value)),
    removeItem: key => values.delete(key),
  };
  let cacheClears = 0;
  let queryCancels = 0;
  let consumedIntents = 0;
  Object.defineProperty(globalThis, "localStorage", { configurable: true, value: storage });
  const fixtureKey = "__weddingAuthStoreTestServices";
  globalThis[fixtureKey] = {
    queryClient: { cancelQueries: () => { queryCancels++; return Promise.resolve(); }, clear: () => { cacheClears++; } },
    useIntentStore: { getState: () => ({ consume: () => { consumedIntents++; return null; } }) },
  };
  try {
    const queryMockUrl = asModule(`export const queryClient = globalThis.${fixtureKey}.queryClient;`);
    const intentMockUrl = asModule(`export const useIntentStore = globalThis.${fixtureKey}.useIntentStore;`);
    const storeSource = (await readFile(new URL("../src/store/authStore.ts", import.meta.url), "utf8"))
      .replace(/from\s+['"]zustand['"]/g, `from "${import.meta.resolve("zustand")}"`)
      .replace(/from\s+['"]\.\.\/services\/queryClient['"]/g, `from "${queryMockUrl}"`)
      .replace(/from\s+['"]\.\.\/features\/auth\/sessionPolicy['"]/g, `from "${sessionUrl}"`)
      .replace(/from\s+['"]\.\/intentStore['"]/g, `from "${intentMockUrl}"`);
    const { useAuthStore } = await import(asModule(storeSource));
    const firstToken = jwt({ sub: user.id, exp: Date.now() / 1000 + 3600 });
    const secondUser = { ...user, id: "qa-vendor", role: "VendorOwner" };
    const secondToken = jwt({ sub: secondUser.id, exp: Date.now() / 1000 + 3600 });
    assert.equal(useAuthStore.getState().token, null);

    useAuthStore.getState().setAuth(user, firstToken);
    const firstRevision = useAuthStore.getState().sessionRevision;
    assert.equal(useAuthStore.getState().validated, true);
    assert.deepEqual(JSON.parse(storage.getItem("user")), user);

    useAuthStore.getState().setAuth(secondUser, secondToken);
    assert.equal(useAuthStore.getState().user.id, secondUser.id);
    assert.ok(useAuthStore.getState().sessionRevision > firstRevision);
    assert.equal(consumedIntents, 1, "account changes drop the previous actor's return intent");
    useAuthStore.getState().confirmSession(user, firstToken);
    assert.equal(useAuthStore.getState().token, secondToken, "late /me from old account is ignored");

    useAuthStore.getState().logout();
    assert.equal(useAuthStore.getState().user, null);
    assert.equal(useAuthStore.getState().token, null);
    assert.equal(useAuthStore.getState().validated, false);
    assert.equal(storage.getItem("token"), null);
    assert.equal(storage.getItem("user"), null);
    assert.equal(consumedIntents, 2);
    assert.equal(cacheClears, 3);
    assert.equal(queryCancels, 3);

    storage.setItem("user", JSON.stringify(user));
    storage.setItem("token", firstToken);
    useAuthStore.getState().syncStorage();
    assert.equal(useAuthStore.getState().user.id, user.id);
    assert.equal(useAuthStore.getState().validated, false, "another tab's session must be checked by /me");
    useAuthStore.getState().confirmSession(user, firstToken);
    assert.equal(useAuthStore.getState().validated, true);
    storage.removeItem("token");
    storage.removeItem("user");
    useAuthStore.getState().syncStorage();
    assert.equal(useAuthStore.getState().user, null, "another tab's logout immediately removes the old actor");
    assert.equal(useAuthStore.getState().token, null);
    assert.equal(cacheClears, 5);
    assert.equal(queryCancels, 5);
  } finally {
    delete globalThis[fixtureKey];
    if (previousStorage) Object.defineProperty(globalThis, "localStorage", previousStorage);
    else delete globalThis.localStorage;
  }
});
