import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { Heart, Lock, Mail, AlertCircle, ArrowRight } from 'lucide-react';

export default function LoginForm() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const { login, isLoggingIn } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage('');

    try {
      const res = await login({ email, password });
      // Điều hướng theo phân quyền RBAC
      if (res.user.role === 'VendorOwner') {
        navigate('/vendor/dashboard');
      } else if (['Moderator', 'Finance', 'CustomerCare', 'SuperAdmin'].includes(res.user.role)) {
        navigate('/admin/dashboard');
      } else {
        navigate('/customer/dashboard');
      }
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      setErrorMessage(error.response?.data?.message || 'Đăng nhập không thành công. Vui lòng kiểm tra lại.');
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-rose-50 via-white to-pink-50 flex items-center justify-center p-4">
      <div className="w-full max-w-md bg-white rounded-2xl shadow-xl shadow-rose-100 border border-rose-100 p-8">
        {/* Brand Header */}
        <div className="text-center mb-8">
          <Link to="/" className="inline-flex items-center justify-center w-12 h-12 rounded-full bg-rose-500 text-white shadow-md shadow-rose-200 mb-3">
            <Heart className="w-6 h-6 fill-current" />
          </Link>
          <h2 className="text-2xl font-bold text-slate-900">Chào Mừng Trở Lại</h2>
          <p className="text-sm text-slate-500 mt-1">Đăng nhập tài khoản Wedding Platform</p>
        </div>

        {/* Error Alert */}
        {errorMessage && (
          <div className="mb-6 p-4 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3 text-red-700 text-sm">
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
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                className="w-full pl-11 pr-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
              />
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

        {/* Quick Test Accounts Box */}
        <div className="mt-6 pt-5 border-t border-slate-100">
          <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2 text-center">
            🚀 Tài khoản Test Nhanh (Mật khẩu: 123456)
          </p>
          <div className="grid grid-cols-2 gap-2 text-xs">
            <button
              type="button"
              onClick={() => {
                setEmail('customer@wedding.com');
                setPassword('123456');
              }}
              className="px-2.5 py-1.5 rounded-lg border border-rose-200 bg-rose-50/60 hover:bg-rose-100 text-rose-800 text-left font-medium transition cursor-pointer"
            >
              👰 Dâu Rể (Customer)
            </button>
            <button
              type="button"
              onClick={() => {
                setEmail('vendor.studio@wedding.com');
                setPassword('123456');
              }}
              className="px-2.5 py-1.5 rounded-lg border border-amber-200 bg-amber-50/60 hover:bg-amber-100 text-amber-800 text-left font-medium transition cursor-pointer"
            >
              📸 Mai Studio (Vendor)
            </button>
            <button
              type="button"
              onClick={() => {
                setEmail('vendor.palace@wedding.com');
                setPassword('123456');
              }}
              className="px-2.5 py-1.5 rounded-lg border border-purple-200 bg-purple-50/60 hover:bg-purple-100 text-purple-800 text-left font-medium transition cursor-pointer"
            >
              🏛️ White Palace (Tiệc)
            </button>
            <button
              type="button"
              onClick={() => {
                setEmail('moderator@wedding.com');
                setPassword('123456');
              }}
              className="px-2.5 py-1.5 rounded-lg border border-blue-200 bg-blue-50/60 hover:bg-blue-100 text-blue-800 text-left font-medium transition cursor-pointer"
            >
              🛡️ Ban Duyệt (Mod)
            </button>
            <button
              type="button"
              onClick={() => {
                setEmail('finance@wedding.com');
                setPassword('123456');
              }}
              className="px-2.5 py-1.5 rounded-lg border border-emerald-200 bg-emerald-50/60 hover:bg-emerald-100 text-emerald-800 text-left font-medium transition cursor-pointer"
            >
              💵 Kế Toán (Finance)
            </button>
            <button
              type="button"
              onClick={() => {
                setEmail('admin@wedding.com');
                setPassword('123456');
              }}
              className="px-2.5 py-1.5 rounded-lg border border-slate-300 bg-slate-100 hover:bg-slate-200 text-slate-800 text-left font-medium transition cursor-pointer"
            >
              👑 Quản Trị (Admin)
            </button>
          </div>
        </div>

        <div className="mt-6 text-center text-sm text-slate-500">
          Chưa có tài khoản?{' '}
          <Link to="/register" className="font-semibold text-rose-600 hover:text-rose-700 transition">
            Đăng ký ngay
          </Link>
        </div>
      </div>
    </div>
  );
}
