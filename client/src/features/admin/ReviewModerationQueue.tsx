import { useState } from "react";
import { Check, Loader2, RefreshCw, X } from "lucide-react";
import { ReviewList } from "../customer/components/reviews/ReviewList";
import { reviewErrorMessage } from "../customer/reviewTransport";
import { reviewEditComparisonComplete, reviewEditSnapshots, reviewTargetLabel, reviewTargetName, reviewTransactionLabel } from "../customer/reviewPresentationModel";
import type { PendingReviewEdit } from "../../types/review.types";
import { comboMediaSources } from "../journey/combo/comboMedia";
import {
  useModerateReviewEdit,
  useModerationReviews,
  usePendingReviewEdits,
} from "../customer/hooks/useReviews";

export default function ReviewModerationQueue() {
  const reviews = useModerationReviews();
  const edits = usePendingReviewEdits();
  const moderateEdit = useModerateReviewEdit();
  const [error, setError] = useState("");
  const [reasonById, setReasonById] = useState<Record<string, string>>({});
  const [busyId, setBusyId] = useState<string | null>(null);
  async function decide(editRequestId: string, approve: boolean) {
    setError("");
    const edit = edits.data?.find(item => item.id === editRequestId);
    if (approve && (!edit || !reviewEditComparisonComplete(edit))) {
      setError("Chưa đọc đủ ảnh và điểm tiêu chí của hai phiên bản. Tải lại trước khi duyệt."); return;
    }
    if (!approve && !reasonById[editRequestId]?.trim()) {
      setError("Nhập lý do từ chối bản chỉnh sửa."); return;
    }
    setBusyId(editRequestId);
    try {
      await moderateEdit.mutateAsync({ editRequestId, approve, reason: reasonById[editRequestId] });
      setReasonById(current => { const next = { ...current }; delete next[editRequestId]; return next; });
      void edits.refetch(); void reviews.refetch();
    } catch (failure) { setError(reviewErrorMessage(failure, "Không thể xử lý phiên bản chỉnh sửa.")); }
    finally { setBusyId(null); }
  }
  return <div className="space-y-8">
    <section className="space-y-3">
      <header className="flex items-center justify-between gap-3">
        <div><h3 className="font-semibold">Đánh giá mới đang chờ duyệt</h3><p className="text-xs text-slate-500">Chỉ nội dung đã duyệt mới được công khai và tính vào target tương ứng.</p></div>
        <button className="rounded-lg border p-2 disabled:opacity-50" disabled={reviews.isFetching} aria-label="Tải lại đánh giá" onClick={() => void reviews.refetch()}><RefreshCw size={15} /></button>
      </header>
      {reviews.isPending && <p role="status" className="flex items-center gap-2 text-sm text-slate-500"><Loader2 size={15} className="animate-spin" />Đang tải đánh giá mới...</p>}
      {reviews.isFetching && !reviews.isPending && <p role="status" className="text-xs text-slate-500">Đang cập nhật hàng chờ đánh giá...</p>}
      {reviews.isError && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-800">{reviewErrorMessage(reviews.error)}</p>}
      {reviews.isSuccess && reviews.data.length === 0 && <p className="text-sm text-slate-500">Không có đánh giá mới đang chờ.</p>}
      {reviews.isSuccess && reviews.data.map(review => <section key={review.id} className="space-y-2" aria-label={`${reviewTargetLabel(review.targetType)}: ${reviewTargetName(review)}`}>
        <div className="rounded-xl border bg-slate-50 p-3">
          <h4 className="font-semibold">{reviewTargetLabel(review.targetType)}: {reviewTargetName(review)}</h4>
          <p className="mt-1 break-words text-xs text-slate-600">Shop: {review.vendorBrandName?.trim() || "Chưa có tên"} · Khách hàng: {review.customerName?.trim() || "Chưa có tên"}</p>
          <p className="mt-1 break-words text-xs text-slate-500">{reviewTransactionLabel(review)} · Mã đánh giá {review.id}</p>
        </div>
        <ReviewList reviews={[review]} isFetching={reviews.isFetching} onRefresh={() => void reviews.refetch()} />
      </section>)}
    </section>
    <section className="space-y-3">
      <header className="flex items-center justify-between gap-3">
        <div><h3 className="font-semibold">Yêu cầu chỉnh sửa đang chờ</h3><p className="text-xs text-slate-500">Bản duyệt hiện hành vẫn được công khai cho tới khi chấp nhận bản sửa.</p></div>
        <button className="rounded-lg border p-2 disabled:opacity-50" disabled={edits.isFetching} aria-label="Tải lại yêu cầu chỉnh sửa" onClick={() => void edits.refetch()}><RefreshCw size={15} /></button>
      </header>
      {edits.isError && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-800">{reviewErrorMessage(edits.error)}</p>}
      {edits.isPending && <p role="status" className="flex items-center gap-2 text-sm text-slate-500"><Loader2 size={15} className="animate-spin" />Đang tải...</p>}
      {edits.isSuccess && edits.data.length === 0 && <p className="text-sm text-slate-500">Không có yêu cầu chỉnh sửa đang chờ.</p>}
      {error && <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm text-red-800">{error}</p>}
      {edits.isSuccess && edits.data.map(edit => <article key={edit.id} className="space-y-3 rounded-xl border bg-white p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div><strong>{reviewTargetLabel(edit.review.targetType)}: {reviewTargetName(edit.review)}</strong>
            <p className="mt-1 break-words text-xs text-slate-600">Shop: {edit.review.vendorBrandName?.trim() || "Chưa có tên"} · Khách hàng: {edit.review.customerName?.trim() || "Chưa có tên"}</p>
            <p className="mt-1 break-words text-xs text-slate-500">{reviewTransactionLabel(edit.review)} · Lần gửi {edit.attemptNumber} · {new Date(edit.requestedAt).toLocaleString("vi-VN")}</p></div>
          <span className="rounded-full bg-amber-50 px-3 py-1 text-xs text-amber-800">Chờ duyệt</span>
        </div>
        <ReviewEditComparison edit={edit} />
        <label className="block text-xs text-slate-600">Lý do từ chối (bắt buộc nếu từ chối)
          <textarea maxLength={1000} className="mt-1 min-h-16 w-full rounded-lg border p-2" value={reasonById[edit.id] ?? ""}
            onChange={event => setReasonById(current => ({ ...current, [edit.id]: event.target.value }))} />
        </label>
        <div className="flex justify-end gap-2">
          <button className="inline-flex items-center gap-1 rounded-lg border px-3 py-2 text-sm disabled:opacity-50"
            disabled={busyId !== null} onClick={() => void decide(edit.id, false)}><X size={14} />Từ chối</button>
          <button className="inline-flex items-center gap-1 rounded-lg bg-slate-900 px-3 py-2 text-sm text-white disabled:opacity-50"
            disabled={busyId !== null || !reviewEditComparisonComplete(edit)} onClick={() => void decide(edit.id, true)}>{busyId === edit.id ? <Loader2 size={14} className="animate-spin" /> : <Check size={14} />}Duyệt bản sửa</button>
        </div>
      </article>)}
    </section>
  </div>;
}

export function ReviewEditComparison({ edit }: { edit: PendingReviewEdit }) {
  return <div className="space-y-3">
    {!reviewEditComparisonComplete(edit) && <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-800">Chưa đọc đủ dữ liệu ảnh và điểm tiêu chí của hai phiên bản. Tải lại yêu cầu trước khi duyệt; không suy đoán nội dung còn thiếu.</p>}
    <div className="grid gap-3 md:grid-cols-2">{reviewEditSnapshots(edit).map(snapshot => <section key={snapshot.side}
      aria-label={snapshot.side === "before" ? "Phiên bản đang công khai" : "Phiên bản đề nghị chỉnh sửa"}
      className={`min-w-0 rounded-lg p-3 ${snapshot.side === "before" ? "bg-slate-50" : "bg-amber-50"}`}>
      <h4 className="text-xs font-semibold text-slate-700">{snapshot.side === "before" ? "ĐANG CÔNG KHAI" : "BẢN SỬA"} · {snapshot.rating}/5</h4>
      <p className="mt-2 whitespace-pre-wrap break-words text-sm">{snapshot.content}</p>
      <dl className="mt-3 grid grid-cols-2 gap-2 text-xs">{snapshot.criteria.map(criterion => <div key={criterion.key}>
        <dt className="text-slate-500">{criterion.label}</dt>
        <dd className="mt-1 font-medium">{criterion.value === undefined ? "Chưa có dữ liệu" : criterion.value === null ? "Không chấm" : `${criterion.value}/5`}</dd>
      </div>)}</dl>
      <div className="mt-3 border-t border-slate-200 pt-3 text-xs">
        <h5 className="font-semibold">Ảnh {snapshot.photos.state === "ready" ? `(${snapshot.photos.photos.length})` : ""}</h5>
        {snapshot.photos.state !== "ready" ? <p className="mt-1 text-red-700">{snapshot.photos.state === "unavailable" ? "API chưa cung cấp ảnh của phiên bản này." : "Không đọc được danh sách ảnh của phiên bản này."}</p>
          : snapshot.photos.photos.length === 0 ? <p className="mt-1 text-slate-500">Không có ảnh.</p>
          : <ul className="mt-2 space-y-2">{snapshot.photos.photos.map((photo, index) => {
            const href = photo.href ? comboMediaSources([photo.href])[0] : undefined;
            return <li key={index} className="min-w-0">{href ? <a href={href} target="_blank" rel="noopener noreferrer" className="block break-all underline">
              <img loading="lazy" src={href} alt={`${snapshot.side === "before" ? "Ảnh đang công khai" : "Ảnh bản sửa"} ${index + 1}`} className="mb-1 h-20 w-20 rounded-lg border object-cover" />{photo.reference}
            </a> : <p className="break-all text-red-700">Liên kết ảnh không an toàn, không mở tự động: {photo.reference}</p>}</li>;
          })}</ul>}
      </div>
    </section>)}</div>
  </div>;
}
