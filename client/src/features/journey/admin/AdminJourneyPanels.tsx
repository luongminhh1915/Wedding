import { useState } from "react";
import { BadgeCheck, CalendarClock, FileCheck2, Gavel, PackageCheck, Receipt, Store, Users } from "lucide-react";
import {
  apiError, useAdminKpi, useAuditLogs, useDisputes, usePaySettlement, useResolveDispute, useReviewVerification, useRunSettlement, useSettlements, useVerifications,
} from "../api";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { shortVnd, vnd, vnDate, vnDateTime } from "../format";
import { useAuthStore } from "../../../store/authStore";

/** P6 – KPI vận hành (GMV, tỷ lệ chuyển đổi, SLA, tranh chấp...). */
export function AdminKpiPanel() {
  const { data: k } = useAdminKpi();
  const role = useAuthStore((s) => s.user?.role);
  const { data: logs = [] } = useAuditLogs(role === "SuperAdmin" || role === "Finance");
  if (!k) return <p className="muted text-sm">Đang tải...</p>;
  const cards = [
    { icon: Receipt, label: "GMV hợp đồng", value: shortVnd(k.gmv), sub: `${k.contractsConfirmed} HĐ hiệu lực` },
    { icon: FileCheck2, label: "Lead tháng này", value: k.leadsThisMonth, sub: `Chuyển đổi ${k.leadToContractRate}% · ${k.leadsSlaBreached} trễ SLA` },
    { icon: Receipt, label: "Hoa hồng chờ thu", value: shortVnd(k.commissionPending), sub: `Đã thu ${shortVnd(k.commissionPaid)}` },
    { icon: Store, label: "Nhà cung cấp", value: k.vendors, sub: `${k.verifiedVendors} đã xác minh · ${k.pendingVerifications} chờ duyệt` },
    { icon: Users, label: "Cặp đôi", value: k.customers, sub: "tài khoản Customer" },
    { icon: PackageCheck, label: "Đơn combo", value: k.comboOrders, sub: "tổng đã gửi" },
    { icon: CalendarClock, label: "Lịch hẹn sắp tới", value: k.upcomingAppointments, sub: "đã xác nhận" },
    { icon: Gavel, label: "Tranh chấp mở", value: k.openDisputes, sub: "SLA 72 giờ" },
  ];
  return (
    <div className="wd">
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3 mb-6">
        {cards.map(({ icon: Icon, label, value, sub }) => (
          <div key={label} className="card p-4">
            <Icon size={16} className="text-[var(--gold)]" />
            <p className="eyebrow muted mt-2">{label}</p>
            <p className="serif text-2xl font-bold">{value}</p>
            <p className="text-xs muted">{sub}</p>
          </div>
        ))}
      </div>
      {logs.length > 0 && (
        <Panel eyebrow="AuditLog" title="Nhật ký thao tác gần đây">
          <table className="wd-table">
            <thead><tr><th>Thời gian</th><th>Vai trò</th><th>Hành động</th><th>Đối tượng</th></tr></thead>
            <tbody>{logs.slice(0, 12).map((l) => <tr key={l.id}><td>{vnDateTime(l.createdAt)}</td><td>{l.actorRole ?? "—"}</td><td><b>{l.action}</b></td><td className="text-xs muted">{l.entityType} {l.entityId?.slice(0, 8)}</td></tr>)}</tbody>
          </table>
        </Panel>
      )}
    </div>
  );
}

/** P4 – Finance: chốt bảng kê tháng, xem QR thu hoa hồng, ghi nhận thanh toán (mock webhook) + hóa đơn điện tử giả lập. */
export function SettlementsPanel() {
  const [period, setPeriod] = useState(() => new Date().toISOString().slice(0, 7));
  const { data = [] } = useSettlements(period);
  const run = useRunSettlement();
  const pay = usePaySettlement();
  return (
    <Panel eyebrow="Đối soát BR-007" title={`Bảng kê hoa hồng kỳ ${period}`}
      action={<div className="flex gap-2"><input className="input !w-[150px]" type="month" value={period} onChange={(e) => setPeriod(e.target.value)} aria-label="Kỳ đối soát" />
        <button className="btn btn-primary" disabled={run.isPending} onClick={() => run.mutate(period)}>Chốt bảng kê</button></div>}>
      <ErrorText>{run.isError ? apiError(run.error) : pay.isError ? apiError(pay.error) : ""}</ErrorText>
      {data.length === 0 ? <Empty>Chưa có bảng kê cho kỳ này. Bấm “Chốt bảng kê” để gom hoa hồng đến hạn ngày 25.</Empty> : (
        <table className="wd-table">
          <thead><tr><th>Nhà cung cấp</th><th>Số khoản</th><th>Phải thu</th><th>Đã thu</th><th>Hạn</th><th>Nội dung CK</th><th>Trạng thái</th><th /></tr></thead>
          <tbody>
            {data.map((s) => (
              <tr key={s.id}>
                <td><b>{s.vendorName}</b></td><td>{s.commissionCount}</td><td>{vnd(s.totalAmount)}</td><td>{vnd(s.paidAmount)}</td><td>{vnDate(s.dueDate)}</td>
                <td className="text-xs">{s.paymentReference}{s.invoiceNumber && <div className="muted">HĐĐT: {s.invoiceNumber}</div>}</td>
                <td><StatusPill status={s.status} /></td>
                <td>{(s.status === "Closed" || s.status === "Overdue") && <button className="btn btn-gold !px-3 !py-2" disabled={pay.isPending} onClick={() => pay.mutate(s.id)}>Ghi nhận đã thu</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Panel>
  );
}

/** P6 – Moderator duyệt hồ sơ xác minh NCC. */
export function VerificationQueue() {
  const { data = [] } = useVerifications();
  const review = useReviewVerification();
  return (
    <Panel eyebrow="Xác minh NCC" title="Hồ sơ chờ duyệt">
      <ErrorText>{review.isError ? apiError(review.error) : ""}</ErrorText>
      {data.length === 0 ? <Empty>Không có hồ sơ.</Empty> : (
        <table className="wd-table">
          <thead><tr><th>NCC</th><th>GPKD</th><th>Giấy tờ</th><th>Nộp</th><th>Trạng thái</th><th /></tr></thead>
          <tbody>
            {data.map((v) => (
              <tr key={v.id}>
                <td><b>{v.vendorName}</b></td><td>{v.businessLicenseNumber}</td>
                <td className="text-xs">{v.documentUrls.map((u, i) => <a key={u} href={u} target="_blank" rel="noreferrer" className="underline mr-2">Tệp {i + 1}</a>)}</td>
                <td>{vnDate(v.createdAt)}</td><td><StatusPill status={v.status} /></td>
                <td className="whitespace-nowrap">{v.status === "Submitted" && <>
                  <button className="btn btn-primary !px-3 !py-2" onClick={() => review.mutate({ id: v.id, approve: true, note: "Hồ sơ hợp lệ" })}><BadgeCheck size={13} /> Duyệt</button>{" "}
                  <button className="btn btn-outline !px-3 !py-2" onClick={() => { const n = prompt("Cần bổ sung gì?"); if (n) review.mutate({ id: v.id, approve: false, note: n }); }}>Yêu cầu bổ sung</button>
                </>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </Panel>
  );
}

/** P6 – CSKH xử lý tranh chấp (SLA 72h); hủy HĐ sẽ hủy hoa hồng chưa thu. */
export function DisputeDesk() {
  const { data = [] } = useDisputes();
  const resolve = useResolveDispute();
  const role = useAuthStore((s) => s.user?.role);
  return (
    <Panel eyebrow="CSKH" title="Tranh chấp & sự cố">
      <ErrorText>{resolve.isError ? apiError(resolve.error) : ""}</ErrorText>
      {data.length === 0 ? <Empty>Không có tranh chấp.</Empty> : (
        <ul className="grid gap-3">
          {data.map((d) => (
            <li key={d.id} className="rounded-2xl border border-[var(--line)] p-4 bg-[var(--paper)]">
              <div className="flex flex-wrap justify-between gap-2">
                <p className="font-bold">{d.code} · HĐ {d.contractCode} · mở bởi {d.openedByRole} <StatusPill status={d.status} /></p>
                <p className={`text-xs ${d.slaBreached ? "text-red-700 font-bold" : "muted"}`}>Hạn SLA {vnDateTime(d.slaDeadline)}{d.slaBreached ? " – QUÁ HẠN" : ""}</p>
              </div>
              <p className="text-sm mt-1">{d.reason}</p>
              {d.resolution ? <p className="text-sm mt-2"><b>Kết luận:</b> {d.resolution}</p> : (
                <div className="flex flex-wrap gap-2 mt-3">
                  <button className="btn btn-outline !py-2" onClick={() => { const r = prompt("Kết luận (giữ HĐ, có lợi cho NCC)?"); if (r) resolve.mutate({ id: d.id, inFavorOfCustomer: false, cancelContract: false, resolution: r }); }}>Giữ hợp đồng</button>
                  <button className="btn btn-primary !py-2" onClick={() => { const r = prompt("Kết luận (có lợi cho khách, giữ HĐ)?"); if (r) resolve.mutate({ id: d.id, inFavorOfCustomer: true, cancelContract: false, resolution: r }); }}>Bồi thường – giữ HĐ</button>
                  {(role === "Finance" || role === "SuperAdmin") && <button className="btn btn-gold !py-2" onClick={() => { const r = prompt("Kết luận hủy và chứng từ xác minh cọc thực giữ?"); if (!r) return; const input = prompt("Số cọc NCC thực giữ đã được xác minh (VNĐ; nhập 0 nếu hoàn toàn bộ)?"); if (input === null || input.trim() === "") return; const retainedDeposit = Number(input); if (Number.isFinite(retainedDeposit) && retainedDeposit >= 0) resolve.mutate({ id: d.id, inFavorOfCustomer: true, cancelContract: true, resolution: r, retainedDeposit }); }}>Hủy tài chính (đã xác minh)</button>}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}
