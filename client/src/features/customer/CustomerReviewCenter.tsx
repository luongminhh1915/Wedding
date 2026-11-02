import { useEffect, useId, useRef, useState, type FormEvent } from "react";
import { CheckCircle2, Loader2, Star } from "lucide-react";
import type { EligibleReviewTarget, Review } from "../../types/review.types";
import {
  useEligibleReviewTargets,
  useMyReviews,
  useSubmitComboReview,
  useSubmitReview,
  useSubmitReviewEdit,
  useSubmitShopReview,
} from "./hooks/useReviews";
import { reviewErrorMessage } from "./reviewTransport";
import { reviewTargetLabel, reviewTargetName, reviewTextEditPayload, reviewTransactionLabel } from "./reviewPresentationModel";

export default function CustomerReviewCenter() {
  const eligible = useEligibleReviewTargets();
  const mine = useMyReviews();
  const serviceMutation = useSubmitReview();
  const shopMutation = useSubmitShopReview();
  const comboMutation = useSubmitComboReview();
  const editMutation = useSubmitReviewEdit();
  const [selected, setSelected] = useState<EligibleReviewTarget | null>(null);
  const [editing, setEditing] = useState<Review | null>(null);
  const [rating, setRating] = useState(5);
  const [content, setContent] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const dialogRef = useRef<HTMLDialogElement>(null);
  const openerRef = useRef<HTMLElement | null>(null);
  const dialogTitleId = useId();
  const dialogDescriptionId = useId();
  const formOpen = Boolean(selected || editing);
  const busy = serviceMutation.isPending || shopMutation.isPending || comboMutation.isPending || editMutation.isPending;

  useEffect(() => {
    if (!formOpen) return;
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (!dialog.open) dialog.showModal();
    return () => {
      if (dialog.open) dialog.close();
      if (openerRef.current?.isConnected) openerRef.current.focus();
    };
  }, [formOpen]);

  function start(target: EligibleReviewTarget, opener: HTMLElement) {
    openerRef.current = opener;
    setSelected(target); setEditing(null); setRating(5); setContent(""); setError(""); setNotice("");
  }
  function startEdit(review: Review, opener: HTMLElement) {
    openerRef.current = opener;
    setSelected(null); setEditing(review); setRating(review.rating); setContent(review.content); setError(""); setNotice("");
  }
  function closeForm() { setSelected(null); setEditing(null); setError(""); }
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (content.trim().length < 30 || content.trim().length > 3000) {
      setError("Nội dung cần từ 30 đến 3.000 ký tự."); return;
    }
    setError("");
    try {
      if (editing) {
        await editMutation.mutateAsync({ reviewId: editing.id, payload: reviewTextEditPayload(rating, content) });
        setNotice("Đã gửi yêu cầu chỉnh sửa. Bản đánh giá hiện được duyệt vẫn hiển thị trong khi chờ kiểm duyệt.");
      } else if (selected?.targetType === "Service") {
        if (!selected.bookingContractId || !selected.listingId) throw new Error("Thiếu thông tin hợp đồng dịch vụ.");
        await serviceMutation.mutateAsync({ bookingContractId: selected.bookingContractId, listingId: selected.listingId, rating, content: content.trim() });
        setNotice("Đã gửi đánh giá dịch vụ để kiểm duyệt.");
      } else if (selected?.targetType === "Shop") {
        await shopMutation.mutateAsync({
          bookingContractId: selected.bookingContractId ?? undefined,
          comboOrderId: selected.comboOrderId ?? undefined,
          rating, content: content.trim(),
        });
        setNotice("Đã gửi đánh giá Shop để kiểm duyệt.");
      } else if (selected?.targetType === "Combo" && selected.comboOrderId) {
        await comboMutation.mutateAsync({ comboOrderId: selected.comboOrderId, rating, content: content.trim() });
        setNotice("Đã gửi đánh giá Combo để kiểm duyệt.");
      } else {
        throw new Error("Không xác định được giao dịch đủ điều kiện.");
      }
      closeForm();
      void eligible.refetch(); void mine.refetch();
    } catch (failure) { setError(reviewErrorMessage(failure, "Không thể gửi đánh giá.")); }
  }

  return <section className="space-y-6">
    <header>
      <p className="text-xs font-semibold uppercase tracking-widest text-amber-700">TRẢI NGHIỆM THẬT · ĐÁNH GIÁ RIÊNG THEO ĐỐI TƯỢNG</p>
      <h2 className="mt-1 text-2xl font-semibold text-slate-900">Đánh giá Shop, dịch vụ & Combo</h2>
      <p className="mt-2 max-w-3xl text-sm leading-relaxed text-slate-600">
        Chỉ giao dịch có hợp đồng thành phần đã hoàn tất mới đủ điều kiện. Mỗi mục chỉ được đánh giá một lần trong cùng giao dịch.
      </p>
    </header>
    {notice && <p role="status" className="rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">{notice}</p>}
    {eligible.isError && <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800">
      <p>{reviewErrorMessage(eligible.error, "Không tải được giao dịch đủ điều kiện.")}</p>
      <button className="mt-2 underline disabled:opacity-50" disabled={eligible.isFetching} onClick={() => void eligible.refetch()}>Thử lại</button>
    </div>}
    <div className="space-y-3">
      <h3 className="font-semibold text-slate-800">Giao dịch đủ điều kiện</h3>
      {eligible.isPending && <p role="status" className="flex items-center gap-2 text-sm text-slate-500"><Loader2 size={15} className="animate-spin" />Đang kiểm tra giao dịch...</p>}
      {eligible.isSuccess && eligible.data.length === 0 && <p className="rounded-xl border border-dashed p-5 text-sm text-slate-500">
        Chưa có giao dịch hoàn tất nào chưa được đánh giá. Hợp đồng chưa Completed không đủ điều kiện.
      </p>}
      {eligible.isSuccess && eligible.data.map((target, index) => <article key={`${target.targetType}:${target.transactionId}:${target.targetId}:${index}`}
        className="flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-white p-4">
        <div>
          <p className="font-medium text-slate-800">{target.targetName}</p>
          <p className="mt-1 text-xs text-slate-500">
            {reviewTargetLabel(target.targetType)}
            {" · "}Giao dịch {target.transactionCode}
          </p>
        </div>
        <button className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white hover:bg-slate-700"
          onClick={event => start(target, event.currentTarget)}>Viết đánh giá</button>
      </article>)}
    </div>
    <div className="space-y-3">
      <h3 className="font-semibold text-slate-800">Đánh giá của bạn</h3>
      {mine.isPending && <p role="status" className="flex items-center gap-2 text-sm text-slate-500"><Loader2 size={15} className="animate-spin" />Đang tải đánh giá của bạn...</p>}
      {mine.isError && <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800">
        <p>{reviewErrorMessage(mine.error, "Không tải được đánh giá của bạn.")}</p>
        <button className="mt-2 underline disabled:opacity-50" disabled={mine.isFetching} onClick={() => void mine.refetch()}>Thử lại</button>
      </div>}
      {mine.isSuccess && mine.data.length === 0 && <p className="text-sm text-slate-500">Bạn chưa gửi đánh giá nào.</p>}
      {mine.isSuccess && mine.data.map(review => <article key={review.id} className="rounded-xl border bg-white p-4">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p className="font-medium text-slate-800">{reviewTargetName(review)}</p>
            <p className="mt-1 text-xs text-slate-500">{reviewTargetLabel(review.targetType)} · {new Date(review.createdAt).toLocaleDateString("vi-VN")}</p>
            <p className="mt-1 break-words text-xs text-slate-500">{reviewTransactionLabel(review)}</p>
          </div>
          <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${review.status === "Approved" ? "bg-emerald-50 text-emerald-700" : review.status === "Rejected" ? "bg-rose-50 text-rose-700" : "bg-amber-50 text-amber-800"}`}>
            {review.editStatus === "Pending" ? "Bản sửa đang chờ duyệt" : review.editStatus === "Rejected" ? "Bản sửa bị từ chối · bản duyệt cũ vẫn giữ" :
              review.status === "Approved" ? "Đã duyệt" : review.status === "Rejected" ? "Bị từ chối" : "Đang chờ duyệt"}
          </span>
        </div>
        <p className="mt-2 flex items-center gap-1 text-amber-600"><Star size={14} className="fill-current" />{review.rating} / 5</p>
        <p className="mt-2 whitespace-pre-wrap text-sm text-slate-700">{review.content}</p>
        {review.status === "Rejected" && review.rejectionReason?.trim() && <p className="mt-2 text-sm text-rose-700">Lý do từ chối: {review.rejectionReason}</p>}
        {review.canEdit && <button className="mt-3 rounded-lg border px-3 py-2 text-sm hover:bg-slate-50" onClick={event => startEdit(review, event.currentTarget)}>
          Yêu cầu chỉnh sửa ({review.editAttemptCount ?? 0}/3)
        </button>}
        {review.editStatus === "Approved" && <p className="mt-2 flex items-center gap-1 text-xs text-emerald-700"><CheckCircle2 size={14} />Phiên bản chỉnh sửa mới đã được duyệt.</p>}
      </article>)}
    </div>
    {formOpen && <dialog ref={dialogRef} aria-labelledby={dialogTitleId} aria-describedby={dialogDescriptionId}
      className="m-auto max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-xl overflow-y-auto rounded-2xl bg-white p-6 text-slate-900 shadow-xl backdrop:bg-slate-950/50"
      onCancel={event => { event.preventDefault(); if (!busy) closeForm(); }}>
      <form onSubmit={submit} className="space-y-4" aria-busy={busy}>
        <div>
          <h3 id={dialogTitleId} className="text-lg font-semibold">{editing ? "Yêu cầu chỉnh sửa đánh giá" : `Đánh giá ${reviewTargetLabel(selected?.targetType)}`}</h3>
          <p className="mt-1 font-medium">{editing ? reviewTargetName(editing) : selected?.targetName}</p>
          <p id={dialogDescriptionId} className="mt-1 text-sm text-slate-600">{editing ? "Bản đang công khai chỉ thay đổi nếu moderator duyệt bản sửa. Ảnh và bốn điểm tiêu chí hiện có được giữ nguyên; biểu mẫu này chỉ sửa điểm tổng thể và nội dung." : `Giao dịch ${selected?.transactionCode}`}</p>
          {editing && <p className="mt-2 text-xs text-slate-500">Tối đa một bản sửa được duyệt, ba lần gửi yêu cầu trong 7 ngày từ ngày đăng ban đầu.</p>}
        </div>
        <label className="block text-sm font-medium">Điểm đánh giá
          <select autoFocus disabled={busy} className="mt-1 w-full rounded-lg border p-3" value={rating} onChange={event => setRating(Number(event.target.value))}>
            {[5, 4, 3, 2, 1].map(value => <option key={value} value={value}>{value} sao</option>)}
          </select>
        </label>
        <label className="block text-sm font-medium">Nội dung (30–3.000 ký tự)
          <textarea disabled={busy} className="mt-1 min-h-32 w-full rounded-lg border p-3" maxLength={3000} value={content}
            onChange={event => setContent(event.target.value)} required />
        </label>
        {error && <p role="alert" className="text-sm text-red-700">{error}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" className="rounded-lg border px-4 py-2 text-sm" disabled={busy} onClick={closeForm}>Hủy</button>
          <button className="rounded-lg bg-slate-900 px-4 py-2 text-sm font-medium text-white disabled:opacity-50" disabled={busy}>
            {busy ? "Đang gửi..." : editing ? "Gửi yêu cầu sửa" : "Gửi đánh giá"}
          </button>
        </div>
      </form>
    </dialog>}
  </section>;
}
