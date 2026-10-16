import { useState } from "react";
import { LifeBuoy } from "lucide-react";
import { apiError, useMyContractsLite, useMyDisputes, useOpenDispute } from "../api";
import { ErrorText, Panel, StatusPill } from "../ui";
import { vnDateTime } from "../format";

/** P6 – Khách báo sự cố/tranh chấp hợp đồng; CSKH xử lý trong 72 giờ. */
export function CustomerDisputes() {
  const { data: contracts = [] } = useMyContractsLite();
  const { data: disputes = [] } = useMyDisputes();
  const open = useOpenDispute();
  const [contractId, setContractId] = useState("");
  const [reason, setReason] = useState("");
  const eligible = contracts.filter((c) => ["Confirmed", "Completed", "PendingVerification"].includes(c.status));
  return (
    <Panel eyebrow="Hỗ trợ" title="Báo sự cố với hợp đồng">
      {disputes.length > 0 && (
        <ul className="grid gap-2 mb-4">
          {disputes.map((d) => (
            <li key={d.id} className="rounded-xl border border-[var(--line)] p-3 text-sm bg-[var(--paper)]">
              <p className="font-bold">{d.code} · HĐ {d.contractCode} <StatusPill status={d.status} /></p>
              <p>{d.reason}</p>
              <p className="text-xs muted">Hạn xử lý: {vnDateTime(d.slaDeadline)}{d.resolution ? ` · Kết luận: ${d.resolution}` : ""}</p>
            </li>
          ))}
        </ul>
      )}
      {eligible.length === 0 ? <p className="text-sm muted">Chưa có hợp đồng nào để báo sự cố.</p> : (
        <div className="grid md:grid-cols-[240px_1fr_auto] gap-2 items-end">
          <label><span className="label">Hợp đồng</span>
            <select className="input" value={contractId} onChange={(e) => setContractId(e.target.value)}>
              <option value="">— Chọn —</option>
              {eligible.map((c) => <option key={c.id} value={c.id}>{c.contractCode} · {c.vendorBrandName}</option>)}
            </select>
          </label>
          <label><span className="label">Mô tả sự cố</span><input className="input" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Tối thiểu 10 ký tự" /></label>
          <button className="btn btn-outline" disabled={!contractId || open.isPending} onClick={() => open.mutate({ contractId, reason }, { onSuccess: () => { setReason(""); setContractId(""); } })}><LifeBuoy size={14} /> Gửi CSKH</button>
        </div>
      )}
      <ErrorText>{open.isError ? apiError(open.error) : ""}</ErrorText>
    </Panel>
  );
}
