import { vendorDashboardTab } from "../features/vendor-portal/vendorJourneyModel.ts";

export interface ActorNavigationItem {
  id: string;
  label: string;
  mobileLabel: string;
  to: string;
  active: boolean;
}

type WorkspaceRole = "VendorOwner" | "Moderator" | "Finance" | "CustomerCare" | "SuperAdmin";

// Primary shortcuts only. The dashboard retains the full role-filtered feature menu.
const primaryTabs: Record<WorkspaceRole, readonly string[]> = {
  VendorOwner: ["overview", "listings", "combos", "leads"],
  Moderator: ["overview", "moderation", "reviews", "published-listings", "cms"],
  Finance: ["overview", "finance", "settlements", "disputes"],
  CustomerCare: ["overview", "disputes", "emergency"],
  SuperAdmin: ["overview", "moderation", "users", "finance", "cms"],
};

const labels: Record<string, readonly [string, string]> = {
  overview: ["Tổng quan", "Tổng quan"],
  listings: ["Dịch vụ của tôi", "Dịch vụ"],
  combos: ["Combo của tôi", "Combo"],
  leads: ["Yêu cầu tư vấn", "Tư vấn"],
  moderation: ["Kiểm duyệt", "Duyệt"],
  reviews: ["Đánh giá", "Đánh giá"],
  "published-listings": ["Quản lý dịch vụ", "Dịch vụ"],
  finance: ["Tài chính", "Tài chính"],
  settlements: ["Bảng kê tháng", "Bảng kê"],
  disputes: ["Tranh chấp", "Tranh chấp"],
  emergency: ["Hỗ trợ khẩn cấp", "Hỗ trợ"],
  users: ["Người dùng & đối tác", "Tài khoản"],
  cms: ["Cẩm nang CMS", "Cẩm nang"],
};

// Keep overview fallback consistent with AdminDashboard's visibleTabs, including
// deep links to authorized tabs that are not primary navbar shortcuts.
const staffTabs: Record<Exclude<WorkspaceRole, "VendorOwner">, readonly string[]> = {
  Moderator: ["overview", "moderation", "reviews", "published-listings", "verifications", "cms", "combos"],
  Finance: ["overview", "finance", "settlements", "disputes", "change-requests"],
  CustomerCare: ["overview", "disputes", "emergency"],
  SuperAdmin: ["overview", "moderation", "reviews", "published-listings", "users", "finance", "settlements", "verifications", "disputes", "change-requests", "cms", "combos", "emergency"],
};

export function usesCustomerNavigation(role: unknown): boolean {
  return role == null || role === "Customer";
}

const normalizedPath = (path: string) => path.toLowerCase().replace(/\/+$/, "") || "/";

/** UI visibility only; server authorization and route guards remain authoritative. */
export function actorWorkspaceNavigation(role: unknown, pathname: string, search = "", mobile = false): ActorNavigationItem[] {
  const items: ActorNavigationItem[] = [];
  if (typeof role === "string" && Object.hasOwn(primaryTabs, role)) {
    const workspaceRole = role as WorkspaceRole;
    const workspacePath = workspaceRole === "VendorOwner" ? "/vendor/dashboard" : "/admin/dashboard";
    const params = new URLSearchParams(search);
    const requested = params.get("tab") || "overview";
    const activeTab = workspaceRole === "VendorOwner" ? vendorDashboardTab(params)
      : staffTabs[workspaceRole].includes(requested) ? requested : "overview";
    for (const tab of primaryTabs[workspaceRole]) {
      const [label, mobileLabel] = labels[tab];
      items.push({ id: tab, label, mobileLabel,
        to: workspacePath + (tab === "overview" ? "" : `?tab=${tab}`),
        active: normalizedPath(pathname) === workspacePath && activeTab === tab });
    }
  }
  // Account and notifications remain common to authenticated actors, even if an
  // unexpected role needs to be revalidated. Never fall back to privileged links.
  if (mobile && role != null && role !== "Customer") items.push({
    id: "account", label: "Tài khoản", mobileLabel: "Cá nhân", to: "/account",
    active: normalizedPath(pathname) === "/account",
  });
  return items;
}
