import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { ArrowLeft, ArrowRight, CalendarHeart, Sparkles } from "lucide-react";
import { apiError, usePlannerOverview } from "../api";
import { ErrorText } from "../ui";
import { vnd } from "../format";
import { useMembers, useSaveExtendedProfile, type ExtendedProfile } from "./plannerApi";
import { safeReturnPath, useIntentStore } from "../../../store/intentStore";
import { MoneyInput } from "../../../components/MoneyInput";

const CITIES = ["Hồ Chí Minh", "Hà Nội", "Đà Nẵng", "Cần Thơ", "Hải Phòng", "Khác"];
const STYLES = ["Classic", "Minimal", "Garden", "Luxury", "Vintage", "Boho"];
const STYLE_NAMES = ["Cổ điển", "Tối giản", "Sân vườn", "Sang trọng", "Hoài cổ", "Boho"];
const STEPS = ["Vai trò", "Hai bạn", "Ngày cưới", "Ăn hỏi", "Khu vực", "Quy mô", "Ngân sách", "Sự kiện", "Phong cách", "Ưu tiên"];
const BUDGETS = [150_000_000, 300_000_000, 500_000_000, 800_000_000];
const EVENTS = ["Lễ gia tiên", "Lễ ăn hỏi", "Lễ thành hôn", "Tiệc cưới", "Tiệc thân mật"];
const PRIORITIES = ["Ẩm thực", "Ảnh & video", "Trang phục", "Trang trí", "Trải nghiệm khách mời", "Tiết kiệm"];

export default function SetupWizard() {
  const overview = usePlannerOverview();
  const location = useLocation();
  useEffect(() => { const from = roleAwareReturnPath("Customer", safeReturnPath((location.state as { from?: string } | null)?.from)); if (from && from !== "/planner/setup") useIntentStore.getState().remember(from); }, [location.state]);
  const members = useMembers();
  if (overview.isLoading || members.isLoading) return <main className="wd wd-page"><p className="container muted">Đang tải hồ sơ...</p></main>;
  if (overview.isError || members.isError) return <main className="wd wd-page"><div className="container card p-6" role="alert"><h1 className="serif text-2xl">Chưa tải được hồ sơ hiện có</h1><p className="text-sm muted my-3">Không khởi tạo hồ sơ trống khi dữ liệu chưa tải thành công.</p><button className="btn btn-primary" onClick={() => { void overview.refetch(); void members.refetch(); }}>Thử lại</button></div></main>;
  return <WizardForm key={overview.data?.profile?.id ?? "new"} profile={overview.data?.profile ?? null} viewer={members.data?.myRole === "Viewer"} />;
}
function WizardForm({ profile: p, viewer }: { profile: ExtendedProfile | null; viewer: boolean }) {
  const navigate = useNavigate();
  const save = useSaveExtendedProfile();
  const [step, setStep] = useState(0);
  const [f, setF] = useState(() => ({
    planningRole: p?.planningRole || "Couple", groomName: p?.groomName ?? "", brideName: p?.brideName ?? "",
    weddingDate: p?.weddingDate?.slice(0, 10) ?? "", engagementDate: p?.engagementDate?.slice(0, 10) ?? "",
    city: p?.city || "Hồ Chí Minh", guestCount: p?.guestCount ?? 300, totalBudget: p?.totalBudget ?? 300_000_000,
    style: p?.style || "Classic", partnerEmail: p?.partnerEmail ?? "", events: p?.events ?? ["Lễ gia tiên", "Tiệc cưới"], priorities: p?.priorities ?? [],
  }));
  const set = <K extends keyof typeof f>(k: K, value: (typeof f)[K]) => setF(x => ({ ...x, [k]: value }));
  const toggle = (key: "events" | "priorities", value: string) => set(key, f[key].includes(value) ? f[key].filter(x => x !== value) : [...f[key], value]);
  const canNext = step === 1 ? !!(f.groomName.trim() && f.brideName.trim()) : step === 4 ? !!f.city.trim() : step === 5 ? f.guestCount > 0 : step === 6 ? f.totalBudget > 0 : step === 7 ? f.events.length > 0 : true;
  const submit = () => save.mutate({ ...f, weddingDate: f.weddingDate || null, engagementDate: f.engagementDate || null, partnerEmail: f.partnerEmail || null, generatePlan: true },
    { onSuccess: () => navigate("/customer/dashboard?welcome=1", { replace: true }) });
  return <main className="wd wd-page"><div className="container max-w-[760px]">
    <div className="text-center mb-7"><p className="eyebrow">Lập kế hoạch cưới · Bước {step + 1}/10</p><h1 className="serif text-3xl md:text-4xl font-bold mt-2">Ngày vui, bắt đầu từ hai bạn</h1><p className="muted mt-2 text-sm">10 bước ngắn để tạo 35 công việc và 14 khoản ngân sách. Không ghi đè những gì bạn đã tự chỉnh.</p></div>
    <ol className="flex flex-wrap justify-center gap-2 mb-6" aria-label="Các bước">{STEPS.map((s, i) => <li key={s}><button type="button" className={`chip ${i === step ? "active" : ""}`} onClick={() => i < step && setStep(i)} aria-current={i === step ? "step" : undefined}>{i + 1}<span className="hidden lg:inline">. {s}</span></button></li>)}</ol>
    {viewer && <p role="alert" className="mb-4">Bạn có quyền xem. Hãy nhờ chủ hồ sơ cấp quyền chỉnh sửa.</p>}
    <fieldset disabled={viewer || save.isPending} className="card p-6 md:p-8 space-y-5 disabled:opacity-60">
      <legend className="sr-only">{STEPS[step]}</legend><h2 className="serif text-2xl">{STEPS[step]}</h2>
      {step === 0 && <div className="grid sm:grid-cols-3 gap-3">{[["Bride", "Mình là cô dâu"], ["Groom", "Mình là chú rể"], ["Couple", "Cùng lập kế hoạch"]].map(([id, label]) => <button type="button" key={id} className={`kiosk-option ${f.planningRole === id ? "selected" : ""}`} onClick={() => set("planningRole", id)}>{label}</button>)}</div>}
      {step === 1 && <div className="grid sm:grid-cols-2 gap-4"><label><span className="label">Tên chú rể</span><input maxLength={100} className="input" value={f.groomName} onChange={e => set("groomName", e.target.value)} /></label><label><span className="label">Tên cô dâu</span><input maxLength={100} className="input" value={f.brideName} onChange={e => set("brideName", e.target.value)} /></label><p className="sm:col-span-2 text-sm muted">Sau khi lưu, bạn có thể mời người ấy với quyền chỉnh sửa hoặc chỉ xem.</p></div>}
      {step === 2 && <label className="block"><span className="label">Ngày cưới dự kiến (có thể thêm sau)</span><input className="input" type="date" min={new Date().toISOString().slice(0, 10)} value={f.weddingDate} onChange={e => set("weddingDate", e.target.value)} /></label>}
      {step === 3 && <label className="block"><span className="label">Ngày ăn hỏi (tùy chọn)</span><input className="input" type="date" value={f.engagementDate} onChange={e => set("engagementDate", e.target.value)} /></label>}
      {step === 4 && <><span className="label">Thành phố tổ chức</span><div className="flex flex-wrap gap-2">{CITIES.map(c => <button key={c} type="button" className={`chip ${f.city === c ? "active" : ""}`} onClick={() => set("city", c)}>{c}</button>)}</div><label className="block"><span className="label">Khu vực cụ thể</span><input maxLength={200} className="input" value={f.city} onChange={e => set("city", e.target.value)} /></label></>}
      {step === 5 && <label className="block"><span className="label">Số khách dự kiến</span><input className="input" type="number" min={1} max={100000} value={f.guestCount} onChange={e => set("guestCount", +e.target.value)} /><p className="muted text-xs mt-2">Bạn sẽ quản lý từng nhóm khách và sơ đồ bàn trong Planner.</p></label>}
      {step === 6 && <><div className="flex flex-wrap gap-2">{BUDGETS.map(b => <button type="button" key={b} className={`chip ${f.totalBudget === b ? "active" : ""}`} onClick={() => set("totalBudget", b)}>{vnd(b)}</button>)}</div><label className="block"><span className="label">Tổng ngân sách</span><MoneyInput className="input" value={f.totalBudget} onValueChange={value => set("totalBudget", value ?? 0)} /></label><p className="muted text-xs">≈ {vnd(f.guestCount ? f.totalBudget / f.guestCount : 0)}/khách. Chỉ là gợi ý, bạn có thể điều chỉnh từng khoản.</p></>}
      {step === 7 && <div className="flex flex-wrap gap-3">{EVENTS.map(e => <button type="button" key={e} className={`chip ${f.events.includes(e) ? "active" : ""}`} aria-pressed={f.events.includes(e)} onClick={() => toggle("events", e)}>{e}</button>)}</div>}
      {step === 8 && <div className="grid sm:grid-cols-2 gap-3">{STYLES.map((s, i) => <button type="button" key={s} className={`kiosk-option ${f.style === s ? "selected" : ""}`} onClick={() => set("style", s)}>{STYLE_NAMES[i]}</button>)}</div>}
      {step === 9 && <><p className="text-sm muted">Điều gì quan trọng nhất với hai bạn?</p><div className="flex flex-wrap gap-3">{PRIORITIES.map(e => <button type="button" key={e} className={`chip ${f.priorities.includes(e) ? "active" : ""}`} aria-pressed={f.priorities.includes(e)} onClick={() => toggle("priorities", e)}>{e}</button>)}</div><div className="p-4 bg-[var(--bg)] rounded-xl text-sm">{f.groomName} & {f.brideName} · {f.city}<br />{f.guestCount} khách · {vnd(f.totalBudget)}<br />35 công việc · 14 hạng mục ngân sách</div></>}
      <ErrorText>{save.isError ? apiError(save.error) : ""}</ErrorText>
      <div className="flex justify-between pt-3"><button type="button" className="btn btn-ghost" disabled={step === 0} onClick={() => setStep(step - 1)}><ArrowLeft size={14} /> Quay lại</button>{step < 9 ? <button type="button" className="btn btn-primary" disabled={!canNext} onClick={() => setStep(step + 1)}>Tiếp tục <ArrowRight size={14} /></button> : <button type="button" className="btn btn-primary" disabled={save.isPending || !f.groomName.trim() || !f.brideName.trim()} onClick={submit}><Sparkles size={14} />{save.isPending ? "Đang lưu..." : "Tạo kế hoạch"}</button>}</div>
    </fieldset><p className="text-center text-xs muted mt-4 flex items-center justify-center gap-1"><CalendarHeart size={14} />Hoàn tất 10 bước để mở Tổng quan kế hoạch cưới của hai bạn.</p>
  </div></main>;
}
import { roleAwareReturnPath } from "../../auth/authNavigation";
