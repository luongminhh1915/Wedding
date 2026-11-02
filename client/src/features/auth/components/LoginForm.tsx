import { useState } from "react";
import { useNavigate, Link, useLocation } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../hooks/useAuth";
import { Lock, Mail, AlertCircle, ArrowRight, Eye, EyeOff } from "lucide-react";
import { QuickLogin } from "../../journey/planner/QuickLogin";
import { apiError, authApi } from "../../journey/api";
import { safeReturnPath, useIntentStore } from "../../../store/intentStore";

export default function LoginForm({ embedded = false, onComplete }: { embedded?: boolean; onComplete?: () => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [recovering, setRecovering] = useState(false);
  const [recoveryMessage, setRecoveryMessage] = useState("");
  const [recoveryError, setRecoveryError] = useState("");
  const [recoveryBusy, setRecoveryBusy] = useState(false);
  const { login, isLoggingIn } = useAuth();
  const providers = useQuery({ queryKey: ["auth-providers"], queryFn: authApi.providers, staleTime: 60000 });
  const passwordRecoveryReady = !providers.isLoading && !providers.isError && providers.data?.passwordRecovery === true;
  const navigate = useNavigate();
  const location = useLocation();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (isLoggingIn) return;
    setErrorMessage("");

    try {
      await login({ email: email.trim(), password });
      onComplete?.();
      const returnTo = safeReturnPath((location.state as { from?: string } | null)?.from) ?? useIntentStore.getState().returnTo;
      navigate("/auth/continue", { replace: true, state: { from: returnTo } });
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      setErrorMessage(
        error.response?.data?.message ||
          "Đăng nhập không thành công. Vui lòng kiểm tra lại.",
      );
    }
  };

  const requestPasswordReset = async (event: React.FormEvent) => {
    event.preventDefault();
    if (recoveryBusy) return;
    setRecoveryMessage(""); setRecoveryError("");
    setRecoveryBusy(true);
    try {
      const response = await authApi.requestPasswordReset(email.trim());
      setRecoveryMessage(response.message);
    } catch (error) {
      setRecoveryError(apiError(error, "Chưa gửi được yêu cầu. Vui lòng thử lại sau."));
    } finally { setRecoveryBusy(false); }
  };

  return (
    <main className={embedded ? "auth-modal-content" : "auth-page"}>
      <div className="auth-story" hidden={embedded}>
        <span className="wm-eyebrow">CHÀO MỪNG ĐẾN VỚI WEDDING MATCH</span>
        <h1>
          Mọi thứ cho
          <br />
          <em>đám cưới của bạn.</em>
        </h1>
        <p>
          Đăng nhập để tiếp tục viết nên ngày trọng đại. Kế hoạch, ngân sách và
          những yêu thương đều ở đây.
        </p>
        <img className="wm-mascot-image" src="/wedding-match/be-hy-mascot.png" alt="Bé Hý chào hai bạn" />
      </div>
      <div className="auth-card">
        {/* Brand Header */}
        <div className="text-center mb-8">
          <h2>{recovering ? "Khôi phục mật khẩu" : "Chào mừng bạn trở lại!"}</h2>
          <p className="text-xs text-slate-400 mt-3">
            {recovering ? "Nhập email tài khoản để nhận liên kết đặt mật khẩu mới." : "Một tài khoản, đúng không gian theo vai trò của bạn"}
          </p>
        </div>

        {/* Error Alert */}
        {!recovering && (location.state as { passwordChanged?: boolean } | null)?.passwordChanged && <p role="status" className="mb-5 rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">Đã đổi mật khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới.</p>}
        {!recovering && errorMessage && (
          <div role="alert" className="mb-6 p-4 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3 text-red-700 text-sm">
            <AlertCircle className="w-5 h-5 flex-shrink-0 text-red-500 mt-0.5" />
            <span>{errorMessage}</span>
          </div>
        )}

        {!recovering && <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-2">
              Email hoặc tên đăng nhập
            </label>
            <div className="relative">
              <Mail className="w-5 h-5 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="text"
                aria-label="Email hoặc tên đăng nhập"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                maxLength={150}
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Email hoặc tên đăng nhập của NCC"
                className="w-full pl-11 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-2">
              Mật khẩu
            </label>
            <div className="relative">
              <Lock className="w-5 h-5 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type={showPassword ? "text" : "password"}
                aria-label="Mật khẩu"
                autoComplete="current-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full pl-11 pr-11 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
              />
              <button type="button" aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"} aria-pressed={showPassword} className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500" onClick={() => setShowPassword(value => !value)}>{showPassword ? <EyeOff size={18} /> : <Eye size={18} />}</button>
            </div>
          </div>

          <div className="-mt-2 flex justify-end"><button type="button" className="text-sm font-medium text-[#233a42] hover:underline" onClick={() => { setRecovering(true); setRecoveryError(""); setRecoveryMessage(""); if (!email.includes('@')) setEmail(""); }}>Quên mật khẩu?</button></div>

          <button
            type="submit"
            disabled={isLoggingIn}
            className="w-full py-3 px-4 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 text-white font-semibold text-sm hover:from-rose-600 hover:to-pink-600 shadow-md shadow-rose-200 transition disabled:opacity-50 flex items-center justify-center gap-2"
          >
            {isLoggingIn ? (
              <span>Đang xác thực...</span>
            ) : (
              <>
                <span>Đăng Nhập</span>
                <ArrowRight className="w-4 h-4" />
              </>
            )}
          </button>
        </form>}
        {recovering && <form onSubmit={requestPasswordReset} className="space-y-4" aria-label="Yêu cầu khôi phục mật khẩu">
          <p className="text-xs leading-relaxed text-slate-600">Nếu tài khoản NCC chưa có email liên hệ, hãy liên hệ Admin để được hỗ trợ. Tên đăng nhập không thay cho địa chỉ nhận thư.</p>
          <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider">Email đăng nhập
            <span className="relative mt-2 block"><Mail className="w-5 h-5 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" /><input type="email" aria-label="Email đăng nhập để khôi phục" autoComplete="email" maxLength={150} required value={email} onChange={(event) => { setEmail(event.target.value); setRecoveryMessage(""); }} placeholder="vidu@email.com" className="w-full pl-11 pr-4 py-3 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition" /></span>
          </label>
          {providers.isLoading && <p role="status" className="text-sm text-slate-600">Đang kiểm tra dịch vụ gửi email...</p>}
          {providers.isError && <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">Chưa kiểm tra được dịch vụ email. Hãy thử kiểm tra lại trước khi gửi yêu cầu.<button type="button" className="ml-2 font-semibold underline" onClick={() => { void providers.refetch(); }}>Kiểm tra lại</button></div>}
          {providers.data && !providers.data.passwordRecovery && <p role="status" className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">Khôi phục mật khẩu hiện chưa được cấu hình gửi email cho môi trường này. Hãy liên hệ quản trị viên để được hỗ trợ.</p>}
          {recoveryMessage && <p role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">{recoveryMessage}</p>}
          {recoveryError && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{recoveryError}</p>}
          <button type="submit" className="w-full py-3 px-4 rounded-xl bg-[#e38c82] text-[#233a42] font-semibold text-sm disabled:opacity-50" disabled={!passwordRecoveryReady || !email.trim() || recoveryBusy}>{recoveryBusy ? "Đang gửi..." : "Gửi liên kết đặt lại mật khẩu"}</button>
          <button type="button" className="w-full text-sm font-semibold text-[#233a42] hover:underline" onClick={() => { setRecovering(false); setRecoveryMessage(""); setRecoveryError(""); }}>Quay lại đăng nhập</button>
        </form>}
        {!recovering && <><QuickLogin onComplete={onComplete} />
        <p className="text-xs text-slate-500 mt-4 leading-relaxed">Khách hàng, nhà cung cấp và nhân sự dùng chung trang đăng nhập. Tài khoản kiểm duyệt, kế toán, CSKH do quản trị viên cấp; không đăng ký quyền nội bộ tại đây.</p></>}

        {!recovering && <div className="mt-6 text-center text-sm text-slate-500">
          Chưa có tài khoản?{" "}
          <Link
            to="/register"
            state={location.state}
            onClick={onComplete}
            className="font-semibold text-rose-600 hover:text-rose-700 transition"
          >
            Đăng ký ngay
          </Link>
        </div>}
      </div>
    </main>
  );
}
