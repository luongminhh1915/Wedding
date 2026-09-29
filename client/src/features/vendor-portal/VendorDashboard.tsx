import { useState } from 'react';
import { useAuth } from '../auth/hooks/useAuth';
import {
  Building2, MessageSquare, FileText, QrCode,
  LogOut, LayoutList
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import axiosClient from '../../services/axiosClient';
import ListingTable from './components/ListingTable';
import { LeadInbox } from './components/LeadInbox';
import { ContractList } from './components/ContractList';
import { SettlementDashboard } from './components/SettlementDashboard';

type Tab = 'listings' | 'leads' | 'contracts' | 'settlement';

const tabs: { id: Tab; label: string; icon: React.ReactNode; badge?: string }[] = [
  { id: 'listings', label: 'Quản Lý Bài Đăng', icon: <LayoutList className="w-4 h-4" /> },
  { id: 'leads', label: 'Hộp Thư Lead', icon: <MessageSquare className="w-4 h-4" /> },
  { id: 'contracts', label: 'Hợp Đồng 2 Chiều', icon: <FileText className="w-4 h-4" /> },
  { id: 'settlement', label: 'Đối Soát VietQR', icon: <QrCode className="w-4 h-4" /> },
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
        <div className="flex gap-0 max-w-7xl mx-auto">
          {tabs.map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`
                relative flex items-center gap-2 px-4 py-4 text-sm font-semibold transition border-b-2
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
        {activeTab === 'listings' && (
          <ListingTable categories={categories} />
        )}

        {activeTab === 'leads' && <LeadInbox />}
        {activeTab === 'contracts' && <ContractList />}
        {activeTab === 'settlement' && <SettlementDashboard />}
      </main>
    </div>
  );
}
