import { useState } from 'react';
import { useAuth } from '../auth/hooks/useAuth';
import {
  Heart, Calendar, CheckSquare, Wallet, LogOut,
  FileCheck2, Compass, ChevronRight
} from 'lucide-react';
import { useNavigate, Link } from 'react-router-dom';
import { MyContracts } from './components/MyContracts';

type CustomerTab = 'contracts' | 'invitations' | 'budget' | 'checklist';

export default function CustomerDashboard() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<CustomerTab>('contracts');

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col" style={{ fontFamily: "'Plus Jakarta Sans', sans-serif" }}>
      {/* Header */}
      <header className="bg-white border-b border-slate-200 px-6 py-4 flex items-center justify-between sticky top-0 z-40">
        <div className="flex items-center gap-3">
          <Link to="/" className="w-9 h-9 rounded-full bg-gradient-to-br from-rose-500 to-amber-500 text-white flex items-center justify-center shadow-sm">
            <Heart className="w-5 h-5 fill-current" />
          </Link>
          <div>
            <span className="font-extrabold text-slate-900 text-base" style={{ fontFamily: "'Playfair Display', serif" }}>
              Không Gian Cặp Đôi
            </span>
            <p className="text-[11px] text-slate-400">Wedding Planner & Quản Lý Dịch Vụ</p>
          </div>
        </div>

        <div className="flex items-center gap-4">
          <Link
            to="/browse"
            className="hidden sm:inline-flex items-center gap-1 text-xs font-semibold text-rose-600 hover:text-rose-700 bg-rose-50 hover:bg-rose-100 px-3 py-1.5 rounded-xl transition border border-rose-200"
          >
            <Compass className="w-3.5 h-3.5" />
            Khám Phá Nhà Cung Cấp
          </Link>

          <span className="text-xs text-slate-600">
            Xin chào, <strong className="text-slate-800">{user?.fullName}</strong>
          </span>

          <button
            onClick={handleLogout}
            className="p-2 text-slate-400 hover:text-red-600 rounded-xl hover:bg-slate-100 transition"
            title="Đăng xuất"
          >
            <LogOut className="w-4 h-4" />
          </button>
        </div>
      </header>

      {/* Main Container */}
      <main className="flex-1 max-w-7xl mx-auto w-full px-6 py-8">
        {/* Quick Nav Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          <button
            onClick={() => setActiveTab('contracts')}
            className={`p-5 rounded-2xl border text-left transition-all duration-200 shadow-xs flex items-center gap-4 ${
              activeTab === 'contracts'
                ? 'bg-rose-50/70 border-rose-300 ring-2 ring-rose-200'
                : 'bg-white border-slate-200 hover:border-rose-200 hover:bg-rose-50/30'
            }`}
          >
            <div className={`w-12 h-12 rounded-xl flex items-center justify-center shrink-0 ${
              activeTab === 'contracts' ? 'bg-rose-500 text-white' : 'bg-rose-50 text-rose-600'
            }`}>
              <FileCheck2 className="w-6 h-6" />
            </div>
            <div>
              <p className="text-[11px] text-slate-400 uppercase tracking-wider font-extrabold">Cốt Lõi Sàn</p>
              <h3 className="text-sm font-black text-slate-900">Hợp Đồng & Cọc (72h)</h3>
              <p className="text-[11px] text-rose-600 font-semibold mt-0.5">Xác thực 2 chiều</p>
            </div>
          </button>

          <button
            onClick={() => setActiveTab('invitations')}
            className={`p-5 rounded-2xl border text-left transition-all duration-200 shadow-xs flex items-center gap-4 ${
              activeTab === 'invitations'
                ? 'bg-rose-50/70 border-rose-300 ring-2 ring-rose-200'
                : 'bg-white border-slate-200 hover:border-slate-300'
            }`}
          >
            <div className="w-12 h-12 rounded-xl bg-pink-50 text-pink-600 flex items-center justify-center shrink-0">
              <Calendar className="w-6 h-6" />
            </div>
            <div>
              <p className="text-[11px] text-slate-400 uppercase tracking-wider font-extrabold">Epic 7</p>
              <h3 className="text-sm font-black text-slate-900">Thiệp Cưới & RSVP</h3>
              <p className="text-[11px] text-slate-500 font-medium mt-0.5">Mẫu thiệp online</p>
            </div>
          </button>

          <button
            onClick={() => setActiveTab('budget')}
            className={`p-5 rounded-2xl border text-left transition-all duration-200 shadow-xs flex items-center gap-4 ${
              activeTab === 'budget'
                ? 'bg-rose-50/70 border-rose-300 ring-2 ring-rose-200'
                : 'bg-white border-slate-200 hover:border-slate-300'
            }`}
          >
            <div className="w-12 h-12 rounded-xl bg-amber-50 text-amber-600 flex items-center justify-center shrink-0">
              <Wallet className="w-6 h-6" />
            </div>
            <div>
              <p className="text-[11px] text-slate-400 uppercase tracking-wider font-extrabold">FM-006</p>
              <h3 className="text-sm font-black text-slate-900">Dự Toán Chi Phí</h3>
              <p className="text-[11px] text-slate-500 font-medium mt-0.5">Kế hoạch ngân sách</p>
            </div>
          </button>

          <button
            onClick={() => setActiveTab('checklist')}
            className={`p-5 rounded-2xl border text-left transition-all duration-200 shadow-xs flex items-center gap-4 ${
              activeTab === 'checklist'
                ? 'bg-rose-50/70 border-rose-300 ring-2 ring-rose-200'
                : 'bg-white border-slate-200 hover:border-slate-300'
            }`}
          >
            <div className="w-12 h-12 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center shrink-0">
              <CheckSquare className="w-6 h-6" />
            </div>
            <div>
              <p className="text-[11px] text-slate-400 uppercase tracking-wider font-extrabold">Checklist</p>
              <h3 className="text-sm font-black text-slate-900">Kế Hoạch 12 Tháng</h3>
              <p className="text-[11px] text-slate-500 font-medium mt-0.5">Tiến độ ngày cưới</p>
            </div>
          </button>
        </div>

        {/* Tab Content Display */}
        {activeTab === 'contracts' && <MyContracts />}

        {activeTab === 'invitations' && (
          <FeatureComingSoon
            title="Trình Tạo Thiệp Cưới Online & Điểm Danh RSVP 1 Chạm"
            phase="Task AI-12 (Phase 5: Growth Hook)"
            description="Cho phép dâu rể chọn template thiệp, tạo link public /invitation/:slug có nhạc nền, định vị bàn tiệc và khách mời RSVP 1 chạm."
          />
        )}

        {activeTab === 'budget' && (
          <FeatureComingSoon
            title="Công Cụ Dự Toán Ngân Sách Cưới Thông Minh"
            phase="Task AI-13 (Phase 5: Growth Hook)"
            description="Tự động tính toán ngân sách còn lại (Remaining Budget = Planned - Actual) theo quy tắc FM-006 giúp kiểm soát bội chi."
          />
        )}

        {activeTab === 'checklist' && (
          <FeatureComingSoon
            title="Checklist 12 Tháng Chuẩn Bị Ngày Cưới"
            phase="Task AI-13 (Phase 5: Growth Hook)"
            description="Lộ trình từng bước chi tiết từ 12 tháng, 6 tháng, 3 tháng đến tuần lễ cưới có checkbox đánh dấu hoàn thành."
          />
        )}
      </main>
    </div>
  );
}

function FeatureComingSoon({ title, phase, description }: { title: string; phase: string; description: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-6 text-center bg-white rounded-3xl border border-dashed border-slate-300">
      <div className="w-12 h-12 bg-rose-50 rounded-2xl flex items-center justify-center mb-4">
        <ChevronRight className="w-6 h-6 text-rose-400" />
      </div>
      <span className="text-xs px-3 py-1 rounded-full bg-rose-100 text-rose-700 font-bold uppercase tracking-wider mb-2">
        {phase}
      </span>
      <h3 className="text-lg font-bold text-slate-800">{title}</h3>
      <p className="text-xs text-slate-500 mt-1.5 max-w-md leading-relaxed">
        {description}
      </p>
    </div>
  );
}
