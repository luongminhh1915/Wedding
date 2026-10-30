import type { QueryClient } from "@tanstack/react-query";

// Content-only decisions can change the next allowed quote/contract action, but
// never change catalog prices, original order snapshots, payments or the cart.
const lifecycleReads = [
  "customer-order-page", "customer-order-journey", "customer-contracts",
  "vendor-contracts", "my-contracts-lite", "quotes",
] as const;

export async function refreshPrivateAgreementQueries(client: QueryClient, currentKey: readonly unknown[]) {
  await Promise.all([
    ...lifecycleReads.map(scope => client.invalidateQueries({ queryKey: [scope] })),
    client.invalidateQueries({
      queryKey: ["private-service-agreements", currentKey[1], currentKey[2], currentKey[3], currentKey[4]],
      // The successful response is already authoritative for this open form.
      // Refresh only sibling Lead contexts, not the form whose draft was just sent.
      predicate: query => JSON.stringify(query.queryKey) !== JSON.stringify(currentKey),
    }),
  ]);
}
