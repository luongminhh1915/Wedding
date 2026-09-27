import { useState } from 'react';
import { useAuth } from '../auth/hooks/useAuth';
import {
  ShieldCheck, FileCheck, DollarSign, Users,
  LogOut, ChevronRight, Activity
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import ModerationQueue from './ModerationQueue';
import { usePendingListings } from '../vendor-portal/hooks/useListings';

type Tab = 'moderation' | 'finance' | 'vendors' | 'analytics';

const tabs: { id: Tab; label: string; icon: React.ReactNode; taskLabel?: string }[] = [
  { id: 'moderation', label: 'Kiểm Duyệt Bài Đăng', icon: <FileCheck className="w-4 h-4" /> },
  { id: 'finance', label: 'Bảng Kê Ngày 25', icon: <DollarSign className="w-4 h-4" />, taskLabel: 'AI-11' },
  { id: 'vendors', label: 'Quản Lý NCC', icon: <Users className="w-4 h-4" />, taskLabel: 'AI-16' },
  { id: 'analytics', label: 'GMV Dashboard', icon: <Activity className="w-4 h-4" />, taskLabel: 'AI-16' },
];

export default function AdminDashboard() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<Tab>('moderation');
  const { data: pendingListings = [] } = usePendingListings();

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      {/* Header */}
      <header className="bg-slate-900 text-white px-6 py-3.5 flex items-center justify-between sticky top-0 z-30">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl bg-rose-500 text-white flex items-center justify-center shadow-sm">
            <ShieldCheck className="w-5 h-5" />
          </div>
          <div>
            <p className="font-bold text-white leading-none">Back-office Quản Trị</p>
            <p className="text-xs text-slate-400 mt-0.5">Wedding Service Platform</p>
          </div>
          <span className="ml-2 text-xs px-2.5 py-0.5 rounded-full bg-rose-500/20 text-rose-300 font-semibold border border-rose-500/30">
            {user?.role}
          </span>
        </div>

        <div className="flex items-center gap-4">
          <span className="text-sm text-slate-300">
            Nhân sự: <strong className="text-white">{user?.fullName}</strong>
          </span>
          <button
            onClick={handleLogout}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs text-slate-400 hover:text-white rounded-lg hover:bg-slate-700 transition"
          >
            <LogOut className="w-3.5 h-3.5" />
            Đăng xuất
          </button>
        </div>
      </header>

      {/* Tab Navigation */}
      <nav className="bg-white border-b border-slate-200 px-6">
        <div className="flex max-w-7xl mx-auto">
          {tabs.map(tab => {
            const badge = tab.id === 'moderation' && pendingListings.length > 0
              ? String(pendingListings.length)
              : tab.taskLabel;

            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`
                  relative flex items-center gap-2 px-5 py-4 text-sm font-semibold transition border-b-2
                  ${activeTab === tab.id
                    ? 'border-rose-500 text-rose-600 bg-rose-50/30'
                    : 'border-transparent text-slate-500 hover:text-slate-800 hover:border-slate-200'
                  }
                `}
              >
                {tab.icon}
                {tab.label}
                {badge && (
                  <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-bold ${
                    tab.id === 'moderation' && pendingListings.length > 0
                      ? 'bg-amber-100 text-amber-700'
                      : 'bg-slate-100 text-slate-400'
                  }`}>
                    {badge}
                  </span>
                )}
              </button>
            );
          })}
        </div>
      </nav>

      {/* Main Content */}
      <main className="flex-1 max-w-7xl mx-auto w-full px-6 py-8 overflow-hidden">
        {activeTab === 'moderation' && (
          <div className="h-full">
            <div className="mb-6">
              <h1 className="text-xl font-bold text-slate-900">Kiểm Duyệt Bài Đăng Dịch Vụ</h1>
              <p className="text-sm text-slate-500 mt-1">
                Xem xét và phê duyệt bài đăng từ Nhà Cung Cấp theo SLA 24h (BR-008).
              </p>
            </div>
            <ModerationQueue />
          </div>
        )}

        {activeTab !== 'moderation' && (
          <ComingSoon
            title={tabs.find(t => t.id === activeTab)?.label ?? ''}
            taskLabel={tabs.find(t => t.id === activeTab)?.taskLabel ?? ''}
          />
        )}
      </main>
    </div>
  );
}

function ComingSoon({ title, taskLabel }: { title: string; taskLabel: string }) {
  return (
    <div className="flex flex-col items-center justify-center h-64 text-center bg-white rounded-2xl border border-dashed border-slate-300">
      <div className="w-12 h-12 bg-slate-100 rounded-2xl flex items-center justify-center mb-4">
        <ChevronRight className="w-6 h-6 text-slate-400" />
      </div>
      <h3 className="text-lg font-bold text-slate-700">{title}</h3>
      {taskLabel && (
        <p className="text-sm text-slate-500 mt-1">
          Đang phát triển — <span className="font-semibold text-rose-500">{taskLabel}</span>
        </p>
      )}
    </div>
  );
}
