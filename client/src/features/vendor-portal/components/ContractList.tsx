import { useState, useEffect } from 'react';
import {
  FileText, Clock, CheckCircle2, AlertCircle,
  Gift, Calendar, RefreshCw, Banknote, Sparkles, Check, X
} from 'lucide-react';
import { useVendorContracts } from '../hooks/useVendorContracts';
import type { Contract } from '../../../types/contract.types';
import { AdvancePaymentModal, type AdvancePaymentRecord } from './AdvancePaymentModal';

const ADVANCE_STORAGE_KEY = 'wedding_vendor_advance_requests';

export function ContractList() {
  const [selectedStatus, setSelectedStatus] = useState<string>('');
  const { data: contracts = [], isLoading, isError, refetch } = useVendorContracts(selectedStatus || undefined);

  // State for Advance Payment Modal
  const [selectedContractForAdvance, setSelectedContractForAdvance] = useState<Contract | null>(null);
  const [advanceRequests, setAdvanceRequests] = useState<Record<string, AdvancePaymentRecord>>({});
  const [successNotice, setSuccessNotice] = useState<string | null>(null);

  // Load stored advance requests from localStorage
  useEffect(() => {
    try {
      const stored = localStorage.getItem(ADVANCE_STORAGE_KEY);
      if (stored) {
        setAdvanceRequests(JSON.parse(stored));
      }
    } catch {
      // ignore
    }
  }, []);

  const handleOpenAdvanceModal = (contract: Contract) => {
    setSelectedContractForAdvance(contract);
  };

  const handleCloseAdvanceModal = () => {
    setSelectedContractForAdvance(null);
  };

  const handleAdvanceSuccess = (record: AdvancePaymentRecord) => {
    const updated = {
      ...advanceRequests,
      [record.contractId]: record,
    };
    setAdvanceRequests(updated);
    try {
      localStorage.setItem(ADVANCE_STORAGE_KEY, JSON.stringify(updated));
    } catch {
      // ignore
    }

    setSuccessNotice(
      `Đã gửi yêu cầu xin thanh toán trước ${record.amount.toLocaleString('vi-VN')} đ cho hợp đồng ${record.contractCode} thành công! Hệ thống đang chờ xử lý.`
    );
  };

  const handleCancelAdvance = (contractId: string) => {
    const updated = { ...advanceRequests };
    delete updated[contractId];
    setAdvanceRequests(updated);
    try {
      localStorage.setItem(ADVANCE_STORAGE_KEY, JSON.stringify(updated));
    } catch {
      // ignore
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'PendingVerification':
        return (
          <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-amber-50 text-amber-700 border border-amber-200 animate-pulse flex items-center gap-1">
            <Clock className="w-3 h-3" /> Chờ Khách Duyệt (72h)
          </span>
        );
      case 'Confirmed':
        return (
          <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-50 text-emerald-700 border border-emerald-200 flex items-center gap-1">
            <CheckCircle2 className="w-3 h-3" /> ✓ Đã Xác Thực (BR-005)
          </span>
        );
      case 'Draft':
        return (
          <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-rose-50 text-rose-700 border border-rose-200 flex items-center gap-1">
            <AlertCircle className="w-3 h-3" /> Khách Báo Sai Lệch (Draft)
          </span>
        );
      case 'Completed':
        return (
          <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-purple-50 text-purple-700 border border-purple-200">
            Hoàn Tất Đám Cưới
          </span>
        );
      case 'Cancelled':
        return (
          <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-slate-100 text-slate-600">
            Đã Hủy
          </span>
        );
      default:
        return <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-slate-50 text-slate-700">{status}</span>;
    }
  };

  return (
    <div className="space-y-6">
      {/* Success Notification Alert */}
      {successNotice && (
        <div className="p-4 rounded-2xl bg-gradient-to-r from-emerald-500 to-teal-600 text-white shadow-md flex items-center justify-between gap-3 animate-in fade-in slide-in-from-top-2 duration-300">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-white/20 backdrop-blur-md flex items-center justify-center shrink-0">
              <Check className="w-5 h-5 text-white" />
            </div>
            <p className="text-xs font-bold">{successNotice}</p>
          </div>
          <button
            onClick={() => setSuccessNotice(null)}
            className="w-7 h-7 rounded-lg bg-black/10 hover:bg-black/20 flex items-center justify-center transition cursor-pointer shrink-0"
          >
            <X className="w-4 h-4 text-white" />
          </button>
        </div>
      )}

      {/* Header */}
      <div className="bg-white rounded-2xl border border-rose-100 p-6 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 shadow-sm">
        <div>
          <div className="flex items-center gap-2 mb-1">
            <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-rose-50 text-rose-700 border border-rose-200">
              PHẦN 1 • HỢP ĐỒNG KHÁCH HÀNG
            </span>
          </div>
          <h2 className="text-xl font-bold text-slate-900" style={{ fontFamily: "'Playfair Display', serif" }}>
            Hợp Đồng Dịch Vụ Cưới Ký Với Cô Dâu & Chú Rể (BR-005)
          </h2>
          <p className="text-xs text-slate-500 mt-1">
            Hợp đồng dịch vụ cưới ký kết trực tiếp với các cặp đôi cô dâu chú rể · Xác thực 2 chiều trong <strong className="text-rose-600">72 giờ</strong> · Hỗ trợ <strong className="text-amber-600">Xin thanh toán trước (tạm ứng)</strong> một phần kinh phí phục vụ chuẩn bị tiệc cưới.
          </p>
        </div>

        <button
          onClick={() => refetch()}
          className="flex items-center gap-1.5 px-4 py-2 rounded-xl border border-slate-200 text-xs font-bold text-slate-700 hover:bg-slate-50 transition cursor-pointer"
        >
          <RefreshCw className="w-3.5 h-3.5" />
          Làm mới
        </button>
      </div>

      {/* Tabs lọc */}
      <div className="flex gap-2 border-b border-slate-200 pb-2 overflow-x-auto text-xs font-bold">
        {[
          { id: '', label: 'Tất cả Hợp đồng' },
          { id: 'PendingVerification', label: '⏱️ Chờ Khách Duyệt (72h)' },
          { id: 'Confirmed', label: '✓ Đã Xác Thực' },
          { id: 'Draft', label: '⚠️ Báo Sai Lệch' },
          { id: 'Completed', label: '🎉 Hoàn Tất' }
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setSelectedStatus(tab.id)}
            className={`px-3.5 py-2 rounded-xl transition cursor-pointer ${
              selectedStatus === tab.id
                ? 'bg-rose-500 text-white shadow-sm'
                : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Content */}
      {isLoading ? (
        <div className="bg-white rounded-2xl border border-slate-100 p-12 text-center">
          <div className="w-8 h-8 border-2 border-rose-300 border-t-rose-600 rounded-full animate-spin mx-auto mb-3" />
          <p className="text-xs text-slate-400">Đang tải danh sách hợp đồng...</p>
        </div>
      ) : isError ? (
        <div className="bg-white rounded-2xl border border-rose-100 p-8 text-center text-rose-500 text-xs">
          Lỗi khi tải dữ liệu hợp đồng.
        </div>
      ) : contracts.length === 0 ? (
        <div className="bg-white rounded-2xl border border-slate-100 p-12 text-center space-y-2">
          <div className="w-12 h-12 rounded-2xl bg-purple-50 text-purple-500 flex items-center justify-center mx-auto text-xl">
            📋
          </div>
          <p className="text-sm font-bold text-slate-700">Chưa có hợp đồng nào</p>
          <p className="text-xs text-slate-400">
            Khi tiếp nhận yêu cầu tư vấn thành công, bạn có thể tạo hợp đồng chốt khách tại Hộp Thư Lead.
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4">
          {contracts.map((contract: Contract) => {
            const deadline = new Date(contract.verificationDeadline);
            const isOverdue = deadline < new Date();
            const advanceRecord = advanceRequests[contract.id];

            return (
              <div
                key={contract.id}
                className="bg-white rounded-2xl border border-slate-100 hover:border-purple-100 transition shadow-sm p-5 space-y-4"
              >
                {/* Header */}
                <div className="flex flex-wrap items-center justify-between gap-3 pb-3 border-b border-slate-100">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-2xl bg-purple-100 text-purple-700 font-bold flex items-center justify-center text-xs">
                      <FileText className="w-5 h-5" />
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <h4 className="font-bold text-slate-900 text-sm font-mono">{contract.contractCode}</h4>
                        {contract.voucherCode && (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-mono font-bold bg-amber-50 text-amber-700 border border-amber-200">
                            <Gift className="w-3 h-3 text-amber-500" />
                            {contract.voucherCode}
                          </span>
                        )}
                      </div>
                      <p className="text-[11px] text-slate-400">
                        Khách hàng: <strong className="text-slate-700">{contract.customerName}</strong> ({contract.customerPhone}) · Tạo ngày {new Date(contract.createdAt).toLocaleDateString('vi-VN')}
                      </p>
                    </div>
                  </div>

                  <div>{getStatusBadge(contract.status)}</div>
                </div>

                {/* Details */}
                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 text-xs">
                  <div className="p-3 rounded-xl bg-slate-50 border border-slate-100">
                    <span className="text-[10px] text-slate-400 font-semibold block mb-0.5">Tổng Giá Trị Hợp Đồng</span>
                    <span className="text-sm font-extrabold text-slate-900">
                      {contract.contractValue.toLocaleString('vi-VN')} đ
                    </span>
                  </div>

                  <div className="p-3 rounded-xl bg-slate-50 border border-slate-100">
                    <span className="text-[10px] text-slate-400 font-semibold block mb-0.5">Tiền Đặt Cọc Thực Tế</span>
                    <span className="text-sm font-extrabold text-emerald-600">
                      {contract.depositAmount.toLocaleString('vi-VN')} đ
                    </span>
                  </div>

                  <div className="p-3 rounded-xl bg-slate-50 border border-slate-100">
                    <span className="text-[10px] text-slate-400 font-semibold block mb-0.5">Ngày Cưới Dự Kiến</span>
                    <span className="text-xs font-bold text-slate-800 flex items-center gap-1">
                      <Calendar className="w-3.5 h-3.5 text-rose-500" />
                      {contract.weddingDate ? new Date(contract.weddingDate).toLocaleDateString('vi-VN') : 'Chưa định ngày'}
                    </span>
                  </div>

                  <div className={`p-3 rounded-xl border ${
                    contract.status === 'PendingVerification' && isOverdue
                      ? 'bg-rose-50 border-rose-200 text-rose-700'
                      : contract.status === 'PendingVerification'
                      ? 'bg-amber-50 border-amber-200 text-amber-800'
                      : 'bg-slate-50 border-slate-100 text-slate-700'
                  }`}>
                    <span className="text-[10px] font-semibold block mb-0.5 opacity-80">
                      Hạn Chót Xác Thực (72h)
                    </span>
                    <span className="text-xs font-bold flex items-center gap-1">
                      <Clock className="w-3.5 h-3.5" />
                      {contract.status === 'PendingVerification'
                        ? deadline.toLocaleString('vi-VN')
                        : contract.confirmedAt
                        ? `Đã duyệt lúc ${new Date(contract.confirmedAt).toLocaleDateString('vi-VN')}`
                        : 'Đã đóng'}
                    </span>
                  </div>
                </div>

                {/* Banner Thông Báo Yêu Cầu Xin Thanh Toán Trước Đã Gửi */}
                {advanceRecord && (
                  <div className="p-3.5 rounded-xl bg-gradient-to-r from-amber-50 via-amber-50/80 to-orange-50 border border-amber-200 flex flex-wrap items-center justify-between gap-3 text-xs">
                    <div className="flex items-center gap-2.5">
                      <div className="w-8 h-8 rounded-lg bg-amber-100 text-amber-700 flex items-center justify-center font-bold shrink-0">
                        <Banknote className="w-4 h-4" />
                      </div>
                      <div>
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="font-bold text-amber-950">
                            Đã xin thanh toán trước:
                          </span>
                          <span className="font-extrabold text-amber-700 text-sm">
                            {advanceRecord.amount.toLocaleString('vi-VN')} đ
                          </span>
                          <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 text-amber-800 border border-amber-300 flex items-center gap-1">
                            <Clock className="w-3 h-3 text-amber-600" /> Chờ xét duyệt
                          </span>
                        </div>
                        <p className="text-[11px] text-amber-800/80 mt-0.5">
                          Mục đích: <strong className="text-amber-900">{advanceRecord.reason}</strong> · Nhận về: <strong>{advanceRecord.bankAccountNumber}</strong> ({advanceRecord.bankName} - {advanceRecord.bankAccountName})
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-1.5">
                      <button
                        onClick={() => handleOpenAdvanceModal(contract)}
                        className="px-3 py-1.5 rounded-lg bg-white border border-amber-300 text-amber-800 hover:bg-amber-100 font-bold text-xs transition cursor-pointer shadow-2xs"
                      >
                        Chỉnh sửa
                      </button>
                      <button
                        onClick={() => handleCancelAdvance(contract.id)}
                        className="px-2.5 py-1.5 rounded-lg text-slate-400 hover:text-rose-600 hover:bg-rose-50 font-medium text-xs transition cursor-pointer"
                        title="Hủy đề nghị này"
                      >
                        Hủy
                      </button>
                    </div>
                  </div>
                )}

                {/* Hoa hồng đã sinh tự động */}
                {contract.commissions && contract.commissions.length > 0 && (() => {
                  const paid = contract.commissions.filter(c => c.status === 'Paid').reduce((s, c) => s + c.commissionAmount, 0);
                  const pending = contract.commissions.filter(c => c.status !== 'Paid').reduce((s, c) => s + c.commissionAmount, 0);
                  const total = paid + pending;

                  const isFullyPaid = pending === 0 && paid > 0;
                  const isPartiallyPaid = paid > 0 && pending > 0;
                  const statusText = isFullyPaid ? 'Đã Thanh Toán' : (isPartiallyPaid ? 'Một Phần' : 'Chờ Thanh Toán');

                  return (
                    <div className="p-3 rounded-xl bg-emerald-50/70 border border-emerald-200 flex flex-wrap items-center justify-between gap-2 text-xs">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="w-2 h-2 rounded-full bg-emerald-500" />
                        <span className="font-bold text-emerald-900">
                          Hoa hồng hợp đồng:
                        </span>
                        <span className="font-extrabold text-emerald-700">
                          {total.toLocaleString('vi-VN')} đ
                        </span>
                        {isPartiallyPaid && (
                          <span className="text-[11px] text-blue-700 font-semibold bg-blue-50 px-2 py-0.5 rounded-full border border-blue-200">
                            Đã thanh toán: {paid.toLocaleString('vi-VN')} đ · Còn chưa thanh toán: {pending.toLocaleString('vi-VN')} đ
                          </span>
                        )}
                        <span className="text-[10px] text-emerald-600 bg-white px-2 py-0.5 rounded-full border border-emerald-200 font-semibold">
                          Hạn thanh toán: Ngày 25
                        </span>
                      </div>

                      <span className={`text-[11px] font-bold uppercase tracking-wider ${isFullyPaid ? 'text-emerald-800' : isPartiallyPaid ? 'text-blue-700' : 'text-amber-800'}`}>
                        Trạng thái: {statusText}
                      </span>
                    </div>
                  );
                })()}

                {contract.cancellationReason && (
                  <div className="p-3 rounded-xl bg-rose-50 border border-rose-100 text-xs text-rose-700">
                    <strong>Lý do từ chối/hủy:</strong> {contract.cancellationReason}
                  </div>
                )}

                {/* ACTION BAR: Nút Xin Thanh Toán Trước Một Phần Số Tiền Của Hợp Đồng */}
                <div className="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-100">
                  <div className="text-[11px] text-slate-500 flex items-center gap-1.5">
                    {contract.status === 'Confirmed' ? (
                      <span className="text-emerald-700 font-medium flex items-center gap-1">
                        <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500" />
                        Hợp đồng hợp lệ · Bạn có thể gửi yêu cầu xin thanh toán trước một phần tiền HĐ để phục vụ tiệc cưới.
                      </span>
                    ) : contract.status === 'PendingVerification' ? (
                      <span className="text-amber-700 font-medium flex items-center gap-1">
                        <Clock className="w-3.5 h-3.5 text-amber-500" />
                        Đang chờ khách duyệt trong 72h.
                      </span>
                    ) : contract.status === 'Completed' ? (
                      <span className="text-purple-700 font-medium flex items-center gap-1">
                        🎉 Đám cưới đã hoàn tất.
                      </span>
                    ) : (
                      <span className="text-slate-400">Trạng thái: {contract.status}</span>
                    )}
                  </div>

                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => handleOpenAdvanceModal(contract)}
                      disabled={contract.status === 'Cancelled'}
                      className="px-4 py-2 rounded-xl bg-gradient-to-r from-rose-500 via-rose-600 to-amber-500 hover:from-rose-600 hover:to-amber-600 text-white font-bold text-xs shadow-md shadow-rose-500/20 hover:shadow-lg flex items-center gap-2 transition transform hover:-translate-y-0.5 active:translate-y-0 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed disabled:transform-none"
                    >
                      <Banknote className="w-4 h-4 text-amber-200" />
                      <span>Xin Thanh Toán Trước</span>
                      <Sparkles className="w-3 h-3 text-amber-200" />
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {/* Modal Xin Thanh Toán Trước Hợp Đồng */}
      <AdvancePaymentModal
        isOpen={selectedContractForAdvance !== null}
        onClose={handleCloseAdvanceModal}
        contract={selectedContractForAdvance}
        onSuccess={handleAdvanceSuccess}
      />
    </div>
  );
}
