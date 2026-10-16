import { Link } from "react-router-dom";
import { CalendarClock, CheckCheck, FileCheck2, PackageCheck, Settings2, Users, Wallet } from "lucide-react";
import { usePlannerOverview } from "../api";
import { vnd } from "../format";

/** P1 – Thẻ tổng quan đầu Planner: đếm ngược, tiến độ checklist, ngân sách, lịch hẹn, khách mời. */
export function PlannerSnapshot() {
  const { data } = usePlannerOverview();
  if (!data) return null;
  const p = data.profile;
  if (!p)
    return (
      <div className="wd card p-6 mb-6 flex flex-wrap items-center justify-between gap-4" style={{ background: "linear-gradient(135deg,#fff,var(--paper))" }}>
        <div>
          <p className="eyebrow text-[var(--gold)]">Bắt đầu</p>
          <h2 className="serif text-xl font-bold">Thiết lập kế hoạch cưới trong 1 phút</h2>
          <p className="text-sm muted">Wedding Match sẽ gợi ý checklist 12 tháng và chia ngân sách theo từng hạng mục.</p>
        </div>
        <Link to="/planner/setup" className="btn btn-gold">Bắt đầu ngay</Link>
      </div>
    );
  const pct = data.checklistTotal ? Math.round((data.checklistDone / data.checklistTotal) * 100) : 0;
  const stats = [
    { icon: CheckCheck, label: "Công việc", value: `${data.checklistDone}/${data.checklistTotal}`, sub: `${pct}% hoàn thành` },
    { icon: Wallet, label: "Ngân sách", value: vnd(p.totalBudget), sub: `Đã chi ${vnd(data.budgetActual)}` },
    { icon: CalendarClock, label: "Lịch hẹn sắp tới", value: data.appointmentUpcoming, sub: "với nhà cung cấp" },
    { icon: FileCheck2, label: "Yêu cầu / HĐ", value: `${data.leadCount} / ${data.contractCount}`, sub: "tư vấn / hợp đồng" },
    { icon: Users, label: "Khách xác nhận", value: data.rsvpAttending, sub: `${data.guestCount} nhóm khách trong Planner` },
  ];
  return (
    <div className="wd card p-6 mb-6">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-5">
        <div>
          <p className="eyebrow text-[var(--gold)]">{p.city} · {p.guestCount} khách · {p.style}</p>
          <h2 className="serif text-2xl font-bold">
            {p.daysLeft != null ? <>Còn <span className="wd-gradient-text">{p.daysLeft} ngày</span> đến ngày cưới</> : "Chưa chọn ngày cưới"}
          </h2>
        </div>
        <div className="flex gap-2">
          <Link to="/combo" className="btn btn-gold"><PackageCheck size={14} /> Chọn combo</Link>
          <Link to="/planner/setup" className="btn btn-outline"><Settings2 size={14} /> Sửa hồ sơ</Link>
        </div>
      </div>
      <div className="h-2 rounded-full bg-[var(--paper)] overflow-hidden mb-5" aria-label={`Tiến độ ${pct}%`}>
        <div className="h-full" style={{ width: `${pct}%`, background: "linear-gradient(90deg,var(--brand),var(--gold))" }} />
      </div>
      <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
        {stats.map(({ icon: Icon, label, value, sub }) => (
          <div key={label} className="rounded-2xl border border-[var(--line)] bg-[var(--paper)] p-4">
            <Icon size={16} className="text-[var(--gold)]" />
            <p className="eyebrow muted mt-2">{label}</p>
            <p className="serif text-xl font-bold">{value}</p>
            <p className="text-xs muted">{sub}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
