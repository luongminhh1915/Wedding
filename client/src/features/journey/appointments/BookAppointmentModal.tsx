import { useState } from "react";
import { Link } from "react-router-dom";
import { CalendarCheck2, Clock, X } from "lucide-react";
import { apiError, useAppointmentAction, useBookAppointment, useNextAvailableDate, useSlots, useVendorAppointmentTypes, type Appointment } from "../api";
import { ErrorText, StatusPill } from "../ui";
import { vnd, vnDateTime } from "../format";
import { useAuthStore } from "../../../store/authStore";

const ymd = (d: Date) => d.toLocaleDateString("en-CA", { timeZone: "Asia/Ho_Chi_Minh" });

/** P3 – Đặt lịch hẹn: chọn loại hẹn → ngày (14 ngày tới) → khung giờ còn chỗ → ghi chú → (cọc VietQR nếu có). */
export function BookAppointmentModal({ vendorId, vendorName, onClose }: { vendorId: string; vendorName: string; onClose: () => void }) {
  const user = useAuthStore((s) => s.user);
  const { data: types = [], isLoading } = useVendorAppointmentTypes(vendorId);
  const [typeId, setTypeId] = useState<string>();
  const [days] = useState(() => { const now = Date.now(); return Array.from({ length: 14 }, (_, i) => new Date(now + (i + 1) * 86400000)); });
  const [bookingWindow] = useState(() => ({ min: ymd(new Date()), max: ymd(new Date(Date.now() + 90 * 86400000)) }));
  const [date, setDate] = useState(ymd(days[0]));
  const [slot, setSlot] = useState<string>();
  const [attendees, setAttendees] = useState(2);
  const [note, setNote] = useState("");
  const [result, setResult] = useState<Appointment | null>(null);
  const activeType = types.find((t) => t.id === (typeId ?? types[0]?.id));
  const { data: slots = [], isFetching } = useSlots(activeType?.id, date);
  const dayOff = !isFetching && !!activeType && slots.every((s) => s.remaining <= 0);
  const later = days.map(ymd).filter((v) => v > date);
  const { data: nextDate, isFetching: findingNext } = useNextAvailableDate(activeType?.id, later, dayOff);
  const book = useBookAppointment();
  const act = useAppointmentAction();

  return (
    <div className="wd-overlay" role="dialog" aria-modal="true" aria-label={`Đặt lịch hẹn với ${vendorName}`} onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="wd wd-modal wd-fade max-w-[640px]">
        <button className="wd-x" onClick={onClose} aria-label="Đóng"><X size={18} /></button>
        <p className="eyebrow text-[var(--gold)]">Đặt lịch hẹn</p>
        <h2 className="serif text-2xl font-bold mb-4">{vendorName}</h2>
        {!user ? (
          <div className="text-center py-6">
            <p className="text-sm muted mb-4">Đăng nhập để đặt lịch hẹn với nhà cung cấp.</p>
            <Link to="/login" className="btn btn-primary">Đăng nhập</Link>
          </div>
        ) : result ? (
          <div className="text-center space-y-3">
            <CalendarCheck2 className="mx-auto text-[var(--gold)]" size={36} />
            <h3 className="serif text-xl font-bold">Đã gửi lịch hẹn {result.code}</h3>
            <p className="text-sm">{result.typeName} · {vnDateTime(result.startAt)}</p>
            <StatusPill status={result.status} />
            {result.status === "AwaitingDeposit" && (
              <div className="pt-2">
                {result.depositQrUrl && <img src={result.depositQrUrl} alt="QR đặt cọc" width={170} height={170} className="mx-auto rounded-xl border border-[var(--line)]" />}
                <p className="text-xs muted mt-2">Cọc {vnd(result.depositAmount)} giữ chỗ trong 30 phút – chuyển thẳng cho NCC, nội dung <b>HEN {result.code}</b>. NCC đối soát tiền nhận được; nút bên dưới chỉ báo đã chuyển, không tự xác nhận thanh toán.</p>
                <button className="btn btn-gold mt-3" disabled={act.isPending}
                  onClick={() => act.mutate({ id: result.id, op: "pay-deposit" }, { onSuccess: setResult })}>Báo đã chuyển cọc cho NCC</button>
              </div>
            )}
            <div className="pt-2"><Link to="/customer/dashboard?tab=appointments" className="btn btn-outline" onClick={onClose}>Xem lịch hẹn của tôi</Link></div>
          </div>
        ) : isLoading ? (
          <p className="muted text-sm">Đang tải...</p>
        ) : types.length === 0 ? (
          <p className="muted text-sm">Nhà cung cấp chưa mở lịch hẹn trực tuyến. Hãy gửi yêu cầu tư vấn nhé.</p>
        ) : (
          <div className="space-y-4">
            <div className="grid gap-2">
              {types.map((t) => (
                <button key={t.id} className={`kiosk-option ${activeType?.id === t.id ? "selected" : ""}`} onClick={() => { setTypeId(t.id); setSlot(undefined); }}>
                  <div className="flex justify-between gap-2">
                    <b>{t.name}</b>
                    <span className="text-xs muted flex items-center gap-1"><Clock size={12} /> {t.durationMinutes} phút</span>
                  </div>
                  <p className="text-xs muted">{t.description}</p>
                  <p className="text-xs mt-1">{t.depositAmount > 0 ? <span className="pill warn">Cọc {vnd(t.depositAmount)}</span> : <span className="pill ok">Miễn phí</span>}{" "}
                    {t.requiresApproval ? <span className="pill info">NCC xác nhận</span> : <span className="pill ok">Xác nhận ngay</span>}</p>
                </button>
              ))}
            </div>
            <div>
              <span className="label">Chọn ngày</span>
              <input className="input mb-2" aria-label="Chọn ngày trong 90 ngày tới" type="date" value={date} min={bookingWindow.min} max={bookingWindow.max} onChange={(e) => { setDate(e.target.value); setSlot(undefined); }} />
              <div className="wd-scroll-x flex gap-2 pb-1">
                {days.map((d) => {
                  const v = ymd(d);
                  return (
                    <button key={v} className={`chip flex-col !py-2 ${date === v ? "active" : ""}`} onClick={() => { setDate(v); setSlot(undefined); }}>
                      <span className="text-[10px] uppercase">{d.toLocaleDateString("vi-VN", { weekday: "short", timeZone: "Asia/Ho_Chi_Minh" })}</span>
                      <span>{d.toLocaleDateString("vi-VN", { day: "2-digit", month: "2-digit", timeZone: "Asia/Ho_Chi_Minh" })}</span>
                    </button>
                  );
                })}
              </div>
            </div>
            <div>
              <span className="label">Khung giờ (giờ Việt Nam)</span>
              {isFetching ? <p className="text-xs muted">Đang tải khung giờ...</p> : dayOff ? (
                <div className="text-xs muted space-y-2">
                  <p>{slots.length === 0 ? "Ngày này nhà cung cấp nghỉ." : "Ngày này đã kín lịch."}</p>
                  {findingNext ? <p>Đang tìm ngày trống gần nhất...</p> : nextDate ? (
                    <button className="chip active" onClick={() => { setDate(nextDate); setSlot(undefined); }}>
                      Ngày trống gần nhất: {new Date(`${nextDate}T00:00:00+07:00`).toLocaleDateString("vi-VN", { weekday: "long", day: "2-digit", month: "2-digit", year: "numeric", timeZone: "Asia/Ho_Chi_Minh" })} – chọn ngày này
                    </button>
                  ) : <p>Không còn ngày trống trong 14 ngày tới – hãy gửi yêu cầu tư vấn.</p>}
                </div>
              ) : (
                <div className="grid grid-cols-4 sm:grid-cols-6 gap-2">
                  {slots.map((s) => (
                    <button key={s.startAt} disabled={s.remaining <= 0} className={`slot ${slot === s.startAt ? "selected" : ""}`} onClick={() => setSlot(s.startAt)}
                      aria-label={`${s.localTime}${s.remaining <= 0 ? " – hết chỗ" : ""}`}>{s.localTime}</button>
                  ))}
                </div>
              )}
            </div>
            <div className="grid sm:grid-cols-[120px_1fr] gap-3">
              <label><span className="label">Số người</span><input className="input" type="number" min={1} max={10} value={attendees} onChange={(e) => setAttendees(+e.target.value)} /></label>
              <label><span className="label">Ghi chú cho NCC</span><input className="input" value={note} onChange={(e) => setNote(e.target.value)} placeholder="VD: muốn xem sảnh 300 khách" /></label>
            </div>
            <ErrorText>{book.isError ? apiError(book.error) : ""}</ErrorText>
            <p className="text-xs muted">Đặt trước tối thiểu 12 giờ, tối đa 90 ngày. Mỗi tài khoản tối đa 5 lịch đang mở; một lịch/loại/NCC. Đổi lịch tối đa 2 lần và cần phía còn lại đồng ý.</p>
            <button className="btn btn-primary w-full" disabled={!slot || !activeType || book.isPending}
              onClick={() => activeType && slot && book.mutate({ appointmentTypeId: activeType.id, startAt: slot, attendees, note }, { onSuccess: setResult })}>
              <CalendarCheck2 size={14} /> {book.isPending ? "Đang đặt..." : "Xác nhận đặt lịch"}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
