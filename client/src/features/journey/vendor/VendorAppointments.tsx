import { useState } from "react";
import { Check, Plus, UserCheck, UserX, X } from "lucide-react";
import { apiError, useAppointmentAction, useSaveAppointmentType } from "../api";
import { useBookingTypes, useDayExceptions, useSaveException, useVendorBookings, type BookingType } from "../bookingApi";
import { useAuthStore } from "../../../store/authStore";
import { ServiceItemManager } from "./ServiceItemManager";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd, vnDateTime } from "../format";

const DAYS = [["1", "T2"], ["2", "T3"], ["3", "T4"], ["4", "T5"], ["5", "T6"], ["6", "T7"], ["0", "CN"]];
const blank = { id: null as string | null, name: "", description: "", durationMinutes: 60, capacity: 1, workingDays: "1,2,3,4,5,6", openTime: "09:00", closeTime: "18:00", depositAmount: 0, requiresApproval: true, isActive: true, bufferBeforeMinutes: 0, bufferAfterMinutes: 0, minBookingHours: 12, maxBookingDays: 90, maxAttendees: 10, freeCancellationHours: 24, meetingMode: "Store", location: "", breakStart: "", breakEnd: "" };

/** P3 – NCC: cấu hình loại lịch hẹn/giờ làm việc và xử lý lịch hẹn (duyệt, từ chối, check-in, vắng mặt). */
export function VendorAppointments() {
  const { data: appts = [] } = useVendorBookings();
  const { data: types = [] } = useBookingTypes();
  const user = useAuthStore((s) => s.user);
  const act = useAppointmentAction();
  const save = useSaveAppointmentType();
  const [form, setForm] = useState<typeof blank | null>(null);
  const edit = (t: BookingType) => setForm({ ...blank, ...t, description: t.description ?? "", location: t.location ?? "", breakStart: t.breakStart ?? "", breakEnd: t.breakEnd ?? "" });
  const [exceptionType, setExceptionType] = useState("");
  const [exception, setException] = useState({ date: "", isClosed: true, openTime: "09:00", closeTime: "18:00", reason: "" });
  const { data: exceptions = [] } = useDayExceptions(exceptionType || undefined);
  const saveException = useSaveException();
  const pending = appts.filter((a) => a.status === "Pending").length;

  return (
    <div className="wd">
      <ServiceItemManager />
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
                    {a.status === "AwaitingDeposit" && <button className="btn btn-primary !px-3 !py-2" disabled={act.isPending} onClick={() => confirm("Bạn đã kiểm tra tài khoản ngân hàng và thực nhận đủ tiền cọc? Không bấm chỉ dựa vào ảnh chuyển khoản.") && act.mutate({ id: a.id, op: "confirm-deposit" })}>{a.depositReportedAt ? "Khách đã báo CK – đối soát" : "Xác nhận thực nhận cọc"}</button>}
                    {a.status === "RescheduleProposed" && <span className="text-xs">Đề xuất {a.proposedStartAt && vnDateTime(a.proposedStartAt)}{a.proposedById !== user?.id && <button className="btn btn-primary !px-3 !py-2" onClick={() => act.mutate({ id: a.id, op: "accept-proposal" })}>Đồng ý</button>}</span>}
                    {a.status === "CheckedIn" && <button className="btn btn-primary !px-3 !py-2" onClick={() => act.mutate({ id: a.id, op: "complete" })}>Hoàn tất buổi hẹn</button>}
                    {a.refundDue > 0 && !a.refundedAt && <button className="btn btn-outline !px-3 !py-2" onClick={() => { const reference = prompt(`Đã hoàn ${vnd(a.refundDue)} trực tiếp cho khách? Nhập mã giao dịch hoàn:`); if (reference) act.mutate({ id: a.id, op: "refund", note: reference }); }}>Ghi nhận hoàn cọc</button>}
                    {["Pending", "Confirmed"].includes(a.status) && a.rescheduleCount < 2 && <button className="btn btn-outline !px-3 !py-2" onClick={() => { const text = prompt("Đề xuất giờ Việt Nam YYYY-MM-DDTHH:mm"); if (!text) return; const d = new Date(`${text}+07:00`); if (!Number.isNaN(d.getTime())) act.mutate({ id: a.id, op: "propose", newStartAt: d.toISOString() }); }}>Đổi giờ</button>}
                    {["Pending", "Confirmed", "RescheduleProposed"].includes(a.status) && <button className="btn btn-outline !px-3 !py-2" onClick={() => { const reason = prompt("Lý do NCC hủy (hoàn 100% cọc và ghi vi phạm SLA):"); if (reason) act.mutate({ id: a.id, op: "cancel", note: reason }); }}>NCC hủy</button>}
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
            {([["bufferBeforeMinutes", "Đệm trước (phút)"], ["bufferAfterMinutes", "Đệm sau (phút)"], ["minBookingHours", "Đặt trước tối thiểu (giờ, ≥12)"], ["maxBookingDays", "Đặt trước tối đa (ngày, ≤90)"], ["maxAttendees", "Tối đa người/buổi"], ["freeCancellationHours", "Hủy miễn phí trước (giờ, ≥24)"]] as const).map(([key, label]) => <label key={key}><span className="label">{label}</span><input className="input" type="number" value={form[key]} onChange={(e) => setForm({ ...form, [key]: +e.target.value })} /></label>)}
            <label><span className="label">Nghỉ trưa từ</span><input className="input" type="time" value={form.breakStart} onChange={(e) => setForm({ ...form, breakStart: e.target.value })} /></label>
            <label><span className="label">Nghỉ trưa đến</span><input className="input" type="time" value={form.breakEnd} onChange={(e) => setForm({ ...form, breakEnd: e.target.value })} /></label>
            <label><span className="label">Hình thức</span><select className="input" value={form.meetingMode} onChange={(e) => setForm({ ...form, meetingMode: e.target.value })}><option value="Store">Tại cửa hàng</option><option value="Online">Online</option><option value="OnSite">Tại địa điểm</option></select></label>
            <label className="md:col-span-3"><span className="label">Địa điểm / link gặp online</span><input className="input" value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} /></label>
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
      <Panel eyebrow="Ngày nghỉ & giờ đặc biệt" title="Ngoại lệ lịch làm việc">
        <div className="grid sm:grid-cols-3 gap-3"><select aria-label="Loại hẹn cấu hình ngày nghỉ" className="input" value={exceptionType} onChange={(e) => setExceptionType(e.target.value)}><option value="">Chọn loại hẹn</option>{types.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select>
          <input aria-label="Ngày ngoại lệ" className="input" type="date" value={exception.date} onChange={(e) => setException({ ...exception, date: e.target.value })} />
          <label className="text-sm"><input type="checkbox" checked={exception.isClosed} onChange={(e) => setException({ ...exception, isClosed: e.target.checked })} /> Nghỉ cả ngày</label>
          {!exception.isClosed && <><input aria-label="Giờ mở đặc biệt" className="input" type="time" value={exception.openTime} onChange={(e) => setException({ ...exception, openTime: e.target.value })} /><input aria-label="Giờ đóng đặc biệt" className="input" type="time" value={exception.closeTime} onChange={(e) => setException({ ...exception, closeTime: e.target.value })} /></>}
          <input className="input" placeholder="Lý do / ghi chú" value={exception.reason} onChange={(e) => setException({ ...exception, reason: e.target.value })} />
          <button className="btn btn-primary" disabled={!exceptionType || !exception.date || saveException.isPending} onClick={() => saveException.mutate({ ...exception, typeId: exceptionType })}>Lưu ngày đặc biệt</button>
        </div><ErrorText>{saveException.isError ? apiError(saveException.error) : ""}</ErrorText>
        <ul className="mt-3 grid gap-2">{exceptions.map(e => <li key={e.id} className="text-sm flex justify-between"><span>{e.date}: {e.isClosed ? "Nghỉ" : `${e.openTime}–${e.closeTime}`} · {e.reason}</span><button className="underline" onClick={() => saveException.mutate({ typeId: exceptionType, date: e.date, isClosed: true, delete: true })}>Xóa ngoại lệ</button></li>)}</ul>
        <p className="text-xs muted mt-3">Thay đổi giờ không tự hủy các lịch đã xác nhận. Hãy chủ động đổi/hủy và hoàn cọc cho khách nếu nghỉ đột xuất.</p>
      </Panel>
    </div>
  );
}
