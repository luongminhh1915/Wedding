import { useLayoutEffect, useRef, useState } from "react";
import { CalendarDays } from "lucide-react";
import { Link } from "react-router-dom";
import { apiError, useAppointmentAction } from "../api";
import { useAuthStore } from "../../../store/authStore";
import { downloadAppointmentCalendar, useMyBookings, type Booking } from "../bookingApi";
import { ComboDialog } from "../combo/ComboDialog";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { vnd, vnDateTime } from "../format";
import { useNotificationFocus } from "../../notifications/useNotificationFocus";

type AppointmentRead = { data: Booking[]; isLoading: boolean; isError: boolean; isFetching: boolean; user?: { id: string; role: string } | null };
function appointmentActionProblem(latest: AppointmentRead, appointment: Booking, actorId: string | undefined, operation: string) {
  if (!actorId || latest.user?.id !== actorId || latest.user.role !== "Customer") return "Phiên đăng nhập đã thay đổi. Hãy mở lại lịch hẹn trong tài khoản của bạn.";
  if (latest.isError || latest.isLoading || latest.isFetching) return "Cần tải lại danh sách lịch hẹn thành công trước khi xác nhận.";
  const fresh = latest.data.find(item => item.id === appointment.id);
  if (!fresh || JSON.stringify(fresh) !== JSON.stringify(appointment)) return "Lịch hẹn đã thay đổi. Hãy quay lại, kiểm tra thông tin mới rồi chọn lại thao tác.";
  const proposedByOther = fresh.status === "RescheduleProposed" && !!fresh.proposedStartAt && !!fresh.proposedById && fresh.proposedById !== actorId;
  const allowed = operation === "cancel" ? ["Pending", "Confirmed", "AwaitingDeposit", "RescheduleProposed"].includes(fresh.status)
    : operation === "accept-proposal" || operation === "decline-proposal" ? proposedByOther
    : operation === "pay-deposit" ? fresh.status === "AwaitingDeposit"
    : operation === "reschedule" && ["Pending", "Confirmed"].includes(fresh.status) && fresh.rescheduleCount < 2;
  return allowed ? "" : "Thao tác không còn phù hợp với trạng thái lịch hẹn hiện tại.";
}

/** P3 – "Lịch hẹn của tôi" trong Planner. */
export function MyAppointments() {
  const { data = [], isLoading, isError, isFetching, refetch } = useMyBookings();
  const { focusId, missing } = useNotificationFocus("appointment", data.map(a => a.id), !isLoading && !isError);
  const user = useAuthStore((s) => s.user);
  const act = useAppointmentAction();
  const [confirmation, setConfirmation] = useState<{ appointment: Booking; operation: "cancel" | "decline-proposal"; actorId: string } | null>(null);
  const [actionError, setActionError] = useState("");
  const sending = useRef(false);
  const read = { data, isLoading, isError, isFetching, user };
  const current = useRef(read);
  useLayoutEffect(() => { current.current = { data, isLoading, isError, isFetching, user }; }, [data, isLoading, isError, isFetching, user]);
  const submit = (appointment: Booking, operation: string, note?: string, newStartAt?: string, actorId = user?.id) => {
    if (sending.current || act.isPending) return;
    const problem = appointmentActionProblem(current.current, appointment, actorId, operation);
    if (problem) { setActionError(problem); return; }
    sending.current = true;
    setActionError("");
    try {
      act.mutate({ id: appointment.id, op: operation, note, newStartAt }, {
        onSuccess: () => setConfirmation(null),
        onSettled: () => { sending.current = false; },
      });
    } catch (error) { sending.current = false; setActionError(apiError(error)); }
  };
  const openConfirmation = (appointment: Booking, operation: "cancel" | "decline-proposal") => {
    if (sending.current || act.isPending) return;
    const problem = appointmentActionProblem(current.current, appointment, user?.id, operation);
    if (problem) { setActionError(problem); return; }
    act.reset(); setActionError("");
    setConfirmation({ appointment: { ...appointment }, operation, actorId: user!.id });
  };
  const closeConfirmation = () => { if (!sending.current && !act.isPending) { setConfirmation(null); setActionError(""); } };
  const confirmationProblem = confirmation ? appointmentActionProblem(read, confirmation.appointment, confirmation.actorId, confirmation.operation) : "";
  const busy = act.isPending;
  const unavailable = busy || isLoading || isError || isFetching || user?.role !== "Customer" || !user?.id;
  return (
    <Panel eyebrow="Lịch hẹn" title="Lịch hẹn với nhà cung cấp" action={<Link to="/browse" className="btn btn-outline">Tìm NCC để đặt hẹn</Link>}>
      <ErrorText>{actionError || (act.isError ? apiError(act.error) : "")}</ErrorText>
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
                {a.status === "AwaitingDeposit" && <div>{a.depositQrUrl && <img src={a.depositQrUrl} alt="QR cọc chuyển thẳng NCC" width={130} height={130} />}<button className="btn btn-gold" disabled={unavailable} onClick={() => submit(a, "pay-deposit")}>{a.depositReportedAt ? "Đã báo chuyển – chờ NCC" : "Báo đã chuyển cọc"}</button></div>}
                {a.status === "RescheduleProposed" && a.proposedById !== user?.id && <><button className="btn btn-primary" disabled={unavailable} onClick={() => submit(a, "accept-proposal")}>Đồng ý giờ mới</button><button className="btn btn-outline" disabled={unavailable} onClick={() => openConfirmation(a, "decline-proposal")}>Từ chối giờ mới & hủy lịch</button></>}
                {["Pending", "Confirmed"].includes(a.status) && a.rescheduleCount < 2 && <button className="btn btn-outline" disabled={unavailable} onClick={() => {
                  if (sending.current || act.isPending) return;
                  const problem = appointmentActionProblem(current.current, a, user?.id, "reschedule");
                  if (problem) { setActionError(problem); return; }
                  const value = prompt("Giờ mới (giờ Việt Nam), dạng YYYY-MM-DDTHH:mm");
                  if (!value) return; const date = new Date(`${value}+07:00`);
                  if (Number.isNaN(date.getTime())) { alert("Ngày giờ không hợp lệ."); return; }
                  submit(a, "reschedule", undefined, date.toISOString());
                }}>Đề xuất đổi lịch</button>}
                {["Pending", "Confirmed", "AwaitingDeposit", "RescheduleProposed"].includes(a.status) && (
                  <button className="btn btn-outline" disabled={unavailable} onClick={() => openConfirmation(a, "cancel")}>Hủy lịch hẹn</button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}
      {confirmation && <ComboDialog title={confirmation.operation === "decline-proposal" ? "Từ chối giờ mới và hủy lịch hẹn" : "Xác nhận hủy lịch hẹn"} onClose={closeConfirmation}>
        <div className="space-y-4 text-sm [overflow-wrap:anywhere]">
          <dl className="rounded-xl border border-[var(--line)] p-4 space-y-2">
            <div><dt className="muted text-xs">Lịch hẹn</dt><dd className="font-semibold">{confirmation.appointment.typeName} · {confirmation.appointment.code}</dd></div>
            <div><dt className="muted text-xs">Nhà cung cấp</dt><dd>{confirmation.appointment.vendorName}</dd></div>
            <div><dt className="muted text-xs">Giờ hẹn hiện tại</dt><dd>{vnDateTime(confirmation.appointment.startAt)}</dd></div>
            {confirmation.appointment.proposedStartAt && <div><dt className="muted text-xs">Giờ đang được đề xuất</dt><dd>{vnDateTime(confirmation.appointment.proposedStartAt)}</dd></div>}
          </dl>
          <p>{confirmation.operation === "decline-proposal" ? "Thao tác này hủy toàn bộ lịch hẹn, không chỉ từ chối giờ mới. Giờ hẹn cũ cũng không được giữ lại." : "Bạn có chắc muốn hủy toàn bộ lịch hẹn này?"}</p>
          <p className="text-xs muted">Tiền cọc (nếu có) được xử lý theo điều kiện hủy hiện hành. Xác nhận này không tự thực hiện hoàn tiền.</p>
          {(confirmationProblem || actionError || act.isError) && <p role="alert" className="rounded-xl bg-red-50 p-3 text-red-700">{confirmationProblem || actionError || apiError(act.error)}</p>}
          {(isError || isFetching) && <button type="button" className="btn btn-outline" disabled={busy || isFetching} onClick={() => void refetch()}>Tải lại lịch hẹn</button>}
          <div className="flex flex-wrap justify-end gap-3 border-t border-[var(--line)] pt-4">
            <button type="button" autoFocus className="btn btn-outline" disabled={busy} onClick={closeConfirmation}>Quay lại</button>
            <button type="button" className="btn btn-primary" disabled={busy || !!confirmationProblem} onClick={() => submit(confirmation.appointment, confirmation.operation, confirmation.operation === "decline-proposal" ? "Không phù hợp giờ đề xuất" : "Khách hủy", undefined, confirmation.actorId)}>{busy ? "Đang gửi..." : confirmation.operation === "decline-proposal" ? "Xác nhận từ chối và hủy lịch" : "Xác nhận hủy lịch"}</button>
          </div>
        </div>
      </ComboDialog>}
    </Panel>
  );
}
