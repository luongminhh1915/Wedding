import { useState, useEffect } from 'react';
import {
  Clock, Lock, Unlock, CheckCircle2, MessageSquare,
  Gift, Calendar, Users, DollarSign, AlertCircle, RefreshCw, AlertTriangle, FileText
} from 'lucide-react';
import { useVendorLeads } from '../hooks/useVendorLeads';
import { ChatRoomModal } from '../../chat/components/ChatRoomModal';
import { CreateContractModal } from './CreateContractModal';
import type { Lead } from '../../../types/lead.types';

// Component đếm ngược SLA 2h realtime BR-003
function SlaCountdown({ deadline }: { deadline: string }) {
  const [timeLeft, setTimeLeft] = useState<{ hours: number; minutes: number; seconds: number; isOverdue: boolean }>({
    hours: 0,
    minutes: 0,
    seconds: 0,
    isOverdue: false
  });

  useEffect(() => {
    const updateCountdown = () => {
      const now = new Date().getTime();
      const target = new Date(deadline).getTime();
      const diff = target - now;

      if (diff <= 0) {
        setTimeLeft({ hours: 0, minutes: 0, seconds: 0, isOverdue: true });
      } else {
        const hours = Math.floor(diff / (1000 * 60 * 60));
        const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
        const seconds = Math.floor((diff % (1000 * 60)) / 1000);
        setTimeLeft({ hours, minutes, seconds, isOverdue: false });
      }
    };

    updateCountdown();
    const interval = setInterval(updateCountdown, 1000);
    return () => clearInterval(interval);
  }, [deadline]);

  if (timeLeft.isOverdue) {
    return (
      <span className="flex items-center gap-1 text-rose-600 font-bold">
        <AlertTriangle className="w-3.5 h-3.5" /> Quá hạn SLA 2h!
      </span>
    );
  }

  const pad = (n: number) => n.toString().padStart(2, '0');

  return (
    <span className="font-mono text-amber-900 font-bold">
      Còn {pad(timeLeft.hours)}:{pad(timeLeft.minutes)}:{pad(timeLeft.seconds)}
    </span>
  );
}

export function LeadInbox() {
  const [selectedStatus, setSelectedStatus] = useState<string>('');
  const { data: leads = [], isLoading, isError, refetch, acceptLead, isAccepting } = useVendorLeads(selectedStatus || undefined);
  const [actionError, setActionError] = useState<string | null>(null);
  const [activeChatLead, setActiveChatLead] = useState<Lead | null>(null);
  const [activeContractLead, setActiveContractLead] = useState<Lead | null>(null);

  const handleAccept = async (lead: Lead) => {
    setActionError(null);
    try {
      await acceptLead(lead.id);
      // Mở chat ngay sau khi tiếp nhận để trao đổi báo giá
      setActiveChatLead(lead);
    } catch (err: any) {
      setActionError(err.message || 'Lỗi khi tiếp nhận lead.');
    }
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'New':
        return <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-amber-50 text-amber-700 border border-amber-200 animate-pulse">⚡ Mới nhận (Chờ duyệt)</span>;
      case 'Accepted':
        return <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">✓ Đã tiếp nhận</span>;
      case 'Contacted':
        return <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-blue-50 text-blue-700 border border-blue-200">Đang trao đổi</span>;
      case 'Contracted':
        return <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-purple-50 text-purple-700 border border-purple-200">Đã chốt HĐ</span>;
      case 'Cancelled':
      case 'Expired':
        return <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-rose-50 text-rose-700 border border-rose-200">Đã hủy / Quá hạn</span>;
      default:
        return <span className="px-2.5 py-1 rounded-full text-xs font-bold bg-slate-50 text-slate-700">{status}</span>;
    }
  };

  return (
    <div className="space-y-6">
      {/* Header card */}
      <div className="bg-white rounded-2xl border border-rose-100 p-6 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 shadow-sm">
        <div>
          <h2 className="text-xl font-bold text-slate-900" style={{ fontFamily: "'Playfair Display', serif" }}>
            Hộp Thư Tiếp Nhận Lead &amp; Báo Giá Realtime
          </h2>
          <p className="text-xs text-slate-500 mt-1">
            Tuân thủ <strong className="text-rose-600">BR-003</strong> (SLA tiếp nhận 2h đếm ngược) &amp; <strong className="text-rose-600">BR-002</strong> (Smart Privacy bảo mật SĐT khách).
          </p>
        </div>

        <button
          onClick={() => refetch()}
          className="flex items-center gap-1.5 px-4 py-2 rounded-xl border border-slate-200 text-xs font-bold text-slate-700 hover:bg-slate-50 transition"
        >
          <RefreshCw className="w-3.5 h-3.5" />
          Làm mới
        </button>
      </div>

      {/* Filter tabs */}
      <div className="flex gap-2 border-b border-slate-200 pb-2 overflow-x-auto text-xs font-bold">
        {[
          { id: '', label: 'Tất cả Lead' },
          { id: 'New', label: '⚡ Chờ tiếp nhận (SLA 2h)' },
          { id: 'Accepted', label: '✓ Đã tiếp nhận' },
          { id: 'Contracted', label: '🎉 Đã ký hợp đồng' }
        ].map((tab) => (
          <button
            key={tab.id}
            onClick={() => setSelectedStatus(tab.id)}
            className={`px-3.5 py-2 rounded-xl transition ${
              selectedStatus === tab.id
                ? 'bg-rose-500 text-white shadow-sm'
                : 'text-slate-600 hover:bg-slate-100'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {actionError && (
        <div className="p-4 rounded-xl bg-rose-50 border border-rose-100 text-rose-600 text-xs font-medium flex items-center gap-2">
          <AlertCircle className="w-4 h-4 flex-shrink-0" />
          {actionError}
        </div>
      )}

      {/* Content list */}
      {isLoading ? (
        <div className="bg-white rounded-2xl border border-slate-100 p-12 text-center">
          <div className="w-8 h-8 border-2 border-rose-300 border-t-rose-600 rounded-full animate-spin mx-auto mb-3" />
          <p className="text-xs text-slate-400">Đang tải danh sách Lead...</p>
        </div>
      ) : isError ? (
        <div className="bg-white rounded-2xl border border-rose-100 p-8 text-center text-rose-500 text-xs">
          Có lỗi xảy ra khi tải dữ liệu.
        </div>
      ) : leads.length === 0 ? (
        <div className="bg-white rounded-2xl border border-slate-100 p-12 text-center space-y-2">
          <div className="w-12 h-12 rounded-2xl bg-rose-50 text-rose-500 flex items-center justify-center mx-auto text-xl">
            💌
          </div>
          <p className="text-sm font-bold text-slate-700">Chưa có yêu cầu tư vấn nào</p>
          <p className="text-xs text-slate-400">Các yêu cầu từ cô dâu &amp; chú rể sẽ xuất hiện tại đây.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4">
          {leads.map((lead: Lead) => (
            <div
              key={lead.id}
              className="bg-white rounded-2xl border border-slate-100 hover:border-rose-100 transition shadow-sm p-5 space-y-4"
            >
              {/* Header row */}
              <div className="flex flex-wrap items-center justify-between gap-3 pb-3 border-b border-slate-100">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-full bg-gradient-to-tr from-rose-100 to-pink-100 text-rose-600 font-bold flex items-center justify-center text-sm">
                    {lead.customerName.charAt(0)}
                  </div>
                  <div>
                    <h4 className="font-bold text-slate-900 text-sm">{lead.customerName}</h4>
                    <p className="text-[11px] text-slate-400">
                      Gửi lúc: {new Date(lead.createdAt).toLocaleString('vi-VN')}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  {getStatusBadge(lead.status)}

                  {lead.voucher && (
                    <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-mono font-bold bg-amber-50 text-amber-700 border border-amber-200">
                      <Gift className="w-3 h-3 text-amber-500" />
                      {lead.voucher.code}
                    </span>
                  )}
                </div>
              </div>

              {/* Details grid */}
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 text-xs">
                {/* Phone Smart Privacy BR-002 */}
                <div className="p-3 rounded-xl bg-slate-50 border border-slate-100">
                  <span className="text-[10px] text-slate-400 font-semibold block mb-1">
                    Số Điện Thoại (BR-002)
                  </span>
                  <div className="flex items-center gap-1.5 font-bold text-slate-800">
                    {lead.isPhoneUnlocked ? (
                      <>
                        <Unlock className="w-3.5 h-3.5 text-emerald-500" />
                        <span className="text-emerald-700">{lead.phoneNumber}</span>
                      </>
                    ) : (
                      <>
                        <Lock className="w-3.5 h-3.5 text-amber-500" />
                        <span className="font-mono text-slate-600">{lead.phoneNumber}</span>
                      </>
                    )}
                  </div>
                </div>

                {/* Wedding date */}
                <div className="p-3 rounded-xl bg-slate-50 border border-slate-100">
                  <span className="text-[10px] text-slate-400 font-semibold block mb-1">Ngày Cưới Dự Kiến</span>
                  <div className="flex items-center gap-1.5 font-bold text-slate-800">
                    <Calendar className="w-3.5 h-3.5 text-rose-500" />
                    <span>
                      {lead.weddingDate ? new Date(lead.weddingDate).toLocaleDateString('vi-VN') : 'Chưa định ngày'}
                    </span>
                  </div>
                </div>

                {/* Guests & Budget */}
                <div className="p-3 rounded-xl bg-slate-50 border border-slate-100">
                  <span className="text-[10px] text-slate-400 font-semibold block mb-1">Quy Mô &amp; Ngân Sách</span>
                  <div className="space-y-0.5 font-bold text-slate-800">
                    <div className="flex items-center gap-1">
                      <Users className="w-3 h-3 text-slate-400" />
                      <span>{lead.estimatedGuests ? `${lead.estimatedGuests} khách` : 'Chưa rõ'}</span>
                    </div>
                    <div className="flex items-center gap-1 text-rose-600">
                      <DollarSign className="w-3 h-3" />
                      <span>{lead.estimatedBudget ? `${lead.estimatedBudget.toLocaleString('vi-VN')}đ` : 'Thương lượng'}</span>
                    </div>
                  </div>
                </div>

                {/* SLA Countdown BR-003 */}
                <div className={`p-3 rounded-xl border ${
                  lead.status === 'New'
                    ? 'bg-amber-50/80 border-amber-200'
                    : 'bg-slate-50 border-slate-100 text-slate-700'
                }`}>
                  <span className="text-[10px] font-semibold block mb-1 text-slate-500">
                    Đồng Hồ SLA 2h (BR-003)
                  </span>
                  <div className="flex items-center gap-1.5 text-xs font-bold">
                    <Clock className="w-3.5 h-3.5 flex-shrink-0 text-amber-600" />
                    {lead.status === 'New' ? (
                      <SlaCountdown deadline={lead.slaDeadline} />
                    ) : lead.acceptedAt ? (
                      <span className="text-emerald-700">
                        Đã nhận lúc {new Date(lead.acceptedAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
                      </span>
                    ) : (
                      <span>Đã đóng</span>
                    )}
                  </div>
                </div>
              </div>

              {/* Notes if any */}
              {lead.notes && (
                <div className="p-3 rounded-xl bg-rose-50/50 border border-rose-100 text-xs text-slate-700">
                  <strong className="text-rose-600">Ghi chú của khách:</strong> {lead.notes}
                </div>
              )}

              {/* Actions */}
              <div className="flex items-center justify-end gap-3 pt-2">
                {lead.status === 'New' ? (
                  <button
                    onClick={() => handleAccept(lead)}
                    disabled={isAccepting}
                    className="flex items-center gap-1.5 px-4 py-2 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-600 hover:from-emerald-600 hover:to-teal-700 text-white font-bold text-xs shadow-md transition disabled:opacity-50 cursor-pointer"
                  >
                    <CheckCircle2 className="w-4 h-4" />
                    {isAccepting ? 'Đang xử lý...' : '✓ Tiếp Nhận Lead & Mở Chat'}
                  </button>
                ) : (
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => setActiveContractLead(lead)}
                      className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 text-white font-bold text-xs shadow-sm transition cursor-pointer"
                    >
                      <FileText className="w-4 h-4" />
                      Lập Hợp Đồng (BR-005)
                    </button>
                    <button
                      onClick={() => setActiveChatLead(lead)}
                      className="flex items-center gap-1.5 px-3.5 py-2 rounded-xl bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs shadow-sm transition cursor-pointer"
                    >
                      <MessageSquare className="w-4 h-4" />
                      Mở Chat Báo Giá
                    </button>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Modal Chat Room Realtime SignalR */}
      {activeChatLead && (
        <ChatRoomModal
          isOpen={!!activeChatLead}
          onClose={() => setActiveChatLead(null)}
          leadId={activeChatLead.id}
          partnerName={activeChatLead.customerName}
          isVendorUser={true}
          listingTitle={activeChatLead.listingTitle}
          voucherCode={activeChatLead.voucher?.code}
        />
      )}

      {/* Modal Lập Hợp Đồng Chốt Khách */}
      {activeContractLead && (
        <CreateContractModal
          isOpen={!!activeContractLead}
          onClose={() => setActiveContractLead(null)}
          leadId={activeContractLead.id}
          customerName={activeContractLead.customerName}
          defaultVoucherCode={activeContractLead.voucher?.code}
          defaultWeddingDate={activeContractLead.weddingDate}
          defaultBudget={activeContractLead.estimatedBudget}
          onSuccess={() => refetch()}
        />
      )}
    </div>
  );
}
