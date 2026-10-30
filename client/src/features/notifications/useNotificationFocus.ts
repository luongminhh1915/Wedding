import { useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import "./notifications.css";
import { requestedNotificationFocus } from "./notificationModel";
export { requestedNotificationFocus } from "./notificationModel";

/** Only scrolls to a resource already returned by an owner-authorized API. Does not fetch arbitrary IDs. */
export function useNotificationFocus(kind: string, ids: string[], ready: boolean) {
  const [params, setParams] = useSearchParams();
  const focusId = requestedNotificationFocus(params, kind);
  const available = !!focusId && ids.some(id => id.toLowerCase() === focusId.toLowerCase());
  useEffect(() => {
    if (!ready || !available) return;
    const element = document.getElementById(`notification-${kind}-${focusId}`);
    element?.scrollIntoView({ block: "center", behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth" });
    element?.focus({ preventScroll: true });
  }, [kind, focusId, available, ready]);
  const clearFocus = () => setParams(previous => {
    const next = new URLSearchParams(previous); next.delete("focus"); next.delete("focusKind"); return next;
  }, { replace: true });
  return { focusId, missing: !!focusId && ready && !available, clearFocus };
}
