import type { User } from "../../types/auth";
import { isActorRole } from "./authNavigation";

export function tokenExpiresAt(token: string | null): number | null {
  try {
    const parts = token?.split(".");
    if (!parts || parts.length !== 3 || !parts.every(Boolean)) return null;
    const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const decoded = JSON.parse(atob(payload.padEnd(Math.ceil(payload.length / 4) * 4, "=")));
    const expiresAt = typeof decoded.exp === "number" ? decoded.exp * 1000 : NaN;
    return Number.isFinite(expiresAt) ? expiresAt : null;
  } catch { return null; }
}
export function isSessionUser(user: unknown): user is User {
  if (!user || typeof user !== "object") return false;
  const value = user as Partial<User>;
  return typeof value.id === "string" && !!value.id && typeof value.fullName === "string"
    && typeof value.email === "string" && typeof value.phoneNumber === "string" && isActorRole(value.role);
}
/** Stored identity is provisional until /me succeeds; this is not JWT signature validation. */
export function readStoredSession(rawUser: string | null, token: string | null, now = Date.now()) {
  try {
    const user: unknown = rawUser ? JSON.parse(rawUser) : null;
    const expiresAt = tokenExpiresAt(token);
    return token && isSessionUser(user) && expiresAt && expiresAt > now ? { user, token, expiresAt } : null;
  } catch { return null; }
}
export function shouldInvalidateSession(status: number | undefined, url: string | undefined, bearer: unknown, currentToken: string | null) {
  if (status !== 401 || !currentToken || bearer !== `Bearer ${currentToken}`) return false;
  return !/\/api\/auth\/(login|register-customer|register-vendor|providers|otp\/(request|verify)|google)\/?(?:\?|$)/i.test(url ?? "");
}
