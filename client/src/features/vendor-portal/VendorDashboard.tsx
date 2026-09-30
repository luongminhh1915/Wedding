import { useState } from 'react';
import { useAuth } from '../auth/hooks/useAuth';
import {
  Building2, MessageSquare, HeartHandshake, ShieldCheck,
  LogOut, LayoutList
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import axiosClient from '../../services/axiosClient';
import ListingTable from './components/ListingTable';
import { LeadInbox } from './components/LeadInbox';
import { ContractList } from './components/ContractList';
import { SettlementDashboard } from './components/SettlementDashboard';

type Tab = 'listings' | 'leads' | 'contracts-couple' | 'contracts-admin';

const tabs: { id: Tab; label: string; icon: React.ReactNode; badge?: string }[] = [
  { id: 'listings', label: 'Quản Lý Bài Đăng', icon: <LayoutList className="w-4 h-4" /> },
  { id: 'leads', label: 'Hộp Thư Lead', icon: <MessageSquare className="w-4 h-4" /> },
  { id: 'contracts-couple', label: 'HĐ Với Cô Dâu Chú Rể', icon: <HeartHandshake className="w-4 h-4" /> },
  { id: 'contracts-admin', label: 'HĐ Với Admin', icon: <ShieldCheck className="w-4 h-4" /> },
];

export default function VendorDashboard() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<Tab>('listings');

  // Lấy danh sách ngành hàng thực từ database (không hardcode ID nữa)
  const { data: categories = [] } = useQuery<{ id: string; name: string }[]>({
    queryKey: ['categories'],
    queryFn: async () => {
      const res = await axiosClient.get('/api/categories');
      return res.data.map((c: { id: string; name: string }) => ({ id: c.id, name: c.name }));
    },
    staleTime: 5 * 60 * 1000,
  });

  const handleLogout = () => {
    logout();
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 px-6 py-3.5 flex items-center justify-between sticky top-0 z-30">
        <div className="flex items-center gap-3">
          <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-amber-400 to-orange-500 text-white flex items-center justify-center shadow-sm">
            <Building2 className="w-5 h-5" />
          </div>
          <div>
            <p className="font-bold text-slate-900 leading-none">{user?.vendorBrandName || 'Portal Đối Tác'}</p>
            <p className="text-xs text-slate-500 mt-0.5">Đại diện: {user?.fullName}</p>
          </div>
          <span className="ml-2 text-xs px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-700 font-semibold border border-emerald-200">
            BR-001: Vendor Owner
          </span>
        </div>

        <button
          onClick={handleLogout}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs text-slate-500 hover:text-red-600 rounded-lg hover:bg-slate-100 transition font-medium"
          title="Đăng xuất"
        >
          <LogOut className="w-3.5 h-3.5" />
          Đăng xuất
        </button>
      </header>

      {/* Tab Navigation */}
      <nav className="bg-white border-b border-slate-100 px-6">
        <div className="flex gap-0 max-w-7xl mx-auto overflow-x-auto">
          {tabs.map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`
                relative flex items-center gap-2 px-4 py-4 text-sm font-semibold transition border-b-2 whitespace-nowrap cursor-pointer
                ${activeTab === tab.id
                  ? 'border-rose-500 text-rose-600'
                  : 'border-transparent text-slate-500 hover:text-slate-800 hover:border-slate-300'
                }
              `}
            >
              {tab.icon}
              {tab.label}
              {tab.badge && (
                <span className="text-[10px] px-1.5 py-0.5 bg-slate-100 text-slate-400 rounded font-bold">
                  {tab.badge}
                </span>
              )}
            </button>
          ))}
        </div>
      </nav>

      {/* Main Content */}
      <main className="flex-1 max-w-7xl mx-auto w-full px-6 py-8">
        {/* Phân hệ chuyển đổi nhanh giữa 2 loại hợp đồng */}
        {(activeTab === 'contracts-couple' || activeTab === 'contracts-admin') && (
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-6 pb-4 border-b border-slate-200">
            <div className="bg-slate-100 p-1 rounded-2xl border border-slate-200 inline-flex gap-1 shadow-2xs">
              <button
                onClick={() => setActiveTab('contracts-couple')}
                className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold transition cursor-pointer ${
                  activeTab === 'contracts-couple'
                    ? 'bg-white text-rose-600 shadow-xs'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                <HeartHandshake className="w-3.5 h-3.5 text-rose-500" />
                <span>1. Hợp Đồng Với Cô Dâu Chú Rể</span>
              </button>
              <button
                onClick={() => setActiveTab('contracts-admin')}
                className={`flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold transition cursor-pointer ${
                  activeTab === 'contracts-admin'
                    ? 'bg-white text-rose-600 shadow-xs'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                <ShieldCheck className="w-3.5 h-3.5 text-rose-500" />
                <span>2. Hợp Đồng Với Admin (Hoa Hồng)</span>
              </button>
            </div>
            <p className="text-xs text-slate-400">
              Phân hệ quản lý: <strong>HĐ Khách Hàng (Cô dâu chú rể)</strong> và <strong>HĐ Đối Tác (Admin)</strong>
            </p>
          </div>
        )}

        {activeTab === 'listings' && (
          <ListingTable categories={categories} />
        )}

        {activeTab === 'leads' && <LeadInbox />}
        {activeTab === 'contracts-couple' && <ContractList />}
        {activeTab === 'contracts-admin' && <SettlementDashboard />}
      </main>
    </div>
  );
}
