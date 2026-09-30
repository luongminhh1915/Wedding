import { useState, useMemo, useEffect } from 'react';
import {
  QrCode, Calendar, CheckCircle2, Clock, AlertTriangle,
  RotateCw, Check, Loader2, ChevronDown, ChevronUp, Layers,
  Banknote, Sparkles, X
} from 'lucide-react';
import { useSettlement } from '../hooks/useSettlement';
import { VietQrModal } from './VietQrModal';
import { CommissionAdvanceModal, type CommissionAdvanceRecord } from './CommissionAdvanceModal';
import type { CommissionItem } from '../../../types/commission.types';

interface GroupedSettlementContract {
  contractCode: string;
  contractId: string;
  customerName: string;
  contractValue: number;
  commissionRate: number;
  totalCommissionAmount: number;
  totalPaidAmount: number;
  totalPendingAmount: number;
  periods: string[];
  dueDate: string;
  status: 'Paid' | 'PartiallyPaid' | 'Pending' | 'Overdue';
  items: CommissionItem[];
}

const COMMISSION_ADVANCE_STORAGE_KEY = 'wedding_commission_advance_requests';

export function SettlementDashboard() {
  const now = new Date();
  const [selectedMonth, setSelectedMonth] = useState<number>(now.getMonth() + 1);
  const [selectedYear, setSelectedYear] = useState<number>(now.getFullYear());
  const [isQrModalOpen, setIsQrModalOpen] = useState(false);
  const [expandedContract, setExpandedContract] = useState<string | null>(null);

  // State cho yêu cầu xin thanh toán trước hoa hồng
  const [selectedContractForAdvance, setSelectedContractForAdvance] = useState<GroupedSettlementContract | null>(null);
  const [advanceRequests, setAdvanceRequests] = useState<Record<string, CommissionAdvanceRecord>>({});
  const [advanceSuccessNotice, setAdvanceSuccessNotice] = useState<string | null>(null);

  // Load các yêu cầu xin thanh toán trước hoa hồng từ localStorage
  useEffect(() => {
    try {
      const stored = localStorage.getItem(COMMISSION_ADVANCE_STORAGE_KEY);
      if (stored) {
        setAdvanceRequests(JSON.parse(stored));
      }
    } catch {
      // ignore
    }
  }, []);

  const handleAdvanceSuccess = (record: CommissionAdvanceRecord) => {
    const updated = {
      ...advanceRequests,
      [record.contractCode]: record,
    };
    setAdvanceRequests(updated);
    try {
      localStorage.setItem(COMMISSION_ADVANCE_STORAGE_KEY, JSON.stringify(updated));
    } catch {
      // ignore
    }

    setAdvanceSuccessNotice(
      `Đã gửi yêu cầu thanh toán trước ${record.amount.toLocaleString('vi-VN')} đ hoa hồng cho hợp đồng ${record.contractCode} thành công! Hệ thống đang chờ xử lý.`
    );
  };

  const { statement, isLoading, isError, error, refetch } = useSettlement(selectedMonth, selectedYear);

  // Nhóm các đơn có cùng Mã Hợp Đồng thành 1 hàng duy nhất
  const groupedContracts = useMemo<GroupedSettlementContract[]>(() => {
    if (!statement?.items) return [];

    const map = new Map<string, GroupedSettlementContract>();

    for (const item of statement.items) {
      const key = item.contractCode;
      const isPaid = item.status === 'Paid';
      const isOverdue = item.status === 'Overdue';
      const paid = isPaid ? item.commissionAmount : 0;
      const pending = isPaid ? 0 : item.commissionAmount;

      if (!map.has(key)) {
        map.set(key, {
          contractCode: item.contractCode,
          contractId: item.contractId,
          customerName: item.customerName,
          contractValue: item.contractValue,
          commissionRate: item.commissionRate,
          totalCommissionAmount: item.commissionAmount,
          totalPaidAmount: paid,
          totalPendingAmount: pending,
          periods: [item.period],
          dueDate: item.dueDate,
          status: isPaid ? 'Paid' : (isOverdue ? 'Overdue' : 'Pending'),
          items: [item],
        });
      } else {
        const existing = map.get(key)!;
        existing.totalCommissionAmount += item.commissionAmount;
        existing.totalPaidAmount += paid;
        existing.totalPendingAmount += pending;
        if (!existing.periods.includes(item.period)) {
          existing.periods.push(item.period);
        }
        existing.items.push(item);

        // Tổng hợp trạng thái chung của hợp đồng này
        if (existing.totalPendingAmount === 0 && existing.totalPaidAmount > 0) {
          existing.status = 'Paid';
        } else if (existing.items.some(i => i.status === 'Overdue')) {
          existing.status = 'Overdue';
        } else if (existing.totalPaidAmount > 0) {
          existing.status = 'PartiallyPaid';
        } else {
          existing.status = 'Pending';
        }
      }
    }

    return Array.from(map.values());
  }, [statement?.items]);

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[400px] text-slate-500">
        <Loader2 className="w-8 h-8 animate-spin text-rose-500 mb-3" />
        <p className="text-sm font-semibold">Đang tải bảng kê đối soát hoa hồng...</p>
      </div>
    );
  }

  if (isError || !statement) {
    return (
      <div className="p-8 text-center bg-white rounded-3xl border border-red-200">
        <AlertTriangle className="w-10 h-10 text-red-500 mx-auto mb-3" />
        <h3 className="text-lg font-bold text-slate-800">Không thể tải dữ liệu đối soát</h3>
        <p className="text-sm text-slate-500 mt-1 max-w-md mx-auto">
          {(error as Error)?.message || 'Đã xảy ra sự cố khi tải bảng kê đối soát hoa hồng.'}
        </p>
        <button
          onClick={() => refetch()}
          className="mt-4 px-4 py-2 bg-slate-900 text-white rounded-xl text-sm font-semibold hover:bg-slate-800 inline-flex items-center gap-1.5"
        >
          <RotateCw className="w-4 h-4" />
          Thử Lại
        </button>
      </div>
    );
  }

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Paid':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">
            <Check className="w-3 h-3" />
            Đã Thanh Toán
          </span>
        );
      case 'PartiallyPaid':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-blue-100 text-blue-800 border border-blue-200">
            <Clock className="w-3 h-3" />
            Một Phần
          </span>
        );
      case 'Overdue':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-rose-100 text-rose-800 border border-rose-200">
            <AlertTriangle className="w-3 h-3" />
            Quá Hạn (Phạt 0.05%/ngày)
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-100 text-amber-800 border border-amber-200">
            <Clock className="w-3 h-3" />
            Chờ Thanh Toán
          </span>
        );
    }
  };

  return (
    <div className="space-y-6">
      {/* Advance Payment Success Notice */}
      {advanceSuccessNotice && (
        <div className="p-4 rounded-2xl bg-gradient-to-r from-emerald-500 to-teal-600 text-white shadow-md flex items-center justify-between gap-3 animate-in fade-in duration-300">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-white/20 backdrop-blur-md flex items-center justify-center shrink-0">
              <Check className="w-5 h-5 text-white" />
            </div>
            <p className="text-xs font-bold">{advanceSuccessNotice}</p>
          </div>
          <button
            onClick={() => setAdvanceSuccessNotice(null)}
            className="w-7 h-7 rounded-lg bg-black/10 hover:bg-black/20 flex items-center justify-center transition cursor-pointer shrink-0"
          >
            <X className="w-4 h-4 text-white" />
          </button>
        </div>
      )}

      {/* Top Header & Period Selector */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-1 rounded-lg bg-indigo-50 text-indigo-700 font-extrabold text-xs tracking-wider uppercase border border-indigo-200">
              PHẦN 2 • HỢP ĐỒNG ĐỐI TÁC VỚI ADMIN
            </span>
            <span className="text-xs text-slate-400">•</span>
            <span className="text-xs font-semibold text-slate-500">
              Đối tác: <strong className="text-slate-800">{statement.vendorBrandName}</strong>
            </span>
          </div>
          <h2 className="text-2xl font-black text-slate-900 mt-1">Hợp Đồng & Bảng Kê Đối Soát Hoa Hồng Với Admin</h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Hợp đồng nghĩa vụ tài chính và tỷ lệ chiết khấu sàn (BR-007). Chốt đối soát ngày 25 hàng tháng và thanh toán qua VietQR Napas247.
          </p>
        </div>

        {/* Month Selector */}
        <div className="flex items-center gap-2">
          <div className="flex items-center bg-slate-100 p-1 rounded-2xl border border-slate-200">
            <Calendar className="w-4 h-4 text-slate-500 ml-2 mr-1" />
            <select
              value={selectedMonth}
              onChange={(e) => setSelectedMonth(Number(e.target.value))}
              className="bg-transparent text-sm font-bold text-slate-700 py-1.5 px-2 focus:outline-none cursor-pointer"
            >
              {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12].map(m => (
                <option key={m} value={m}>Tháng {m < 10 ? `0${m}` : m}</option>
              ))}
            </select>
            <span className="text-slate-400">/</span>
            <select
              value={selectedYear}
              onChange={(e) => setSelectedYear(Number(e.target.value))}
              className="bg-transparent text-sm font-bold text-slate-700 py-1.5 px-2 focus:outline-none cursor-pointer"
            >
              {[2025, 2026, 2027].map(y => (
                <option key={y} value={y}>{y}</option>
              ))}
            </select>
          </div>
          <button
            onClick={() => refetch()}
            className="p-2.5 rounded-xl border border-slate-200 hover:bg-slate-100 text-slate-600 transition cursor-pointer"
            title="Làm mới"
          >
            <RotateCw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* 4 Stat Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Card 1: Tổng tiền cần thanh toán */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs relative overflow-hidden">
          <div className="text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-1">
            Kỳ Đối Soát {statement.month < 10 ? `0${statement.month}` : statement.month}/{statement.year}
          </div>
          <div className="text-2xl font-black text-rose-600 tracking-tight">
            {statement.totalPendingAmount.toLocaleString('vi-VN')} đ
          </div>
          <div className="text-xs text-slate-500 mt-1 flex items-center gap-1 font-medium">
            <span className="inline-block w-1.5 h-1.5 rounded-full bg-rose-500" />
            Hạn nộp: {new Date(statement.dueDate).toLocaleDateString('vi-VN')}
          </div>
        </div>

        {/* Card 2: Hoa Hồng Đã Thanh Toán */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <div className="text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-1">
            Đã Thanh Toán
          </div>
          <div className="text-2xl font-black text-emerald-600 tracking-tight">
            {statement.totalPaidAmount.toLocaleString('vi-VN')} đ
          </div>
          <div className="text-xs text-emerald-700 mt-1 font-medium">
            {statement.items.filter(i => i.status === 'Paid').length} đợt đã thanh toán thành công
          </div>
        </div>

        {/* Card 3: Tổng Hoa Hồng Phải Trả */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <div className="text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-1">
            Tổng Hoa Hồng Phải Trả
          </div>
          <div className="text-2xl font-black text-slate-900 tracking-tight">
            {(statement.totalPaidAmount + statement.totalPendingAmount).toLocaleString('vi-VN')} đ
          </div>
          <div className="text-xs text-slate-500 mt-1 font-medium">
            Tổng nghĩa vụ hoa hồng đối soát
          </div>
        </div>

        {/* Card 4: Trạng thái */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <div className="text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-1">
            Trạng Thái Bảng Kê
          </div>
          <div className="mt-1">
            {getStatusBadge(statement.paymentStatus)}
          </div>
          <div className="text-xs text-slate-400 font-mono mt-2">
            Mã CK: {statement.transferContent}
          </div>
        </div>
      </div>

      {/* Main Card: Transaction Table */}
      <div className="bg-white rounded-3xl border border-slate-200 shadow-xs overflow-hidden">
        {/* Table Header Action Bar */}
        <div className="p-6 border-b border-slate-100 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-2">
              <h3 className="font-extrabold text-lg text-slate-900">
                Chi Tiết Các Đơn Hợp Đồng Đối Soát Trong Kỳ
              </h3>
              <span className="text-xs text-slate-400 font-normal">
                ({groupedContracts.length} đơn hợp đồng)
              </span>
            </div>
            <p className="text-xs text-slate-500 mt-0.5">
              Các kỳ hoa hồng của cùng một mã hợp đồng được gộp thành 1 hàng để tiện theo dõi tổng thể.
            </p>
          </div>

          <div>
            {statement.totalPendingAmount > 0 ? (
              <button
                onClick={() => setIsQrModalOpen(true)}
                className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-rose-600 to-amber-600 hover:from-rose-500 hover:to-amber-500 text-white font-bold text-sm shadow-md shadow-rose-600/20 flex items-center gap-2 transition transform hover:-translate-y-0.5 active:translate-y-0 cursor-pointer"
              >
                <QrCode className="w-4 h-4" />
                <span>Thanh Toán VietQR ({statement.totalPendingAmount.toLocaleString('vi-VN')} đ)</span>
              </button>
            ) : (
              <div className="flex items-center gap-2 px-4 py-2 bg-emerald-50 text-emerald-700 rounded-xl border border-emerald-200 text-xs font-bold">
                <CheckCircle2 className="w-4 h-4" />
                Đã thanh toán đủ hoa hồng kỳ này
              </div>
            )}
          </div>
        </div>

        {/* Table Data */}
        <div className="overflow-x-auto">
          {groupedContracts.length === 0 ? (
            <div className="p-12 text-center text-slate-400">
              <Clock className="w-8 h-8 mx-auto mb-2 opacity-40" />
              <p className="font-medium text-sm">Chưa có giao dịch hoa hồng nào trong kỳ đối soát này.</p>
            </div>
          ) : (
            <table className="w-full text-left text-sm border-collapse">
              <thead>
                <tr className="bg-slate-50/80 text-slate-500 text-xs uppercase tracking-wider font-bold border-b border-slate-200">
                  <th className="py-3 px-5">Mã Hợp Đồng</th>
                  <th className="py-3 px-5">Cặp Đôi</th>
                  <th className="py-3 px-5 text-right">Giá Trị HĐ</th>
                  <th className="py-3 px-5 text-right">Tổng Hoa Hồng</th>
                  <th className="py-3 px-5 text-right">Đã Thanh Toán</th>
                  <th className="py-3 px-5 text-right">Chưa Thanh Toán</th>
                  <th className="py-3 px-5">Hạn Nộp</th>
                  <th className="py-3 px-5 text-center">Trạng Thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {groupedContracts.map((contract) => {
                  const isExpanded = expandedContract === contract.contractCode;
                  const paidItems = contract.items.filter(i => i.status === 'Paid');
                  const paidCount = paidItems.length;

                  return (
                    <div key={contract.contractCode} style={{ display: 'contents' }}>
                      <tr
                        onClick={() => setExpandedContract(isExpanded ? null : contract.contractCode)}
                        className={`transition cursor-pointer ${isExpanded ? 'bg-rose-50/25' : 'hover:bg-slate-50/60'}`}
                      >
                        <td className="py-3.5 px-5 font-mono font-bold text-slate-900">
                          <div className="flex items-center gap-1.5">
                            <span>{contract.contractCode}</span>
                            <span
                              className="text-[10px] px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 font-sans font-medium flex items-center gap-1"
                              title="Nhấn để xem chi tiết các đợt thanh toán"
                            >
                              {paidCount > 0 ? `Đã trả ${paidCount} đợt` : 'Chưa trả'}
                              {isExpanded ? <ChevronUp className="w-2.5 h-2.5"/> : <ChevronDown className="w-2.5 h-2.5"/>}
                            </span>
                          </div>
                        </td>
                        <td className="py-3.5 px-5 font-semibold text-slate-800">
                          {contract.customerName}
                        </td>
                        <td className="py-3.5 px-5 text-right font-medium text-slate-700">
                          {contract.contractValue.toLocaleString('vi-VN')} đ
                        </td>
                        <td className="py-3.5 px-5 text-right font-bold text-slate-900">
                          <div>
                            <p>{contract.totalCommissionAmount.toLocaleString('vi-VN')} đ</p>
                            <p className="text-[10px] text-slate-400 font-normal">
                              Tỷ lệ: {contract.contractValue > 0 ? ((contract.totalCommissionAmount / contract.contractValue) * 100).toFixed(1) : (contract.commissionRate * 100).toFixed(0)}%
                            </p>
                          </div>
                        </td>
                        <td className="py-3.5 px-5 text-right font-bold text-emerald-600">
                          <div>
                            <p>{contract.totalPaidAmount.toLocaleString('vi-VN')} đ</p>
                            {contract.totalPaidAmount > 0 && contract.totalCommissionAmount > 0 && (
                              <p className="text-[10px] text-emerald-600/80 font-normal">
                                ({Math.round((contract.totalPaidAmount / contract.totalCommissionAmount) * 100)}%)
                              </p>
                            )}
                          </div>
                        </td>
                        <td className="py-3.5 px-5 text-right font-extrabold">
                          {contract.totalPendingAmount > 0 ? (
                            <div>
                              <p className="text-rose-600">{contract.totalPendingAmount.toLocaleString('vi-VN')} đ</p>
                              {contract.totalCommissionAmount > 0 && (
                                <p className="text-[10px] text-rose-500/80 font-normal">
                                  (Còn {Math.round((contract.totalPendingAmount / contract.totalCommissionAmount) * 100)}%)
                                </p>
                              )}
                            </div>
                          ) : (
                            <span className="text-emerald-600 text-xs font-bold">0 đ (Đã trả đủ)</span>
                          )}
                        </td>
                        <td className="py-3.5 px-5 text-xs text-slate-500">
                          {new Date(contract.dueDate).toLocaleDateString('vi-VN')}
                        </td>
                        <td className="py-3.5 px-5 text-center">
                          {getStatusBadge(contract.status)}
                        </td>
                      </tr>

                      {/* Chi tiết từng lần thanh toán của hợp đồng */}
                      {isExpanded && (() => {
                        const paidPercent = contract.totalCommissionAmount > 0
                          ? Math.round((contract.totalPaidAmount / contract.totalCommissionAmount) * 100)
                          : 0;
                        const pendingPercent = 100 - paidPercent;

                        return (
                          <tr>
                            <td colSpan={8} className="py-4 px-6 bg-slate-50/95 border-t border-b border-slate-200/80">
                              <div className="text-xs space-y-3.5">
                                {/* Dòng tiêu đề cùng nút yêu cầu thanh toán trước hoa hồng */}
                                <div className="flex flex-wrap items-center justify-between gap-3 pb-2 border-b border-slate-200/70">
                                  <div className="flex items-center gap-2 flex-wrap">
                                    <Layers className="w-4 h-4 text-rose-500 shrink-0" />
                                    <span className="font-bold text-slate-800 text-xs">
                                      Chi tiết các lần thanh toán của hợp đồng {contract.contractCode}:
                                    </span>
                                    <span className="text-[11px] font-semibold text-slate-600 bg-white px-2.5 py-0.5 rounded-full border border-slate-200">
                                      Tổng phải trả: <strong className="text-slate-900">{contract.totalCommissionAmount.toLocaleString('vi-VN')} đ</strong> ·
                                      Đã thanh toán: <strong className="text-emerald-700">{contract.totalPaidAmount.toLocaleString('vi-VN')} đ</strong> ·
                                      Chưa thanh toán: <strong className="text-rose-600">{contract.totalPendingAmount.toLocaleString('vi-VN')} đ</strong>
                                    </span>
                                    {advanceRequests[contract.contractCode] && (
                                      <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 text-amber-800 border border-amber-300">
                                        <Clock className="w-3 h-3 text-amber-600" />
                                        Đã xin tạm ứng: {advanceRequests[contract.contractCode].amount.toLocaleString('vi-VN')} đ (Chờ duyệt)
                                      </span>
                                    )}
                                  </div>

                                  <button
                                    type="button"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      setSelectedContractForAdvance(contract);
                                    }}
                                    className="px-3.5 py-1.5 rounded-xl bg-gradient-to-r from-rose-500 via-rose-600 to-amber-500 hover:from-rose-600 hover:to-amber-600 text-white font-bold text-xs shadow-xs hover:shadow flex items-center gap-1.5 transition transform hover:-translate-y-0.5 active:translate-y-0 cursor-pointer"
                                  >
                                    <Banknote className="w-3.5 h-3.5 text-amber-200" />
                                    <span>{advanceRequests[contract.contractCode] ? 'Đổi Số Tiền Tạm Ứng' : 'Yêu Cầu Thanh Toán Trước Hoa Hồng'}</span>
                                    <Sparkles className="w-3 h-3 text-amber-200" />
                                  </button>
                                </div>

                                {/* Thanh tiến độ thanh toán trực quan */}
                                <div className="p-3 bg-white rounded-2xl border border-slate-200 space-y-1.5">
                                  <div className="flex items-center justify-between text-[11px] font-bold">
                                    <span className="text-emerald-700 flex items-center gap-1">
                                      <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                                      Đã thanh toán: {contract.totalPaidAmount.toLocaleString('vi-VN')} đ ({paidPercent}%)
                                    </span>
                                    <span className="text-rose-600 flex items-center gap-1">
                                      <Clock className="w-3.5 h-3.5 text-rose-500" />
                                      Chưa thanh toán: {contract.totalPendingAmount.toLocaleString('vi-VN')} đ ({pendingPercent}%)
                                    </span>
                                  </div>
                                  <div className="w-full h-3 bg-slate-100 rounded-full overflow-hidden flex p-0.5 border border-slate-200">
                                    {paidPercent > 0 && (
                                      <div
                                        style={{ width: `${paidPercent}%` }}
                                        className="bg-emerald-500 rounded-full h-full transition-all duration-300 shadow-xs"
                                        title={`Đã thanh toán: ${contract.totalPaidAmount.toLocaleString('vi-VN')} đ`}
                                      />
                                    )}
                                    {pendingPercent > 0 && (
                                      <div
                                        style={{ width: `${pendingPercent}%` }}
                                        className="bg-rose-400 rounded-full h-full transition-all duration-300"
                                        title={`Chưa thanh toán: ${contract.totalPendingAmount.toLocaleString('vi-VN')} đ`}
                                      />
                                    )}
                                  </div>
                                </div>

                                {/* 2 Khung: Các Lần Đã Thanh Toán và Khoản Chưa Thanh Toán */}
                                <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                                  {/* Cột 1: Các Lần Đã Thanh Toán */}
                                  <div className="p-3.5 rounded-2xl bg-white border border-slate-200/90 shadow-2xs space-y-2.5">
                                    <div className="flex items-center justify-between pb-1.5 border-b border-slate-100">
                                      <div className="flex items-center gap-1.5">
                                        <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                                        <span className="font-extrabold text-slate-800 text-xs">
                                          Các Lần Đã Thanh Toán ({paidItems.length} lần)
                                        </span>
                                      </div>
                                      <span className="font-extrabold text-emerald-600 text-xs">
                                        {contract.totalPaidAmount.toLocaleString('vi-VN')} đ
                                      </span>
                                    </div>

                                    <div className="space-y-2 text-[11px]">
                                      {paidItems.length === 0 ? (
                                        <div className="p-3 rounded-xl bg-slate-50 border border-slate-100 text-slate-400 text-center">
                                          Chưa có đợt thanh toán nào được thực hiện.
                                        </div>
                                      ) : (
                                        paidItems.map((item, idx) => (
                                          <div
                                            key={item.id || idx}
                                            className="p-2.5 rounded-xl bg-emerald-50/70 border border-emerald-100 flex items-center justify-between"
                                          >
                                            <div>
                                              <p className="font-bold text-emerald-950">
                                                Lần {idx + 1}: <strong className="text-emerald-700">{item.commissionAmount.toLocaleString('vi-VN')} đ</strong>
                                              </p>
                                              <p className="text-[10px] text-emerald-700/80 mt-0.5">
                                                {item.paidAt && `Ngày trả: ${new Date(item.paidAt).toLocaleDateString('vi-VN')}`}
                                                {item.paymentReferenceCode && ` · Mã GD: ${item.paymentReferenceCode}`}
                                              </p>
                                            </div>
                                            <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">
                                              <Check className="w-3 h-3" /> Đã Thanh Toán
                                            </span>
                                          </div>
                                        ))
                                      )}
                                    </div>
                                  </div>

                                  {/* Cột 2: Khoản Còn Lại Chưa Thanh Toán */}
                                  <div className="p-3.5 rounded-2xl bg-white border border-slate-200/90 shadow-2xs space-y-2.5">
                                    <div className="flex items-center justify-between pb-1.5 border-b border-slate-100">
                                      <div className="flex items-center gap-1.5">
                                        <Clock className="w-4 h-4 text-rose-500" />
                                        <span className="font-extrabold text-slate-800 text-xs">
                                          Khoản Còn Lại Chưa Thanh Toán
                                        </span>
                                      </div>
                                      <span className={`font-extrabold text-xs ${contract.totalPendingAmount > 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
                                        {contract.totalPendingAmount.toLocaleString('vi-VN')} đ
                                      </span>
                                    </div>

                                    <div className="text-[11px]">
                                      {contract.totalPendingAmount > 0 ? (
                                        <div className="p-3 rounded-xl bg-amber-50/60 border border-amber-200 space-y-2">
                                          <div className="flex items-center justify-between">
                                            <span className="font-bold text-amber-950">
                                              Số tiền còn nợ: <strong className="text-rose-600 text-sm font-extrabold">{contract.totalPendingAmount.toLocaleString('vi-VN')} đ</strong>
                                            </span>
                                            <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 text-amber-800 border border-amber-300">
                                              <Clock className="w-3 h-3 text-amber-600" /> Chưa Thanh Toán
                                            </span>
                                          </div>
                                          <p className="text-[11px] text-slate-600">
                                            Hạn chót thanh toán: <strong>{new Date(contract.dueDate).toLocaleDateString('vi-VN')}</strong>.
                                          </p>
                                          <p className="text-[10px] text-slate-500 italic">
                                            Bạn có thể thanh toán trước bất kỳ số tiền nào bằng nút <strong>Yêu Cầu Thanh Toán Trước Hoa Hồng</strong> ở trên hoặc thanh toán qua VietQR.
                                          </p>
                                        </div>
                                      ) : (
                                        <div className="p-3 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-center font-bold">
                                          ✓ Hợp đồng đã hoàn tất thanh toán đủ 100% tiền hoa hồng!
                                        </div>
                                      )}
                                    </div>
                                  </div>
                                </div>
                              </div>
                            </td>
                          </tr>
                        );
                      })()}
                    </div>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        {/* Footer Summary */}
        <div className="bg-slate-50/80 p-5 border-t border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs text-slate-600">
          <div>
            Tổng số đơn hợp đồng trong bảng kê: <strong>{groupedContracts.length}</strong> •
            Tổng giá trị hợp đồng dịch vụ: <strong>{statement.totalContractValue.toLocaleString('vi-VN')} đ</strong>
          </div>
          <div className="flex items-center gap-4">
            <span>Đã thanh toán: <strong className="text-emerald-700">{statement.totalPaidAmount.toLocaleString('vi-VN')} đ</strong></span>
            <span>Còn phải nộp: <strong className="text-rose-600">{statement.totalPendingAmount.toLocaleString('vi-VN')} đ</strong></span>
          </div>
        </div>
      </div>

      {/* VietQR Payment Modal */}
      <VietQrModal
        isOpen={isQrModalOpen}
        onClose={() => setIsQrModalOpen(false)}
        statement={statement}
        onPaymentSuccess={() => {
          refetch();
        }}
      />

      {/* Commission Advance Payment Modal */}
      <CommissionAdvanceModal
        isOpen={selectedContractForAdvance !== null}
        onClose={() => setSelectedContractForAdvance(null)}
        contract={selectedContractForAdvance}
        onSuccess={handleAdvanceSuccess}
      />
    </div>
  );
}
export default SettlementDashboard;
