export const SETUP_PATH = "/planner/setup";
export const OVERVIEW_PATH = "/customer/dashboard";
export function safeIntent(path?: string | null): string | null {
  return path?.startsWith("/") && !path.startsWith("//") && !path.includes("\\") && !/^\/(login|register|auth\/continue)(\/|\?|$)/.test(path) ? path : null;
}
export function isJoinIntent(path: string | null): boolean {
  if (!path) return false;
  const url = new URL(path, "https://wedding-match.invalid");
  return url.pathname === "/planner" && !!url.searchParams.get("join");
}
/** Called only after an authoritative status response, never by treating a failed request as an empty profile. */
export function afterLoginDestination(role: string, completed: boolean, intent?: string | null): string {
  if (role === "VendorOwner") return "/vendor/dashboard";
  if (role !== "Customer") return "/admin/dashboard";
  const safe = safeIntent(intent);
  if (!completed) return isJoinIntent(safe) ? safe! : SETUP_PATH;
  return safe === SETUP_PATH ? OVERVIEW_PATH : safe ?? OVERVIEW_PATH;
}
