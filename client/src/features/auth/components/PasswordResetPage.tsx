import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { AlertCircle, Lock, Eye, EyeOff } from "lucide-react";
import { apiError, authApi } from "../../journey/api";

export default function PasswordResetPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const [token] = useState(() => new URLSearchParams(window.location.hash.slice(1)).get("token") ?? "");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (window.location.hash.includes("token="))
      window.history.replaceState(window.history.state, "", `${window.location.pathname}${window.location.search}`);
  }, []);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setError("");
    if (new TextEncoder().encode(password).length > 72 || password.length < 8 || !password.trim()) {
      setError("Mật khẩu cần ít nhất 8 ký tự và tối đa 72 byte UTF-8."); return;
    }
    if (password !== confirmPassword) { setError("Mật khẩu nhập lại chưa khớp."); return; }
    if (!token) { setError("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn."); return; }
    setBusy(true);
    try {
      await authApi.confirmPasswordReset(token, password, confirmPassword);
      navigate("/login", { replace: true, state: { passwordChanged: true, from: (location.state as { from?: string } | null)?.from } });
    } catch (requestError) {
      setError(apiError(requestError, "Không đặt lại được mật khẩu. Hãy yêu cầu liên kết mới."));
    } finally { setBusy(false); }
  };

  return <main className="wd wd-page auth-page auth-reset-page"><section className="auth-card" aria-labelledby="reset-password-title">
    <div className="text-center mb-8"><h1 id="reset-password-title">Đặt mật khẩu mới</h1><p className="text-sm text-slate-500 mt-3">Mật khẩu mới sẽ đăng xuất các phiên cũ trên mọi thiết bị.</p></div>
    {error && <p role="alert" className="mb-5 p-4 rounded-xl bg-red-50 border border-red-200 flex gap-3 text-red-700 text-sm"><AlertCircle className="w-5 h-5 shrink-0" />{error}</p>}
    {token ? <form onSubmit={submit} className="space-y-5">
      <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider">Mật khẩu mới<span className="relative mt-2 block"><Lock className="w-5 h-5 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" /><input type={showPassword ? "text" : "password"} autoComplete="new-password" maxLength={72} required value={password} onChange={(event) => setPassword(event.target.value)} className="w-full pl-11 pr-12 py-3 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm" /></span></label>
      <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider">Nhập lại mật khẩu<span className="relative mt-2 block"><Lock className="w-5 h-5 absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400" /><input type={showPassword ? "text" : "password"} autoComplete="new-password" maxLength={72} required value={confirmPassword} onChange={(event) => setConfirmPassword(event.target.value)} className="w-full pl-11 pr-12 py-3 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm" /><button type="button" aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"} className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500" onClick={() => setShowPassword(value => !value)}>{showPassword ? <EyeOff size={18} /> : <Eye size={18} />}</button></span></label>
      <button type="submit" disabled={busy} className="w-full py-3 rounded-xl bg-[#e38c82] text-[#233a42] font-semibold disabled:opacity-50">{busy ? "Đang cập nhật..." : "Lưu mật khẩu mới"}</button>
    </form> : <div className="text-sm text-slate-600"><p>Liên kết không có mã hoặc đã hết hạn.</p><Link className="inline-block mt-4 font-semibold text-[#233a42] underline" to="/login">Quay lại đăng nhập để yêu cầu liên kết mới</Link></div>}
  </section></main>;
}
