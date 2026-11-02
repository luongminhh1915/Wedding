import { create } from 'zustand';
import type { User } from '../types/auth';
import { queryClient } from '../services/queryClient';
import { isSessionUser, readStoredSession } from '../features/auth/sessionPolicy';
import { useIntentStore } from './intentStore';

interface AuthState {
  user: User | null;
  token: string | null;
  validated: boolean;
  sessionRevision: number;
  setAuth: (user: User, token: string) => void;
  confirmSession: (user: User, expectedToken: string) => void;
  syncStorage: () => void;
  logout: () => void;
}

const stored = readStoredSession(localStorage.getItem('user'), localStorage.getItem('token'));
if (!stored) { localStorage.removeItem('user'); localStorage.removeItem('token'); }
const clearPrivateCache = () => { void queryClient.cancelQueries(); queryClient.clear(); };
export const useAuthStore = create<AuthState>((set, get) => ({
  user: stored?.user ?? null,
  token: stored?.token ?? null,
  validated: false,
  sessionRevision: 0,
  setAuth: (user, token) => {
    if (!isSessionUser(user) || !readStoredSession(JSON.stringify(user), token)) throw new Error('Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.');
    if (get().user && get().user?.id !== user.id) useIntentStore.getState().consume();
    clearPrivateCache();
    localStorage.setItem('user', JSON.stringify(user));
    localStorage.setItem('token', token);
    set(state => ({ user, token, validated: true, sessionRevision: state.sessionRevision + 1 }));
  },
  confirmSession: (user, expectedToken) => {
    if (get().token !== expectedToken) return;
    if (!isSessionUser(user) || get().user?.id !== user.id) { get().logout(); return; }
    const roleChanged = get().user?.role !== user.role;
    if (roleChanged) clearPrivateCache();
    localStorage.setItem('user', JSON.stringify(user));
    set(state => ({ user, validated: true, sessionRevision: state.sessionRevision + (roleChanged ? 1 : 0) }));
  },
  syncStorage: () => {
    const next = readStoredSession(localStorage.getItem('user'), localStorage.getItem('token'));
    if (get().token === next?.token && get().user?.id === next?.user.id && get().user?.role === next?.user.role) return;
    clearPrivateCache();
    useIntentStore.getState().consume();
    set(state => ({ user: next?.user ?? null, token: next?.token ?? null, validated: false, sessionRevision: state.sessionRevision + 1 }));
  },
  logout: () => {
    clearPrivateCache();
    useIntentStore.getState().consume();
    localStorage.removeItem('user');
    localStorage.removeItem('token');
    set(state => ({ user: null, token: null, validated: false, sessionRevision: state.sessionRevision + 1 }));
  },
}));
