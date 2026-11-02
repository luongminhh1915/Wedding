export interface NotificationItem {
  id: string;
  message: string;
  createdAt: string;
  readAt: string | null;
  targetPath: string | null;
}
export interface NotificationPage {
  items: NotificationItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  unreadCount: number;
}

export function requestedNotificationFocus(params: URLSearchParams, kind: string): string | null {
  const requested = params.get("focusKind") === kind ? params.get("focus") : null;
  return requested && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu.test(requested) ? requested.toLowerCase() : null;
}

// Never let an unexpected API value become an external redirect.
export function notificationTarget(path: string | null | undefined, role: string | undefined): string | null {
  if (!path || !role || path.length > 512 || path.includes("\\") || path.includes("%") || [...path].some(char => char.charCodeAt(0) <= 32)) return null;
  const dashboard = role === "Customer" ? "/customer/dashboard" : role === "VendorOwner" ? "/vendor/dashboard"
    : ["Finance", "SuperAdmin"].includes(role) ? "/admin/dashboard" : null;
  if (!dashboard || !path.startsWith(`${dashboard}?`)) return null;
  const params = new URLSearchParams(path.slice(path.indexOf("?") + 1));
  const tabs = role === "Customer" ? ["contracts", "appointments", "combos", "checklist"] : role === "VendorOwner"
    ? ["contracts-couple", "quotes", "leads", "appointments", "change-requests"] : ["change-requests"];
  const kinds = ["contract", "quote", "lead", "appointment", "combo", "task", "order-request"];
  if (!tabs.includes(params.get("tab") ?? "") || !kinds.includes(params.get("focusKind") ?? "") ||
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu.test(params.get("focus") ?? "")) return null;
  if ([...params.keys()].some(key => !["tab", "section", "focus", "focusKind"].includes(key)) ||
      [...new Set(params.keys())].some(key => params.getAll(key).length !== 1) ||
      (params.has("section") && !["quotes", "leads", "requests"].includes(params.get("section")!))) return null;
  return path;
}

export function notificationTime(value: string): string {
  // EF datetime2 is UTC but does not carry a JSON timezone suffix.
  const date = new Date(/(?:Z|[+-]\d\d:\d\d)$/iu.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.valueOf()) ? "" : date.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh", hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit", year: "numeric" });
}
