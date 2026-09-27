import { useState, useEffect } from 'react';
import {
  FileText, CheckCircle2, AlertTriangle, Clock, X,
  ExternalLink, Gift, ShieldCheck,
  Building2, Calendar, Loader2, Sparkles
} from 'lucide-react';
import type { Contract } from '../../../types/contract.types';
import { useCustomerContracts } from '../hooks/useCustomerContracts';

export function MyContracts() {
  const {
    contracts,
    isLoading,
    isError,
    error,
    refetch,
    confirmContract,
    isConfirming,
    rejectContract,
    isRejecting
  } = useCustomerContracts();

  const [activeTab, setActiveTab] = useState<'pending' | 'confirmed' | 'all'>('pending');
  const [selectedContractForPreview, setSelectedContractForPreview] = useState<Contract | null>(null);
  const [rejectingContract, setRejectingContract] = useState<Contract | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [actionSuccessMsg, setActionSuccessMsg] = useState<string | null>(null);
  const [actionErrorMsg, setActionErrorMsg] = useState<string | null>(null);

  // Lọc theo tab
  const pendingContracts = contracts.filter(c => c.status === 'PendingVerification');
  const confirmedContracts = contracts.filter(c => c.status === 'Confirmed' || c.status === 'Completed');

  const displayedContracts =
    activeTab === 'pending'
      ? pendingContracts
      : activeTab === 'confirmed'
      ? confirmedContracts
      : contracts;

  const handleConfirm = async (contractId: string) => {
    try {
      setActionErrorMsg(null);
      const res = await confirmContract(contractId);
      setActionSuccessMsg(res.message || 'Xác nhận hợp đồng thành công! Ưu đãi sàn đã được áp dụng.');
      setTimeout(() => setActionSuccessMsg(null), 5000);
    } catch (err: unknown) {
      const e = err as Error;
      setActionErrorMsg(e.message || 'Không thể xác nhận hợp đồng.');
    }
  };

  const handleRejectSubmit = async () => {
    if (!rejectingContract || !rejectReason.trim()) return;
    try {
      setActionErrorMsg(null);
      await rejectContract({ contractId: rejectingContract.id, reason: rejectReason.trim() });
      setActionSuccessMsg('Đã gửi phản hồi sai lệch cho Nhà cung cấp để điều chỉnh lại.');
      setRejectingContract(null);
      setRejectReason('');
      setTimeout(() => setActionSuccessMsg(null), 5000);
    } catch (err: unknown) {
      const e = err as Error;
      setActionErrorMsg(e.message || 'Không thể gửi báo cáo sai lệch.');
    }
  };

  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[300px] text-slate-500">
        <Loader2 className="w-8 h-8 animate-spin text-rose-500 mb-3" />
        <p className="text-sm font-semibold">Đang tải danh sách hợp đồng cưới của bạn...</p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-3xl p-8 text-center max-w-lg mx-auto">
        <AlertTriangle className="w-10 h-10 text-red-500 mx-auto mb-3" />
        <h3 className="font-bold text-slate-800 text-base">Không thể tải hợp đồng</h3>
        <p className="text-xs text-slate-600 mt-1">{(error as Error)?.message || 'Vui lòng thử lại sau.'}</p>
        <button
          onClick={() => refetch()}
          className="mt-4 px-4 py-2 bg-slate-900 text-white rounded-xl text-xs font-semibold hover:bg-slate-800"
        >
          Tải lại
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header Banner */}
      <div className="relative overflow-hidden rounded-3xl bg-gradient-to-r from-rose-900 via-rose-800 to-amber-900 text-white p-7 shadow-lg">
        <div className="relative z-10 max-w-2xl">
          <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white/20 backdrop-blur-md text-rose-100 text-xs font-bold uppercase tracking-wider mb-3">
            <ShieldCheck className="w-3.5 h-3.5" />
            Cơ Chế Xác Thực 2 Chiều (BR-005)
          </div>
          <h2 className="text-2xl md:text-3xl font-extrabold tracking-tight" style={{ fontFamily: "'Playfair Display', serif" }}>
            Hợp Đồng Cưới Của Bạn
          </h2>
          <p className="text-rose-100/90 text-sm mt-2 leading-relaxed">
            Nhà cung cấp đã gửi thông tin ký kết & hóa đơn cọc. Bạn có <strong className="text-amber-300">72 giờ</strong> để kiểm tra và bấm [Xác Nhận] để nhận bảo trợ dịch vụ và phần quà tri ân từ sàn Wedding Platform!
          </p>
        </div>
      </div>

      {/* Thông báo thành công / lỗi */}
      {actionSuccessMsg && (
        <div className="p-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-2xl flex items-center justify-between text-sm shadow-xs animate-in fade-in">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-5 h-5 text-emerald-600 shrink-0" />
            <span className="font-semibold">{actionSuccessMsg}</span>
          </div>
          <button onClick={() => setActionSuccessMsg(null)} className="text-emerald-500 hover:text-emerald-800">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {actionErrorMsg && (
        <div className="p-4 bg-red-50 border border-red-200 text-red-800 rounded-2xl flex items-center justify-between text-sm shadow-xs animate-in fade-in">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-5 h-5 text-red-600 shrink-0" />
            <span className="font-semibold">{actionErrorMsg}</span>
          </div>
          <button onClick={() => setActionErrorMsg(null)} className="text-red-500 hover:text-red-800">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Tabs */}
      <div className="flex items-center gap-2 border-b border-slate-200 pb-3">
        <button
          onClick={() => setActiveTab('pending')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-bold transition ${
            activeTab === 'pending'
              ? 'bg-rose-500 text-white shadow-sm'
              : 'text-slate-600 hover:bg-slate-100'
          }`}
        >
          <Clock className="w-4 h-4" />
          Cần Duyệt (72h)
          {pendingContracts.length > 0 && (
            <span className={`text-xs px-2 py-0.5 rounded-full ${activeTab === 'pending' ? 'bg-white text-rose-600 font-extrabold' : 'bg-rose-100 text-rose-700'}`}>
              {pendingContracts.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab('confirmed')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-bold transition ${
            activeTab === 'confirmed'
              ? 'bg-rose-500 text-white shadow-sm'
              : 'text-slate-600 hover:bg-slate-100'
          }`}
        >
          <CheckCircle2 className="w-4 h-4" />
          Đã Xác Thực
          {confirmedContracts.length > 0 && (
            <span className={`text-xs px-2 py-0.5 rounded-full ${activeTab === 'confirmed' ? 'bg-white text-rose-600' : 'bg-slate-200 text-slate-700'}`}>
              {confirmedContracts.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab('all')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-bold transition ${
            activeTab === 'all'
              ? 'bg-rose-500 text-white shadow-sm'
              : 'text-slate-600 hover:bg-slate-100'
          }`}
        >
          <FileText className="w-4 h-4" />
          Tất Cả ({contracts.length})
        </button>
      </div>

      {/* Danh sách thẻ hợp đồng */}
      {displayedContracts.length === 0 ? (
        <div className="bg-white rounded-3xl border border-dashed border-slate-300 p-12 text-center text-slate-400">
          <FileText className="w-12 h-12 mx-auto mb-3 opacity-30 text-rose-500" />
          <h4 className="text-base font-bold text-slate-700">Chưa có hợp đồng nào trong mục này</h4>
          <p className="text-xs text-slate-500 mt-1 max-w-sm mx-auto">
            Khi bạn đồng ý dịch vụ với Nhà cung cấp, họ sẽ gửi hợp đồng và hóa đơn cọc lên sàn để bạn xác thực tại đây.
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-6">
          {displayedContracts.map((contract) => (
            <ContractItemCard
              key={contract.id}
              contract={contract}
              onConfirm={() => handleConfirm(contract.id)}
              onReject={() => setRejectingContract(contract)}
              onPreviewImage={() => setSelectedContractForPreview(contract)}
              isConfirming={isConfirming}
            />
          ))}
        </div>
      )}

      {/* Modal Báo Sai Lệch */}
      {rejectingContract && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in">
          <div className="bg-white rounded-3xl max-w-md w-full shadow-2xl p-6 border border-slate-100">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2 text-rose-600 font-bold text-base">
                <AlertTriangle className="w-5 h-5" />
                Báo Sai Lệch Hợp Đồng
              </div>
              <button
                onClick={() => setRejectingContract(null)}
                className="text-slate-400 hover:text-slate-600"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="my-4 text-xs text-slate-600 space-y-2">
              <p>
                Mã HĐ: <strong className="font-mono text-slate-800">{rejectingContract.contractCode}</strong> • Đối tác: <strong>{rejectingContract.vendorBrandName}</strong>
              </p>
              <p className="text-slate-500 leading-relaxed">
                Vui lòng chỉ rõ thông tin chưa chính xác (Ví dụ: <em>Số tiền cọc thực tế đã chuyển là 15 triệu thay vì 10 triệu</em>, hoặc <em>Ngày cưới ghi nhầm</em>) để Nhà cung cấp sửa lại:
              </p>

              <textarea
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                placeholder="Nhập chi tiết sai lệch cần NCC điều chỉnh..."
                rows={4}
                className="w-full mt-2 p-3 text-sm rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
              />
            </div>

            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                onClick={() => setRejectingContract(null)}
                className="px-4 py-2 rounded-xl text-xs font-semibold text-slate-500 hover:bg-slate-100"
              >
                Hủy bỏ
              </button>
              <button
                onClick={handleRejectSubmit}
                disabled={isRejecting || !rejectReason.trim()}
                className="px-5 py-2 rounded-xl text-xs font-bold bg-rose-600 text-white hover:bg-rose-500 disabled:opacity-50 flex items-center gap-1.5"
              >
                {isRejecting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : null}
                Gửi Báo Sai Lệch
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal Preview Ảnh Hóa Đơn / Phiếu Thu */}
      {selectedContractForPreview && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/80 backdrop-blur-sm animate-in fade-in">
          <div className="bg-white rounded-3xl max-w-2xl w-full shadow-2xl overflow-hidden border border-slate-100 flex flex-col max-h-[90vh]">
            <div className="p-4 bg-slate-900 text-white flex items-center justify-between">
              <div>
                <h4 className="font-bold text-sm">Ảnh Phiếu Thu / Hóa Đơn Cọc</h4>
                <p className="text-xs text-slate-400 font-mono">Hợp đồng: {selectedContractForPreview.contractCode}</p>
              </div>
              <button
                onClick={() => setSelectedContractForPreview(null)}
                className="text-slate-400 hover:text-white"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 overflow-y-auto flex items-center justify-center bg-slate-100">
              {selectedContractForPreview.contractImageUrl ? (
                <img
                  src={selectedContractForPreview.contractImageUrl}
                  alt="Phiếu thu cọc"
                  className="max-h-[70vh] object-contain rounded-xl shadow-md border border-slate-300"
                />
              ) : (
                <div className="text-center py-12 text-slate-400">
                  <p className="text-sm font-semibold">Chưa có ảnh hóa đơn cọc đính kèm</p>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// Subcomponent: Thẻ chi tiết từng hợp đồng
function ContractItemCard({
  contract,
  onConfirm,
  onReject,
  onPreviewImage,
  isConfirming
}: {
  contract: Contract;
  onConfirm: () => void;
  onReject: () => void;
  onPreviewImage: () => void;
  isConfirming: boolean;
}) {
  const isPending = contract.status === 'PendingVerification';
  const isConfirmed = contract.status === 'Confirmed' || contract.status === 'Completed';

  return (
    <div className={`bg-white rounded-3xl border transition-all duration-200 overflow-hidden shadow-xs hover:shadow-md ${
      isPending ? 'border-rose-300 ring-2 ring-rose-100' : 'border-slate-200'
    }`}>
      {/* Top Banner Status & Countdown */}
      <div className={`px-6 py-3 flex flex-wrap items-center justify-between gap-3 text-xs font-bold border-b ${
        isPending ? 'bg-rose-50/70 border-rose-100 text-rose-900' : 'bg-slate-50 border-slate-100 text-slate-600'
      }`}>
        <div className="flex items-center gap-2">
          <span className="font-mono bg-white px-2 py-0.5 rounded-md border border-slate-200 text-slate-800">
            {contract.contractCode}
          </span>
          <span className="text-slate-400">•</span>
          <span>Khởi tạo: {new Date(contract.createdAt).toLocaleDateString('vi-VN')}</span>
        </div>

        <div>
          {isPending ? (
            <CountdownTimer deadline={contract.verificationDeadline} />
          ) : isConfirmed ? (
            <span className="inline-flex items-center gap-1 text-emerald-700 bg-emerald-100 px-3 py-1 rounded-full">
              <CheckCircle2 className="w-3.5 h-3.5" />
              {contract.status === 'Completed' ? 'Đã Hoàn Tất Dịch Vụ Cưới' : 'Đã Xác Thực 2 Chiều'}
            </span>
          ) : (
            <span className="text-slate-500 bg-slate-200 px-2.5 py-0.5 rounded-full">
              {contract.status}
            </span>
          )}
        </div>
      </div>

      {/* Main Content Body */}
      <div className="p-6 grid grid-cols-1 lg:grid-cols-12 gap-6 items-center">
        {/* Cột 1: Thông tin NCC & Dịch vụ */}
        <div className="lg:col-span-5 space-y-3">
          <div className="flex items-start gap-3">
            <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-rose-500 to-amber-500 text-white flex items-center justify-center shrink-0 shadow-sm">
              <Building2 className="w-6 h-6" />
            </div>
            <div>
              <p className="text-[11px] font-bold text-rose-600 uppercase tracking-wider">Nhà Cung Cấp Dịch Vụ</p>
              <h3 className="text-lg font-black text-slate-900">{contract.vendorBrandName}</h3>
              <p className="text-xs text-slate-500">Khách hàng: <strong>{contract.customerName}</strong> ({contract.customerPhone})</p>
            </div>
          </div>

          <div className="flex items-center gap-4 text-xs text-slate-600 pt-1">
            <div className="flex items-center gap-1.5">
              <Calendar className="w-4 h-4 text-rose-500" />
              <span>
                Ngày cưới: <strong>{contract.weddingDate ? new Date(contract.weddingDate).toLocaleDateString('vi-VN') : 'Chưa định ngày'}</strong>
              </span>
            </div>
            {contract.voucherCode && (
              <div className="flex items-center gap-1.5 bg-amber-50 text-amber-800 px-2 py-0.5 rounded-lg border border-amber-200">
                <Gift className="w-3.5 h-3.5 text-amber-600" />
                <span>Voucher: <strong className="font-mono">{contract.voucherCode}</strong> (-{contract.voucherDiscount?.toLocaleString('vi-VN')} đ)</span>
              </div>
            )}
          </div>
        </div>

        {/* Cột 2: Giá trị hợp đồng & Tiền cọc */}
        <div className="lg:col-span-4 bg-slate-50/80 rounded-2xl p-4 border border-slate-200/80 space-y-2">
          <div className="flex items-center justify-between text-xs">
            <span className="text-slate-500">Tổng Giá Trị Hợp Đồng:</span>
            <span className="font-bold text-slate-900 text-sm">
              {contract.contractValue.toLocaleString('vi-VN')} đ
            </span>
          </div>

          <div className="flex items-center justify-between text-xs pb-1 border-b border-slate-200">
            <span className="text-slate-500">Tiền Đã Đặt Cọc:</span>
            <span className="font-extrabold text-rose-600 text-base">
              {contract.depositAmount.toLocaleString('vi-VN')} đ
            </span>
          </div>

          {/* Link xem ảnh phiếu thu cọc */}
          <div className="flex items-center justify-between pt-1">
            <span className="text-[11px] text-slate-500">Hóa đơn / Phiếu cọc:</span>
            {contract.contractImageUrl ? (
              <button
                onClick={onPreviewImage}
                className="inline-flex items-center gap-1 text-xs text-rose-600 hover:text-rose-700 font-bold hover:underline"
              >
                <ExternalLink className="w-3.5 h-3.5" />
                Xem Phiếu Thu
              </button>
            ) : (
              <span className="text-[11px] text-slate-400 italic">Không có ảnh</span>
            )}
          </div>
        </div>

        {/* Cột 3: Nút Thao tác khách hàng */}
        <div className="lg:col-span-3 flex flex-col gap-2">
          {isPending ? (
            <>
              <button
                onClick={onConfirm}
                disabled={isConfirming}
                className="w-full py-2.5 px-4 rounded-xl bg-gradient-to-r from-rose-600 to-amber-600 hover:from-rose-500 hover:to-amber-500 text-white font-bold text-xs shadow-md shadow-rose-600/20 flex items-center justify-center gap-1.5 transition disabled:opacity-50"
              >
                {isConfirming ? (
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                ) : (
                  <Sparkles className="w-3.5 h-3.5" />
                )}
                <span>Xác Nhận Hợp Đồng (Nhận Quà Sàn)</span>
              </button>

              <button
                onClick={onReject}
                className="w-full py-2 px-3 rounded-xl border border-slate-300 text-slate-600 hover:bg-slate-100 font-semibold text-xs transition flex items-center justify-center gap-1"
              >
                <AlertTriangle className="w-3.5 h-3.5 text-amber-500" />
                Báo Sai Lệch
              </button>
            </>
          ) : isConfirmed ? (
            <div className="bg-emerald-50 border border-emerald-200 rounded-2xl p-3 text-center">
              <div className="inline-flex items-center gap-1 text-emerald-700 font-bold text-xs mb-1">
                <ShieldCheck className="w-4 h-4" />
                Hợp Đồng Đã Có Hiệu Lực
              </div>
              <p className="text-[11px] text-emerald-600">
                Bạn đã được bảo trợ bởi Wedding Platform
              </p>
            </div>
          ) : (
            <div className="text-center text-xs text-slate-400 py-2">
              Trạng thái: {contract.status}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// Subcomponent: Đồng hồ đếm ngược 72 giờ (BR-005)
function CountdownTimer({ deadline }: { deadline: string }) {
  const [timeLeft, setTimeLeft] = useState<{ hours: number; minutes: number; seconds: number; isExpired: boolean }>({
    hours: 72,
    minutes: 0,
    seconds: 0,
    isExpired: false
  });

  useEffect(() => {
    const calculateTime = () => {
      const target = new Date(deadline).getTime();
      const now = new Date().getTime();
      const diff = target - now;

      if (diff <= 0) {
        setTimeLeft({ hours: 0, minutes: 0, seconds: 0, isExpired: true });
        return;
      }

      const hours = Math.floor(diff / (1000 * 60 * 60));
      const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
      const seconds = Math.floor((diff % (1000 * 60)) / 1000);

      setTimeLeft({ hours, minutes, seconds, isExpired: false });
    };

    calculateTime();
    const interval = setInterval(calculateTime, 1000);
    return () => clearInterval(interval);
  }, [deadline]);

  if (timeLeft.isExpired) {
    return (
      <span className="inline-flex items-center gap-1 text-red-600 bg-red-100 px-3 py-1 rounded-full text-xs font-bold">
        <AlertTriangle className="w-3.5 h-3.5" />
        Đã Quá Hạn 72 Giờ (Tự Hủy)
      </span>
    );
  }

  return (
    <span className="inline-flex items-center gap-1.5 text-amber-800 bg-amber-100 px-3 py-1 rounded-full text-xs font-extrabold animate-pulse">
      <Clock className="w-3.5 h-3.5 text-amber-600" />
      Còn lại: {timeLeft.hours}h {timeLeft.minutes < 10 ? `0${timeLeft.minutes}` : timeLeft.minutes}m {timeLeft.seconds < 10 ? `0${timeLeft.seconds}` : timeLeft.seconds}s
    </span>
  );
}

export default MyContracts;
