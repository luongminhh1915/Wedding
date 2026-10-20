export interface BusinessGroup {
  id: string;
  code: string;
  name: string;
  description: string;
  defaultCommissionRate: number;
  requiresCommissionBasisApproval: boolean;
  policyVersion: string;
}

export interface VendorBusinessGroupPolicy {
  groups: BusinessGroup[];
  usesLegacyCommissionRate: boolean;
  legacyCommissionRate: number | null;
  policyAcceptedAt: string | null;
  policyVersion: string | null;
}

export function commissionPercent(rate: number): string {
  return new Intl.NumberFormat("vi-VN", { style: "percent", maximumFractionDigits: 2 }).format(rate);
}

/** SQL datetime2 stores UTC but may be serialized without an offset after reload. */
export function policyAcceptedTime(value: string): string {
  const utcValue = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?$/.test(value) ? `${value}Z` : value;
  const timestamp = new Date(utcValue);
  return Number.isNaN(timestamp.getTime()) ? 'Chưa có thông tin' : timestamp.toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' });
}

// Fail closed if a stale API returns the old dropdown contract or malformed policy data.
export function readBusinessGroups(value: unknown): BusinessGroup[] {
  if (!Array.isArray(value) || value.some(group => !group || typeof group !== "object" ||
    typeof group.id !== "string" || !group.id.trim() || typeof group.code !== "string" ||
    typeof group.name !== "string" || !group.name.trim() || typeof group.description !== "string" ||
    typeof group.defaultCommissionRate !== "number" || !Number.isFinite(group.defaultCommissionRate) ||
    group.defaultCommissionRate < 0 || group.defaultCommissionRate > 1 ||
    typeof group.requiresCommissionBasisApproval !== "boolean" || typeof group.policyVersion !== "string" || !group.policyVersion.trim()) ||
    new Set(value.map(group => group.id)).size !== value.length)
    throw new Error("Chưa tải được chính sách nhóm dịch vụ hợp lệ.");
  return value as BusinessGroup[];
}

export function readVendorBusinessGroupPolicy(value: unknown): VendorBusinessGroupPolicy {
  if (!value || typeof value !== "object") throw new Error("Chưa tải được chính sách hợp tác.");
  const policy = value as Record<string, unknown>;
  const groups = readBusinessGroups(policy.groups);
  if (typeof policy.usesLegacyCommissionRate !== "boolean" ||
    !(policy.legacyCommissionRate === null || typeof policy.legacyCommissionRate === "number" && Number.isFinite(policy.legacyCommissionRate) && policy.legacyCommissionRate >= 0 && policy.legacyCommissionRate <= 1) ||
    !(policy.policyAcceptedAt === null || typeof policy.policyAcceptedAt === "string" && !Number.isNaN(Date.parse(policy.policyAcceptedAt))) ||
    !(policy.policyVersion === null || typeof policy.policyVersion === "string"))
    throw new Error("Chưa tải được chính sách hợp tác hợp lệ.");
  return { groups, usesLegacyCommissionRate: policy.usesLegacyCommissionRate, legacyCommissionRate: policy.legacyCommissionRate,
    policyAcceptedAt: policy.policyAcceptedAt, policyVersion: policy.policyVersion } as VendorBusinessGroupPolicy;
}
