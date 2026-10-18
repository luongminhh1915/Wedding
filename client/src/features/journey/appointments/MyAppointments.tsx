import { CalendarDays } from "lucide-react";
import { Link } from "react-router-dom";
import { apiError, useAppointmentAction } from "../api";
import { useAuthStore } from "../../../store/authStore";
import { downloadAppointmentCalendar, useMyBookings } from "../bookingApi";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd, vnDateTime } from "../format";
import { useNotificationFocus } from "../../notifications/useNotificationFocus";

/** P3 – "Lịch hẹn của tôi" trong Planner. */
export function MyAppointments() {
  const { data = [], isLoading, isError, refetch } = useMyBookings();
  const { focusId, missing } = useNotificationFocus("appointment", data.map(a => a.id), !isLoading && !isError);
  const user = useAuthStore((s) => s.user);
  const act = useAppointmentAction();
  return (
    <Panel eyebrow="Lịch hẹn" title="Lịch hẹn với nhà cung cấp" action={<Link to="/browse" className="btn btn-outline">Tìm NCC để đặt hẹn</Link>}>
      <ErrorText>{act.isError ? apiError(act.error) : ""}</ErrorText>
      {missing && <p role="status" className="text-sm muted">Lịch hẹn trong thông báo không còn khả dụng trong tài khoản của bạn.</p>}
      {isError ? <div role="alert"><p>Chưa tải được lịch hẹn.</p><button className="btn btn-outline" onClick={() => refetch()}>Thử lại</button></div> : isLoading ? <p className="muted text-sm">Đang tải...</p> : data.length === 0 ? <Empty>Chưa có lịch hẹn nào. Mở trang dịch vụ và bấm “Đặt lịch hẹn”.</Empty> : (
        <ul className="grid gap-3">
          {data.map((a) => (
            <li key={a.id} id={`notification-appointment-${a.id}`} tabIndex={-1} data-notification-focus={focusId === a.id} className="rounded-2xl border border-[var(--line)] p-4 flex flex-wrap justify-between gap-3 bg-[var(--paper)]">
              <div>
                <p className="font-bold flex items-center gap-2"><CalendarDays size={15} className="text-[var(--gold)]" /> {vnDateTime(a.startAt)} <StatusPill status={a.status} /></p>
                <p className="text-sm">{a.typeName} · <b>{a.vendorName}</b></p>
                <p className="text-xs muted">Mã {a.code} · {a.attendees} người{a.depositAmount > 0 ? ` · Cọc ${vnd(a.depositAmount)}${a.depositPaid ? " (đã cọc)" : ""}` : ""}</p>
                {a.vendorNote && <p className="text-xs mt-1">NCC: “{a.vendorNote}”</p>}
                {a.proposedStartAt && <p className="text-sm mt-1">Giờ đề xuất: <b>{vnDateTime(a.proposedStartAt)}</b> · đã đổi {a.rescheduleCount}/2 lần</p>}
                {a.depositHoldUntil && a.status === "AwaitingDeposit" && <p className="text-xs muted">Giữ chỗ đến {vnDateTime(a.depositHoldUntil)}. NCC phải xác nhận tiền nhận được.</p>}
                {a.refundDue > 0 && <p className="text-sm mt-1">Hoàn cọc {vnd(a.refundDue)}: {a.refundedAt ? "NCC đã ghi nhận hoàn tiền" : "Đang chờ NCC hoàn trực tiếp"}</p>}
                {a.location && <a className="text-xs underline" target="_blank" rel="noreferrer" href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(a.location)}`}>Địa điểm / chỉ đường</a>}
              </div>
              <div className="flex flex-wrap gap-2 items-start">
                <button className="btn btn-outline" onClick={() => downloadAppointmentCalendar(a.id).catch(() => alert("Không tải được lịch, vui lòng thử lại."))}>Thêm vào lịch (.ics)</button>
                {a.status === "AwaitingDeposit" && <div>{a.depositQrUrl && <img src={a.depositQrUrl} alt="QR cọc chuyển thẳng NCC" width={130} height={130} />}<button className="btn btn-gold" onClick={() => act.mutate({ id: a.id, op: "pay-deposit" })}>{a.depositReportedAt ? "Đã báo chuyển – chờ NCC" : "Báo đã chuyển cọc"}</button></div>}
                {a.status === "RescheduleProposed" && a.proposedById !== user?.id && <><button className="btn btn-primary" onClick={() => act.mutate({ id: a.id, op: "accept-proposal" })}>Đồng ý giờ mới</button><button className="btn btn-outline" onClick={() => act.mutate({ id: a.id, op: "decline-proposal", note: "Không phù hợp giờ đề xuất" })}>Từ chối đổi lịch</button></>}
                {["Pending", "Confirmed"].includes(a.status) && a.rescheduleCount < 2 && <button className="btn btn-outline" onClick={() => {
                  const value = prompt("Giờ mới (giờ Việt Nam), dạng YYYY-MM-DDTHH:mm");
                  if (!value) return; const date = new Date(`${value}+07:00`);
                  if (Number.isNaN(date.getTime())) { alert("Ngày giờ không hợp lệ."); return; }
                  act.mutate({ id: a.id, op: "reschedule", newStartAt: date.toISOString() });
                }}>Đề xuất đổi lịch</button>}
                {["Pending", "Confirmed", "AwaitingDeposit", "RescheduleProposed"].includes(a.status) && (
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
