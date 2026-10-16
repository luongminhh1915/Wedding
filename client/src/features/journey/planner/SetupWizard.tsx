import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowLeft, ArrowRight, CalendarHeart, Sparkles } from "lucide-react";
import { apiError, usePlannerOverview, useSaveProfile, type WeddingProfile } from "../api";
import { ErrorText } from "../ui";
import { vnd } from "../format";

const CITIES = ["Hồ Chí Minh", "Hà Nội", "Đà Nẵng", "Cần Thơ", "Hải Phòng", "Khác"];
const STYLES = [
  { id: "Classic", label: "Cổ điển", desc: "Trang nhã, tông trắng – vàng" },
  { id: "Minimal", label: "Tối giản", desc: "Ấm cúng, tinh gọn" },
  { id: "Garden", label: "Sân vườn", desc: "Ngoài trời, nhiều hoa" },
  { id: "Luxury", label: "Sang trọng", desc: "Sảnh lớn, dịch vụ cao cấp" },
];
const BUDGETS = [150_000_000, 300_000_000, 500_000_000, 800_000_000];

/** P1 – Wizard 4 bước tạo hồ sơ cưới; backend sinh checklist 12 tháng + ngân sách mẫu. */
export default function SetupWizard() {
  const { data, isLoading } = usePlannerOverview();
  if (isLoading) return <main className="wd wd-page"><p className="container muted">Đang tải hồ sơ...</p></main>;
  return <WizardForm profile={data?.profile ?? null} />;
}

function WizardForm({ profile: p }: { profile: WeddingProfile | null }) {
  const navigate = useNavigate();
  const save = useSaveProfile();
  const [step, setStep] = useState(0);
  const [f, setF] = useState(() => ({
    groomName: p?.groomName ?? "", brideName: p?.brideName ?? "", weddingDate: p?.weddingDate?.slice(0, 10) ?? "",
    city: p?.city || "Hồ Chí Minh", guestCount: p?.guestCount || 300, totalBudget: p?.totalBudget || 300_000_000,
    style: p?.style || "Classic", partnerEmail: p?.partnerEmail ?? "",
  }));
  const set = <K extends keyof typeof f>(k: K, v: (typeof f)[K]) => setF((x) => ({ ...x, [k]: v }));
  const steps = ["Hai bạn", "Ngày & nơi", "Quy mô & ngân sách", "Phong cách"];
  const canNext = step === 0 ? f.groomName.trim() && f.brideName.trim() : step === 1 ? !!f.city : true;

  const submit = () =>
    save.mutate(
      { ...f, weddingDate: f.weddingDate || null, partnerEmail: f.partnerEmail || null, generatePlan: true },
      { onSuccess: () => navigate("/customer/dashboard?welcome=1") },
    );

  return (
    <main className="wd wd-page">
      <div className="container max-w-[720px]">
        <div className="text-center mb-8">
          <p className="eyebrow text-[var(--gold)]">Lập kế hoạch cưới</p>
          <h1 className="serif text-3xl md:text-4xl font-bold mt-2">Kể cho Wedding Match nghe về <span className="wd-gradient-text">ngày của hai bạn</span></h1>
          <p className="muted mt-2 text-sm">Chỉ 1 phút – chúng mình sẽ tạo checklist 12 tháng và bảng ngân sách gợi ý.</p>
        </div>
        <ol className="flex items-center justify-center gap-2 mb-6" aria-label="Các bước">
          {steps.map((s, i) => (
            <li key={s} className="flex items-center gap-2 text-xs font-bold">
              <span className={`step-dot ${i <= step ? "on" : ""}`}>{i + 1}</span>
              <span className="hidden sm:inline">{s}</span>
              {i < steps.length - 1 && <span className="w-6 h-px bg-[var(--line)]" />}
            </li>
          ))}
        </ol>
        <div className="card p-6 md:p-8 space-y-5">
          {step === 0 && (
            <div className="grid sm:grid-cols-2 gap-4">
              <label><span className="label">Tên chú rể</span><input className="input" value={f.groomName} onChange={(e) => set("groomName", e.target.value)} /></label>
              <label><span className="label">Tên cô dâu</span><input className="input" value={f.brideName} onChange={(e) => set("brideName", e.target.value)} /></label>
              <label className="sm:col-span-2"><span className="label">Email người ấy (tùy chọn – cùng lập kế hoạch)</span>
                <input className="input" type="email" value={f.partnerEmail} onChange={(e) => set("partnerEmail", e.target.value)} /></label>
            </div>
          )}
          {step === 1 && (
            <div className="space-y-4">
              <label className="block"><span className="label">Ngày cưới dự kiến</span>
                <input className="input" type="date" min={new Date().toISOString().slice(0, 10)} value={f.weddingDate} onChange={(e) => set("weddingDate", e.target.value)} /></label>
              <div><span className="label">Thành phố tổ chức</span>
                <div className="flex flex-wrap gap-2">{CITIES.map((c) => <button key={c} type="button" className={`chip ${f.city === c ? "active" : ""}`} onClick={() => set("city", c)}>{c}</button>)}</div>
              </div>
            </div>
          )}
          {step === 2 && (
            <div className="space-y-5">
              <label className="block"><span className="label">Số khách mời dự kiến: {f.guestCount}</span>
                <input type="range" min={50} max={1000} step={10} value={f.guestCount} onChange={(e) => set("guestCount", +e.target.value)} className="w-full accent-[var(--gold)]" /></label>
              <div><span className="label">Tổng ngân sách</span>
                <div className="flex flex-wrap gap-2 mb-2">{BUDGETS.map((b) => <button key={b} type="button" className={`chip ${f.totalBudget === b ? "active" : ""}`} onClick={() => set("totalBudget", b)}>{vnd(b)}</button>)}</div>
                <input className="input" type="number" min={0} step={1_000_000} value={f.totalBudget} onChange={(e) => set("totalBudget", +e.target.value)} aria-label="Ngân sách tùy chỉnh" />
                <p className="text-xs muted mt-1">≈ {vnd(f.guestCount ? f.totalBudget / f.guestCount : 0)}/khách</p>
              </div>
            </div>
          )}
          {step === 3 && (
            <div className="grid sm:grid-cols-2 gap-3">
              {STYLES.map((s) => (
                <button key={s.id} type="button" className={`kiosk-option ${f.style === s.id ? "selected" : ""}`} onClick={() => set("style", s.id)}>
                  <p className="serif font-bold text-lg">{s.label}</p><p className="text-xs muted">{s.desc}</p>
                </button>
              ))}
            </div>
          )}
          <ErrorText>{save.isError ? apiError(save.error) : ""}</ErrorText>
          <div className="flex justify-between pt-2">
            <button className="btn btn-ghost" disabled={step === 0} onClick={() => setStep(step - 1)}><ArrowLeft size={14} /> Quay lại</button>
            {step < 3 ? (
              <button className="btn btn-primary" disabled={!canNext} onClick={() => setStep(step + 1)}>Tiếp tục <ArrowRight size={14} /></button>
            ) : (
              <button className="btn btn-gold" disabled={save.isPending} onClick={submit}><Sparkles size={14} /> {save.isPending ? "Đang tạo..." : "Tạo kế hoạch"}</button>
            )}
          </div>
        </div>
        <p className="text-center text-xs muted mt-4 flex items-center justify-center gap-1"><CalendarHeart size={14} /> Bạn có thể chỉnh sửa lại bất cứ lúc nào trong Planner.</p>
      </div>
    </main>
  );
}
