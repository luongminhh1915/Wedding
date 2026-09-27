import { useState } from 'react';
import {
  X, QrCode, CheckCircle2, Copy, Download,
  ExternalLink, Building2, AlertCircle, Loader2, Sparkles
} from 'lucide-react';
import type { MonthlySettlementStatement } from '../../../types/commission.types';
import { useSettlement } from '../hooks/useSettlement';

interface VietQrModalProps {
  isOpen: boolean;
  onClose: () => void;
  statement: MonthlySettlementStatement;
  onPaymentSuccess?: () => void;
}

export function VietQrModal({ isOpen, onClose, statement, onPaymentSuccess }: VietQrModalProps) {
  const { simulatePayment, isSimulatingPayment } = useSettlement(statement.month, statement.year);
  const [copiedField, setCopiedField] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const vietQr = statement.vietQr;
  const qrImage = vietQr?.qrImageUrl ||
    `https://img.vietqr.io/image/MB-0338889999-compact2.png?amount=${statement.totalPendingAmount}&addInfo=${encodeURIComponent(statement.transferContent)}&accountName=WEDDING%20PLATFORM%20VN`;

  const copyToClipboard = (text: string, fieldName: string) => {
    navigator.clipboard.writeText(text);
    setCopiedField(fieldName);
    setTimeout(() => setCopiedField(null), 2500);
  };

  const handleSimulateWebhook = async () => {
    try {
      setErrorMsg(null);
      const res = await simulatePayment({
        gateway: 'MBBank',
        amount: statement.totalPendingAmount,
        content: statement.transferContent,
        referenceCode: `FT${Date.now()}`
      });

      setSuccessMessage(res.message || 'Thanh toán hoa hồng thành công! Hệ thống đã tự động gạch nợ sang trạng thái Đã Thanh Toán (Paid).');
      if (onPaymentSuccess) {
        onPaymentSuccess();
      }
    } catch (err: unknown) {
      const e = err as Error;
      setErrorMsg(e.message || 'Không thể mô phỏng gạch nợ.');
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="bg-white rounded-3xl max-w-lg w-full shadow-2xl overflow-hidden border border-slate-100 flex flex-col">
        {/* Header */}
        <div className="bg-gradient-to-r from-slate-900 via-zinc-800 to-rose-950 text-white px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-rose-500/20 border border-rose-400/30 flex items-center justify-center text-rose-300">
              <QrCode className="w-5 h-5" />
            </div>
            <div>
              <h3 className="font-bold text-base leading-tight">Thanh Toán Hoa Hồng VietQR</h3>
              <p className="text-xs text-rose-200/80">Kỳ đối soát Tháng {statement.month}/{statement.year} (BR-007)</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-full bg-white/10 hover:bg-white/20 flex items-center justify-center text-white/80 hover:text-white transition"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto max-h-[85vh]">
          {successMessage ? (
            <div className="py-8 text-center flex flex-col items-center">
              <div className="w-16 h-16 bg-emerald-100 text-emerald-600 rounded-full flex items-center justify-center mb-4 animate-bounce">
                <CheckCircle2 className="w-9 h-9" />
              </div>
              <h4 className="text-xl font-bold text-slate-800 mb-2">Thanh Toán Hoàn Tất!</h4>
              <p className="text-sm text-slate-600 max-w-xs leading-relaxed mb-6">
                {successMessage}
              </p>
              <div className="bg-emerald-50 border border-emerald-200 rounded-2xl p-4 w-full text-left text-xs space-y-1.5 mb-6 text-emerald-800">
                <div className="flex justify-between">
                  <span>Mã Đối Tác:</span>
                  <span className="font-mono font-bold">{statement.vendorBrandName}</span>
                </div>
                <div className="flex justify-between">
                  <span>Số tiền đã gạch nợ:</span>
                  <span className="font-bold text-sm text-emerald-700">
                    {statement.totalPendingAmount.toLocaleString('vi-VN')} đ
                  </span>
                </div>
                <div className="flex justify-between">
                  <span>Cú pháp ghi nhận:</span>
                  <span className="font-mono">{statement.transferContent}</span>
                </div>
              </div>
              <button
                onClick={onClose}
                className="w-full py-3 bg-slate-900 text-white font-bold rounded-xl hover:bg-slate-800 transition"
              >
                Đóng & Xem Bảng Kê Đã Cập Nhật
              </button>
            </div>
          ) : (
            <>
              {/* QR Image Box */}
              <div className="border-2 border-dashed border-rose-200 rounded-2xl p-4 bg-rose-50/40 text-center mb-5 relative group">
                <div className="text-[11px] font-bold text-rose-600 uppercase tracking-wider mb-2 flex items-center justify-center gap-1">
                  <Sparkles className="w-3.5 h-3.5" />
                  Quét Mã Bằng App Ngân Hàng Hoặc Ví MoMo/ZaloPay
                </div>
                <div className="relative inline-block bg-white p-3 rounded-xl shadow-md border border-slate-100">
                  <img
                    src={qrImage}
                    alt="VietQR Settlement Napas247"
                    className="w-56 h-56 object-contain mx-auto rounded-lg"
                  />
                </div>
                <div className="mt-2 flex items-center justify-center gap-2">
                  <a
                    href={qrImage}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1 text-xs text-rose-600 hover:text-rose-700 font-semibold py-1 px-2.5 rounded-lg bg-white border border-rose-200 shadow-xs transition hover:bg-rose-50"
                  >
                    <Download className="w-3.5 h-3.5" />
                    Tải Ảnh QR
                  </a>
                  <a
                    href={qrImage}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1 text-xs text-slate-600 hover:text-slate-800 font-semibold py-1 px-2.5 rounded-lg bg-white border border-slate-200 shadow-xs transition hover:bg-slate-50"
                  >
                    <ExternalLink className="w-3.5 h-3.5" />
                    Mở Ảnh To
                  </a>
                </div>
                <p className="text-[11px] text-slate-400 mt-2">
                  Chuẩn Napas 24/7 • Tự động điền số tiền và nội dung đối soát
                </p>
              </div>

              {/* Bank Info Rows */}
              <div className="bg-slate-50 rounded-2xl p-4 border border-slate-200/80 space-y-2.5 text-xs mb-5">
                <div className="flex items-center justify-between pb-2 border-b border-slate-200/60">
                  <span className="text-slate-500 font-medium">Ngân Hàng Thụ Hưởng:</span>
                  <span className="font-bold text-slate-800 flex items-center gap-1">
                    <Building2 className="w-3.5 h-3.5 text-slate-400" />
                    {vietQr?.bankName || 'MBBank (Ngân Hàng Quân Đội)'}
                  </span>
                </div>

                <div className="flex items-center justify-between pb-2 border-b border-slate-200/60">
                  <span className="text-slate-500 font-medium">Số Tài Khoản:</span>
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-bold text-sm text-rose-600">
                      {vietQr?.accountNumber || '0338889999'}
                    </span>
                    <button
                      onClick={() => copyToClipboard(vietQr?.accountNumber || '0338889999', 'accountNumber')}
                      className="p-1 text-slate-400 hover:text-slate-700 rounded transition"
                      title="Sao chép STK"
                    >
                      {copiedField === 'accountNumber' ? (
                        <span className="text-[10px] text-emerald-600 font-bold">Đã chép</span>
                      ) : (
                        <Copy className="w-3.5 h-3.5" />
                      )}
                    </button>
                  </div>
                </div>

                <div className="flex items-center justify-between pb-2 border-b border-slate-200/60">
                  <span className="text-slate-500 font-medium">Tên Chủ Tài Khoản:</span>
                  <span className="font-bold text-slate-800 uppercase font-mono">
                    {vietQr?.accountName || 'WEDDING PLATFORM VN'}
                  </span>
                </div>

                <div className="flex items-center justify-between pb-2 border-b border-slate-200/60">
                  <span className="text-slate-500 font-medium">Số Tiền Cần Nộp:</span>
                  <span className="font-extrabold text-base text-rose-600">
                    {statement.totalPendingAmount.toLocaleString('vi-VN')} đ
                  </span>
                </div>

                <div className="flex items-center justify-between">
                  <span className="text-slate-500 font-medium">Nội Dung Chuyển Khoản:</span>
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-bold text-xs bg-amber-100 text-amber-900 px-2 py-0.5 rounded border border-amber-200">
                      {statement.transferContent}
                    </span>
                    <button
                      onClick={() => copyToClipboard(statement.transferContent, 'content')}
                      className="p-1 text-slate-400 hover:text-slate-700 rounded transition"
                      title="Sao chép nội dung"
                    >
                      {copiedField === 'content' ? (
                        <span className="text-[10px] text-emerald-600 font-bold">Đã chép</span>
                      ) : (
                        <Copy className="w-3.5 h-3.5" />
                      )}
                    </button>
                  </div>
                </div>
              </div>

              {/* Error feedback if any */}
              {errorMsg && (
                <div className="mb-4 p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-xl flex items-center gap-2">
                  <AlertCircle className="w-4 h-4 shrink-0" />
                  <span>{errorMsg}</span>
                </div>
              )}

              {/* Actions */}
              <div className="space-y-2">
                <button
                  onClick={handleSimulateWebhook}
                  disabled={isSimulatingPayment}
                  className="w-full py-3 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-bold rounded-xl shadow-md shadow-emerald-600/20 flex items-center justify-center gap-2 transition disabled:opacity-50 text-sm"
                >
                  {isSimulatingPayment ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin" />
                      Đang xử lý webhook biến động số dư...
                    </>
                  ) : (
                    <>
                      <span>⚡</span>
                      Mô Phỏng Quét App Ngân Hàng Thành Công (Napas247)
                    </>
                  )}
                </button>
                <button
                  onClick={onClose}
                  className="w-full py-2.5 text-xs text-slate-500 hover:text-slate-700 font-semibold transition"
                >
                  Đóng cửa sổ
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
