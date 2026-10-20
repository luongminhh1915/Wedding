export const SETUP_PATH = "/planner/setup";
export const OVERVIEW_PATH = "/customer/dashboard";
export function completedSetupDestination(completed: boolean, pathname: string): string | null {
  let normalized = pathname;
  try { normalized = decodeURIComponent(pathname); } catch { /* Keep malformed paths for the router's normal handling. */ }
  return completed && normalized.toLowerCase().replace(/\/+$/, "") === SETUP_PATH ? OVERVIEW_PATH : null;
}
export function safeIntent(path?: string | null): string | null {
  return safeInternalReturnPath(path);
}
export function isJoinIntent(path: string | null): boolean {
  if (!path) return false;
  const url = new URL(path, "https://wedding-match.invalid");
  return url.pathname === "/planner" && !!url.searchParams.get("join");
}
/** Called only after an authoritative status response, never by treating a failed request as an empty profile. */
export function afterLoginDestination(role: string, completed: boolean, intent?: string | null): string {
  if (role !== "Customer") return actorDashboard(role);
  const safe = roleAwareReturnPath(role, intent);
  if (!completed) return isJoinIntent(safe) ? safe! : SETUP_PATH;
  return safe && completedSetupDestination(true, new URL(safe, "https://wedding-match.invalid").pathname) ? OVERVIEW_PATH : safe ?? OVERVIEW_PATH;
}
import { actorDashboard, roleAwareReturnPath, safeInternalReturnPath } from "../../auth/authNavigation";
