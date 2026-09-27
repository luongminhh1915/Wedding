import { useState } from 'react';
import {
  FileText, Clock, CheckCircle2, AlertCircle,
  Gift, Calendar, RefreshCw
} from 'lucide-react';
import { useVendorContracts } from '../hooks/useVendorContracts';
import type { Contract } from '../../../types/contract.types';

export function ContractList() {
  const [selectedStatus, setSelectedStatus] = useState<string>('');
  const { data: contracts = [], isLoading, isError, refetch } = useVendorContracts(selectedStatus || undefined);

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
      {/* Header */}
      <div className="bg-white rounded-2xl border border-rose-100 p-6 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 shadow-sm">
        <div>
          <h2 className="text-xl font-bold text-slate-900" style={{ fontFamily: "'Playfair Display', serif" }}>
            Quản Lý Hợp Đồng Dịch Vụ Cưới (BR-005)
          </h2>
          <p className="text-xs text-slate-500 mt-1">
            Xác thực 2 chiều trong <strong className="text-rose-600">72 giờ</strong> · Tự động sinh hoa hồng kỳ 1 khi khách duyệt (<strong className="text-rose-600">BR-006</strong>).
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

                {/* Hoa hồng đã sinh tự động */}
                {contract.commissions && contract.commissions.length > 0 && (
                  <div className="p-3 rounded-xl bg-emerald-50/70 border border-emerald-200 flex flex-wrap items-center justify-between gap-2 text-xs">
                    <div className="flex items-center gap-2">
                      <span className="w-2 h-2 rounded-full bg-emerald-500" />
                      <span className="font-bold text-emerald-900">
                        Phí hoa hồng Đợt 1 (50% lúc cọc theo BR-006):
                      </span>
                      <span className="font-extrabold text-emerald-700">
                        {contract.commissions[0].commissionAmount.toLocaleString('vi-VN')} đ
                      </span>
                      <span className="text-[10px] text-emerald-600 bg-white px-2 py-0.5 rounded-full border border-emerald-200 font-semibold">
                        Hạn thanh toán: Ngày 25
                      </span>
                    </div>

                    <span className="text-[11px] font-bold text-emerald-800 uppercase tracking-wider">
                      Trạng thái: {contract.commissions[0].status}
                    </span>
                  </div>
                )}

                {contract.cancellationReason && (
                  <div className="p-3 rounded-xl bg-rose-50 border border-rose-100 text-xs text-rose-700">
                    <strong>Lý do từ chối/hủy:</strong> {contract.cancellationReason}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
