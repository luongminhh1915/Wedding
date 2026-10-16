import { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { BadgeCheck, Check, Crown, Gem, PiggyBank, ShoppingBag, Sparkles, Star, X } from "lucide-react";
import { apiError, useComboBuilder, usePlannerOverview, useSubmitCombo, type ComboOrder, type ComboTemplate } from "../api";
import { ErrorText, StatusPill } from "../ui";
import { vnd } from "../format";
import { useAuthStore } from "../../../store/authStore";

const TIER_ICON: Record<string, typeof Crown> = { Saving: PiggyBank, Popular: Sparkles, Luxury: Gem };
const FALLBACK_IMG = "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=400&q=60";

/** P5 – Combo builder kiểu kiosk: chọn combo mẫu → đổi từng hạng mục → giỏ dính đáy (giảm giá theo số hạng mục) → gửi 1 lần tới nhiều NCC. */
export default function ComboBuilderPage() {
  const user = useAuthStore((s) => s.user);
  const navigate = useNavigate();
  const { data, isLoading, isError } = useComboBuilder();
  const { data: overview } = usePlannerOverview();
  const [templateId, setTemplateId] = useState<string | null>(null);
  const [picked, setPicked] = useState<Record<string, string>>({}); // categoryId -> listingId
  const [checkout, setCheckout] = useState(false);
  const [done, setDone] = useState<ComboOrder | null>(null);
  const profile = user?.role === "Customer" ? overview?.profile : null;
  const [form, setForm] = useState({ weddingDate: "", guestCount: 0, phoneNumber: user?.phoneNumber && /^\d+$/.test(user.phoneNumber) ? user.phoneNumber : "", note: "" });
  const submit = useSubmitCombo();

  const options = useMemo(() => new Map((data?.categories ?? []).flatMap((c) => c.options.map((o) => [o.listingId, o] as const))), [data]);
  const lines = Object.values(picked).map((id) => options.get(id)!).filter(Boolean);
  const subtotal = lines.reduce((s, o) => s + o.price, 0);
  const tiers = data?.discountTiers ?? [];
  const tier = [...tiers].reverse().find((t) => lines.length >= t.minItems);
  const nextTier = tiers.find((t) => lines.length < t.minItems);
  const discount = Math.round(subtotal * (tier?.percent ?? 0));
  const total = subtotal - discount;
  const budget = profile?.totalBudget ?? 0;

  const applyTemplate = (t: ComboTemplate) => {
    setTemplateId(t.id);
    setPicked(Object.fromEntries(t.items.filter((i) => !i.isOptional).map((i) => [i.categoryId, i.listingId])));
    document.getElementById("combo-items")?.scrollIntoView({ behavior: "smooth" });
  };
  const toggle = (categoryId: string, listingId: string) =>
    setPicked((p) => { const n = { ...p }; if (n[categoryId] === listingId) delete n[categoryId]; else n[categoryId] = listingId; return n; });

  const send = () => {
    if (!user) { navigate("/login", { state: { from: "/combo" } }); return; }
    submit.mutate(
      { templateId, listingIds: lines.map((l) => l.listingId), weddingDate: form.weddingDate || profile?.weddingDate || null, guestCount: form.guestCount || profile?.guestCount || 0, phoneNumber: form.phoneNumber || undefined, note: form.note },
      { onSuccess: (o) => { setDone(o); setCheckout(false); } },
    );
  };

  if (isLoading) return <main className="wd wd-page"><p className="container muted">Đang tải combo...</p></main>;
  if (isError || !data) return <main className="wd wd-page"><p className="container">Không tải được dữ liệu combo.</p></main>;

  if (done)
    return (
      <main className="wd wd-page">
        <div className="container max-w-[760px]">
          <div className="card p-8 text-center wd-fade">
            <BadgeCheck size={44} className="mx-auto text-[var(--gold)]" />
            <h1 className="serif text-3xl font-bold mt-3">Đã gửi combo {done.code}!</h1>
            <p className="muted mt-2">{done.items.length} nhà cung cấp sẽ liên hệ trong 2 giờ làm việc. Mỗi dịch vụ đi kèm 1 mã ưu đãi riêng.</p>
            <ul className="text-left mt-6 grid gap-2">
              {done.items.map((i) => (
                <li key={i.id} className="flex justify-between gap-3 border border-[var(--line)] rounded-xl p-3 text-sm">
                  <span><b>{i.vendorName}</b><br /><span className="muted">{i.title}</span></span>
                  <span className="text-right">{vnd(i.price - i.discountShare)}<br />{i.voucherCode && <span className="pill ok">{i.voucherCode}</span>}</span>
                </li>
              ))}
            </ul>
            <p className="mt-4 font-bold">Tổng dự kiến: {vnd(done.total)} <span className="muted font-normal">(đã giảm {vnd(done.discountAmount)})</span></p>
            <div className="flex justify-center gap-2 mt-6">
              <Link className="btn btn-primary" to="/customer/dashboard?tab=combos">Theo dõi combo</Link>
              <button className="btn btn-outline" onClick={() => { setDone(null); setPicked({}); setTemplateId(null); }}>Tạo combo khác</button>
            </div>
          </div>
        </div>
      </main>
    );

  return (
    <main className="wd wd-page">
      <div className="container">
        <header className="text-center mb-8">
          <p className="eyebrow text-[var(--gold)]">Combo cưới trọn gói</p>
          <h1 className="serif text-3xl md:text-5xl font-bold mt-2">Tự chọn combo – <span className="wd-gradient-text">một lần gửi, nhiều báo giá</span></h1>
          <p className="muted mt-3 max-w-[640px] mx-auto">Chọn combo mẫu rồi đổi từng hạng mục như gọi món. Chọn càng nhiều hạng mục, ưu đãi càng lớn: {tiers.map((t) => `${t.minItems}+ mục giảm ${t.percent * 100}%`).join(" · ")}.</p>
        </header>

        <section aria-label="Combo mẫu" className="grid md:grid-cols-3 gap-4 mb-10">
          {data.templates.map((t) => {
            const Icon = TIER_ICON[t.tier] ?? Crown;
            return (
              <button key={t.id} className={`kiosk-option !p-6 ${templateId === t.id ? "selected" : ""}`} onClick={() => applyTemplate(t)}>
                <div className="flex items-center justify-between">
                  <span className="w-11 h-11 rounded-full grid place-items-center bg-[var(--paper)]"><Icon className="text-[var(--gold)]" size={20} /></span>
                  {t.tier === "Popular" && <span className="badge badge-premium">PHỔ BIẾN NHẤT</span>}
                </div>
                <h2 className="serif text-xl font-bold mt-3">{t.name}</h2>
                <p className="text-sm muted mt-1 min-h-[40px]">{t.description}</p>
                <p className="mt-3 text-xs muted">{t.items.length} hạng mục · từ</p>
                <p className="serif text-2xl font-bold">{vnd(t.estimatedTotal)}</p>
                <span className="btn btn-primary mt-4 w-full">{templateId === t.id ? "Đang dùng combo này" : "Chọn combo này"}</span>
              </button>
            );
          })}
        </section>

        <section id="combo-items" aria-label="Tùy chỉnh hạng mục" className="space-y-8 mb-10">
          {data.categories.map((c) => (
            <div key={c.categoryId}>
              <div className="flex items-baseline justify-between mb-3">
                <h3 className="serif text-xl font-bold">{c.name}</h3>
                {picked[c.categoryId] ? <button className="text-xs underline muted" onClick={() => toggle(c.categoryId, picked[c.categoryId])}>Bỏ hạng mục</button> : <span className="text-xs muted">Chưa chọn</span>}
              </div>
              <div className="wd-scroll-x flex gap-3 pb-2">
                {c.options.map((o) => {
                  const sel = picked[c.categoryId] === o.listingId;
                  return (
                    <button key={o.listingId} className={`kiosk-option w-[250px] shrink-0 ${sel ? "selected" : ""}`} onClick={() => toggle(c.categoryId, o.listingId)} aria-pressed={sel}>
                      <div className="relative">
                        <img src={o.imageUrl || FALLBACK_IMG} alt="" className="h-[130px] w-full object-cover rounded-xl" loading="lazy" />
                        {sel && <span className="absolute top-2 right-2 w-7 h-7 rounded-full grid place-items-center bg-[var(--gold)] text-white"><Check size={15} /></span>}
                      </div>
                      <p className="font-bold text-sm mt-2 line-clamp-2 min-h-[40px]">{o.title}</p>
                      <p className="text-xs muted flex items-center gap-1">{o.vendorName} {o.vendorVerified && <BadgeCheck size={12} className="text-[var(--gold)]" />}</p>
                      <div className="flex justify-between items-center mt-1">
                        <span className="font-bold">{vnd(o.price)}</span>
                        <span className="text-xs flex items-center gap-1"><Star size={11} className="fill-[var(--gold)] text-[var(--gold)]" /> {o.ratingAvg.toFixed(1)}</span>
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </section>

        <div className="kiosk-cart flex flex-wrap items-center justify-between gap-3" role="region" aria-label="Giỏ combo">
          <div className="flex items-center gap-3">
            <span className="w-11 h-11 rounded-full grid place-items-center bg-white/15"><ShoppingBag size={18} /></span>
            <div>
              <p className="text-xs uppercase tracking-widest opacity-80">{lines.length} hạng mục {tier ? `· giảm ${tier.percent * 100}%` : ""}</p>
              <p className="serif text-2xl font-bold">{vnd(total)} {discount > 0 && <span className="text-sm line-through opacity-60 font-normal">{vnd(subtotal)}</span>}</p>
              <p className="text-xs opacity-80">
                {nextTier ? `Thêm ${nextTier.minItems - lines.length} hạng mục để được giảm ${nextTier.percent * 100}%` : "Bạn đang nhận mức ưu đãi cao nhất"}
                {budget > 0 && ` · ${total <= budget ? "Trong" : "Vượt"} ngân sách ${vnd(budget)}`}
              </p>
            </div>
          </div>
          <button className="btn btn-gold" disabled={lines.length === 0} onClick={() => setCheckout(true)}>Gửi combo cho NCC</button>
        </div>
      </div>

      {checkout && (
        <div className="wd-overlay" role="dialog" aria-modal="true" aria-label="Xác nhận combo" onMouseDown={(e) => e.target === e.currentTarget && setCheckout(false)}>
          <div className="wd wd-modal wd-fade max-w-[560px]">
            <button className="wd-x" onClick={() => setCheckout(false)} aria-label="Đóng"><X size={18} /></button>
            <h2 className="serif text-2xl font-bold mb-3">Xác nhận combo</h2>
            <ul className="text-sm divide-y divide-[var(--line)] mb-4">
              {lines.map((l) => <li key={l.listingId} className="py-2 flex justify-between gap-3"><span>{l.title}<br /><span className="muted text-xs">{l.vendorName}</span></span><b>{vnd(l.price)}</b></li>)}
              <li className="py-2 flex justify-between"><span>Ưu đãi combo</span><b>-{vnd(discount)}</b></li>
              <li className="py-2 flex justify-between text-base"><span>Tổng dự kiến</span><b>{vnd(total)}</b></li>
            </ul>
            <div className="grid sm:grid-cols-2 gap-3">
              <label><span className="label">Ngày cưới</span><input className="input" type="date" value={form.weddingDate || profile?.weddingDate?.slice(0, 10) || ""} onChange={(e) => setForm({ ...form, weddingDate: e.target.value })} /></label>
              <label><span className="label">Số khách</span><input className="input" type="number" value={form.guestCount || profile?.guestCount || ""} onChange={(e) => setForm({ ...form, guestCount: +e.target.value })} /></label>
              <label className="sm:col-span-2"><span className="label">SĐT nhận tư vấn (NCC chỉ thấy số đã che)</span><input className="input" value={form.phoneNumber} onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })} placeholder="09xxxxxxxx" /></label>
              <label className="sm:col-span-2"><span className="label">Ghi chú</span><input className="input" value={form.note} onChange={(e) => setForm({ ...form, note: e.target.value })} /></label>
            </div>
            <p className="text-xs muted mt-3">Giá là giá khởi điểm; NCC sẽ gửi báo giá chính thức. Ưu đãi combo được chia theo tỷ lệ giá trị từng dịch vụ.</p>
            <ErrorText>{submit.isError ? apiError(submit.error) : ""}</ErrorText>
            <button className="btn btn-primary w-full mt-4" disabled={submit.isPending} onClick={send}>{user ? (submit.isPending ? "Đang gửi..." : `Gửi tới ${lines.length} nhà cung cấp`) : "Đăng nhập để gửi"}</button>
          </div>
        </div>
      )}
    </main>
  );
}

export function ComboStatusList({ orders }: { orders: ComboOrder[] }) {
  return (
    <ul className="grid gap-3">
      {orders.map((o) => (
        <li key={o.id} className="rounded-2xl border border-[var(--line)] p-4 bg-[var(--paper)]">
          <div className="flex flex-wrap justify-between gap-2">
            <p className="font-bold">{o.code} <StatusPill status={o.status} /></p>
            <p className="text-sm">{vnd(o.total)} <span className="muted text-xs">(-{Math.round(o.discountPercent * 100)}%)</span></p>
          </div>
          <ol className="mt-3 grid sm:grid-cols-2 gap-2">
            {o.items.map((i) => (
              <li key={i.id} className="text-sm flex justify-between gap-2 bg-white rounded-xl border border-[var(--line)] px-3 py-2">
                <span className="truncate"><b>{i.vendorName}</b> · {i.title}</span>
                <StatusPill status={i.status} />
              </li>
            ))}
          </ol>
        </li>
      ))}
    </ul>
  );
}
