import { create } from "zustand";
import { persist } from "zustand/middleware";
import { safeInternalReturnPath } from "../features/auth/authNavigation";
export const safeReturnPath = safeInternalReturnPath;
export const useIntentStore = create<{ returnTo: string | null; remember: (path: string) => void; consume: () => string | null }>()(persist((set, get) => ({
  returnTo: null,
  remember: (path) => set({ returnTo: safeReturnPath(path) }),
  consume: () => { const path = safeReturnPath(get().returnTo); set({ returnTo: null }); return path; },
}), { name: "wedding-match-intent", storage: { getItem: (name) => { try { const value = sessionStorage.getItem(name); return value ? JSON.parse(value) : null; } catch { sessionStorage.removeItem(name); return null; } }, setItem: (name, value) => sessionStorage.setItem(name, JSON.stringify(value)), removeItem: (name) => sessionStorage.removeItem(name) } }));
