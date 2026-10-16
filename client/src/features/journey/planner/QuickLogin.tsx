import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { KeyRound, Mail } from "lucide-react";
import { useAuthStore } from "../../../store/authStore";
import { safeReturnPath, useIntentStore } from "../../../store/intentStore";
import axiosClient from "../../../services/axiosClient";
import { apiError, authApi } from "../api";
import { ErrorText } from "../ui";
import type { AuthResponse } from "../../../types/auth";

type GoogleApi = { accounts: { id: { initialize: (options: { client_id: string; callback: (response: { credential: string }) => void }) => void; renderButton: (element: HTMLElement, options: { theme: string; size: string; text: string }) => void; } } };
export function QuickLogin({ onComplete }: { onComplete?: () => void }) {
  const setAuth = useAuthStore((s) => s.setAuth);
  const navigate = useNavigate(); const location = useLocation();
  const [target, setTarget] = useState(""); const [code, setCode] = useState("");
  const [step, setStep] = useState<"target" | "otp">("target"); const [busy, setBusy] = useState(false); const [error, setError] = useState("");
  const googleButton = useRef<HTMLDivElement>(null);
  const providers = useQuery({ queryKey: ["auth-providers"], queryFn: async () => (await axiosClient.get<{ emailOtp: boolean; googleClientId?: string }>("/api/auth/providers")).data, staleTime: 60000 });
  const finishRef = useRef<(res: AuthResponse) => void>(() => {});
  useEffect(() => { finishRef.current = (res) => {
    setAuth(res.user, res.token); onComplete?.();
    const returnTo = safeReturnPath((location.state as { from?: string } | null)?.from) ?? useIntentStore.getState().returnTo;
    navigate(res.user.role === "Customer" ? "/auth/continue" : res.user.role === "VendorOwner" ? "/vendor/dashboard" : "/admin/dashboard", { replace: true, state: { from: returnTo } });
  }; }, [setAuth, onComplete, location.state, navigate]);
  useEffect(() => {
    const clientId = providers.data?.googleClientId; if (!clientId || !googleButton.current) return;
    let cancelled = false;
    const render = () => {
      const google = (window as Window & { google?: GoogleApi }).google;
      if (cancelled || !google || !googleButton.current) return;
      google.accounts.id.initialize({ client_id: clientId, callback: ({ credential }) => {
        setBusy(true); setError(""); authApi.google(credential).then((res) => finishRef.current(res)).catch((e) => setError(apiError(e))).finally(() => setBusy(false));
      } });
      google.accounts.id.renderButton(googleButton.current, { theme: "outline", size: "large", text: "continue_with" });
    };
    let script = document.querySelector<HTMLScriptElement>("script[data-google-identity]");
    if (!script) { script = document.createElement("script"); script.src = "https://accounts.google.com/gsi/client"; script.async = true; script.dataset.googleIdentity = "true"; document.head.appendChild(script); }
    script.addEventListener("load", render); render();
    return () => { cancelled = true; script?.removeEventListener("load", render); };
  }, [providers.data?.googleClientId]);
  const run = async (fn: () => Promise<void>) => { setBusy(true); setError(""); try { await fn(); } catch (e) { setError(apiError(e)); } finally { setBusy(false); } };
  return <div className="wd mt-6 pt-6 border-t border-[var(--line)] space-y-3" aria-label="Đăng nhập nhanh">
    <ErrorText>{error}</ErrorText>
    {providers.data?.emailOtp ? <><p className="eyebrow text-center muted">Hoặc đăng nhập bằng mã email</p>
      {step === "target" ? <div className="flex gap-2"><input className="input" type="email" autoComplete="email" placeholder="Email nhận mã" value={target} onChange={(e) => setTarget(e.target.value)} aria-label="Email nhận OTP" /><button type="button" className="btn btn-outline shrink-0" disabled={busy || !target.includes("@")} onClick={() => run(async () => { await authApi.requestOtp(target); setStep("otp"); })}><Mail size={14} /> Gửi mã</button></div>
      : <div className="space-y-2"><div className="flex gap-2"><input className="input tracking-[.5em] text-center" inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))} aria-label="Mã OTP 6 số" /><button type="button" className="btn btn-primary shrink-0" disabled={busy || code.length !== 6} onClick={() => run(async () => finishRef.current(await authApi.verifyOtp(target, code)))}><KeyRound size={14} /> Xác nhận</button></div><p className="text-xs muted">Mã đã gửi tới {target}, hiệu lực 5 phút. <button type="button" className="underline" onClick={() => { setStep("target"); setCode(""); }}>Gửi lại / đổi email</button></p></div>}</>
      : <p className="text-xs muted text-center">OTP email chưa được cấu hình. Bạn có thể đăng nhập bằng mật khẩu.</p>}
    <div ref={googleButton} aria-label="Đăng nhập bằng Google" />
  </div>;
}
