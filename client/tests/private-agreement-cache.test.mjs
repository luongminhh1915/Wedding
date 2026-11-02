import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { QueryClient, QueryObserver } from "@tanstack/react-query";
import { refreshPrivateAgreementQueries } from "../src/features/private-agreements/privateAgreementCacheModel.ts";

const current = ["private-service-agreements", "customer-a", "Customer", "combo", "combo-a", null];
const sibling = [...current.slice(0, 5), "lead-a"];
const refreshKeys = [
  ["customer-order-page", "customer-a", "quote", 2, 5, "pending"],
  ["customer-order-page", "customer-a", "contract", 1, 10, "all"],
  ["customer-order-journey", "customer-a", ["combo-a"]],
  ["customer-contracts", "customer-a", 1, 50],
  ["vendor-contracts", "PendingVerification", "contract-a"],
  ["my-contracts-lite"], ["quotes", "lead-a"], ["quotes", "all"], sibling,
];
const preserved = [
  current, ["private-service-agreements", "customer-a", "Customer", "combo", "other-combo", null],
  ["private-service-agreements", "customer-b", "Customer", "combo", "combo-a", null],
  ["my-combos", "customer-a", "page", 1, 5, "all"], ["combo-price", { templateId: "combo-a" }],
  ["cart", "customer-a"], ["combo-catalog-page", { page: 1 }], ["settlements", "2026-10"],
  ["commission-credits"], ["wedding-budget"], ["payment-unmatched"],
];

test("private agreement success invalidates lifecycle reads and same-target sibling contexts only", async () => {
  const client = new QueryClient();
  try {
    for (const key of [...refreshKeys, ...preserved]) client.setQueryData(key, { frozen: key });
    await refreshPrivateAgreementQueries(client, current);
    for (const key of refreshKeys) assert.equal(client.getQueryState(key).isInvalidated, true, JSON.stringify(key));
    for (const key of preserved) assert.equal(client.getQueryState(key).isInvalidated, false, JSON.stringify(key));
    for (const key of [...refreshKeys, ...preserved]) assert.deepEqual(client.getQueryData(key), { frozen: key });
  } finally { client.clear(); }
});

test("active quote/contract/order observers refetch after a decision without resetting the authoritative form", async () => {
  const client = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, retry: false } } });
  const stops = []; const reads = new Map();
  try {
    for (const key of [...refreshKeys, current]) {
      client.setQueryData(key, { version: "before" });
      const observer = new QueryObserver(client, { queryKey: key, queryFn: async () => {
        reads.set(JSON.stringify(key), (reads.get(JSON.stringify(key)) ?? 0) + 1);
        return { version: "after" };
      } });
      stops.push(observer.subscribe(() => {}));
    }
    client.setQueryData(current, { version: "mutation-response", draft: "preserved" });
    await refreshPrivateAgreementQueries(client, current);
    for (const key of refreshKeys) {
      assert.equal(reads.get(JSON.stringify(key)), 1);
      assert.deepEqual(client.getQueryData(key), { version: "after" });
    }
    assert.equal(reads.get(JSON.stringify(current)), undefined);
    assert.deepEqual(client.getQueryData(current), { version: "mutation-response", draft: "preserved" });
  } finally { stops.forEach(stop => stop()); client.clear(); }
});

test("cache scopes match real query callers and only successful mutations trigger refresh", () => {
  const source = file => readFileSync(new URL(file, import.meta.url), "utf8");
  const api = source("../src/features/private-agreements/privateAgreementApi.ts");
  assert.match(api, /onSuccess: async data => \{\s*cache\.setQueryData\(queryKey, data\);\s*await refreshPrivateAgreementQueries\(cache, queryKey\);/);
  assert.doesNotMatch(api, /onError:.*refreshPrivateAgreementQueries|onSettled:.*refreshPrivateAgreementQueries/s);
  for (const [file, key] of [
    ["../src/features/journey/customerOrders/customerOrderApi.ts", "customer-order-page"],
    ["../src/features/journey/customerOrders/customerOrderApi.ts", "customer-order-journey"],
    ["../src/features/customer/hooks/useCustomerContracts.ts", "customer-contracts"],
    ["../src/features/vendor-portal/hooks/useVendorContracts.ts", "vendor-contracts"],
    ["../src/features/journey/api.ts", "my-contracts-lite"],
    ["../src/features/journey/financeApi.ts", "quotes"],
  ]) assert.ok(source(file).includes(key), `${file}: ${key}`);
});
