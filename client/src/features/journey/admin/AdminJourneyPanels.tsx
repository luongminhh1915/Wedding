import { useState } from "react";
import {
  BadgeCheck, CalendarClock, FileCheck2, Gavel, PackageCheck, Receipt, Store, Users,
  User, UserCheck, Mail, Phone, MapPin, Calendar, Clock, Info, X
} from "lucide-react";
import {
  apiError, useAdminKpi, useAuditLogs, useDisputes, usePaySettlement, useResolveDispute,
  useReviewVerification, useRunSettlement, useSettlements, useVerifications,
} from "../api";
import type { AuditAccountInfo, AuditLog } from "../api";
import { Empty, ErrorText, Panel, StatusPill } from "../ui";
import { shortVnd, vnd, vnDate, vnDateTime } from "../format";
import { useAuthStore } from "../../../store/authStore";
import { AdminQueryReadState } from "./AdminQueryReadState";
import { VerificationDocumentLink } from "../VerificationDocumentLink";

/** P6 – KPI vận hành (GMV, tỷ lệ chuyển đổi, SLA, tranh chấp...). */
export function AdminKpiPanel() {
  const query = useAdminKpi();
  const k = query.data;
  const role = useAuthStore((s) => s.user?.role);
  const canReadAudit = role === "SuperAdmin" || role === "Finance";
  const audit = useAuditLogs(canReadAudit);
  const logs = audit.data ?? [];
  const [selectedLog, setSelectedLog] = useState<AuditLog | null>(null);
  const [selectedAccount, setSelectedAccount] = useState<AuditAccountInfo | null>(null);
  if (query.isPending || query.isError) return <AdminQueryReadState query={query} label="tổng quan vận hành" />;
  if (!k) return <Empty>Chưa có dữ liệu tổng quan vận hành.</Empty>;
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
      {canReadAudit && (audit.isPending || audit.isError) && <Panel eyebrow="AuditLog" title="Nhật ký thao tác gần đây">
        <AdminQueryReadState query={audit} label="nhật ký thao tác" />
      </Panel>}
      {canReadAudit && !audit.isPending && !audit.isError && logs.length > 0 && (
        <Panel eyebrow="AuditLog" title="Nhật ký thao tác gần đây">
          <div className="overflow-x-auto">
            <table className="wd-table">
              <thead>
                <tr>
                  <th>Thời gian</th>
                  <th>Vai trò</th>
                  <th>Hành động</th>
                  <th>Đối tượng</th>
                  <th>Tài khoản</th>
                  <th className="text-right">Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {logs.slice(0, 15).map((l) => {
                  const targetAcc = l.targetAccount;
                  const accDisplayName = targetAcc?.brandName || targetAcc?.fullName || targetAcc?.loginName;
                  return (
                    <tr key={l.id} className="hover:bg-slate-50/60 transition-colors">
                      <td className="whitespace-nowrap text-xs text-slate-600">{vnDateTime(l.createdAt)}</td>
                      <td>
                        <span className="font-medium text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-700">
                          {l.actorRole ?? "—"}
                        </span>
                      </td>
                      <td>
                        <b className="text-xs font-mono text-slate-800">{l.action}</b>
                      </td>
                      <td>
                        <div className="flex flex-col">
                          <span className="text-xs font-medium text-slate-900">
                            {l.entityType} <span className="font-mono text-muted text-[11px]">#{l.entityId?.slice(0, 8)}</span>
                          </span>
                          {l.entityTitle && (
                            <span className="text-[11px] text-slate-600 line-clamp-1 max-w-[200px]" title={l.entityTitle}>
                              {l.entityTitle}
                            </span>
                          )}
                        </div>
                      </td>
                      <td>
                        {targetAcc ? (
                          <button
                            type="button"
                            className="text-left group flex flex-col cursor-pointer"
                            onClick={() => {
                              setSelectedLog(l);
                              setSelectedAccount(targetAcc);
                            }}
                            title="Bấm để xem chi tiết tài khoản này"
                          >
                            <span className="text-xs font-semibold text-[var(--gold)] group-hover:underline flex items-center gap-1">
                              <User size={12} className="shrink-0" />
                              {accDisplayName}
                              {targetAcc.loginName && (
                                <span className="text-[11px] font-normal text-muted">(@{targetAcc.loginName})</span>
                              )}
                            </span>
                            <span className="text-[11px] text-muted">
                              {targetAcc.email || targetAcc.phoneNumber || targetAcc.role}
                            </span>
                          </button>
                        ) : (
                          <span className="text-xs muted">—</span>
                        )}
                      </td>
                      <td className="text-right">
                        {targetAcc ? (
                          <button
                            type="button"
                            className="btn btn-secondary !py-1 !px-2.5 !text-xs inline-flex items-center gap-1 rounded-lg border hover:border-[var(--gold)] cursor-pointer"
                            onClick={() => {
                              setSelectedLog(l);
                              setSelectedAccount(targetAcc);
                            }}
                            title="Xem chi tiết tài khoản và thao tác"
                          >
                            <UserCheck size={13} className="text-[var(--gold)]" />
                            <span>Xem tài khoản</span>
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="btn btn-secondary !py-1 !px-2.5 !text-xs inline-flex items-center gap-1 rounded-lg border opacity-70 cursor-pointer"
                            onClick={() => {
                              setSelectedLog(l);
                              setSelectedAccount(l.actorAccount ?? null);
                            }}
                            title="Xem chi tiết nhật ký"
                          >
                            <Info size={13} />
                            <span>Chi tiết log</span>
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Panel>
      )}

      {selectedLog && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-950/60 backdrop-blur-sm animate-in fade-in duration-150"
          role="dialog"
          aria-modal="true"
          aria-label="Chi tiết tài khoản và nhật ký thao tác"
          onClick={() => { setSelectedLog(null); setSelectedAccount(null); }}
        >
          <div
            className="bg-white rounded-2xl shadow-2xl max-w-lg w-full overflow-hidden flex flex-col max-h-[90vh] border border-[#eadbd4]"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div className="flex items-center justify-between p-4 sm:p-5 border-b border-slate-100 bg-linear-to-r from-[#fbf8f5] to-white">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-[#fcf6ea] border border-[#eadbd4] flex items-center justify-center text-[var(--gold)] shadow-xs">
                  <UserCheck size={20} />
                </div>
                <div>
                  <h3 className="serif text-lg font-bold text-[#233a42]">
                    {selectedAccount ? (selectedAccount.brandName || selectedAccount.fullName || "Chi tiết tài khoản") : "Chi tiết thao tác nhật ký"}
                  </h3>
                  <p className="text-xs text-muted">
                    Đối tượng: {selectedLog.entityType} #{selectedLog.entityId?.slice(0, 8)}
                  </p>
                </div>
              </div>
              <button
                type="button"
                className="p-1.5 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-lg transition-colors cursor-pointer"
                onClick={() => { setSelectedLog(null); setSelectedAccount(null); }}
                aria-label="Đóng"
              >
                <X size={18} />
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-5 overflow-y-auto space-y-4 text-sm text-[#233a42]">
              {selectedAccount ? (
                <div className="rounded-xl border border-slate-200/80 p-4 bg-slate-50/50 space-y-3">
                  <div className="flex items-start justify-between gap-2 border-b border-slate-200/60 pb-3">
                    <div>
                      <div className="font-bold text-base text-slate-900 flex items-center gap-2">
                        {selectedAccount.fullName}
                        {selectedAccount.brandName && (
                          <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-amber-50 text-amber-800 border border-amber-200">
                            {selectedAccount.brandName}
                          </span>
                        )}
                      </div>
                      {selectedAccount.loginName && (
                        <div className="text-xs font-mono text-muted mt-0.5">
                          Tên đăng nhập: <span className="font-semibold text-slate-800">@{selectedAccount.loginName}</span>
                        </div>
                      )}
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <span className="text-xs font-medium px-2.5 py-0.5 rounded-full bg-blue-50 text-blue-700 border border-blue-200">
                        {selectedAccount.role}
                      </span>
                      <span className={`text-[11px] font-medium px-2 py-0.5 rounded-full ${selectedAccount.isActive ? "bg-emerald-50 text-emerald-700 border border-emerald-200" : "bg-red-50 text-red-700 border border-red-200"}`}>
                        {selectedAccount.isActive ? "● Đang hoạt động" : "● Tạm khóa"}
                      </span>
                    </div>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 text-xs">
                    <div className="flex items-center gap-2">
                      <Mail size={14} className="text-slate-400 shrink-0" />
                      <div>
                        <span className="text-muted block text-[11px]">Email</span>
                        <span className="font-medium text-slate-800">{selectedAccount.email || "—"}</span>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Phone size={14} className="text-slate-400 shrink-0" />
                      <div>
                        <span className="text-muted block text-[11px]">Số điện thoại</span>
                        <span className="font-medium text-slate-800">{selectedAccount.phoneNumber || "—"}</span>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Calendar size={14} className="text-slate-400 shrink-0" />
                      <div>
                        <span className="text-muted block text-[11px]">Ngày tạo tài khoản</span>
                        <span className="font-medium text-slate-800">{selectedAccount.createdAt ? vnDateTime(selectedAccount.createdAt) : "—"}</span>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <Clock size={14} className="text-slate-400 shrink-0" />
                      <div>
                        <span className="text-muted block text-[11px]">Đăng nhập lần cuối</span>
                        <span className="font-medium text-slate-800">{selectedAccount.lastLoginAt ? vnDateTime(selectedAccount.lastLoginAt) : "Chưa ghi nhận"}</span>
                      </div>
                    </div>
                    {(selectedAccount.city || selectedAccount.address) && (
                      <div className="flex items-center gap-2 sm:col-span-2">
                        <MapPin size={14} className="text-slate-400 shrink-0" />
                        <div>
                          <span className="text-muted block text-[11px]">Địa chỉ / Khu vực</span>
                          <span className="font-medium text-slate-800">{[selectedAccount.address, selectedAccount.city].filter(Boolean).join(", ")}</span>
                        </div>
                      </div>
                    )}
                    <div className="sm:col-span-2 text-[11px] text-muted">
                      User ID: <code className="font-mono text-slate-700 select-all">{selectedAccount.userId}</code>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="rounded-xl border border-dashed border-slate-200 p-4 text-center text-xs text-muted">
                  Không tìm thấy tài khoản người dùng trực tiếp liên kết với đối tượng này.
                </div>
              )}

              {/* Thông tin thao tác */}
              <div className="rounded-xl border border-slate-200/80 p-3.5 bg-white space-y-2">
                <div className="text-xs font-semibold text-slate-700 uppercase tracking-wide">
                  Chi tiết thao tác nhật ký
                </div>
                <div className="grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <span className="text-muted block text-[11px]">Hành động</span>
                    <span className="font-semibold text-slate-900">{selectedLog.action}</span>
                  </div>
                  <div>
                    <span className="text-muted block text-[11px]">Thời gian ghi nhận</span>
                    <span className="font-medium text-slate-800">{vnDateTime(selectedLog.createdAt)}</span>
                  </div>
                  <div>
                    <span className="text-muted block text-[11px]">Người thao tác</span>
                    <span className="font-medium text-slate-800">{selectedLog.actorRole ?? "Hệ thống"} {selectedLog.actorAccount ? `(${selectedLog.actorAccount.fullName || selectedLog.actorAccount.loginName})` : ""}</span>
                  </div>
                  <div>
                    <span className="text-muted block text-[11px]">Loại đối tượng</span>
                    <span className="font-medium text-slate-800">{selectedLog.entityType}</span>
                  </div>
                  {selectedLog.entityTitle && (
                    <div className="col-span-2">
                      <span className="text-muted block text-[11px]">Tên / Tiêu đề đối tượng</span>
                      <span className="font-medium text-slate-900">{selectedLog.entityTitle}</span>
                    </div>
                  )}
                  <div className="col-span-2 text-[11px] text-muted">
                    Entity ID: <code className="font-mono text-slate-700 select-all">{selectedLog.entityId ?? "—"}</code>
                  </div>
                </div>

                {selectedLog.dataJson && (
                  <div className="mt-2 pt-2 border-t border-slate-100">
                    <span className="text-muted block text-[11px] mb-1">Dữ liệu thao tác (JSON):</span>
                    <pre className="text-[11px] font-mono bg-slate-50 border border-slate-200 rounded-lg p-2.5 overflow-x-auto max-h-36">
                      {(() => {
                        try {
                          return JSON.stringify(JSON.parse(selectedLog.dataJson), null, 2);
                        } catch {
                          return selectedLog.dataJson;
                        }
                      })()}
                    </pre>
                  </div>
                )}
              </div>
            </div>

            {/* Modal Footer */}
            <div className="p-4 border-t border-slate-100 bg-slate-50 flex justify-end">
              <button
                type="button"
                className="btn btn-secondary !py-1.5 !px-4 text-xs font-medium cursor-pointer"
                onClick={() => { setSelectedLog(null); setSelectedAccount(null); }}
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/** P4 – Finance: chốt bảng kê tháng, xem QR thu hoa hồng, ghi nhận thanh toán (mock webhook) + hóa đơn điện tử giả lập. */
export function SettlementsPanel() {
  const [period, setPeriod] = useState(() => new Date().toISOString().slice(0, 7));
  const query = useSettlements(period);
  const data = query.data ?? [];
  const run = useRunSettlement();
  const pay = usePaySettlement();
  return (
    <Panel eyebrow="Đối soát BR-007" title={`Bảng kê hoa hồng kỳ ${period}`}
      action={<div className="flex gap-2"><input className="input !w-[150px]" type="month" value={period} onChange={(e) => setPeriod(e.target.value)} aria-label="Kỳ đối soát" />
        <button className="btn btn-primary" disabled={run.isPending} onClick={() => run.mutate(period)}>Chốt bảng kê</button></div>}>
      <ErrorText>{run.isError ? apiError(run.error) : pay.isError ? apiError(pay.error) : ""}</ErrorText>
      {query.isPending || query.isError ? <AdminQueryReadState query={query} label="bảng kê" /> : data.length === 0 ? <Empty>Chưa có bảng kê cho kỳ này. Bấm “Chốt bảng kê” để gom hoa hồng đến hạn ngày 25.</Empty> : (
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
  const query = useVerifications();
  const data = query.data ?? [];
  const review = useReviewVerification();
  return (
    <Panel eyebrow="Xác minh NCC" title="Hồ sơ chờ duyệt">
      <ErrorText>{review.isError ? apiError(review.error) : ""}</ErrorText>
      {query.isPending || query.isError ? <AdminQueryReadState query={query} label="hồ sơ xác minh" /> : data.length === 0 ? <Empty>Không có hồ sơ.</Empty> : (
        <table className="wd-table">
          <thead><tr><th>NCC</th><th>GPKD</th><th>Giấy tờ</th><th>Nộp</th><th>Trạng thái</th><th /></tr></thead>
          <tbody>
            {data.map((v) => (
              <tr key={v.id}>
                <td><b>{v.vendorName}</b></td><td>{v.businessLicenseNumber}</td>
                <td className="text-xs">{v.documentUrls.map((u, i) => <VerificationDocumentLink key={u} reference={u} vendorId={v.vendorId} className="underline mr-2">Tệp {i + 1}</VerificationDocumentLink>)}</td>
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
  const query = useDisputes();
  const data = query.data ?? [];
  const resolve = useResolveDispute();
  const role = useAuthStore((s) => s.user?.role);
  return (
    <Panel eyebrow="CSKH" title="Tranh chấp & sự cố">
      <ErrorText>{resolve.isError ? apiError(resolve.error) : ""}</ErrorText>
      {query.isPending || query.isError ? <AdminQueryReadState query={query} label="tranh chấp" /> : data.length === 0 ? <Empty>Không có tranh chấp.</Empty> : (
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
