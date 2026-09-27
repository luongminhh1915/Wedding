import { useState } from 'react';
import {
  QrCode, Calendar, CheckCircle2, Clock, AlertTriangle,
  RotateCw, Check, Loader2
} from 'lucide-react';
import { useSettlement } from '../hooks/useSettlement';
import { VietQrModal } from './VietQrModal';

export function SettlementDashboard() {
  const now = new Date();
  const [selectedMonth, setSelectedMonth] = useState<number>(now.getMonth() + 1);
  const [selectedYear, setSelectedYear] = useState<number>(now.getFullYear());
  const [isQrModalOpen, setIsQrModalOpen] = useState(false);

  const { statement, isLoading, isError, error, refetch } = useSettlement(selectedMonth, selectedYear);

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

  // Phân loại các khoản Kỳ 1 & Kỳ 2
  const period1Items = statement.items.filter(i => i.period.includes('Kỳ 1'));
  const period2Items = statement.items.filter(i => i.period.includes('Kỳ 2'));
  const period1Amount = period1Items.reduce((acc, cur) => acc + cur.commissionAmount, 0);
  const period2Amount = period2Items.reduce((acc, cur) => acc + cur.commissionAmount, 0);

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Paid':
        return (
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">
            <Check className="w-3 h-3" />
            Đã Thanh Toán
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
      {/* Top Header & Period Selector */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs">
        <div>
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-1 rounded-lg bg-rose-100 text-rose-700 font-extrabold text-xs tracking-wider uppercase">
              BR-007 • Chu kỳ ngày 25
            </span>
            <span className="text-xs text-slate-400">•</span>
            <span className="text-xs font-semibold text-slate-500">
              Đối tác: <strong className="text-slate-800">{statement.vendorBrandName}</strong>
            </span>
          </div>
          <h2 className="text-2xl font-black text-slate-900 mt-1">Bảng Kê Đối Soát & Hoa Hồng VietQR</h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Chốt số liệu ngày 25 hàng tháng. Hạn chót chuyển khoản hoa hồng là ngày cuối cùng của tháng.
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
            className="p-2.5 rounded-xl border border-slate-200 hover:bg-slate-100 text-slate-600 transition"
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

        {/* Card 2: Hoa Hồng Kỳ 1 */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <div className="text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-1">
            Hoa Hồng Kỳ 1 (Lúc Cọc)
          </div>
          <div className="text-2xl font-black text-slate-900 tracking-tight">
            {period1Amount.toLocaleString('vi-VN')} đ
          </div>
          <div className="text-xs text-sky-600 mt-1 font-medium">
            {period1Items.length} HĐ đã xác thực 2 chiều (BR-005)
          </div>
        </div>

        {/* Card 3: Hoa Hồng Kỳ 2 */}
        <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs">
          <div className="text-[11px] font-bold uppercase tracking-wider text-slate-400 mb-1">
            Hoa Hồng Kỳ 2 (Sau Cưới)
          </div>
          <div className="text-2xl font-black text-slate-900 tracking-tight">
            {period2Amount.toLocaleString('vi-VN')} đ
          </div>
          <div className="text-xs text-amber-600 mt-1 font-medium">
            {period2Items.length} Đám cưới hoàn tất trong tháng
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
            <h3 className="font-extrabold text-lg text-slate-900">
              Chi Tiết Các Khoản Hoa Hồng Trong Kỳ
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Tỷ lệ hoa hồng chuẩn: 3% - 12% theo chính sách ngành cưới (Kỳ 1: 50% khi xác nhận, Kỳ 2: 50% sau tiệc cưới).
            </p>
          </div>

          <div>
            {statement.totalPendingAmount > 0 ? (
              <button
                onClick={() => setIsQrModalOpen(true)}
                className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-rose-600 to-amber-600 hover:from-rose-500 hover:to-amber-500 text-white font-bold text-sm shadow-md shadow-rose-600/20 flex items-center gap-2 transition transform hover:-translate-y-0.5 active:translate-y-0"
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
          {statement.items.length === 0 ? (
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
                  <th className="py-3 px-5 text-center">Tỷ Lệ HH</th>
                  <th className="py-3 px-5">Kỳ Đối Soát</th>
                  <th className="py-3 px-5 text-right">Số Tiền Phải Nộp</th>
                  <th className="py-3 px-5">Hạn Nộp</th>
                  <th className="py-3 px-5 text-center">Trạng Thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {statement.items.map((item) => (
                  <tr key={item.id} className="hover:bg-rose-50/20 transition">
                    <td className="py-3.5 px-5 font-mono font-bold text-slate-900">
                      {item.contractCode}
                    </td>
                    <td className="py-3.5 px-5 font-semibold text-slate-800">
                      {item.customerName}
                    </td>
                    <td className="py-3.5 px-5 text-right font-medium text-slate-700">
                      {item.contractValue.toLocaleString('vi-VN')} đ
                    </td>
                    <td className="py-3.5 px-5 text-center">
                      <span className="font-semibold text-slate-600 bg-slate-100 px-2 py-0.5 rounded text-xs">
                        {(item.commissionRate * 100).toFixed(0)}%
                      </span>
                    </td>
                    <td className="py-3.5 px-5">
                      <span className={`text-xs px-2 py-0.5 rounded font-bold ${
                        item.period.includes('Kỳ 1')
                          ? 'bg-sky-50 text-sky-700 border border-sky-200'
                          : 'bg-amber-50 text-amber-800 border border-amber-200'
                      }`}>
                        {item.period}
                      </span>
                    </td>
                    <td className="py-3.5 px-5 text-right font-extrabold text-rose-600">
                      {item.commissionAmount.toLocaleString('vi-VN')} đ
                    </td>
                    <td className="py-3.5 px-5 text-xs text-slate-500">
                      {new Date(item.dueDate).toLocaleDateString('vi-VN')}
                    </td>
                    <td className="py-3.5 px-5 text-center">
                      {getStatusBadge(item.status)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Footer Summary */}
        <div className="bg-slate-50/80 p-5 border-t border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-xs text-slate-600">
          <div>
            Tổng số hợp đồng trong bảng kê: <strong>{statement.items.length}</strong> •
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
    </div>
  );
}
export default SettlementDashboard;
