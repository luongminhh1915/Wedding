import { Fragment, useEffect, useState, type ReactNode } from "react";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import { tokenExpiresAt } from "../sessionPolicy";
import type { User } from "../../../types/auth";

/** Revalidate restored sessions before mounting actor screens; remount private state on account changes. */
export default function AuthSessionBoundary({ children }: { children: ReactNode }) {
  const { user, token, validated, sessionRevision, logout } = useAuthStore();
  const [attempt, setAttempt] = useState(0);
  const [errorToken, setErrorToken] = useState<string | null>(null);
  useEffect(() => {
    const storageChanged = (event: StorageEvent) => {
      if (event.key === null || event.key === "user" || event.key === "token") useAuthStore.getState().syncStorage();
    };
    window.addEventListener("storage", storageChanged);
    return () => window.removeEventListener("storage", storageChanged);
  }, []);
  useEffect(() => {
    if (!token) return;
    const controller = new AbortController();
    const verify = async () => {
      if (useAuthStore.getState().token !== token) return;
      const expiresAt = tokenExpiresAt(token);
      if (!expiresAt || expiresAt <= Date.now()) { useAuthStore.getState().logout(); return; }
      try {
        const { data } = await axiosClient.get<User>("/api/auth/me", { signal: controller.signal });
        if (!controller.signal.aborted) useAuthStore.getState().confirmSession(data, token);
      } catch {
        if (!controller.signal.aborted && useAuthStore.getState().token === token && !useAuthStore.getState().validated) setErrorToken(token);
      }
    };
    if (!useAuthStore.getState().validated) void verify();
    const foreground = () => { useAuthStore.getState().syncStorage(); void verify(); };
    window.addEventListener("focus", foreground);
    const remaining = (tokenExpiresAt(token) ?? Date.now()) - Date.now();
    const expiration = window.setTimeout(() => {
      if (useAuthStore.getState().token !== token) return;
      if ((tokenExpiresAt(token) ?? 0) <= Date.now()) useAuthStore.getState().logout();
      else setAttempt(value => value + 1);
    }, Math.min(Math.max(remaining, 0), 2_147_483_647));
    return () => { controller.abort(); window.clearTimeout(expiration); window.removeEventListener("focus", foreground); };
  }, [token, attempt]);

  if (token && !validated) return <main className="wd wd-page container text-center" role={errorToken === token ? "alert" : "status"}>
    <h1 className="serif text-2xl mb-3">{errorToken === token ? "Chưa kiểm tra được phiên đăng nhập" : "Đang xác thực tài khoản..."}</h1>
    {errorToken === token && <><p className="muted mb-5">Chưa mở dữ liệu tài khoản khi máy chủ chưa xác nhận phiên. Vui lòng thử lại.</p><div className="flex justify-center gap-3"><button className="btn btn-primary" onClick={() => { setErrorToken(null); setAttempt(value => value + 1); }}>Thử lại</button><button className="btn btn-outline" onClick={logout}>Đăng xuất</button></div></>}
  </main>;
  return <Fragment key={`${sessionRevision}:${user?.id ?? "guest"}`}>{children}</Fragment>;
}
