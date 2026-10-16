import { CalendarDays } from "lucide-react";
import { Link } from "react-router-dom";
import { apiError, useAppointmentAction, useMyAppointments } from "../api";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd, vnDateTime } from "../format";

/** P3 – "Lịch hẹn của tôi" trong Planner. */
export function MyAppointments() {
  const { data = [], isLoading } = useMyAppointments();
  const act = useAppointmentAction();
  return (
    <Panel eyebrow="Lịch hẹn" title="Lịch hẹn với nhà cung cấp" action={<Link to="/browse" className="btn btn-outline">Tìm NCC để đặt hẹn</Link>}>
      <ErrorText>{act.isError ? apiError(act.error) : ""}</ErrorText>
      {isLoading ? <p className="muted text-sm">Đang tải...</p> : data.length === 0 ? <Empty>Chưa có lịch hẹn nào. Mở trang dịch vụ và bấm “Đặt lịch hẹn”.</Empty> : (
        <ul className="grid gap-3">
          {data.map((a) => (
            <li key={a.id} className="rounded-2xl border border-[var(--line)] p-4 flex flex-wrap justify-between gap-3 bg-[var(--paper)]">
              <div>
                <p className="font-bold flex items-center gap-2"><CalendarDays size={15} className="text-[var(--gold)]" /> {vnDateTime(a.startAt)} <StatusPill status={a.status} /></p>
                <p className="text-sm">{a.typeName} · <b>{a.vendorName}</b></p>
                <p className="text-xs muted">Mã {a.code} · {a.attendees} người{a.depositAmount > 0 ? ` · Cọc ${vnd(a.depositAmount)}${a.depositPaid ? " (đã cọc)" : ""}` : ""}</p>
                {a.vendorNote && <p className="text-xs mt-1">NCC: “{a.vendorNote}”</p>}
              </div>
              <div className="flex gap-2 items-start">
                {a.status === "AwaitingDeposit" && <button className="btn btn-gold" onClick={() => act.mutate({ id: a.id, op: "pay-deposit" })}>Đã chuyển cọc</button>}
                {["Pending", "Confirmed", "AwaitingDeposit"].includes(a.status) && (
                  <button className="btn btn-outline" onClick={() => confirm("Hủy lịch hẹn này?") && act.mutate({ id: a.id, op: "cancel", note: "Khách hủy" })}>Hủy</button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}
