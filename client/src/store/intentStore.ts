import { create } from "zustand";
import { persist } from "zustand/middleware";
export const safeReturnPath = (path?: string | null) => path?.startsWith("/") && !path.startsWith("//") && !path.includes("\\") && !/^\/(login|register|auth\/continue)(\/|\?|$)/.test(path) ? path : null;
export const useIntentStore = create<{ returnTo: string | null; remember: (path: string) => void; consume: () => string | null }>()(persist((set, get) => ({
  returnTo: null,
  remember: (path) => set({ returnTo: safeReturnPath(path) }),
  consume: () => { const path = safeReturnPath(get().returnTo); set({ returnTo: null }); return path; },
}), { name: "wedding-match-intent", storage: { getItem: (name) => { const value = sessionStorage.getItem(name); return value ? JSON.parse(value) : null; }, setItem: (name, value) => sessionStorage.setItem(name, JSON.stringify(value)), removeItem: (name) => sessionStorage.removeItem(name) } }));
