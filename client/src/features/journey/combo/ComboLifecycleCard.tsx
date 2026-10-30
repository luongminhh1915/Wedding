import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import { apiError } from "../api";
import { useComboMutation, type CatalogTemplate } from "../comboApi";
import { comboDate, comboPreviewPath, comboReviewPayload } from "./comboTemplateEditorModel";
import { ComboPhoto } from "./ComboPhoto";
import { comboVnd } from "./FixedComboServicePicker";
import { ApprovalConfirmationDialog, type ApprovalConfirmationDetails } from "../../admin/ApprovalConfirmationDialog";

const labels: Record<string, string> = { Draft: "Nháp", Pending: "Chờ kiểm duyệt", Approved: "Đã duyệt", Rejected: "Bị từ chối", Superseded: "Đã thay thế", Archived: "Đã ẩn" };
export function ComboLifecycleCard({ template, onEdit, staff = false }: { template: CatalogTemplate; onEdit: () => void; staff?: boolean }) {
  const latest = template.latestRevision;
  const proposal = latest?.proposal;
  const pricing = proposal && ["Draft", "Pending", "Rejected"].includes(latest?.status ?? "") ? proposal : template;
  const originalPrice = pricing.originalPrice ?? (pricing.components?.length ? pricing.components.reduce((total, item) => total + item.basePrice, 0) : null);
  const savings = originalPrice != null && pricing.comboPrice != null && pricing.comboPrice < originalPrice ? originalPrice - pricing.comboPrice : 0;
  const [noteState, setNote] = useState({ revisionId: latest?.id, value: "" });
  const [reviewFailure, setReviewFailure] = useState("");
  const [approval, setApproval] = useState<{ payload: ReturnType<typeof comboReviewPayload>; templateId: string; contentSignature: string; details: ApprovalConfirmationDetails } | null>(null);
  const reviewLock = useRef(false);
  const note = noteState.revisionId === latest?.id ? noteState.value : "";
  const review = useComboMutation(`/api/combos/templates/${template.id}/review`);
  const submit = useComboMutation(`/api/combos/templates/${template.id}/submit`);
  const activity = useComboMutation(`/api/combos/templates/${template.id}/activity`);
  const busy = review.isPending || submit.isPending || activity.isPending;
  const pending = latest ? latest.status === "Pending" : template.moderationStatus === "Pending";
  const revisionLabel = latest ? `phiên bản #${latest.sequence}` : "bản cũ chưa có lịch sử phiên bản";
  const sendReview = (payload: ReturnType<typeof comboReviewPayload>) => {
    if (busy || reviewLock.current) return;
    reviewLock.current = true;
    review.mutate(payload, { onSuccess: () => setApproval(null), onSettled: () => { reviewLock.current = false; } });
  };
  const reviewLatest = (approve: boolean) => {
    if (!staff || busy || reviewLock.current) return;
    setReviewFailure("");
    try {
      const payload = comboReviewPayload(template, approve, note);
      if (approve) {
        if (!template.ownerVendorId) throw new Error("Combo chưa gắn nhà cung cấp. Hãy tải lại và kiểm tra trước khi duyệt.");
        review.reset();
        setApproval({ payload, templateId: template.id, contentSignature: JSON.stringify(template), details: { typeLabel: "Combo NCC", title: proposal?.name ?? template.name, vendorName: template.ownerVendorName, versionLabel: revisionLabel } });
      } else sendReview(payload);
    }
    catch (error) { setReviewFailure(error instanceof Error ? error.message : "Chưa gửi được kết quả duyệt."); }
  };
  const confirmApproval = () => {
    if (!approval || !staff || busy || reviewLock.current) return;
    try {
      const current = comboReviewPayload(template, true, note);
      if (!template.ownerVendorId || template.id !== approval.templateId || JSON.stringify(template) !== approval.contentSignature || JSON.stringify(current) !== JSON.stringify(approval.payload)) throw new Error("Combo hoặc ghi chú đã thay đổi. Hãy xem lại đúng phiên bản trước khi duyệt.");
      sendReview(approval.payload);
    } catch (error) {
      setApproval(null);
      setReviewFailure(error instanceof Error ? error.message : "Hãy tải lại combo trước khi duyệt.");
    }
  };
  return <article className="min-w-0 [overflow-wrap:anywhere] rounded-2xl border border-[#dedccf] bg-white p-4 sm:p-5">
    <div className="flex min-w-0 items-start gap-3">
      <div className="h-20 w-20 shrink-0 overflow-hidden rounded-xl"><ComboPhoto sources={[proposal?.imageUrl, template.imageUrl, ...(proposal?.content?.galleryUrls ?? template.content?.galleryUrls ?? [])]} alt={`Ảnh combo ${proposal?.name ?? template.name}`} /></div>
      <div className="min-w-0 flex-1">
        <h3 className="break-words font-semibold">{proposal?.name ?? template.name}</h3>
        <p className="mt-1 text-xs muted">{template.ownerVendorName || "Chưa gắn NCC"} · {(proposal?.items ?? template.items).length} hạng mục</p>
        <p className="mt-2 text-xs">Bản công bố: {labels[template.moderationStatus] ?? template.moderationStatus} · {template.isActive ? "Đang kinh doanh" : "Đang tắt"}</p>
        {latest && <p className="mt-1 text-xs">Đề xuất #{latest.sequence}: <b>{labels[latest.status] ?? latest.status}</b> · {comboDate(latest.createdAt)}</p>}
      </div>
    </div>
    {pricing.pricingMode === "FixedPrice" && <dl className="mt-4 grid gap-3 rounded-xl bg-[#fcf6ea] p-3 text-sm sm:grid-cols-3"><div><dt className="text-xs muted">Tổng giá bán lẻ</dt><dd className="mt-1 font-semibold">{originalPrice == null ? "Chưa có snapshot giá" : comboVnd(originalPrice)}</dd></div><div><dt className="text-xs muted">Giá combo</dt><dd className="mt-1 font-semibold">{pricing.comboPrice == null ? "Chưa nhập giá" : comboVnd(pricing.comboPrice)}</dd></div>{savings > 0 && originalPrice != null && <div><dt className="text-xs muted">Tiết kiệm</dt><dd className="mt-1 font-semibold text-emerald-700">{comboVnd(savings)} ({(savings / originalPrice * 100).toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%)</dd></div>}</dl>}
    {template.requiresComponentReview && <p className="mt-3 rounded-xl bg-amber-50 p-3 text-xs leading-relaxed text-amber-900">Dịch vụ thành phần đã thay đổi hoặc không còn đủ điều kiện. Combo đang được giữ khỏi trang công khai; rà soát thành phần và gửi phiên bản mới để duyệt. Giá và giao dịch đã chốt được giữ nguyên.</p>}
    {!template.ownerVendorId && <p className="mt-3 rounded-xl bg-amber-50 p-3 text-xs text-amber-900">Dữ liệu cũ chưa gắn một NCC nên không được đặt mới. Hãy cấu hình lại đề xuất từ các dịch vụ của một NCC; giao dịch lịch sử vẫn giữ nguyên.</p>}
    {template.moderationStatus === "Approved" && latest && ["Draft", "Pending", "Rejected"].includes(latest.status) && <p className="mt-3 rounded-xl bg-[#fcf6ea] p-3 text-xs leading-relaxed">Bản công bố “{template.name}” được giữ nguyên. Khách chỉ thấy bản này nếu đang kinh doanh, còn thời hạn và dịch vụ thành phần vẫn đủ điều kiện. Đề xuất mới chưa thay nội dung công khai cho đến khi được duyệt.</p>}
    {latest?.reviewNote && <p className="mt-3 whitespace-pre-wrap break-words rounded-xl border border-[#dedccf] bg-[#fcf6ea] p-3 text-xs"><b>Phản hồi kiểm duyệt:</b> {latest.reviewNote}</p>}
    <div className="mt-4 flex flex-wrap gap-2">
      <button type="button" className="btn btn-outline" disabled={busy} onClick={onEdit}>Sửa đề xuất</button>
      <Link className="btn btn-outline" to={comboPreviewPath(template.id, latest?.id)}>Xem trước chỉ đọc</Link>
      {template.moderationStatus === "Approved" && template.isActive && <Link className="btn btn-outline" to={`/combo/${template.id}`}>Xem bản công khai</Link>}
      {latest && ["Draft", "Rejected"].includes(latest.status) && <button type="button" className="btn btn-primary" disabled={busy} onClick={() => submit.mutate({})}>{latest.status === "Draft" ? "Gửi duyệt" : "Gửi duyệt lại"}</button>}
      {template.moderationStatus === "Approved" && <button type="button" className="btn btn-outline" disabled={busy} onClick={() => activity.mutate({ isActive: !template.isActive })}>{template.isActive ? "Tạm ngừng kinh doanh" : "Kích hoạt kinh doanh"}</button>}
    </div>
    {staff && pending && <section className="mt-4 rounded-xl border border-[#d4c894] bg-[#fcf6ea] p-4" aria-label={`Duyệt đề xuất combo ${revisionLabel}`}>
      <p className="text-xs leading-relaxed">Đang xử lý {revisionLabel}. Hãy mở bản xem trước trước khi quyết định. Máy chủ từ chối duyệt nếu NCC đã gửi phiên bản khác.</p>
      <label className="mt-3 block"><span className="label">Ghi chú / lý do từ chối (bắt buộc khi từ chối)</span><textarea className="input" rows={2} maxLength={2000} value={note} onChange={event => setNote({ revisionId: latest?.id, value: event.target.value })} /></label>
      <div className="mt-3 flex flex-wrap gap-2">
        <button type="button" className="btn btn-primary" disabled={busy || !template.ownerVendorId} onClick={() => reviewLatest(true)}>Phê duyệt {revisionLabel}</button>
        <button type="button" className="btn btn-outline" disabled={busy || !note.trim()} onClick={() => reviewLatest(false)}>Từ chối kèm lý do</button>
      </div>
    </section>}
    {(reviewFailure || review.isError || submit.isError || activity.isError) && <p role="alert" className="mt-3 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{reviewFailure || apiError(review.error ?? submit.error ?? activity.error)}</p>}
    {approval && staff && <ApprovalConfirmationDialog details={approval.details} busy={busy} error={review.isError ? apiError(review.error) : undefined}
      onCancel={() => { if (!busy && !reviewLock.current) setApproval(null); }} onConfirm={confirmApproval} />}
    <details className="mt-4 text-xs">
      <summary className="cursor-pointer font-semibold">Lịch sử phiên bản ({template.revisionHistory?.length ?? 0})</summary>
      <ul className="mt-3 space-y-3">{(template.revisionHistory ?? []).map(revision => <li key={revision.id} className="border-l-2 border-[#d9cfad] pl-3">
        <p><b>#{revision.sequence} · {labels[revision.status] ?? revision.status}</b> · {comboDate(revision.createdAt)}</p>
        <p className="mt-1 muted">Tạo bởi {revision.createdByRole || "Chưa xác định"}{revision.submittedAt ? ` · Gửi duyệt ${comboDate(revision.submittedAt)}` : ""}{revision.reviewedAt ? ` · Xử lý ${comboDate(revision.reviewedAt)} bởi ${revision.reviewedByRole || "Chưa xác định"}` : ""}</p>
        {revision.supersededAt && <p className="mt-1 muted">Thay thế lúc {comboDate(revision.supersededAt)}{revision.supersededFromStatus ? ` · trạng thái trước đó: ${labels[revision.supersededFromStatus] ?? revision.supersededFromStatus}` : ""}</p>}
        {revision.reviewNote && <p className="mt-1 whitespace-pre-wrap break-words">{revision.reviewNote}</p>}
        <Link className="mt-1 inline-block underline" to={comboPreviewPath(template.id, revision.id)}>Xem phiên bản chỉ đọc</Link>
      </li>)}</ul>
      {!template.revisionHistory?.length && <p className="mt-2 muted">Chưa có lịch sử phiên bản được ghi nhận. Không tự tạo lịch sử cho dữ liệu cũ.</p>}
    </details>
  </article>;
}
