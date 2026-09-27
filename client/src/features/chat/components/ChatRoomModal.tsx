import React, { useState, useRef, useEffect } from 'react';
import {
  X, Send, DollarSign, Sparkles,
  RefreshCw, Wifi, WifiOff
} from 'lucide-react';
import { useChatRoom } from '../hooks/useChatRoom';
import type { ChatMessage } from '../../../types/chat.types';

interface ChatRoomModalProps {
  isOpen: boolean;
  onClose: () => void;
  leadId: string;
  partnerName: string;
  isVendorUser: boolean;
  listingTitle?: string;
  voucherCode?: string;
}

export function ChatRoomModal({
  isOpen,
  onClose,
  leadId,
  partnerName,
  isVendorUser,
  listingTitle,
  voucherCode
}: ChatRoomModalProps) {
  const { messages, isLoading, isConnected, sendMessage, refetch } = useChatRoom(leadId);
  const [inputText, setInputText] = useState('');
  const [showQuoteForm, setShowQuoteForm] = useState(false);
  const [quoteAmount, setQuoteAmount] = useState<string>('');
  const [quoteDesc, setQuoteDesc] = useState<string>('');
  const [isSending, setIsSending] = useState(false);

  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Tự động cuộn xuống cuối khi có tin nhắn mới
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  if (!isOpen) return null;

  const handleSendText = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputText.trim() || isSending) return;

    try {
      setIsSending(true);
      await sendMessage({ content: inputText.trim() });
      setInputText('');
    } catch (err: any) {
      alert(err.message || 'Lỗi gửi tin nhắn.');
    } finally {
      setIsSending(false);
    }
  };

  const handleSendQuote = async (e: React.FormEvent) => {
    e.preventDefault();
    const amount = parseFloat(quoteAmount);
    if (isNaN(amount) || amount <= 0 || isSending) return;

    try {
      setIsSending(true);
      await sendMessage({
        content: `Báo giá dịch vụ trọn gói: ${amount.toLocaleString('vi-VN')} đ`,
        isQuote: true,
        quoteAmount: amount,
        quoteDescription: quoteDesc.trim() || undefined
      });
      setQuoteAmount('');
      setQuoteDesc('');
      setShowQuoteForm(false);
    } catch (err: any) {
      alert(err.message || 'Lỗi gửi báo giá.');
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-950/60 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-white rounded-3xl max-w-2xl w-full h-[85vh] flex flex-col shadow-2xl border border-rose-100 overflow-hidden relative">
        {/* Header */}
        <div className="px-5 py-4 bg-white border-b border-slate-100 flex items-center justify-between shadow-sm">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-gradient-to-tr from-rose-500 to-pink-500 text-white font-bold flex items-center justify-center text-sm shadow-md shadow-rose-200">
              {partnerName.charAt(0)}
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="font-bold text-slate-900 text-sm">{partnerName}</h3>
                {isConnected ? (
                  <span className="flex items-center gap-1 text-[10px] font-bold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded-full">
                    <Wifi className="w-3 h-3" /> Live
                  </span>
                ) : (
                  <span className="flex items-center gap-1 text-[10px] font-medium text-slate-400 bg-slate-100 px-2 py-0.5 rounded-full">
                    <WifiOff className="w-3 h-3" /> Polling
                  </span>
                )}
              </div>
              <p className="text-[11px] text-slate-400 truncate max-w-xs">
                {listingTitle || 'Tư vấn dịch vụ cưới'} {voucherCode && `· Voucher: ${voucherCode}`}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => refetch()}
              className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition"
              title="Làm mới tin nhắn"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
            <button
              onClick={onClose}
              className="p-2 rounded-xl text-slate-400 hover:text-slate-600 hover:bg-slate-100 transition"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Message history */}
        <div className="flex-1 p-5 overflow-y-auto space-y-4 bg-[#fcfbf9]">
          {isLoading ? (
            <div className="flex flex-col items-center justify-center h-full text-slate-400 text-xs">
              <div className="w-7 h-7 border-2 border-rose-300 border-t-rose-600 rounded-full animate-spin mb-2" />
              Đang tải tin nhắn...
            </div>
          ) : messages.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-center p-6 space-y-2">
              <div className="w-12 h-12 rounded-2xl bg-rose-50 text-rose-500 flex items-center justify-center text-xl">
                💬
              </div>
              <p className="text-sm font-bold text-slate-700">Chưa có tin nhắn nào</p>
              <p className="text-xs text-slate-400 max-w-xs">
                Hãy bắt đầu trao đổi chi tiết về dịch vụ, lịch hẹn tư vấn và báo giá ưu đãi tại đây.
              </p>
            </div>
          ) : (
            messages.map((msg: ChatMessage) => {
              // Nếu là Vendor user: tin từ customer là người khác (bên trái), tin của vendor là bên phải.
              // Nếu là Customer user: tin từ customer là bên phải.
              const isMine = isVendorUser ? !msg.isFromCustomer : msg.isFromCustomer;

              return (
                <div
                  key={msg.id}
                  className={`flex flex-col ${isMine ? 'items-end' : 'items-start'}`}
                >
                  <span className="text-[10px] text-slate-400 mb-1 px-1">
                    {msg.senderName} · {new Date(msg.createdAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
                  </span>

                  {msg.isQuote ? (
                    /* Thẻ Báo Giá (Quote Card) */
                    <div
                      className={`max-w-sm rounded-2xl p-4 shadow-sm border ${
                        isMine
                          ? 'bg-gradient-to-br from-rose-500 to-pink-600 text-white border-rose-400'
                          : 'bg-white text-slate-900 border-amber-200'
                      }`}
                    >
                      <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wider mb-2 opacity-90">
                        <Sparkles className="w-3.5 h-3.5" /> Báo Giá Chính Thức
                      </div>

                      <div className="text-2xl font-extrabold mb-1">
                        {msg.quoteAmount?.toLocaleString('vi-VN')} đ
                      </div>

                      {msg.quoteDescription && (
                        <p className="text-xs opacity-90 mb-2 leading-relaxed">
                          {msg.quoteDescription}
                        </p>
                      )}

                      <div className="text-[10px] pt-2 border-t border-white/20 flex justify-between items-center opacity-80">
                        <span>Đã bao gồm chiết khấu</span>
                        <span>{isMine ? 'Bạn đã gửi' : 'Từ nhà cung cấp'}</span>
                      </div>
                    </div>
                  ) : (
                    /* Tin nhắn Text thông thường */
                    <div
                      className={`max-w-md px-4 py-2.5 rounded-2xl text-xs leading-relaxed shadow-sm ${
                        isMine
                          ? 'bg-slate-900 text-white rounded-br-sm'
                          : 'bg-white text-slate-800 border border-slate-100 rounded-bl-sm'
                      }`}
                    >
                      {msg.content}
                    </div>
                  )}
                </div>
              );
            })
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* Form gửi Báo giá nhanh (chỉ dành cho Vendor) */}
        {isVendorUser && showQuoteForm && (
          <form
            onSubmit={handleSendQuote}
            className="p-4 bg-amber-50/80 border-t border-amber-200 space-y-3 animate-in slide-in-from-bottom duration-200"
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-amber-900 flex items-center gap-1">
                <DollarSign className="w-4 h-4 text-amber-600" /> Tạo Báo Giá Gửi Khách Hàng
              </span>
              <button
                type="button"
                onClick={() => setShowQuoteForm(false)}
                className="text-xs text-amber-700 hover:text-amber-900"
              >
                Đóng
              </button>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
              <input
                type="number"
                step="100000"
                placeholder="Số tiền báo giá (VNĐ) *"
                value={quoteAmount}
                onChange={(e) => setQuoteAmount(e.target.value)}
                className="w-full px-3 py-2 rounded-xl bg-white border border-amber-200 text-xs focus:outline-none focus:ring-2 focus:ring-amber-500/20"
                required
              />
              <input
                type="text"
                placeholder="Ghi chú gói dịch vụ..."
                value={quoteDesc}
                onChange={(e) => setQuoteDesc(e.target.value)}
                className="w-full px-3 py-2 rounded-xl bg-white border border-amber-200 text-xs focus:outline-none focus:ring-2 focus:ring-amber-500/20"
              />
            </div>

            <button
              type="submit"
              disabled={isSending}
              className="w-full py-2 rounded-xl bg-gradient-to-r from-amber-500 to-rose-500 hover:from-amber-600 hover:to-rose-600 text-white font-bold text-xs shadow-md transition disabled:opacity-50"
            >
              {isSending ? 'Đang gửi...' : '📤 Gửi Thẻ Báo Giá Này'}
            </button>
          </form>
        )}

        {/* Input bar */}
        <form onSubmit={handleSendText} className="p-3 bg-white border-t border-slate-100 flex items-center gap-2">
          {isVendorUser && (
            <button
              type="button"
              onClick={() => setShowQuoteForm(!showQuoteForm)}
              className={`p-2.5 rounded-xl border text-xs font-bold flex items-center gap-1 transition ${
                showQuoteForm
                  ? 'bg-amber-100 border-amber-300 text-amber-800'
                  : 'border-slate-200 hover:bg-slate-50 text-slate-700'
              }`}
              title="Gửi báo giá"
            >
              <DollarSign className="w-4 h-4 text-amber-600" />
              <span className="hidden sm:inline">Báo giá</span>
            </button>
          )}

          <input
            type="text"
            placeholder="Nhập tin nhắn..."
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            className="flex-1 px-4 py-2.5 rounded-xl border border-slate-200 text-xs focus:outline-none focus:ring-2 focus:ring-rose-500/20 focus:border-rose-500"
          />

          <button
            type="submit"
            disabled={!inputText.trim() || isSending}
            className="p-2.5 rounded-xl bg-rose-500 hover:bg-rose-600 text-white font-bold transition shadow-md shadow-rose-200 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            <Send className="w-4 h-4" />
          </button>
        </form>
      </div>
    </div>
  );
}
