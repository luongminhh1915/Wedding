/** Group only records that really belong to the same supplier and the same combo. */
export function groupCustomerOrders<T extends { vendorId: string; comboOrderId?: string | null }>(rows: T[]) {
  const groups = new Map<string, T[]>();
  for (const row of rows) {
    const key = `${row.vendorId}:${row.comboOrderId || "individual"}`;
    const group = groups.get(key) ?? [];
    group.push(row);
    groups.set(key, group);
  }
  return [...groups.entries()].map(([key, items]) => ({ key, items }));
}

export function isCustomerOrderArchived(row: { isArchived?: boolean; archivedAt?: string | null }) {
  return row.isArchived === true || !!row.archivedAt;
}

export function matchesOrderFilter(
  row: { status: string; isArchived?: boolean; archivedAt?: string | null },
  filter: string,
) {
  if (filter === "archived") return isCustomerOrderArchived(row);
  if (isCustomerOrderArchived(row)) return false;
  if (filter === "all") return true;
  if (filter === "closed") return ["Cancelled", "Expired", "Completed", "Rejected", "Superseded"].includes(row.status);
  if (filter === "confirmed") return ["Confirmed", "Disputed"].includes(row.status);
  if (filter === "pending") return ["New", "PendingVerification", "Sent", "Draft"].includes(row.status);
  return !["Cancelled", "Expired", "Completed", "Rejected", "Superseded"].includes(row.status);
}

/** Never infer financial permissions from a label, a deposit amount, or cached local state. */
export function orderCapability(capabilities: object | null | undefined, key: string) {
  return !!capabilities && (capabilities as Record<string, unknown>)[key] === true;
}
