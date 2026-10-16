import { useState } from "react";
import { Check, Plus, UserCheck, UserX, X } from "lucide-react";
import { apiError, useAppointmentAction, useMyAppointmentTypes, useSaveAppointmentType, useVendorAppointments, type AppointmentType } from "../api";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd, vnDateTime } from "../format";

const DAYS = [["1", "T2"], ["2", "T3"], ["3", "T4"], ["4", "T5"], ["5", "T6"], ["6", "T7"], ["0", "CN"]];
const blank = { id: null as string | null, name: "", description: "", durationMinutes: 60, capacity: 1, workingDays: "1,2,3,4,5,6", openTime: "09:00", closeTime: "18:00", depositAmount: 0, requiresApproval: true, isActive: true };

/** P3 – NCC: cấu hình loại lịch hẹn/giờ làm việc và xử lý lịch hẹn (duyệt, từ chối, check-in, vắng mặt). */
export function VendorAppointments() {
  const { data: appts = [] } = useVendorAppointments();
  const { data: types = [] } = useMyAppointmentTypes();
  const act = useAppointmentAction();
  const save = useSaveAppointmentType();
  const [form, setForm] = useState<typeof blank | null>(null);
  const edit = (t: AppointmentType) => setForm({ ...blank, ...t, description: t.description ?? "" });
  const pending = appts.filter((a) => a.status === "Pending").length;

  return (
    <div className="wd">
      <Panel eyebrow={`${pending} lịch chờ duyệt`} title="Lịch hẹn của khách">
        <ErrorText>{act.isError ? apiError(act.error) : ""}</ErrorText>
        {appts.length === 0 ? <Empty>Chưa có lịch hẹn.</Empty> : (
          <table className="wd-table">
            <thead><tr><th>Thời gian</th><th>Khách</th><th>Loại hẹn</th><th>Trạng thái</th><th /></tr></thead>
            <tbody>
              {appts.map((a) => (
                <tr key={a.id}>
                  <td><b>{vnDateTime(a.startAt)}</b><div className="text-xs muted">{a.code}</div></td>
                  <td>{a.customerName}<div className="text-xs muted">{a.attendees} người{a.note ? ` · ${a.note}` : ""}</div></td>
                  <td>{a.typeName}{a.depositAmount > 0 && <div className="text-xs muted">Cọc {vnd(a.depositAmount)}{a.depositPaid ? " ✓" : ""}</div>}</td>
                  <td><StatusPill status={a.status} /></td>
                  <td className="whitespace-nowrap text-right">
                    {a.status === "Pending" && <>
                      <button className="btn btn-primary !px-3 !py-2" onClick={() => act.mutate({ id: a.id, op: "confirm", note: "Hẹn gặp bạn!" })}><Check size={13} /> Duyệt</button>{" "}
                      <button className="btn btn-outline !px-3 !py-2" onClick={() => { const n = prompt("Lý do từ chối?"); if (n) act.mutate({ id: a.id, op: "reject", note: n }); }}><X size={13} /></button>
                    </>}
                    {a.status === "Confirmed" && <>
                      <button className="btn btn-gold !px-3 !py-2" onClick={() => act.mutate({ id: a.id, op: "check-in" })}><UserCheck size={13} /> Check-in</button>{" "}
                      <button className="btn btn-outline !px-3 !py-2" title="Vắng mặt" onClick={() => act.mutate({ id: a.id, op: "no-show" })}><UserX size={13} /></button>
                    </>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Panel>
      <Panel eyebrow="Cấu hình" title="Loại lịch hẹn & giờ làm việc" action={<button className="btn btn-outline" onClick={() => setForm({ ...blank })}><Plus size={14} /> Thêm loại hẹn</button>}>
        <div className="grid-auto">
          {types.map((t) => (
            <button key={t.id} className="kiosk-option" onClick={() => edit(t)}>
              <b>{t.name}</b> {!t.isActive && <span className="pill bad">Tạm tắt</span>}
              <p className="text-xs muted">{t.durationMinutes}′ · tối đa {t.capacity} khách/khung · {t.openTime}–{t.closeTime}</p>
              <p className="text-xs">{DAYS.filter(([d]) => t.workingDays.split(",").includes(d)).map(([, l]) => l).join(" ")}</p>
              {t.depositAmount > 0 && <span className="pill warn">Cọc {vnd(t.depositAmount)}</span>}
            </button>
          ))}
        </div>
        {form && (
          <div className="mt-5 rounded-2xl border border-[var(--line)] p-4 bg-[var(--paper)] grid md:grid-cols-3 gap-3">
            <label className="md:col-span-2"><span className="label">Tên loại hẹn</span><input className="input" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></label>
            <label><span className="label">Thời lượng (phút)</span><input className="input" type="number" value={form.durationMinutes} onChange={(e) => setForm({ ...form, durationMinutes: +e.target.value })} /></label>
            <label className="md:col-span-3"><span className="label">Mô tả</span><input className="input" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></label>
            <label><span className="label">Mở cửa</span><input className="input" type="time" value={form.openTime} onChange={(e) => setForm({ ...form, openTime: e.target.value })} /></label>
            <label><span className="label">Đóng cửa</span><input className="input" type="time" value={form.closeTime} onChange={(e) => setForm({ ...form, closeTime: e.target.value })} /></label>
            <label><span className="label">Sức chứa/khung</span><input className="input" type="number" value={form.capacity} onChange={(e) => setForm({ ...form, capacity: +e.target.value })} /></label>
            <div className="md:col-span-2"><span className="label">Ngày làm việc</span>
              <div className="flex flex-wrap gap-1">{DAYS.map(([d, l]) => {
                const on = form.workingDays.split(",").includes(d);
                return <button key={d} type="button" className={`chip ${on ? "active" : ""}`} onClick={() => setForm({ ...form, workingDays: (on ? form.workingDays.split(",").filter((x) => x !== d) : [...form.workingDays.split(","), d]).filter(Boolean).join(",") })}>{l}</button>;
              })}</div>
            </div>
            <label><span className="label">Tiền cọc (0 = miễn phí)</span><input className="input" type="number" step={100000} value={form.depositAmount} onChange={(e) => setForm({ ...form, depositAmount: +e.target.value })} /></label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.requiresApproval} onChange={(e) => setForm({ ...form, requiresApproval: e.target.checked })} /> Cần NCC duyệt</label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} /> Đang mở đặt</label>
            <div className="md:col-span-3 flex gap-2 justify-end">
              <ErrorText>{save.isError ? apiError(save.error) : ""}</ErrorText>
              <button className="btn btn-ghost" onClick={() => setForm(null)}>Đóng</button>
              <button className="btn btn-primary" disabled={save.isPending} onClick={() => save.mutate(form, { onSuccess: () => setForm(null) })}>Lưu</button>
            </div>
          </div>
        )}
      </Panel>
    </div>
  );
}
