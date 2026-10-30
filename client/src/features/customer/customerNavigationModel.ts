export const CUSTOMER_TABS = ["overview", "checklist", "budget", "combos", "contracts", "reviews", "guests", "invitations", "appointments"] as const;
export type CustomerTab = (typeof CUSTOMER_TABS)[number];
export const CUSTOMER_TAB_LABELS: Record<CustomerTab, string> = {
  overview: "Tổng quan", checklist: "Công việc", budget: "Ngân sách", combos: "Lựa chọn & yêu cầu",
  contracts: "Hợp đồng & tư vấn", reviews: "Đánh giá", guests: "Khách & xếp bàn", invitations: "Thiệp & RSVP", appointments: "Lịch hẹn",
};
export const CUSTOMER_NAV_GROUPS = [
  { id: "planning", label: "Kế hoạch cưới", items: ["overview", "checklist", "budget"] },
  { id: "services", label: "Dịch vụ & tư vấn", items: ["discovery", "browse", "combos", "contracts", "reviews", "appointments"] },
  { id: "guests", label: "Khách mời & thiệp", items: ["guests", "invitations"] },
] as const;
const workspacePaths = new Set(["/customer/dashboard", "/customer/contracts", "/planner", "/planner/setup", "/hop-dong", "/lich-hen", "/thiep/create", "/invitation/manage"]);
const routePath = (pathname: string) => pathname.toLowerCase().replace(/\/+$/, "") || "/";

export function isCustomerWorkspace(pathname: string): boolean {
  return workspacePaths.has(routePath(pathname));
}
/** An explicit tab still wins on a legacy alias, just as it does on the canonical dashboard. */
export function customerTab(pathname: string, search: string | URLSearchParams): CustomerTab {
  const params = new URLSearchParams(search);
  const requested = params.get("tab");
  if (requested) return CUSTOMER_TABS.includes(requested as CustomerTab) ? requested as CustomerTab : "overview";
  switch (routePath(pathname)) {
    case "/hop-dong": case "/customer/contracts": return "contracts";
    case "/lich-hen": return "appointments";
    case "/thiep/create": case "/invitation/manage": return "invitations";
    default: return "overview";
  }
}

const focusKinds: Partial<Record<CustomerTab, readonly string[]>> = {
  combos: ["combo"], contracts: ["contract", "lead", "quote", "order-request"], appointments: ["appointment"], checklist: ["task"],
};
/** Retain filters and wedding invitations; remove only resource focus incompatible with the destination. */
export function customerTabHref(tab: CustomerTab, search: string | URLSearchParams = ""): string {
  const next = new URLSearchParams(search);
  if (tab === "overview") next.delete("tab"); else next.set("tab", tab);
  if (!(focusKinds[tab] ?? []).includes(next.get("focusKind") ?? "")) { next.delete("focus"); next.delete("focusKind"); }
  if (tab !== "contracts") next.delete("section");
  const query = next.toString();
  return `/customer/dashboard${query ? `?${query}` : ""}`;
}

/** The same grouping and route state drive the dashboard menu and the desktop Planner menu. */
export function customerNavigationGroups(pathname: string, search: string | URLSearchParams) {
  const activeTab = isCustomerWorkspace(pathname) ? customerTab(pathname, search) : null;
  return CUSTOMER_NAV_GROUPS.map(group => ({ ...group, items: group.items.map(id => {
    if (id === "discovery") return { id, label: "Khám phá combo", to: "/combo", active: routePath(pathname) === "/combo" || routePath(pathname).startsWith("/combo/") };
    if (id === "browse") return { id, label: "Tìm dịch vụ lẻ", to: "/browse", active: ["/browse", "/vendors"].includes(routePath(pathname)) || routePath(pathname).startsWith("/listings/") };
    return { id, label: CUSTOMER_TAB_LABELS[id], to: customerTabHref(id, search), active: id === activeTab };
  }) }));
}

/** Customer-only bottom navigation. Staff/vendor navigation is intentionally defined by its existing caller. */
export function customerMobileSection(pathname: string): "home" | "planner" | "combo" | "services" | "account" | null {
  const path = routePath(pathname);
  if (path === "/") return "home";
  if (isCustomerWorkspace(path) || path === "/invitation") return "planner";
  if (path === "/combo" || path.startsWith("/combo/")) return "combo";
  if (path === "/browse" || path === "/vendors" || path.startsWith("/listings/")) return "services";
  if (["/account", "/notifications", "/login", "/register"].includes(path)) return "account";
  return null;
}
