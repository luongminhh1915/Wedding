import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuthStore } from "../../../store/authStore";
import { safeReturnPath, useIntentStore } from "../../../store/intentStore";
import { apiError, authApi } from "../api";
import { loadFacebookSdk } from "../../auth/facebookSdk";
import { ErrorText } from "../ui";
import type { AuthResponse } from "../../../types/auth";

type GoogleApi = { accounts: { id: { initialize: (options: { client_id: string; callback: (response: { credential: string }) => void }) => void; renderButton: (element: HTMLElement, options: { theme: string; size: string; text: string }) => void; } } };
export function QuickLogin({ onComplete }: { onComplete?: () => void }) {
  const setAuth = useAuthStore((s) => s.setAuth);
  const navigate = useNavigate(); const location = useLocation();
  const [busy, setBusy] = useState(false); const [error, setError] = useState(""); const [googleSdkError, setGoogleSdkError] = useState("");
  const googleButton = useRef<HTMLDivElement>(null);
  const sessionRevision = useRef(useAuthStore.getState().sessionRevision);
  const providers = useQuery({ queryKey: ["auth-providers"], queryFn: authApi.providers, staleTime: 60000 });
  const facebookAppId = providers.data?.facebookAppId;
  const facebookVersion = providers.data?.facebookGraphApiVersion;
  const facebookReady = Boolean(facebookAppId && facebookVersion);
  const finishRef = useRef<(res: AuthResponse) => void>(() => {});
  useEffect(() => { finishRef.current = (res) => {
    if (useAuthStore.getState().sessionRevision !== sessionRevision.current) return;
    setAuth(res.user, res.token); onComplete?.();
    const returnTo = safeReturnPath((location.state as { from?: string } | null)?.from) ?? useIntentStore.getState().returnTo;
    navigate("/auth/continue", { replace: true, state: { from: returnTo } });
  }; }, [setAuth, onComplete, location.state, navigate]);
  useEffect(() => {
    const clientId = providers.data?.googleClientId; if (!clientId || !googleButton.current) return;
    let cancelled = false;
    let script = document.querySelector<HTMLScriptElement>("script[data-google-identity]");
    const render = () => {
      const google = (window as Window & { google?: GoogleApi }).google;
      if (cancelled || !googleButton.current) return;
      if (!google) {
        if (script?.dataset.loaded === "true") setGoogleSdkError("Không tải được dịch vụ Google. Hãy tải lại trang rồi thử lại.");
        return;
      }
      google.accounts.id.initialize({ client_id: clientId, callback: ({ credential }) => {
        setBusy(true); setError(""); authApi.google(credential).then((res) => finishRef.current(res)).catch((e) => setError(apiError(e))).finally(() => setBusy(false));
      } });
      google.accounts.id.renderButton(googleButton.current, { theme: "outline", size: "large", text: "continue_with" });
    };
    if (!script) { script = document.createElement("script"); script.src = "https://accounts.google.com/gsi/client"; script.async = true; script.dataset.googleIdentity = "true"; document.head.appendChild(script); }
    const onLoad = () => { script!.dataset.loaded = "true"; render(); };
    const onError = () => setGoogleSdkError("Không thể tải dịch vụ Google. Hãy kiểm tra kết nối rồi thử lại.");
    script.addEventListener("load", onLoad); script.addEventListener("error", onError); render();
    return () => { cancelled = true; script?.removeEventListener("load", onLoad); script?.removeEventListener("error", onError); };
  }, [providers.data?.googleClientId]);
  const run = async (fn: () => Promise<void>) => {
    setBusy(true); setError("");
    try { await fn(); }
    catch (e) {
      const responseMessage = (e as { response?: { data?: { message?: string } } })?.response?.data?.message;
      setError(responseMessage || (e instanceof Error ? e.message : apiError(e)));
    } finally { setBusy(false); }
  };
  const signInFacebook = () => run(async () => {
    if (!facebookAppId || !facebookVersion) return;
    const facebook = await loadFacebookSdk(facebookAppId, facebookVersion);
    const response = await new Promise<{ authResponse?: { accessToken: string } | null }>(resolve =>
      facebook.login(resolve, { scope: "public_profile" }));
    const accessToken = response.authResponse?.accessToken;
    if (!accessToken) return;
    finishRef.current(await authApi.facebook(accessToken));
  });
  return <div className="wd mt-6 pt-6 border-t border-[var(--line)] space-y-3" aria-label="Đăng nhập nhanh">
    <ErrorText>{error}</ErrorText>
    <section className="space-y-3" aria-label="Đăng nhập bằng mạng xã hội">
      <p className="text-xs muted text-center">Hoặc đăng nhập khách hàng bằng mạng xã hội</p>
      {providers.isError && <div className="text-xs text-center" role="alert">Không kiểm tra được cấu hình đăng nhập mạng xã hội. <button type="button" className="underline" onClick={() => void providers.refetch()}>Thử lại</button></div>}
      {providers.data?.googleClientId
        ? <div ref={googleButton} aria-label="Khách hàng đăng nhập bằng Google" />
        : <button type="button" className="btn btn-outline w-full justify-center" disabled>
            Tiếp tục với Google{providers.data ? " · chưa cấu hình" : providers.isError ? " · chưa kiểm tra được" : " · đang kiểm tra"}
          </button>}
      {googleSdkError && <p className="text-xs text-center" role="alert">{googleSdkError}</p>}
      <button type="button" className="btn btn-facebook w-full justify-center" disabled={!facebookReady || busy} onClick={signInFacebook}>
        <span aria-hidden="true" className="font-bold">f</span>
        Tiếp tục với Facebook{providers.data ? (!facebookReady ? " · chưa cấu hình" : "") : providers.isError ? " · chưa kiểm tra được" : " · đang kiểm tra"}
      </button>
      <p className="text-[11px] muted text-center">Google/Facebook chỉ dùng cho tài khoản khách hàng. Nhà cung cấp và nhân sự đăng nhập bằng email, mật khẩu.</p>
    </section>
    {providers.data && !providers.data.passwordRecovery && <p className="text-xs muted text-center">Khôi phục mật khẩu qua email chưa bật trên môi trường này.</p>}
  </div>;
}
