import { useAuth } from '../auth/hooks/useAuth';
import { Heart, Calendar, CheckSquare, Wallet, LogOut } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

export default function CustomerDashboard() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      <header className="bg-white border-b border-slate-200 px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <div className="w-9 h-9 rounded-full bg-rose-500 text-white flex items-center justify-center">
            <Heart className="w-5 h-5 fill-current" />
          </div>
          <span className="font-bold text-slate-900">Không Gian Cặp Đôi</span>
        </div>
        <div className="flex items-center gap-4">
          <span className="text-sm text-slate-600">Xin chào, <strong>{user?.fullName}</strong></span>
          <button
            onClick={handleLogout}
            className="p-2 text-slate-500 hover:text-red-600 rounded-lg hover:bg-slate-100 transition"
            title="Đăng xuất"
          >
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </header>

      <main className="flex-1 max-w-7xl mx-auto w-full p-6">
        <div className="mb-8">
          <h1 className="text-2xl font-bold text-slate-900">Bảng Điều Khiển Chuẩn Bị Cưới</h1>
          <p className="text-sm text-slate-500 mt-1">Quản lý dịch vụ, hợp đồng và công cụ cưới của bạn</p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl bg-rose-50 text-rose-600 flex items-center justify-center">
              <Calendar className="w-6 h-6" />
            </div>
            <div>
              <p className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Thiệp & RSVP</p>
              <h3 className="text-lg font-bold text-slate-900">Thiệp Cưới Online</h3>
            </div>
          </div>

          <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center">
              <Wallet className="w-6 h-6" />
            </div>
            <div>
              <p className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Ngân Sách</p>
              <h3 className="text-lg font-bold text-slate-900">Dự Toán Chi Phí</h3>
            </div>
          </div>

          <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center">
              <CheckSquare className="w-6 h-6" />
            </div>
            <div>
              <p className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Tiến Độ</p>
              <h3 className="text-lg font-bold text-slate-900">Checklist 12 Tháng</h3>
            </div>
          </div>

          <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-sm flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
              <Heart className="w-6 h-6" />
            </div>
            <div>
              <p className="text-xs text-slate-500 uppercase tracking-wider font-semibold">Hợp Đồng</p>
              <h3 className="text-lg font-bold text-slate-900">Xác Thực 2 Chiều</h3>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
