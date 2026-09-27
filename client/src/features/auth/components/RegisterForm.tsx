import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import { Heart, Building2, User, AlertCircle, ArrowRight, Sparkles } from 'lucide-react';

export default function RegisterForm() {
  const [tab, setTab] = useState<'customer' | 'vendor'>('customer');
  const [errorMessage, setErrorMessage] = useState('');
  const { registerCustomer, registerVendor, isRegisteringCustomer, isRegisteringVendor } = useAuth();
  const navigate = useNavigate();

  // Customer Form State
  const [custFullName, setCustFullName] = useState('');
  const [custEmail, setCustEmail] = useState('');
  const [custPhone, setCustPhone] = useState('');
  const [custPassword, setCustPassword] = useState('');

  // Vendor Form State (BR-001)
  const [vendFullName, setVendFullName] = useState('');
  const [vendEmail, setVendEmail] = useState('');
  const [vendPhone, setVendPhone] = useState('');
  const [vendBrandName, setVendBrandName] = useState('');
  const [vendCity, setVendCity] = useState('TP. Hồ Chí Minh');
  const [vendCommissionRate, setVendCommissionRate] = useState(0.08);
  const [vendPassword, setVendPassword] = useState('');

  const handleCustomerSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage('');
    try {
      await registerCustomer({
        fullName: custFullName,
        email: custEmail,
        phoneNumber: custPhone,
        password: custPassword,
      });
      navigate('/customer/dashboard');
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      setErrorMessage(error.response?.data?.message || 'Đăng ký không thành công.');
    }
  };

  const handleVendorSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage('');
    try {
      await registerVendor({
        fullName: vendFullName,
        email: vendEmail,
        phoneNumber: vendPhone,
        brandName: vendBrandName,
        city: vendCity,
        commissionRate: vendCommissionRate,
        password: vendPassword,
      });
      navigate('/vendor/dashboard');
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      setErrorMessage(error.response?.data?.message || 'Đăng ký đối tác không thành công.');
    }
  };

  const isLoading = isRegisteringCustomer || isRegisteringVendor;

  return (
    <div className="min-h-screen bg-gradient-to-br from-rose-50 via-white to-pink-50 flex items-center justify-center p-4 py-12">
      <div className="w-full max-w-lg bg-white rounded-3xl shadow-xl shadow-rose-100 border border-rose-100 p-8">
        {/* Header */}
        <div className="text-center mb-6">
          <Link to="/" className="inline-flex items-center justify-center w-12 h-12 rounded-full bg-rose-500 text-white shadow-md shadow-rose-200 mb-3">
            <Heart className="w-6 h-6 fill-current" />
          </Link>
          <h2 className="text-2xl font-bold text-slate-900">Tạo Tài Khoản Mới</h2>
          <p className="text-sm text-slate-500 mt-1">Đồng hành cùng nền tảng dịch vụ cưới thông minh</p>
        </div>

        {/* Tab Selection */}
        <div className="flex bg-slate-100 p-1.5 rounded-2xl mb-6">
          <button
            type="button"
            onClick={() => { setTab('customer'); setErrorMessage(''); }}
            className={`flex-1 py-2.5 rounded-xl text-sm font-semibold flex items-center justify-center gap-2 transition ${
              tab === 'customer'
                ? 'bg-white text-rose-600 shadow-sm'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <User className="w-4 h-4" />
            <span>Cô Dâu / Chú Rể</span>
          </button>

          <button
            type="button"
            onClick={() => { setTab('vendor'); setErrorMessage(''); }}
            className={`flex-1 py-2.5 rounded-xl text-sm font-semibold flex items-center justify-center gap-2 transition ${
              tab === 'vendor'
                ? 'bg-white text-rose-600 shadow-sm'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            <Building2 className="w-4 h-4" />
            <span>Đối Tác Nhà Cung Cấp</span>
          </button>
        </div>

        {/* Error Alert */}
        {errorMessage && (
          <div className="mb-6 p-4 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3 text-red-700 text-sm">
            <AlertCircle className="w-5 h-5 flex-shrink-0 text-red-500 mt-0.5" />
            <span>{errorMessage}</span>
          </div>
        )}

        {/* Customer Register Form */}
        {tab === 'customer' ? (
          <form onSubmit={handleCustomerSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                Họ và tên
              </label>
              <input
                type="text"
                required
                value={custFullName}
                onChange={(e) => setCustFullName(e.target.value)}
                placeholder="Nguyễn Văn A"
                className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                Email
              </label>
              <input
                type="email"
                required
                value={custEmail}
                onChange={(e) => setCustEmail(e.target.value)}
                placeholder="dourre@gmail.com"
                className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                Số điện thoại
              </label>
              <input
                type="tel"
                required
                value={custPhone}
                onChange={(e) => setCustPhone(e.target.value)}
                placeholder="0912345678"
                className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                Mật khẩu
              </label>
              <input
                type="password"
                required
                value={custPassword}
                onChange={(e) => setCustPassword(e.target.value)}
                placeholder="Tối thiểu 6 ký tự"
                className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
              />
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full mt-2 py-3 px-4 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 text-white font-semibold text-sm hover:from-rose-600 hover:to-pink-600 shadow-md shadow-rose-200 transition disabled:opacity-50 flex items-center justify-center gap-2"
            >
              <span>{isLoading ? 'Đang tạo tài khoản...' : 'Đăng Ký Khách Hàng'}</span>
              <ArrowRight className="w-4 h-4" />
            </button>
          </form>
        ) : (
          /* Vendor Register Form (BR-001) */
          <form onSubmit={handleVendorSubmit} className="space-y-4">
            <div className="p-3 bg-amber-50 rounded-xl border border-amber-200 text-amber-800 text-xs flex items-center gap-2">
              <Sparkles className="w-4 h-4 text-amber-600 flex-shrink-0" />
              <span>Chính sách Lean MVP (BR-001): 1 Nhà cung cấp sở hữu 1 tài khoản quản trị duy nhất.</span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Tên thương hiệu NCC
                </label>
                <input
                  type="text"
                  required
                  value={vendBrandName}
                  onChange={(e) => setVendBrandName(e.target.value)}
                  placeholder="La Rose Bridal"
                  className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Người đại diện
                </label>
                <input
                  type="text"
                  required
                  value={vendFullName}
                  onChange={(e) => setVendFullName(e.target.value)}
                  placeholder="Trần Thị B"
                  className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Email liên hệ
                </label>
                <input
                  type="email"
                  required
                  value={vendEmail}
                  onChange={(e) => setVendEmail(e.target.value)}
                  placeholder="contact@larose.vn"
                  className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Hotline NCC
                </label>
                <input
                  type="tel"
                  required
                  value={vendPhone}
                  onChange={(e) => setVendPhone(e.target.value)}
                  placeholder="0987654321"
                  className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Thành phố
                </label>
                <select
                  value={vendCity}
                  onChange={(e) => setVendCity(e.target.value)}
                  className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
                >
                  <option value="TP. Hồ Chí Minh">TP. Hồ Chí Minh</option>
                  <option value="Hà Nội">Hà Nội</option>
                  <option value="Đà Nẵng">Đà Nẵng</option>
                  <option value="Khác">Tỉnh thành khác</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                  Tỷ lệ hoa hồng (FM-001)
                </label>
                <select
                  value={vendCommissionRate}
                  onChange={(e) => setVendCommissionRate(parseFloat(e.target.value))}
                  className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
                >
                  <option value={0.03}>Tiệc cưới: 3%</option>
                  <option value={0.08}>Trang trí / Thiệp: 8%</option>
                  <option value={0.10}>Quay chụp / Váy / Makeup: 10%</option>
                  <option value={0.12}>Wedding Planner: 12%</option>
                </select>
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 uppercase tracking-wider mb-1.5">
                Mật khẩu quản trị
              </label>
              <input
                type="password"
                required
                value={vendPassword}
                onChange={(e) => setVendPassword(e.target.value)}
                placeholder="Tối thiểu 6 ký tự"
                className="w-full px-4 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-slate-900 text-sm focus:outline-none focus:ring-2 focus:ring-rose-500 focus:bg-white transition"
              />
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full mt-2 py-3 px-4 rounded-xl bg-gradient-to-r from-amber-500 to-rose-500 text-white font-semibold text-sm hover:from-amber-600 hover:to-rose-600 shadow-md shadow-amber-200 transition disabled:opacity-50 flex items-center justify-center gap-2"
            >
              <span>{isLoading ? 'Đang tạo hồ sơ NCC...' : 'Đăng Ký Đối Tác NCC'}</span>
              <ArrowRight className="w-4 h-4" />
            </button>
          </form>
        )}

        <div className="mt-6 text-center text-sm text-slate-500">
          Đã có tài khoản?{' '}
          <Link to="/login" className="font-semibold text-rose-600 hover:text-rose-700 transition">
            Đăng nhập
          </Link>
        </div>
      </div>
    </div>
  );
}
