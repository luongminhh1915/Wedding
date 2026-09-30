import { useState, useEffect } from 'react';
import {
  DollarSign, TrendingUp, AlertTriangle, CheckCircle,
  Clock, Search, Download, RefreshCw, ChevronDown, ChevronUp,
  Building2, FileText, Coins, CreditCard, X, CheckCircle2,
  Receipt, Calendar, Phone, Layers
} from 'lucide-react';

// ─── Types ────────────────────────────────────────────────────────────────────
interface AdminCommissionItem {
  id: string;
  commissionAmount: number;
  dueDate: string;
  status: string;
  paidAt?: string | null;
  paymentReferenceCode?: string | null;
}

interface ContractFinancialRow {
  contractId: string;
  contractCode: string;
  vendorId: string;
  vendorBrandName: string;
  ownerName: string;
  ownerEmail: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  contractValue: number;
  depositAmount: number;
  totalCommissionDue: number;
  totalCommissionPaid: number;
  totalCommissionPending: number;
  totalCommissionOverdue: number;
  commissionRate: number;
  paymentStatus: 'Paid' | 'PartiallyPaid' | 'Pending' | 'Overdue';
  createdAt: string;
  weddingDate?: string;
  dueDate?: string;
  items?: AdminCommissionItem[];
}

interface AdminFinancialOverview {
  totalVendors: number;
  totalContracts: number;
  totalGmv: number;
  totalDepositReceived: number;
  totalCommissionDue: number;
  totalCommissionPaid: number;
  totalCommissionPending: number;
  totalCommissionOverdue: number;
  orders: ContractFinancialRow[];
  vendors: ContractFinancialRow[];
}

// ─── Helpers ──────────────────────────────────────────────────────────────────
const fmt = (n: number) =>
  new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(n);

const statusConfig: Record<string, { label: string; bg: string; text: string; dot: string }> = {
  Paid:         { label: 'Đã thanh toán', bg: 'bg-emerald-50', text: 'text-emerald-700', dot: 'bg-emerald-500' },
  PartiallyPaid:{ label: 'Một phần',      bg: 'bg-blue-50',    text: 'text-blue-700',    dot: 'bg-blue-500' },
  Pending:      { label: 'Chờ thanh toán',bg: 'bg-amber-50',   text: 'text-amber-700',   dot: 'bg-amber-500' },
  Overdue:      { label: 'Quá hạn',       bg: 'bg-red-50',     text: 'text-red-700',     dot: 'bg-red-500'   },
};

const MONTHS = ['Tất cả', 'Tháng 1','Tháng 2','Tháng 3','Tháng 4','Tháng 5','Tháng 6',
                'Tháng 7','Tháng 8','Tháng 9','Tháng 10','Tháng 11','Tháng 12'];

// ─── Main Component ───────────────────────────────────────────────────────────
export default function FinancialDashboard() {
  const [data, setData] = useState<AdminFinancialOverview | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [selectedMonth, setSelectedMonth] = useState<number>(0);
  const [selectedYear, setSelectedYear] = useState<number>(new Date().getFullYear());
  const [expandedOrder, setExpandedOrder] = useState<string | null>(null);

  // Modal ghi nhận thanh toán cho từng đơn
  const [paymentTarget, setPaymentTarget] = useState<ContractFinancialRow | null>(null);
  const [paymentAmount, setPaymentAmount] = useState<string>('');
  const [paymentRef, setPaymentRef] = useState<string>('');
  const [isSubmittingPayment, setIsSubmittingPayment] = useState(false);
  const [paymentError, setPaymentError] = useState<string | null>(null);
  const [paymentSuccess, setPaymentSuccess] = useState<string | null>(null);

  const fetchData = async () => {
    setLoading(true);
    setError(null);
    try {
      const token = localStorage.getItem('token');
      const params = new URLSearchParams();
      if (selectedMonth > 0) params.set('month', String(selectedMonth));
      params.set('year', String(selectedYear));

      const res = await fetch(`/api/commissions/admin/overview?${params}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!res.ok) throw new Error(`Lỗi ${res.status}`);
      const json = await res.json();
      setData(json);
    } catch (e: any) {
      setError(e.message || 'Không thể tải dữ liệu tài chính.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { fetchData(); }, [selectedMonth, selectedYear]);

  const openPaymentModal = (order: ContractFinancialRow) => {
    const remaining = order.totalCommissionPending + order.totalCommissionOverdue;
    setPaymentTarget(order);
    setPaymentAmount(remaining > 0 ? String(remaining) : '');
    setPaymentRef('');
    setPaymentError(null);
    setPaymentSuccess(null);
  };

  const closePaymentModal = () => {
    setPaymentTarget(null);
    setPaymentAmount('');
    setPaymentRef('');
    setPaymentError(null);
    setPaymentSuccess(null);
  };

  const handleRecordPayment = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!paymentTarget) return;

    const rawNum = Number(paymentAmount.replace(/[^\d]/g, ''));
    if (!rawNum || rawNum <= 0) {
      setPaymentError('Vui lòng nhập số tiền hợp lệ lớn hơn 0 VNĐ.');
      return;
    }

    setIsSubmittingPayment(true);
    setPaymentError(null);
    setPaymentSuccess(null);

    try {
      const token = localStorage.getItem('token');
      const res = await fetch('/api/commissions/admin/record-payment', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          contractId: paymentTarget.contractId,
          vendorId: paymentTarget.vendorId,
          amount: rawNum,
          paymentReference: paymentRef.trim() || undefined,
        }),
      });

      const result = await res.json();

      if (!res.ok || !result.success) {
        setPaymentError(result.message || 'Ghi nhận thanh toán thất bại.');
        return;
      }

      setPaymentSuccess(result.message || 'Ghi nhận thanh toán hoa hồng thành công!');
      await fetchData();

      setTimeout(() => {
        closePaymentModal();
      }, 1500);
    } catch (err: any) {
      setPaymentError(err.message || 'Có lỗi xảy ra khi kết nối máy chủ.');
    } finally {
      setIsSubmittingPayment(false);
    }
  };

  const ordersList = data?.orders ?? data?.vendors ?? [];

  const filtered = ordersList.filter(o => {
    const q = search.toLowerCase();
    const matchSearch =
      o.contractCode.toLowerCase().includes(q) ||
      o.vendorBrandName.toLowerCase().includes(q) ||
      o.customerName.toLowerCase().includes(q) ||
      (o.customerPhone && o.customerPhone.includes(q));
    const matchStatus = statusFilter === 'all' || o.paymentStatus === statusFilter;
    return matchSearch && matchStatus;
  });

  const cards = data ? [
    { label: 'Tổng GMV', value: fmt(data.totalGmv), sub: `${data.totalContracts} hợp đồng`, icon: <TrendingUp className="w-5 h-5"/>, grad: 'from-violet-500 to-purple-600', light: 'bg-violet-50 text-violet-700' },
    { label: 'Tiền cọc đã nhận', value: fmt(data.totalDepositReceived), sub: `${data.totalVendors} nhà cung cấp`, icon: <Coins className="w-5 h-5"/>, grad: 'from-sky-500 to-blue-600', light: 'bg-sky-50 text-sky-700' },
    { label: 'Tổng hoa hồng phải thu', value: fmt(data.totalCommissionDue), sub: `Đã thu: ${fmt(data.totalCommissionPaid)}`, icon: <DollarSign className="w-5 h-5"/>, grad: 'from-emerald-500 to-teal-600', light: 'bg-emerald-50 text-emerald-700' },
    { label: 'Chưa thu (Chờ / Quá hạn)', value: fmt(data.totalCommissionPending + data.totalCommissionOverdue), sub: `Quá hạn: ${fmt(data.totalCommissionOverdue)}`, icon: <AlertTriangle className="w-5 h-5"/>, grad: 'from-rose-500 to-red-600', light: 'bg-rose-50 text-rose-700' },
  ] : [];

  const years = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i);

  return (
    <div className="space-y-6">
      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative">
          <select value={selectedMonth} onChange={e => setSelectedMonth(Number(e.target.value))}
            className="appearance-none pl-3 pr-8 py-2 text-sm rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm focus:ring-2 focus:ring-rose-300 focus:outline-none cursor-pointer">
            {MONTHS.map((m, i) => <option key={i} value={i}>{m}</option>)}
          </select>
          <ChevronDown className="w-3.5 h-3.5 text-slate-400 absolute right-2.5 top-1/2 -translate-y-1/2 pointer-events-none"/>
        </div>
        <div className="relative">
          <select value={selectedYear} onChange={e => setSelectedYear(Number(e.target.value))}
            className="appearance-none pl-3 pr-8 py-2 text-sm rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm focus:ring-2 focus:ring-rose-300 focus:outline-none cursor-pointer">
            {years.map(y => <option key={y} value={y}>{y}</option>)}
          </select>
          <ChevronDown className="w-3.5 h-3.5 text-slate-400 absolute right-2.5 top-1/2 -translate-y-1/2 pointer-events-none"/>
        </div>
        <div className="relative">
          <select value={statusFilter} onChange={e => setStatusFilter(e.target.value)}
            className="appearance-none pl-3 pr-8 py-2 text-sm rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm focus:ring-2 focus:ring-rose-300 focus:outline-none cursor-pointer">
            <option value="all">Tất cả trạng thái</option>
            <option value="Overdue">Quá hạn</option>
            <option value="Pending">Chờ thanh toán</option>
            <option value="PartiallyPaid">Một phần</option>
            <option value="Paid">Đã thanh toán</option>
          </select>
          <ChevronDown className="w-3.5 h-3.5 text-slate-400 absolute right-2.5 top-1/2 -translate-y-1/2 pointer-events-none"/>
        </div>
        <div className="relative flex-1 min-w-[220px]">
          <Search className="w-4 h-4 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2"/>
          <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Tìm theo mã HĐ, nhà cung cấp, khách hàng..."
            className="w-full pl-9 pr-3 py-2 text-sm rounded-xl border border-slate-200 bg-white shadow-sm focus:ring-2 focus:ring-rose-300 focus:outline-none"/>
        </div>
        <button onClick={fetchData}
          className="flex items-center gap-1.5 px-3 py-2 text-sm rounded-xl border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 shadow-sm transition">
          <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`}/>
          Làm mới
        </button>
      </div>

      {error && (
        <div className="p-4 bg-red-50 border border-red-200 rounded-2xl text-sm text-red-700">
          ⚠️ {error}
        </div>
      )}

      {/* Summary cards */}
      {loading ? (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {[...Array(4)].map((_, i) => <div key={i} className="h-28 rounded-2xl bg-slate-100 animate-pulse"/>)}
        </div>
      ) : data && (
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {cards.map((c, i) => (
            <div key={i} className="relative overflow-hidden bg-white rounded-2xl border border-slate-100 shadow-sm p-5">
              <div className={`absolute inset-0 bg-gradient-to-br ${c.grad} opacity-[0.06] pointer-events-none`}/>
              <div className="flex items-start justify-between mb-3">
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wide">{c.label}</p>
                <span className={`p-1.5 rounded-lg ${c.light}`}>{c.icon}</span>
              </div>
              <p className="text-xl font-bold text-slate-900 leading-tight">{c.value}</p>
              <p className="text-xs text-slate-500 mt-1">{c.sub}</p>
            </div>
          ))}
        </div>
      )}

      {/* Progress bar */}
      {data && data.totalCommissionDue > 0 && (
        <div className="bg-white rounded-2xl border border-slate-100 shadow-sm p-5">
          <div className="flex items-center justify-between mb-3">
            <p className="text-sm font-semibold text-slate-700">Tiến độ thu hồi hoa hồng toàn hệ thống</p>
            <span className="text-sm font-bold text-slate-900">
              {((data.totalCommissionPaid / data.totalCommissionDue) * 100).toFixed(1)}%
            </span>
          </div>
          <div className="h-3 bg-slate-100 rounded-full overflow-hidden flex">
            <div style={{ width: `${(data.totalCommissionPaid / data.totalCommissionDue) * 100}%` }}
              className="bg-emerald-500 h-full transition-all duration-700"/>
            <div style={{ width: `${(data.totalCommissionPending / data.totalCommissionDue) * 100}%` }}
              className="bg-amber-400 h-full transition-all duration-700"/>
            <div style={{ width: `${(data.totalCommissionOverdue / data.totalCommissionDue) * 100}%` }}
              className="bg-red-500 h-full transition-all duration-700"/>
          </div>
          <div className="flex gap-4 mt-2.5 text-xs text-slate-500">
            <span className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-emerald-500 inline-block"/>Đã thu {fmt(data.totalCommissionPaid)}</span>
            <span className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-amber-400 inline-block"/>Chờ {fmt(data.totalCommissionPending)}</span>
            <span className="flex items-center gap-1.5"><span className="w-2 h-2 rounded-full bg-red-500 inline-block"/>Quá hạn {fmt(data.totalCommissionOverdue)}</span>
          </div>
        </div>
      )}

      {/* Order / Contract Table */}
      <div className="bg-white rounded-2xl border border-slate-100 shadow-sm overflow-hidden">
        <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100">
          <div className="flex items-center gap-2">
            <Building2 className="w-4 h-4 text-slate-500"/>
            <span className="text-sm font-bold text-slate-800">Chi tiết theo Từng Đơn Hợp Đồng</span>
            <span className="text-xs text-slate-400 font-normal">({filtered.length} đơn)</span>
          </div>
          <button className="flex items-center gap-1.5 text-xs text-slate-500 hover:text-slate-800 transition px-2.5 py-1.5 rounded-lg hover:bg-slate-100">
            <Download className="w-3.5 h-3.5"/>
            Xuất Excel
          </button>
        </div>

        {loading ? (
          <div className="p-8 space-y-3">
            {[...Array(5)].map((_, i) => <div key={i} className="h-14 bg-slate-50 rounded-xl animate-pulse"/>)}
          </div>
        ) : filtered.length === 0 ? (
          <div className="py-16 text-center text-slate-400">
            <FileText className="w-10 h-10 mx-auto mb-3 opacity-40"/>
            <p className="text-sm font-medium">Không có dữ liệu</p>
            <p className="text-xs mt-1">Thử thay đổi bộ lọc hoặc khoảng thời gian.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-100">
                  <th className="text-left px-5 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Mã Hợp Đồng</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Nhà Cung Cấp</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Khách Hàng</th>
                  <th className="text-right px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Giá Trị HĐ</th>
                  <th className="text-right px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Tổng Hoa Hồng</th>
                  <th className="text-right px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Đã Thanh Toán</th>
                  <th className="text-right px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Chưa Thanh Toán</th>
                  <th className="text-left px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Hạn Nộp</th>
                  <th className="text-center px-4 py-3 text-xs font-semibold text-slate-500 uppercase tracking-wide">Trạng Thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-50">
                {filtered.map(o => {
                  const cfg = statusConfig[o.paymentStatus] ?? statusConfig['Pending'];
                  const remaining = o.totalCommissionPending + o.totalCommissionOverdue;
                  const isExpanded = expandedOrder === o.contractId;
                  const ratePercent = Number((o.commissionRate * 100).toFixed(1));
                  const paidItems = o.items ? o.items.filter(i => i.status === 'Paid') : [];
                  const paidCount = paidItems.length;
                  const paidPercent = o.totalCommissionDue > 0
                    ? Math.round((o.totalCommissionPaid / o.totalCommissionDue) * 100)
                    : 0;
                  const pendingPercent = 100 - paidPercent;

                  return (
                    <div key={o.contractId} style={{ display: 'contents' }}>
                      <tr onClick={() => setExpandedOrder(isExpanded ? null : o.contractId)}
                        className={`transition cursor-pointer group ${isExpanded ? 'bg-rose-50/25' : 'hover:bg-slate-50/80'}`}>
                        {/* Mã HĐ */}
                        <td className="px-5 py-3.5">
                          <div className="flex flex-col">
                            <div className="flex items-center gap-1.5">
                              <span className="font-mono text-xs font-bold text-rose-600 bg-rose-50 px-2 py-0.5 rounded-lg border border-rose-100 inline-block w-fit">
                                {o.contractCode}
                              </span>
                              <span
                                className="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 font-sans font-medium flex items-center gap-1"
                                title="Nhấn để xem chi tiết các đợt thanh toán"
                              >
                                {paidCount > 0 ? `Đã trả ${paidCount} đợt` : 'Chưa trả'}
                                {isExpanded ? <ChevronUp className="w-2.5 h-2.5"/> : <ChevronDown className="w-2.5 h-2.5"/>}
                              </span>
                            </div>
                            <span className="text-[11px] text-slate-400 mt-1 flex items-center gap-1">
                              <Calendar className="w-3 h-3"/>
                              {new Date(o.createdAt).toLocaleDateString('vi-VN')}
                            </span>
                          </div>
                        </td>

                        {/* Nhà cung cấp */}
                        <td className="px-4 py-3.5">
                          <div className="flex items-center gap-2.5">
                            <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-rose-400 to-pink-600 flex items-center justify-center text-white font-bold text-xs shrink-0">
                              {o.vendorBrandName.charAt(0).toUpperCase()}
                            </div>
                            <div>
                              <p className="font-semibold text-slate-800 group-hover:text-rose-600 transition leading-tight">
                                {o.vendorBrandName}
                              </p>
                              <p className="text-xs text-slate-400 mt-0.5">{o.ownerName}</p>
                            </div>
                          </div>
                        </td>

                        {/* Khách hàng */}
                        <td className="px-4 py-3.5">
                          <div>
                            <p className="font-medium text-slate-800 leading-tight">{o.customerName || 'Khách vãng lai'}</p>
                            {o.customerPhone && (
                              <p className="text-xs text-slate-400 mt-0.5 flex items-center gap-1">
                                <Phone className="w-3 h-3"/> {o.customerPhone}
                              </p>
                            )}
                          </div>
                        </td>

                        {/* Giá trị HĐ */}
                        <td className="px-4 py-3.5 text-right font-medium text-slate-800">
                          <div>
                            <p className="font-semibold text-slate-900">{fmt(o.contractValue)}</p>
                            {o.depositAmount > 0 && (
                              <p className="text-[11px] text-slate-400">Cọc: {fmt(o.depositAmount)}</p>
                            )}
                          </div>
                        </td>

                        {/* Tổng Hoa hồng */}
                        <td className="px-4 py-3.5 text-right font-bold text-slate-900 whitespace-nowrap">
                          <div>
                            <p className="font-bold text-slate-900">
                              {fmt(o.totalCommissionDue)}
                            </p>
                            <p className="text-[11px] text-slate-400 font-normal">
                              Tỷ lệ: {o.contractValue > 0 ? ((o.totalCommissionDue / o.contractValue) * 100).toFixed(1) : ratePercent}%
                            </p>
                          </div>
                        </td>

                        {/* Đã thanh toán */}
                        <td className="px-4 py-3.5 text-right font-bold text-emerald-600 whitespace-nowrap">
                          <div>
                            <p>{fmt(o.totalCommissionPaid)}</p>
                            {o.totalCommissionPaid > 0 && o.totalCommissionDue > 0 && (
                              <p className="text-[10px] text-emerald-600/80 font-normal">
                                ({paidPercent}%)
                              </p>
                            )}
                          </div>
                        </td>

                        {/* Chưa thanh toán */}
                        <td className="px-4 py-3.5 text-right whitespace-nowrap font-extrabold">
                          {remaining > 0 ? (
                            <div>
                              <p className={o.totalCommissionOverdue > 0 ? 'text-red-600' : 'text-rose-600'}>
                                {fmt(remaining)}
                              </p>
                              {o.totalCommissionDue > 0 && (
                                <p className="text-[10px] text-rose-500/80 font-normal">
                                  (Còn {pendingPercent}%)
                                </p>
                              )}
                            </div>
                          ) : (
                            <span className="text-emerald-600 text-xs font-bold">0 đ (Đã trả đủ)</span>
                          )}
                        </td>

                        {/* Hạn Nộp */}
                        <td className="px-4 py-3.5 text-left text-xs text-slate-500 whitespace-nowrap">
                          {o.dueDate ? new Date(o.dueDate).toLocaleDateString('vi-VN') : '—'}
                        </td>

                        {/* Trạng thái */}
                        <td className="px-4 py-3.5 text-center">
                          <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold ${cfg.bg} ${cfg.text}`}>
                            <span className={`w-1.5 h-1.5 rounded-full ${cfg.dot}`}/>
                            {cfg.label}
                          </span>
                        </td>
                      </tr>

                      {/* Chi tiết đơn khi mở rộng */}
                      {isExpanded && (
                        <tr>
                          <td colSpan={9} className="py-4 px-6 bg-slate-50/95 border-t border-b border-slate-200/80">
                            <div className="text-xs space-y-3.5">
                              {/* Dòng tiêu đề cùng nút ghi nhận thanh toán */}
                              <div className="flex flex-wrap items-center justify-between gap-3 pb-2 border-b border-slate-200/70">
                                <div className="flex items-center gap-2 flex-wrap">
                                  <Layers className="w-4 h-4 text-rose-500 shrink-0" />
                                  <span className="font-bold text-slate-800 text-xs">
                                    Chi tiết các lần thanh toán của hợp đồng {o.contractCode}:
                                  </span>
                                  <span className="text-[11px] font-semibold text-slate-600 bg-white px-2.5 py-0.5 rounded-full border border-slate-200">
                                    Tổng hoa hồng: <strong className="text-rose-600">{fmt(o.totalCommissionDue)}</strong>
                                  </span>
                                  <span className="text-[11px] font-semibold text-emerald-700 bg-emerald-50 px-2.5 py-0.5 rounded-full border border-emerald-200">
                                    {paidCount > 0 ? `Đã thanh toán ${paidCount} đợt` : 'Chưa ghi nhận thanh toán'}
                                  </span>
                                  <div className="flex items-center gap-3 text-slate-400 text-[11px] ml-1">
                                    <span>📧 {o.ownerEmail}</span>
                                    <span>👤 {o.customerName}</span>
                                    {o.weddingDate && <span>💒 {new Date(o.weddingDate).toLocaleDateString('vi-VN')}</span>}
                                  </div>
                                </div>
                                <button
                                  onClick={e => { e.stopPropagation(); openPaymentModal(o); }}
                                  className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm transition self-start sm:self-auto cursor-pointer"
                                >
                                  <CreditCard className="w-3.5 h-3.5" />
                                  <span>Điền số tiền đã trả cho đơn này</span>
                                </button>
                              </div>

                              {/* Thanh tiến độ thanh toán hoa hồng */}
                              <div className="bg-white p-3 rounded-2xl border border-slate-200 shadow-2xs">
                                <div className="flex justify-between items-center mb-1.5 text-[11px] font-bold">
                                  <span className="text-emerald-700 flex items-center gap-1.5">
                                    <span className="w-2 h-2 rounded-full bg-emerald-500 inline-block"/>
                                    Đã thanh toán: {fmt(o.totalCommissionPaid)} ({paidPercent}%)
                                  </span>
                                  <span className="text-rose-600 flex items-center gap-1.5">
                                    <span className="w-2 h-2 rounded-full bg-rose-500 inline-block"/>
                                    Chưa thanh toán: {fmt(remaining)} ({pendingPercent}%)
                                  </span>
                                </div>
                                <div className="h-2 w-full bg-slate-100 rounded-full overflow-hidden flex">
                                  <div
                                    style={{ width: `${paidPercent}%` }}
                                    className="bg-emerald-500 h-full transition-all duration-500 rounded-l-full"
                                  />
                                  <div
                                    style={{ width: `${pendingPercent}%` }}
                                    className="bg-rose-500 h-full transition-all duration-500 rounded-r-full"
                                  />
                                </div>
                              </div>

                              {/* Phân 2 khu vực: Các đợt đã thanh toán & Khoản còn nợ */}
                              <div className="grid grid-cols-1 lg:grid-cols-2 gap-3.5">
                                {/* Cột 1: Danh sách các lần đã thanh toán */}
                                <div className="bg-white p-4 rounded-2xl border border-emerald-100 shadow-2xs">
                                  <div className="flex items-center justify-between pb-2 mb-2 border-b border-emerald-50">
                                    <div className="flex items-center gap-1.5 font-bold text-slate-800">
                                      <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                                      <span>Các Lần Đã Thanh Toán</span>
                                    </div>
                                    <span className="text-[11px] font-extrabold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-100">
                                      Tổng đã trả: {fmt(o.totalCommissionPaid)}
                                    </span>
                                  </div>

                                  {paidItems.length === 0 ? (
                                    <div className="py-4 text-center text-slate-400">
                                      <p className="font-medium text-xs">Chưa có khoản thanh toán nào được ghi nhận cho đơn này.</p>
                                    </div>
                                  ) : (
                                    <div className="space-y-2">
                                      {paidItems.map((item, idx) => (
                                        <div
                                          key={item.id || idx}
                                          className="flex items-center justify-between p-2.5 rounded-xl bg-emerald-50/40 border border-emerald-100/60"
                                        >
                                          <div>
                                            <div className="flex items-center gap-2">
                                              <span className="font-bold text-slate-800 text-xs">
                                                Lần {idx + 1}: {fmt(item.commissionAmount)}
                                              </span>
                                              <span className="text-[10px] px-1.5 py-0.2 rounded bg-emerald-100 text-emerald-800 font-semibold">
                                                Đã thanh toán
                                              </span>
                                            </div>
                                            <div className="text-[10px] text-slate-500 mt-0.5 flex items-center gap-2">
                                              <span>Ngày: {item.paidAt ? new Date(item.paidAt).toLocaleDateString('vi-VN') : '—'}</span>
                                              {item.paymentReferenceCode && (
                                                <span className="font-mono text-slate-600 font-medium">Mã: {item.paymentReferenceCode}</span>
                                              )}
                                            </div>
                                          </div>
                                          <div className="text-right">
                                            <CheckCircle className="w-4 h-4 text-emerald-600 inline" />
                                          </div>
                                        </div>
                                      ))}
                                    </div>
                                  )}
                                </div>

                                {/* Cột 2: Khoản còn lại chưa thanh toán */}
                                <div className="bg-white p-4 rounded-2xl border border-amber-200 shadow-2xs flex flex-col justify-between">
                                  <div>
                                    <div className="flex items-center justify-between pb-2 mb-2 border-b border-amber-100">
                                      <div className="flex items-center gap-1.5 font-bold text-slate-800">
                                        <Clock className="w-4 h-4 text-amber-500" />
                                        <span>Khoản Còn Lại Chưa Thanh Toán</span>
                                      </div>
                                      <span className={`text-[11px] font-extrabold px-2 py-0.5 rounded-full border ${remaining > 0 ? 'text-rose-700 bg-rose-50 border-rose-100' : 'text-emerald-700 bg-emerald-50 border-emerald-100'}`}>
                                        {remaining > 0 ? `Còn thiếu: ${fmt(remaining)}` : 'Đã thanh toán đủ 100%'}
                                      </span>
                                    </div>

                                    {remaining > 0 ? (
                                      <div className="space-y-2">
                                        <div className="p-3 rounded-xl bg-amber-50/40 border border-amber-100">
                                          <div className="flex justify-between items-start">
                                            <div>
                                              <div className="font-bold text-rose-600 text-sm">
                                                {fmt(remaining)}
                                              </div>
                                              <p className="text-[11px] text-slate-500 mt-0.5">
                                                Chiếm {pendingPercent}% tổng giá trị hoa hồng của hợp đồng
                                              </p>
                                            </div>
                                            <span className="text-[11px] font-semibold text-slate-600">
                                              Hạn nộp: {o.dueDate ? new Date(o.dueDate).toLocaleDateString('vi-VN') : '—'}
                                            </span>
                                          </div>
                                        </div>
                                        <p className="text-[11px] text-slate-500 italic">
                                          Nhấn "Điền số tiền đã trả cho đơn này" để ghi nhận mỗi khi NCC nộp thêm một phần hoặc toàn bộ hoa hồng.
                                        </p>
                                      </div>
                                    ) : (
                                      <div className="py-4 text-center text-emerald-600">
                                        <CheckCircle2 className="w-8 h-8 mx-auto mb-1 text-emerald-500" />
                                        <p className="font-bold text-xs">Hợp đồng đã hoàn thành 100% hoa hồng</p>
                                        <p className="text-[10px] text-slate-400 mt-0.5">Không còn dư nợ hoa hồng nào cần thu.</p>
                                      </div>
                                    )}
                                  </div>
                                </div>
                              </div>
                            </div>
                          </td>
                        </tr>
                      )}
                    </div>
                  );
                })}
              </tbody>
              {filtered.length > 0 && (
                <tfoot className="border-t-2 border-slate-200 bg-slate-50">
                  <tr>
                    <td className="px-5 py-3 text-xs font-bold text-slate-600 uppercase">Tổng</td>
                    <td className="px-4 py-3 text-xs font-bold text-slate-700" colSpan={2}>
                      {filtered.length} đơn hợp đồng
                    </td>
                    <td className="px-4 py-3 text-right text-xs font-bold text-slate-900">
                      {fmt(filtered.reduce((a, o) => a + o.contractValue, 0))}
                    </td>
                    <td className="px-4 py-3 text-right text-xs font-bold text-slate-900">
                      {fmt(filtered.reduce((a, o) => a + o.totalCommissionDue, 0))}
                    </td>
                    <td className="px-4 py-3 text-right text-xs font-bold text-emerald-600">
                      {fmt(filtered.reduce((a, o) => a + o.totalCommissionPaid, 0))}
                    </td>
                    <td className="px-4 py-3 text-right text-xs font-bold text-rose-600">
                      {fmt(filtered.reduce((a, o) => a + (o.totalCommissionPending + o.totalCommissionOverdue), 0))}
                    </td>
                    <td colSpan={2}/>
                  </tr>
                </tfoot>
              )}
            </table>
          </div>
        )}
      </div>

      {/* ─── Modal Ghi Nhận Hoa Hồng Đã Trả Cho Đơn Hàng ───────────────────────── */}
      {paymentTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="relative w-full max-w-lg bg-white rounded-3xl shadow-2xl border border-slate-100 overflow-hidden transform transition-all">
            {/* Modal Header */}
            <div className="px-6 pt-6 pb-4 bg-gradient-to-r from-rose-500 to-pink-600 text-white flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-2xl bg-white/20 backdrop-blur-sm flex items-center justify-center text-white">
                  <Receipt className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold leading-snug">Ghi Nhận Hoa Hồng Đã Trả</h3>
                  <p className="text-xs text-rose-100">
                    Đơn: <span className="font-mono font-semibold">{paymentTarget.contractCode}</span> · {paymentTarget.vendorBrandName}
                  </p>
                </div>
              </div>
              <button
                onClick={closePaymentModal}
                disabled={isSubmittingPayment}
                className="w-8 h-8 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-white transition disabled:opacity-50"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Modal Body */}
            <form onSubmit={handleRecordPayment} noValidate className="p-6 space-y-5">
              {/* Financial Snapshot */}
              <div className="bg-slate-50 border border-slate-100 rounded-2xl p-4">
                <div className="grid grid-cols-3 gap-3 text-center">
                  <div className="p-2 rounded-xl bg-white border border-slate-100 shadow-xs">
                    <p className="text-[11px] text-slate-400 font-medium">Hoa hồng ({Number((paymentTarget.commissionRate * 100).toFixed(1))}%)</p>
                    <p className="text-xs font-bold text-slate-800 mt-0.5">{fmt(paymentTarget.totalCommissionDue)}</p>
                  </div>
                  <div className="p-2 rounded-xl bg-white border border-slate-100 shadow-xs">
                    <p className="text-[11px] text-slate-400 font-medium">Đã thanh toán</p>
                    <p className="text-xs font-bold text-emerald-600 mt-0.5">{fmt(paymentTarget.totalCommissionPaid)}</p>
                  </div>
                  <div className="p-2 rounded-xl bg-rose-50 border border-rose-100 shadow-xs">
                    <p className="text-[11px] text-rose-600 font-medium">Còn nợ đơn này</p>
                    <p className="text-xs font-bold text-rose-700 mt-0.5">
                      {fmt(paymentTarget.totalCommissionPending + paymentTarget.totalCommissionOverdue)}
                    </p>
                  </div>
                </div>
              </div>

              {/* Amount Input */}
              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="text-xs font-semibold text-slate-700">
                    Số tiền vendor đã trả cho đơn này <span className="text-red-500">*</span>
                  </label>
                  {/* Quick autofill buttons */}
                  {paymentTarget.totalCommissionPending + paymentTarget.totalCommissionOverdue > 0 && (
                    <div className="flex items-center gap-1.5">
                      <button
                        type="button"
                        onClick={() => setPaymentAmount(String(paymentTarget.totalCommissionPending + paymentTarget.totalCommissionOverdue))}
                        className="text-[11px] text-rose-600 hover:text-rose-700 font-medium hover:underline cursor-pointer"
                      >
                        Trả hết nợ
                      </button>
                      <span className="text-slate-300">·</span>
                      <button
                        type="button"
                        onClick={() => setPaymentAmount(String(Math.round((paymentTarget.totalCommissionPending + paymentTarget.totalCommissionOverdue) / 2)))}
                        className="text-[11px] text-slate-500 hover:text-slate-700 font-medium hover:underline cursor-pointer"
                      >
                        50%
                      </button>
                    </div>
                  )}
                </div>

                <div className="relative">
                  <input
                    type="text"
                    inputMode="numeric"
                    value={paymentAmount ? Number(paymentAmount.replace(/[^\d]/g, '')).toLocaleString('vi-VN') : ''}
                    onChange={e => {
                      const raw = e.target.value.replace(/[^\d]/g, '');
                      setPaymentAmount(raw);
                    }}
                    placeholder="VD: 25.000.000"
                    disabled={isSubmittingPayment}
                    className="w-full pl-3 pr-14 py-2.5 text-sm font-semibold text-slate-800 rounded-xl border border-slate-200 bg-white focus:ring-2 focus:ring-rose-400 focus:border-rose-400 focus:outline-none transition disabled:bg-slate-50"
                  />
                  <span className="absolute right-3 top-1/2 -translate-y-1/2 text-xs font-bold text-slate-400 pointer-events-none">
                    VNĐ
                  </span>
                </div>

                {paymentAmount && Number(paymentAmount) > 0 && (
                  <p className="text-xs text-slate-500 mt-1.5 flex items-center gap-1">
                    <span>Số tiền định dạng:</span>
                    <strong className="text-rose-600">{fmt(Number(paymentAmount))}</strong>
                  </p>
                )}
              </div>

              {/* Reference / Note Input */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                  Mã tham chiếu / Ghi chú giao dịch
                </label>
                <input
                  type="text"
                  value={paymentRef}
                  onChange={e => setPaymentRef(e.target.value)}
                  placeholder="VD: UNC MBBank 98421, Chuyển khoản hoa hồng đợt 1..."
                  disabled={isSubmittingPayment}
                  maxLength={100}
                  className="w-full px-3 py-2 text-sm rounded-xl border border-slate-200 bg-white focus:ring-2 focus:ring-rose-400 focus:border-rose-400 focus:outline-none transition disabled:bg-slate-50"
                />
                <p className="text-[11px] text-slate-400 mt-1">
                  Thông tin này sẽ được lưu kèm vào lịch sử đối soát của đơn hàng {paymentTarget.contractCode}.
                </p>
              </div>

              {/* Error Message */}
              {paymentError && (
                <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-xs text-red-700 flex items-start gap-2">
                  <AlertTriangle className="w-4 h-4 text-red-500 shrink-0 mt-0.5" />
                  <span>{paymentError}</span>
                </div>
              )}

              {/* Success Message */}
              {paymentSuccess && (
                <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-xl text-xs text-emerald-700 flex items-start gap-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-500 shrink-0 mt-0.5" />
                  <span>{paymentSuccess}</span>
                </div>
              )}

              {/* Actions */}
              <div className="flex items-center justify-end gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={closePaymentModal}
                  disabled={isSubmittingPayment}
                  className="px-4 py-2 text-xs font-medium text-slate-600 hover:text-slate-800 rounded-xl hover:bg-slate-100 transition disabled:opacity-50"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  disabled={isSubmittingPayment}
                  className="px-5 py-2 text-xs font-semibold rounded-xl bg-gradient-to-r from-rose-500 to-pink-600 hover:from-rose-600 hover:to-pink-700 text-white shadow-sm flex items-center gap-1.5 transition disabled:opacity-50"
                >
                  {isSubmittingPayment ? (
                    <>
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      <span>Đang lưu...</span>
                    </>
                  ) : (
                    <>
                      <CheckCircle className="w-3.5 h-3.5" />
                      <span>Xác nhận thanh toán</span>
                    </>
                  )}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
