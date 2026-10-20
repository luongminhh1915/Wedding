import { useState } from "react";
import { useNavigate, Link, useLocation } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";
import { Lock, Mail, AlertCircle, ArrowRight, Eye, EyeOff } from "lucide-react";
import { QuickLogin } from "../../journey/planner/QuickLogin";
import { safeReturnPath, useIntentStore } from "../../../store/intentStore";

export default function LoginForm({ embedded = false, onComplete }: { embedded?: boolean; onComplete?: () => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const { login, isLoggingIn } = useAuth();
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
        <img src="/wedding-match/milo.png" alt="Milo chào hai bạn" />
      </div>
      <div className="auth-card">
        {/* Brand Header */}
        <div className="text-center mb-8">
          <h2>Chào mừng bạn trở lại!</h2>
          <p className="text-xs text-slate-400 mt-3">
            Một tài khoản, đúng không gian theo vai trò của bạn
          </p>
        </div>

        {/* Error Alert */}
        {(location.state as { passwordChanged?: boolean } | null)?.passwordChanged && <p role="status" className="mb-5 rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">Đã đổi mật khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới.</p>}
        {errorMessage && (
          <div role="alert" className="mb-6 p-4 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3 text-red-700 text-sm">
            <AlertCircle className="w-5 h-5 flex-shrink-0 text-red-500 mt-0.5" />
            <span>{errorMessage}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-2">
              Email đăng nhập
            </label>
            <div className="relative">
              <Mail className="w-5 h-5 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" />
              <input
                type="email"
                aria-label="Email đăng nhập"
                autoComplete="username"
                maxLength={150}
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="vidu@wedding.vn"
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
        </form>
        <QuickLogin onComplete={onComplete} />
        <p className="text-xs text-slate-500 mt-4 leading-relaxed">Khách hàng, nhà cung cấp và nhân sự dùng chung trang đăng nhập. Tài khoản kiểm duyệt, kế toán, CSKH do quản trị viên cấp; không đăng ký quyền nội bộ tại đây.</p>

        {/* Quick Test Accounts Box */}
        {import.meta.env.DEV && (
          <details className="demo-login mt-6 pt-3 border-t border-slate-100">
            <summary>Tài khoản thử nghiệm (Development)</summary>
            <div className="grid grid-cols-2 gap-2 text-xs">
              <button
                type="button"
                onClick={() => {
                  setEmail("customer@wedding.com");
                  setPassword("123456");
                }}
                className="px-2.5 py-1.5 rounded-lg border border-rose-200 bg-rose-50/60 hover:bg-rose-100 text-rose-800 text-left font-medium transition cursor-pointer"
              >
                👰 Dâu Rể (Customer)
              </button>
              <button
                type="button"
                onClick={() => {
                  setEmail("vendor.studio@wedding.com");
                  setPassword("123456");
                }}
                className="px-2.5 py-1.5 rounded-lg border border-amber-200 bg-amber-50/60 hover:bg-amber-100 text-amber-800 text-left font-medium transition cursor-pointer"
              >
                📸 Mai Studio (Vendor)
              </button>
              <button
                type="button"
                onClick={() => {
                  setEmail("vendor.palace@wedding.com");
                  setPassword("123456");
                }}
                className="px-2.5 py-1.5 rounded-lg border border-purple-200 bg-purple-50/60 hover:bg-purple-100 text-purple-800 text-left font-medium transition cursor-pointer"
              >
                🏛️ White Palace (Tiệc)
              </button>
              <button
                type="button"
                onClick={() => {
                  setEmail("moderator@wedding.com");
                  setPassword("123456");
                }}
                className="px-2.5 py-1.5 rounded-lg border border-blue-200 bg-blue-50/60 hover:bg-blue-100 text-blue-800 text-left font-medium transition cursor-pointer"
              >
                🛡️ Ban Duyệt (Mod)
              </button>
              <button
                type="button"
                onClick={() => {
                  setEmail("finance@wedding.com");
                  setPassword("123456");
                }}
                className="px-2.5 py-1.5 rounded-lg border border-emerald-200 bg-emerald-50/60 hover:bg-emerald-100 text-emerald-800 text-left font-medium transition cursor-pointer"
              >
                💵 Kế Toán (Finance)
              </button>
              <button
                type="button"
                onClick={() => {
                  setEmail("admin@wedding.com");
                  setPassword("123456");
                }}
                className="px-2.5 py-1.5 rounded-lg border border-slate-300 bg-slate-100 hover:bg-slate-200 text-slate-800 text-left font-medium transition cursor-pointer"
              >
                👑 Quản Trị (Admin)
              </button>
            </div>
          </details>
        )}

        <div className="mt-6 text-center text-sm text-slate-500">
          Chưa có tài khoản?{" "}
          <Link
            to="/register"
            state={location.state}
            onClick={onComplete}
            className="font-semibold text-rose-600 hover:text-rose-700 transition"
          >
            Đăng ký ngay
          </Link>
        </div>
      </div>
    </main>
  );
}
