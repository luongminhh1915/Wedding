import { useState } from "react";
import { BadgeCheck, ShieldQuestion } from "lucide-react";
import { apiError, useMyVerifications, useSubmitVerification } from "../api";
import { ErrorText, Panel, StatusPill } from "../ui";
import { vnDate } from "../format";
import { VendorTaxProfileForm } from "../admin/OperationsPanels";

/** P6 – NCC nộp hồ sơ xác minh (GPKD + giấy tờ) để nhận huy hiệu "Đã xác minh". */
export function VendorVerificationPanel() {
  const { data = [] } = useMyVerifications();
  const submit = useSubmitVerification();
  const [license, setLicense] = useState("");
  const [docs, setDocs] = useState("");
  const latest = data[0];
  return (
    <div className="wd">
      <VendorTaxProfileForm />
      <Panel eyebrow="Xác minh đối tác" title="Huy hiệu “Đã xác minh”">
        <p className="text-sm muted mb-4">Gửi giấy tờ để bộ phận phụ trách kiểm tra hồ sơ và cấp huy hiệu. Theo dõi trạng thái, đọc ghi chú và bổ sung khi được yêu cầu; không có thời gian duyệt được bảo đảm.</p>
        {latest && (
          <div className="rounded-2xl border border-[var(--line)] p-4 mb-4 flex items-center gap-3 bg-[var(--paper)]">
            {latest.status === "Approved" ? <BadgeCheck className="text-[var(--gold)]" /> : <ShieldQuestion className="text-[var(--brand-2)]" />}
            <div className="text-sm">
              <p>Hồ sơ GPKD <b>{latest.businessLicenseNumber}</b> nộp ngày {vnDate(latest.createdAt)} <StatusPill status={latest.status} /></p>
              {latest.reviewNote && <p className="muted">Ghi chú: {latest.reviewNote}</p>}
            </div>
          </div>
        )}
        {(!latest || latest.status === "Rejected") && (
          <div className="grid md:grid-cols-2 gap-3">
            <label><span className="label">Số giấy phép kinh doanh / MST</span><input className="input" value={license} onChange={(e) => setLicense(e.target.value)} /></label>
            <label><span className="label">3 link HTTPS: GPKD, giấy tờ đại diện, ảnh cơ sở (mỗi dòng 1 link)</span><textarea className="input" rows={4} value={docs} onChange={(e) => setDocs(e.target.value)} placeholder="https://..." /></label>
            <div className="md:col-span-2 flex justify-end gap-3 items-center">
              <ErrorText>{submit.isError ? apiError(submit.error) : ""}</ErrorText>
              <button className="btn btn-primary" disabled={submit.isPending} onClick={() => submit.mutate({ businessLicenseNumber: license, documentUrls: docs.split("\n").map((s) => s.trim()).filter(Boolean) })}>Nộp hồ sơ</button>
            </div>
          </div>
        )}
      </Panel>
    </div>
  );
}
