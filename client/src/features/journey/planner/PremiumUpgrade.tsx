import { useState } from "react";
import { Crown, QrCode } from "lucide-react";
import { apiError, premiumApi, type PremiumInfo } from "../api";
import { ErrorText } from "../ui";
import { vnd } from "../format";

/** P2 – Nâng cấp thiệp Premium 130.000đ: hiển thị VietQR, xác nhận thanh toán (mock webhook). */
export function PremiumUpgrade({ isPremium }: { isPremium?: boolean }) {
  const [info, setInfo] = useState<PremiumInfo | null>(null);
  const [done, setDone] = useState(!!isPremium);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const run = async (fn: () => Promise<void>) => { setBusy(true); setError(""); try { await fn(); } catch (e) { setError(apiError(e)); } finally { setBusy(false); } };

  if (done)
    return (
      <div className="wd card p-5 mb-6 flex items-center gap-3">
        <span className="badge badge-premium"><Crown size={12} /> PREMIUM</span>
        <p className="text-sm">Thiệp của hai bạn đã mở khóa Premium: không quảng cáo, nhạc nền, album không giới hạn, tên miền đẹp.</p>
      </div>
    );
  return (
    <div className="wd card p-6 mb-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="max-w-[520px]">
          <span className="badge badge-premium"><Crown size={12} /> PREMIUM</span>
          <h3 className="serif text-xl font-bold mt-2">Thiệp cưới Premium – {vnd(130000)} (một lần)</h3>
          <ul className="text-sm muted mt-2 list-disc pl-5 space-y-1">
            <li>Tất cả mẫu thiệp cao cấp, đổi mẫu không giới hạn</li>
            <li>Nhạc nền, album ảnh, sổ mừng cưới & QR mừng cưới</li>
            <li>Ẩn logo Wedding Match, link chia sẻ đẹp</li>
          </ul>
        </div>
        {!info ? (
          <button className="btn btn-gold" disabled={busy} onClick={() => run(async () => setInfo(await premiumApi.quote()))}><QrCode size={14} /> Nâng cấp</button>
        ) : (
          <div className="text-center">
            <img src={info.qrUrl} alt={`VietQR thanh toán ${vnd(info.price)}`} width={180} height={180} className="rounded-xl border border-[var(--line)] mx-auto" />
            <p className="text-xs muted mt-2">Nội dung CK: <b>{info.paymentRef}</b></p>
            <button className="btn btn-primary mt-3" disabled={busy} onClick={() => run(async () => { const r = await premiumApi.confirm(); setDone(r.isPremium); })}>
              Tôi đã chuyển khoản
            </button>
            <p className="text-[11px] muted mt-1">Môi trường demo: xác nhận giả lập thay cho webhook ngân hàng.</p>
          </div>
        )}
      </div>
      <ErrorText>{error}</ErrorText>
    </div>
  );
}
