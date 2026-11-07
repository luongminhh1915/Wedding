import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { ArrowLeft, ArrowRight, Check } from "lucide-react";
import { apiError, usePlannerOverview } from "../api";
import { ErrorText } from "../ui";
import { vnd } from "../format";
import { useMembers, useSaveExtendedProfile, type ExtendedProfile } from "./plannerApi";
import { safeReturnPath, useIntentStore } from "../../../store/intentStore";
import { roleAwareReturnPath } from "../../auth/authNavigation";
import { MoneyInput } from "../../../components/MoneyInput";
import { VIETNAM_PROVINCES } from "../../../data/vietnamProvinces";

interface WeddingStylePreset {
  id: string;
  name: string;
  desc: string;
  image: string;
}

const STYLE_PRESETS: WeddingStylePreset[] = [
  {
    id: "Classic",
    name: "Cổ điển Hoàng gia",
    desc: "Sang trọng, chuẩn mực, trường tồn với thời gian",
    image: "https://images.unsplash.com/photo-1519741497674-611481863552?auto=format&fit=crop&w=600&q=80",
  },
  {
    id: "Minimal",
    name: "Tối giản Tinh tế",
    desc: "Hiện đại, thanh thoát, chú trọng cảm xúc chân thật",
    image: "https://images.unsplash.com/photo-1511285560929-80b456fea0bc?auto=format&fit=crop&w=600&q=80",
  },
  {
    id: "Garden",
    name: "Sân vườn Cổ tích",
    desc: "Gần gũi thiên nhiên, ngập tràn hoa lá tươi mát ngoài trời",
    image: "https://images.unsplash.com/photo-1465495976277-4387d4b0b4c6?auto=format&fit=crop&w=600&q=80",
  },
  {
    id: "Luxury",
    name: "Lộng lẫy Glamour",
    desc: "Pha lê rực rỡ, ánh sáng huyền ảo, đẳng cấp ấn tượng",
    image: "https://images.unsplash.com/photo-1519167758481-83f550bb49b3?auto=format&fit=crop&w=600&q=80",
  },
  {
    id: "Vintage",
    name: "Hoài cổ Ấm áp",
    desc: "Hoài niệm mộc mạc, tông màu gỗ ấm và ký ức thanh xuân",
    image: "https://images.unsplash.com/photo-1583939003579-730e3918a45a?auto=format&fit=crop&w=600&q=80",
  },
  {
    id: "Boho",
    name: "Boho Tự do",
    desc: "Phóng khoáng, nghệ thuật, hoa cỏ lau và phong vị du mục",
    image: "https://images.unsplash.com/photo-1520854221256-17451cc331bf?auto=format&fit=crop&w=600&q=80",
  },
];

const POPULAR_PROVINCES = ["Hà Nội", "Hồ Chí Minh", "Đà Nẵng", "Cần Thơ", "Hải Phòng"];
const GUEST_CHIPS = [100, 200, 300, 500];
const BUDGET_CHIPS = [150_000_000, 300_000_000, 500_000_000, 800_000_000];
const EVENTS = ["Lễ gia tiên", "Lễ ăn hỏi", "Lễ thành hôn", "Tiệc cưới", "Tiệc thân mật (After-party)"];
const PRIORITIES = ["Ẩm thực trọn vị", "Ảnh & phóng sự cưới", "Váy cưới & vest", "Trang trí không gian", "Trải nghiệm khách mời", "Tối ưu ngân sách"];

export default function SetupWizard() {
  const overview = usePlannerOverview();
  const location = useLocation();

  useEffect(() => {
    const from = roleAwareReturnPath(
      "Customer",
      safeReturnPath((location.state as { from?: string } | null)?.from)
    );
    if (from && from !== "/planner/setup") {
      useIntentStore.getState().remember(from);
    }
  }, [location.state]);

  const members = useMembers();

  if (overview.isLoading || members.isLoading) {
    return (
      <main className="wd wd-page min-h-[70vh] flex items-center justify-center">
        <p className="text-sm font-medium text-slate-500">Đang chuẩn bị không gian cho hai bạn...</p>
      </main>
    );
  }

  if (overview.isError || members.isError) {
    return (
      <main className="wd wd-page">
        <div className="container max-w-lg mx-auto card p-8 text-center space-y-4" role="alert">
          <h1 className="serif text-2xl text-slate-900">Chưa tải được hồ sơ cưới</h1>
          <p className="text-sm text-slate-500">
            Hệ thống đang bảo toàn dữ liệu hiện có. Bạn vui lòng thử lại để tiếp tục nhé.
          </p>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => {
              void overview.refetch();
              void members.refetch();
            }}
          >
            Tải lại hồ sơ
          </button>
        </div>
      </main>
    );
  }

  return (
    <WizardForm
      key={overview.data?.profile?.id ?? "new"}
      profile={overview.data?.profile ?? null}
      viewer={members.data?.myRole === "Viewer"}
    />
  );
}

function WizardForm({
  profile: p,
  viewer,
}: {
  profile: ExtendedProfile | null;
  viewer: boolean;
}) {
  const navigate = useNavigate();
  const save = useSaveExtendedProfile();
  const [milestone, setMilestone] = useState(0);

  const [f, setF] = useState(() => ({
    planningRole: p?.planningRole || "Couple",
    groomName: p?.groomName ?? "",
    brideName: p?.brideName ?? "",
    weddingDate: p?.weddingDate?.slice(0, 10) ?? "",
    engagementDate: p?.engagementDate?.slice(0, 10) ?? "",
    city: p?.city || "Hồ Chí Minh",
    guestCount: p?.guestCount ?? 300,
    totalBudget: p?.totalBudget ?? 300_000_000,
    style: p?.style || "Classic",
    partnerEmail: p?.partnerEmail ?? "",
    events: p?.events ?? ["Lễ gia tiên", "Tiệc cưới"],
    priorities: p?.priorities ?? ["Ảnh & phóng sự cưới", "Trang trí không gian"],
  }));

  const set = <K extends keyof typeof f>(k: K, value: (typeof f)[K]) =>
    setF((prev) => ({ ...prev, [k]: value }));

  const toggle = (key: "events" | "priorities", value: string) =>
    setF((prev) => ({
      ...prev,
      [key]: prev[key].includes(value)
        ? prev[key].filter((x) => x !== value)
        : [...prev[key], value],
    }));

  const canNext =
    milestone === 0
      ? !!(f.groomName.trim() && f.brideName.trim())
      : milestone === 1
      ? !!f.city.trim()
      : milestone === 2
      ? f.guestCount > 0 && f.totalBudget > 0
      : true;

  const submit = () => {
    save.mutate(
      {
        ...f,
        weddingDate: f.weddingDate || null,
        engagementDate: f.engagementDate || null,
        partnerEmail: f.partnerEmail || null,
        generatePlan: true,
      },
      {
        onSuccess: () => navigate("/customer/dashboard?welcome=1", { replace: true }),
      }
    );
  };

  const costPerGuest = f.guestCount > 0 ? Math.round(f.totalBudget / f.guestCount) : 0;

  return (
    <main className="min-h-screen bg-[#faf8f5] py-8 sm:py-12 px-4 sm:px-6">
      <div className="max-w-2xl mx-auto">
        {/* Header Hero */}
        <div className="text-center mb-8">
          <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2">
            Lập kế hoạch cưới · Bước {milestone + 1}/4
          </p>
          <h1
            className="text-3xl sm:text-4xl font-bold text-slate-900 leading-tight"
            style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
          >
            Ngày vui trọn vẹn, bắt đầu từ hai bạn
          </h1>
          <p className="mt-2 text-sm text-slate-600 max-w-lg mx-auto leading-relaxed">
            Điền nhanh thông tin cơ bản để tạo 35 công việc chu đáo và 14 mục ngân sách riêng cho hai bạn.
          </p>
        </div>

        {viewer && (
          <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 text-amber-800 text-xs font-medium mb-6">
            Bạn hiện có quyền xem kế hoạch. Để chỉnh sửa thông tin, hãy liên hệ chủ tài khoản cấp quyền.
          </div>
        )}

        {/* Form Container */}
        <fieldset
          disabled={viewer || save.isPending}
          className="bg-white rounded-3xl p-6 sm:p-9 border border-slate-200/80 shadow-sm space-y-6 disabled:opacity-60"
        >
          {/* Milestone 0: Tên hai bạn & vai trò */}
          {milestone === 0 && (
            <div className="space-y-6">
              <div className="border-b border-slate-100 pb-3">
                <h2
                  className="text-2xl font-bold text-slate-900"
                  style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                >
                  Cho chúng mình xin tên 2 bạn nhé
                </h2>
                <p className="text-xs text-slate-500 mt-1">
                  Để Wedding Match chuẩn bị lộ trình và không gian cưới riêng cho hai bạn.
                </p>
              </div>

              {/* Groom and Bride Name */}
              <div className="grid sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Tên Chú Rể <span className="text-rose-500">*</span>
                  </label>
                  <input
                    maxLength={100}
                    placeholder="VD: Minh Nhật"
                    className="w-full px-4 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400"
                    value={f.groomName}
                    onChange={(e) => set("groomName", e.target.value)}
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Tên Cô Dâu <span className="text-rose-500">*</span>
                  </label>
                  <input
                    maxLength={100}
                    placeholder="VD: Khánh Linh"
                    className="w-full px-4 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400"
                    value={f.brideName}
                    onChange={(e) => set("brideName", e.target.value)}
                  />
                </div>
              </div>

              {/* Role selection */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-2">
                  Vai trò của bạn:
                </label>
                <div className="grid grid-cols-3 gap-3">
                  {[
                    ["Bride", "Mình là cô dâu", "Chuẩn bị váy cưới, makeup và hoa tươi"],
                    ["Groom", "Mình là chú rể", "Chu toàn thiệp cưới, vest và tiệc mừng"],
                    ["Couple", "Cùng lên kế hoạch", "Hai chúng mình cùng chia sẻ mọi công việc"],
                  ].map(([id, label, hint]) => (
                    <button
                      type="button"
                      key={id}
                      onClick={() => set("planningRole", id)}
                      className={`p-3.5 rounded-2xl text-left border transition cursor-pointer ${
                        f.planningRole === id
                          ? "bg-rose-50/60 border-rose-400 ring-2 ring-rose-400/20 shadow-xs"
                          : "bg-white border-slate-200 hover:border-slate-300"
                      }`}
                    >
                      <p className="text-xs font-bold text-slate-900">{label}</p>
                      <p className="text-[10px] text-slate-500 mt-1 line-clamp-2">{hint}</p>
                    </button>
                  ))}
                </div>
              </div>

              {/* Partner Email */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-1">
                  Email người ấy (Tùy chọn — gửi lời mời cùng chuẩn bị)
                </label>
                <input
                  type="email"
                  placeholder="VD: nguoiay@gmail.com"
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400"
                  value={f.partnerEmail}
                  onChange={(e) => set("partnerEmail", e.target.value)}
                />
              </div>
            </div>
          )}

          {/* Milestone 1: Thời gian & Địa điểm */}
          {milestone === 1 && (
            <div className="space-y-6">
              <div className="border-b border-slate-100 pb-3">
                <h2
                  className="text-2xl font-bold text-slate-900"
                  style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                >
                  Thời gian &amp; nơi tổ chức ngày vui
                </h2>
                <p className="text-xs text-slate-500 mt-1">
                  Dự kiến ngày trọng đại để hệ thống nhắc việc đúng thời điểm cần thiết.
                </p>
              </div>

              {/* Dates */}
              <div className="grid sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Ngày cưới chính thức (Có thể đổi sau)
                  </label>
                  <input
                    type="date"
                    min={new Date().toISOString().slice(0, 10)}
                    value={f.weddingDate}
                    onChange={(e) => set("weddingDate", e.target.value)}
                    className="w-full px-4 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Ngày ăn hỏi / Đính hôn (Tùy chọn)
                  </label>
                  <input
                    type="date"
                    value={f.engagementDate}
                    onChange={(e) => set("engagementDate", e.target.value)}
                    className="w-full px-4 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400"
                  />
                </div>
              </div>

              {/* Location */}
              <div className="space-y-3">
                <label className="block text-xs font-bold text-slate-700">
                  Tỉnh / Thành phố tổ chức chính <span className="text-rose-500">*</span>
                </label>

                {/* Quick Province Chips */}
                <div className="flex flex-wrap gap-2">
                  {POPULAR_PROVINCES.map((prov) => (
                    <button
                      key={prov}
                      type="button"
                      onClick={() => set("city", prov)}
                      className={`px-3 py-1.5 rounded-xl text-xs font-semibold border transition cursor-pointer ${
                        f.city === prov
                          ? "bg-slate-900 text-white border-slate-900 shadow-xs"
                          : "bg-slate-50 text-slate-700 border-slate-200 hover:border-slate-300"
                      }`}
                    >
                      {prov}
                    </button>
                  ))}
                </div>

                {/* Dropdown 34 provinces */}
                <div className="grid sm:grid-cols-2 gap-3 pt-1">
                  <div>
                    <span className="block text-[11px] text-slate-500 mb-1">Chọn từ danh mục tỉnh thành:</span>
                    <select
                      value={VIETNAM_PROVINCES.includes(f.city as (typeof VIETNAM_PROVINCES)[number]) ? f.city : ""}
                      onChange={(e) => e.target.value && set("city", e.target.value)}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400"
                    >
                      <option value="">-- Chọn tỉnh / thành phố --</option>
                      {VIETNAM_PROVINCES.map((p) => (
                        <option key={p} value={p}>
                          {p}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <span className="block text-[11px] text-slate-500 mb-1">Hoặc nhập khu vực cụ thể:</span>
                    <input
                      maxLength={200}
                      placeholder="VD: Quận 1, TP. Hồ Chí Minh"
                      value={f.city}
                      onChange={(e) => set("city", e.target.value)}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400"
                    />
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Milestone 2: Quy mô & Ngân sách */}
          {milestone === 2 && (
            <div className="space-y-6">
              <div className="border-b border-slate-100 pb-3">
                <h2
                  className="text-2xl font-bold text-slate-900"
                  style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                >
                  Quy mô khách mời &amp; ngân sách dự trù
                </h2>
                <p className="text-xs text-slate-500 mt-1">
                  Wedding Match sẽ phân bổ 14 khoản chi tiêu mẫu tương ứng với con số này.
                </p>
              </div>

              {/* Guest Count */}
              <div className="space-y-2">
                <div className="flex justify-between items-center">
                  <label className="text-xs font-bold text-slate-700">
                    Số khách mời dự kiến
                  </label>
                  <span className="text-xs text-slate-500 font-semibold">
                    ≈ {Math.ceil(f.guestCount / 10)} bàn tiệc (10 người/bàn)
                  </span>
                </div>

                <div className="flex flex-wrap gap-2 mb-2">
                  {GUEST_CHIPS.map((cnt) => (
                    <button
                      key={cnt}
                      type="button"
                      onClick={() => set("guestCount", cnt)}
                      className={`px-3 py-1.5 rounded-xl text-xs font-semibold border transition cursor-pointer ${
                        f.guestCount === cnt
                          ? "bg-slate-900 text-white border-slate-900"
                          : "bg-slate-50 text-slate-700 border-slate-200 hover:border-slate-300"
                      }`}
                    >
                      {cnt} khách
                    </button>
                  ))}
                </div>

                <input
                  type="number"
                  min={1}
                  max={100000}
                  value={f.guestCount}
                  onChange={(e) => set("guestCount", Math.max(1, parseInt(e.target.value) || 1))}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 text-sm focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400 font-bold text-slate-800"
                />
              </div>

              {/* Total Budget */}
              <div className="space-y-2">
                <div className="flex justify-between items-center">
                  <label className="text-xs font-bold text-slate-700">
                    Tổng ngân sách cưới dự kiến
                  </label>
                  {costPerGuest > 0 && (
                    <span className="text-xs text-rose-600 font-bold">
                      ≈ {vnd(costPerGuest)} / khách
                    </span>
                  )}
                </div>

                <div className="flex flex-wrap gap-2 mb-2">
                  {BUDGET_CHIPS.map((b) => (
                    <button
                      key={b}
                      type="button"
                      onClick={() => set("totalBudget", b)}
                      className={`px-3 py-1.5 rounded-xl text-xs font-semibold border transition cursor-pointer ${
                        f.totalBudget === b
                          ? "bg-slate-900 text-white border-slate-900"
                          : "bg-slate-50 text-slate-700 border-slate-200 hover:border-slate-300"
                      }`}
                    >
                      {vnd(b)}
                    </button>
                  ))}
                </div>

                <MoneyInput
                  value={f.totalBudget}
                  onValueChange={(val) => set("totalBudget", val ?? 0)}
                  className="w-full px-4 py-2.5 rounded-xl border border-slate-200 text-base font-bold text-rose-600 focus:outline-none focus:ring-2 focus:ring-rose-400/20 focus:border-rose-400"
                />
              </div>
            </div>
          )}

          {/* Milestone 3: Phong cách cưới & Ưu tiên */}
          {milestone === 3 && (
            <div className="space-y-6">
              <div className="border-b border-slate-100 pb-3">
                <h2
                  className="text-2xl font-bold text-slate-900"
                  style={{ fontFamily: "'Playfair Display', Georgia, serif" }}
                >
                  Phong cách cưới &amp; ưu tiên của hai bạn
                </h2>
                <p className="text-xs text-slate-500 mt-1">
                  Chọn phong cách chủ đạo và các sự kiện dự kiến tổ chức.
                </p>
              </div>

              {/* Style Presets */}
              <div className="space-y-3">
                <label className="block text-xs font-bold text-slate-700">
                  Phong cách chủ đạo:
                </label>
                <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                  {STYLE_PRESETS.map((preset) => {
                    const isSelected = f.style === preset.id;
                    return (
                      <button
                        type="button"
                        key={preset.id}
                        onClick={() => set("style", preset.id)}
                        className={`group relative overflow-hidden rounded-2xl text-left border transition cursor-pointer flex flex-col ${
                          isSelected
                            ? "border-rose-500 ring-2 ring-rose-400/30 shadow-xs"
                            : "border-slate-200/90 hover:border-slate-300 bg-white"
                        }`}
                      >
                        <div className="relative h-24 w-full overflow-hidden bg-slate-100">
                          <img
                            src={preset.image}
                            alt={preset.name}
                            className="w-full h-full object-cover"
                            loading="lazy"
                          />
                          <div className="absolute inset-0 bg-gradient-to-t from-black/60 to-transparent" />
                          {isSelected && (
                            <span className="absolute top-2 right-2 w-5 h-5 rounded-full bg-rose-500 text-white flex items-center justify-center text-xs">
                              <Check size={12} strokeWidth={3} />
                            </span>
                          )}
                          <span className="absolute bottom-2 left-2.5 text-white font-bold text-xs drop-shadow-sm">
                            {preset.name}
                          </span>
                        </div>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Events Checklist */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-2">
                  Các sự kiện dự kiến tổ chức:
                </label>
                <div className="flex flex-wrap gap-2">
                  {EVENTS.map((e) => {
                    const isSelected = f.events.includes(e);
                    return (
                      <button
                        type="button"
                        key={e}
                        onClick={() => toggle("events", e)}
                        className={`px-3 py-1.5 rounded-xl text-xs font-semibold border transition cursor-pointer flex items-center gap-1.5 ${
                          isSelected
                            ? "bg-rose-50 text-rose-700 border-rose-300"
                            : "bg-slate-50 text-slate-600 border-slate-200 hover:border-slate-300"
                        }`}
                      >
                        {isSelected && <Check size={12} strokeWidth={3} />}
                        {e}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Priorities */}
              <div>
                <label className="block text-xs font-bold text-slate-700 mb-2">
                  Ưu tiên quan trọng nhất:
                </label>
                <div className="flex flex-wrap gap-2">
                  {PRIORITIES.map((pr) => {
                    const isSelected = f.priorities.includes(pr);
                    return (
                      <button
                        type="button"
                        key={pr}
                        onClick={() => toggle("priorities", pr)}
                        className={`px-3 py-1.5 rounded-xl text-xs font-semibold border transition cursor-pointer flex items-center gap-1.5 ${
                          isSelected
                            ? "bg-slate-900 text-white border-slate-900 shadow-xs"
                            : "bg-slate-50 text-slate-600 border-slate-200 hover:border-slate-300"
                        }`}
                      >
                        {isSelected && <Check size={12} strokeWidth={3} />}
                        {pr}
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Final Summary Card before submission */}
              <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 space-y-2">
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5 text-xs">
                  <div className="bg-white p-2 rounded-xl border border-slate-100">
                    <span className="text-[10px] text-slate-400 block">Dâu &amp; Rể</span>
                    <strong className="text-slate-800 truncate block">
                      {f.groomName} &amp; {f.brideName}
                    </strong>
                  </div>
                  <div className="bg-white p-2 rounded-xl border border-slate-100">
                    <span className="text-[10px] text-slate-400 block">Địa điểm</span>
                    <strong className="text-slate-800 truncate block">{f.city}</strong>
                  </div>
                  <div className="bg-white p-2 rounded-xl border border-slate-100">
                    <span className="text-[10px] text-slate-400 block">Khách mời</span>
                    <strong className="text-slate-800 block">{f.guestCount} khách</strong>
                  </div>
                  <div className="bg-white p-2 rounded-xl border border-slate-100">
                    <span className="text-[10px] text-slate-400 block">Ngân sách</span>
                    <strong className="text-rose-600 font-bold block">
                      {vnd(f.totalBudget)}
                    </strong>
                  </div>
                </div>
              </div>
            </div>
          )}

          <ErrorText>{save.isError ? apiError(save.error) : ""}</ErrorText>

          {/* Navigation Buttons */}
          <div className="flex justify-between items-center pt-4 border-t border-slate-100">
            <button
              type="button"
              className="px-4 py-2 rounded-xl border border-slate-200 text-slate-600 font-semibold text-xs sm:text-sm hover:bg-slate-50 transition cursor-pointer flex items-center gap-1.5 disabled:opacity-40 disabled:cursor-not-allowed"
              disabled={milestone === 0}
              onClick={() => setMilestone(milestone - 1)}
            >
              <ArrowLeft size={16} /> Quay lại
            </button>

            {milestone < 3 ? (
              <button
                type="button"
                className="px-5 py-2.5 rounded-xl bg-slate-900 hover:bg-slate-800 text-white font-bold text-xs sm:text-sm shadow-sm transition cursor-pointer flex items-center gap-1.5 disabled:opacity-40 disabled:cursor-not-allowed"
                disabled={!canNext}
                onClick={() => setMilestone(milestone + 1)}
              >
                Tiếp tục <ArrowRight size={16} />
              </button>
            ) : (
              <button
                type="button"
                className="px-6 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-700 text-white font-bold text-xs sm:text-sm shadow-md shadow-rose-200 transition cursor-pointer disabled:opacity-40"
                disabled={save.isPending || !f.groomName.trim() || !f.brideName.trim()}
                onClick={submit}
              >
                {save.isPending ? "Đang chuẩn bị không gian cưới..." : "Tạo Lộ Trình Cưới Ngay"}
              </button>
            )}
          </div>
        </fieldset>

        <p className="text-center text-xs text-slate-400 mt-5">
          Hoàn tất để mở trang Kế hoạch cưới thông minh của hai bạn.
        </p>
      </div>
    </main>
  );
}
