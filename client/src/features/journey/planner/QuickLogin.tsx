import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { KeyRound, Smartphone } from "lucide-react";
import { useAuthStore } from "../../../store/authStore";
import { apiError, authApi } from "../api";
import { ErrorText } from "../ui";
import type { AuthResponse } from "../../../types/auth";

/** P1 – Đăng nhập không mật khẩu (OTP qua SĐT/email) + Google. Đăng nhập bằng mật khẩu vẫn giữ nguyên ở form phía trên. */
export function QuickLogin() {
  const setAuth = useAuthStore((s) => s.setAuth);
  const navigate = useNavigate();
  const [target, setTarget] = useState("");
  const [code, setCode] = useState("");
  const [devCode, setDevCode] = useState<string | null>(null);
  const [step, setStep] = useState<"target" | "otp">("target");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const finish = (res: AuthResponse) => {
    setAuth(res.user, res.token);
    navigate(res.user.role === "Customer" ? "/planner/setup" : res.user.role === "VendorOwner" ? "/vendor/dashboard" : "/admin/dashboard");
  };
  const run = async (fn: () => Promise<void>) => {
    setBusy(true); setError("");
    try { await fn(); } catch (e) { setError(apiError(e)); } finally { setBusy(false); }
  };

  return (
    <div className="wd mt-6 pt-6 border-t border-[var(--line)] space-y-3" aria-label="Đăng nhập nhanh">
      <p className="eyebrow text-center muted">Hoặc đăng nhập nhanh</p>
      <ErrorText>{error}</ErrorText>
      {step === "target" ? (
        <div className="flex gap-2">
          <input className="input" placeholder="Số điện thoại hoặc email" value={target} onChange={(e) => setTarget(e.target.value)} aria-label="Số điện thoại hoặc email nhận OTP" />
          <button type="button" className="btn btn-outline shrink-0" disabled={busy || target.trim().length < 5}
            onClick={() => run(async () => { const r = await authApi.requestOtp(target); setDevCode(r.devCode ?? null); setStep("otp"); })}>
            <Smartphone size={14} /> Gửi OTP
          </button>
        </div>
      ) : (
        <div className="space-y-2">
          <div className="flex gap-2">
            <input className="input tracking-[.5em] text-center" inputMode="numeric" maxLength={6} placeholder="••••••" value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))} aria-label="Mã OTP 6 số" />
            <button type="button" className="btn btn-primary shrink-0" disabled={busy || code.length !== 6}
              onClick={() => run(async () => finish(await authApi.verifyOtp(target, code)))}>
              <KeyRound size={14} /> Xác nhận
            </button>
          </div>
          <p className="text-xs muted">
            Mã đã gửi tới <b>{target}</b> (hiệu lực 5 phút).{" "}
            {devCode && <span className="pill warn">Môi trường demo – mã: {devCode}</span>}{" "}
            <button type="button" className="underline" onClick={() => { setStep("target"); setCode(""); }}>Đổi</button>
          </p>
        </div>
      )}
      <button type="button" className="btn btn-outline w-full" disabled={busy}
        onClick={() => run(async () => finish(await authApi.google("couple.demo@gmail.com", "Cặp đôi Google")))}>
        <span className="font-black text-[14px]" aria-hidden>G</span> Tiếp tục với Google (demo)
      </button>
    </div>
  );
}
