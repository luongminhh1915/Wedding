import type { UserRole } from "../../types/auth";

export const ACTOR_ROLES: readonly UserRole[] = [
  "Customer", "VendorOwner", "Moderator", "Finance", "CustomerCare", "SuperAdmin",
];
export function isActorRole(role: unknown): role is UserRole {
  return typeof role === "string" && ACTOR_ROLES.includes(role as UserRole);
}
export function actorDashboard(role: unknown): string {
  if (role === "Customer") return "/customer/dashboard";
  if (role === "VendorOwner") return "/vendor/dashboard";
  return isActorRole(role) ? "/admin/dashboard" : "/login";
}
/** Only local router paths; reject encoded slashes, malformed URLs and auth loops too. */
export function safeInternalReturnPath(path?: string | null): string | null {
  if (typeof path !== "string" || !path || path !== path.trim() || !path.startsWith("/") || path.startsWith("//")) return null;
  try {
    const decoded = decodeURIComponent(path);
    if (decoded.includes("\\") || [...decoded].some(char => char.charCodeAt(0) < 32 || char.charCodeAt(0) === 127) || decoded.startsWith("//") || /%[0-9a-f]{2}/i.test(decoded)) return null;
    const url = new URL(decoded, "https://wedding-match.invalid");
    if (url.origin !== "https://wedding-match.invalid" || /^\/(login|register|auth)(\/|$)/i.test(url.pathname)) return null;
    return path;
  } catch { return null; }
}
export function roleAwareReturnPath(role: unknown, path?: string | null): string | null {
  const safe = safeInternalReturnPath(path);
  if (!safe || !isActorRole(role)) return null;
  const pathname = new URL(decodeURIComponent(safe), "https://wedding-match.invalid").pathname.toLowerCase();
  if (/^\/vendor(\/|$)/.test(pathname)) return role === "VendorOwner" ? safe : null;
  if (/^\/admin(\/|$)/.test(pathname)) return !["Customer", "VendorOwner"].includes(role) ? safe : null;
  if (/^\/(customer|planner)(\/|$)/.test(pathname) || /^\/(thiep\/create|invitation\/manage|lich-hen|hop-dong)(\/|$)/.test(pathname))
    return role === "Customer" ? safe : null;
  return safe;
}
