import { useState } from "react";
import {
  Star,
  CheckCircle2,
  MessageSquare,
  Send,
  ShieldCheck,
  Loader2,
} from "lucide-react";
import type { PublicReview } from "../../../../types/review.types";
import { reviewHistoryLabel } from "./reviewPolicyModel";
import { reviewErrorMessage, reviewReadState } from "../../reviewTransport";
import { canReplyToReview } from "../../reviewCacheModel";
import { useAuth } from "../../../auth/hooks/useAuth";
import {
  useVendorReplyReview,
  useApproveReview,
  useRejectReview,
} from "../../hooks/useReviews";

interface ReviewListProps {
  reviews: PublicReview[];
  emptyMessage?: string;
  vendorId?: string;
  isVendorOwner?: boolean;
  onRefresh?: () => void;
  isLoading?: boolean;
  isFetching?: boolean;
  isError?: boolean;
  error?: unknown;
  onRetry?: () => void;
}

export function ReviewList({
  reviews,
  emptyMessage = "Chưa có đánh giá nào",
  isVendorOwner = false,
  onRefresh,
  isLoading = false,
  isFetching = false,
  isError = false,
  error,
  onRetry,
}: ReviewListProps) {
  const { user } = useAuth();
  const replyMutation = useVendorReplyReview();
  const approveMutation = useApproveReview();
  const rejectMutation = useRejectReview();

  const isModerator = user?.role === "Moderator" || user?.role === "SuperAdmin";

  const [replyingReviewId, setReplyingReviewId] = useState<string | null>(null);
  const [replyText, setReplyText] = useState("");
  const [actionError, setActionError] = useState<{ reviewId: string; action: "reply" | "approve" | "reject"; message: string; actor: string } | null>(null);
  const actor = `${user?.id ?? ""}:${user?.role ?? ""}`;
  const actionPending = replyMutation.isPending || approveMutation.isPending || rejectMutation.isPending;

  const handleSendReply = async (reviewId: string) => {
    const review = reviews.find(item => item.id === reviewId);
    if (!replyText.trim() || !isVendorOwner || !review || !canReplyToReview(user, review)) return;
    setActionError(null);
    try {
      await replyMutation.mutateAsync({ reviewId, reply: replyText.trim() });
      setReplyingReviewId(null);
      setReplyText("");
      if (onRefresh) onRefresh();
    } catch (failure) {
      setActionError({ reviewId, action: "reply", message: reviewErrorMessage(failure, "Không thể gửi phản hồi."), actor });
    }
  };

  const handleApprove = async (reviewId: string) => {
    setActionError(null);
    try {
      await approveMutation.mutateAsync(reviewId);
      if (onRefresh) onRefresh();
    } catch (failure) {
      setActionError({ reviewId, action: "approve", message: reviewErrorMessage(failure, "Không thể duyệt đánh giá."), actor });
    }
  };

  const handleReject = async (reviewId: string) => {
    setActionError(null);
    try {
      await rejectMutation.mutateAsync({
        reviewId,
        reason: "Vi phạm quy tắc sàn",
      });
      if (onRefresh) onRefresh();
    } catch (failure) {
      setActionError({ reviewId, action: "reject", message: reviewErrorMessage(failure, "Không thể từ chối đánh giá."), actor });
    }
  };

  const readState = reviewReadState(isLoading, isError, reviews.length);
  const retry = onRetry ?? onRefresh;
  const readError = <div role="alert" className="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">
    <p>{reviewErrorMessage(error)}</p>
    {reviews.length > 0 && <p className="mt-1 text-xs">Các đánh giá bên dưới là dữ liệu đã tải trước đó.</p>}
    {retry && <button type="button" className="mt-2 underline disabled:opacity-50" disabled={isFetching} onClick={retry}>{isFetching ? "Đang tải lại..." : "Thử lại"}</button>}
  </div>;
  if (readState === "loading") return <div role="status" className="flex items-center gap-2 rounded-2xl border border-slate-200 p-5 text-sm text-slate-600"><Loader2 className="h-4 w-4 animate-spin" />Đang tải đánh giá...</div>;
  if (readState === "error" && reviews.length === 0) return readError;
  if (readState === "empty") {
    return (
      <div className="bg-white rounded-3xl border border-dashed border-slate-200 p-12 text-center text-slate-400">
        <Star className="w-10 h-10 mx-auto mb-2 opacity-30 text-amber-500" />
        <h4 className="font-bold text-slate-700 text-sm">
          Chưa có đánh giá nào
        </h4>
        <p className="text-xs text-slate-500 mt-1">
          {emptyMessage}
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {isError && readError}
      {isFetching && !isError && <p role="status" className="text-xs text-slate-500">Đang cập nhật đánh giá...</p>}
      {reviews.some(review => !review.isVerifiedBuyer) && <p className="text-xs text-slate-500 rounded-xl border border-slate-200 p-3">Đánh giá tư vấn cũ vẫn được giữ nguyên và ghi nhãn riêng. Đánh giá mới chỉ được gửi từ hợp đồng đã Hoàn tất; không tự đổi nhãn hoặc trọng số của dữ liệu cũ.</p>}
      {reviews.map((review) => {
        const canReply = isVendorOwner && canReplyToReview(user, review);
        let photos: string[] = [];
        try {
          if (review.photosJson) {
            photos = JSON.parse(review.photosJson);
          }
        } catch {
          photos = [];
        }

        return (
          <div
            key={review.id}
            className={`bg-white rounded-3xl border p-6 transition-all duration-200 shadow-xs hover:shadow-md ${
              review.status === "Pending"
                ? "border-amber-300 ring-2 ring-amber-100"
                : "border-slate-200"
            }`}
          >
            {actionError?.reviewId === review.id && actionError.actor === actor && <div role="alert" className="mb-3 rounded-xl border border-red-200 bg-red-50 p-3 text-xs text-red-800">
              <p>{actionError.message}</p>
              <button type="button" className="mt-2 underline disabled:opacity-50" disabled={actionPending} onClick={() => {
                if (actionError.action === "reply") void handleSendReply(review.id);
                else if (actionError.action === "approve") void handleApprove(review.id);
                else void handleReject(review.id);
              }}>Thử lại</button>
            </div>}
            {/* Header: User & Verified Badge & Rating */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-3 border-b border-slate-100">
              <div>
                <div className="flex items-center gap-2">
                  <span className="font-bold text-slate-900 text-sm">
                    {review.customerName}
                  </span>

                  {/* BR-009: Verified Buyer Badge */}
                  {review.isVerifiedBuyer ? (
                    <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 text-[11px] font-bold border border-emerald-200">
                      <ShieldCheck className="w-3.5 h-3.5" />✓ Đã xác thực dịch
                      vụ Sàn (Verified Buyer)
                    </span>
                  ) : (
                    <span className="text-[10px] text-slate-400 font-semibold bg-slate-100 px-2 py-0.5 rounded-full">
                      {reviewHistoryLabel(false)}
                    </span>
                  )}

                  {review.status === "Pending" && (
                    <span className="text-[10px] text-amber-700 bg-amber-100 px-2 py-0.5 rounded-full font-bold">
                      Chờ duyệt
                    </span>
                  )}
                </div>

                <div className="text-[11px] text-slate-400 mt-0.5 flex items-center gap-2">
                  <span>
                    • {new Date(review.createdAt).toLocaleDateString("vi-VN")}
                  </span>
                </div>
              </div>

              {/* Stars */}
              <div className="flex items-center gap-1 text-amber-400 font-bold text-sm">
                {"★".repeat(review.rating)}
                {"☆".repeat(5 - review.rating)}
                <span className="text-xs text-slate-700 font-black ml-1">
                  {(review.averageRating || review.rating).toFixed(2)}
                </span>
              </div>
            </div>

            {/* Review Content */}
            {review.qualityRating && (
              <p className="text-[11px] text-slate-500 mt-3">
                Chất lượng {review.qualityRating}/5 · Phục vụ{" "}
                {review.serviceRating}/5 · Đúng hẹn {review.punctualityRating}/5
                · Chi phí {review.valueRating}/5
              </p>
            )}
            <p className="text-xs text-slate-700 leading-relaxed my-3 whitespace-pre-line">
              "{review.content}"
            </p>

            {/* Review Photos if any */}
            {photos.length > 0 && (
              <div className="flex flex-wrap gap-2 mb-3">
                {photos.map((p, idx) => (
                  <img
                    key={idx}
                    src={p}
                    alt={`Review photo ${idx + 1}`}
                    className="w-16 h-16 object-cover rounded-xl border border-slate-200 shadow-xs cursor-pointer hover:opacity-90 transition"
                    onClick={() => window.open(p, "_blank")}
                  />
                ))}
              </div>
            )}

            {/* Vendor Official Reply Box */}
            {review.vendorReply && replyingReviewId !== review.id ? (
              <div className="bg-slate-50 border-l-4 border-rose-500 rounded-r-2xl p-3.5 text-xs text-slate-700 space-y-1">
                <div className="font-bold text-rose-600 flex items-center gap-1.5">
                  <MessageSquare className="w-3.5 h-3.5" />
                  <span>Phản hồi từ {review.vendorBrandName}</span>
                  {review.vendorRepliedAt && (
                    <span className="text-[10px] text-slate-400 font-normal">
                      (
                      {new Date(review.vendorRepliedAt).toLocaleDateString(
                        "vi-VN",
                      )}
                      )
                    </span>
                  )}
                </div>
                <p className="text-slate-600 italic">"{review.vendorReply}"</p>
                {canReply && (
                  <button
                    className="text-xs underline mt-2"
                    onClick={() => {
                      setReplyingReviewId(review.id);
                      setReplyText(review.vendorReply ?? "");
                    }}
                  >
                    Sửa phản hồi
                  </button>
                )}
              </div>
            ) : canReply ? (
              <div className="mt-2 pt-2 border-t border-slate-100">
                {replyingReviewId === review.id ? (
                  <div className="space-y-2">
                    <textarea
                      maxLength={2000}
                      value={replyText}
                      onChange={(e) => setReplyText(e.target.value)}
                      placeholder="Viết phản hồi gửi tới cặp đôi..."
                      rows={2}
                      className="w-full p-2.5 text-xs rounded-xl border border-slate-300 focus:outline-none focus:ring-2 focus:ring-rose-500"
                    />
                    <div className="flex items-center justify-end gap-2">
                      <button
                        onClick={() => setReplyingReviewId(null)}
                        className="px-3 py-1 text-xs text-slate-400 hover:text-slate-600 font-semibold"
                      >
                        Hủy
                      </button>
                      <button
                        onClick={() => handleSendReply(review.id)}
                        disabled={actionPending || !replyText.trim()}
                        className="px-3.5 py-1 text-xs font-bold bg-rose-600 text-white rounded-lg hover:bg-rose-500 transition disabled:opacity-50 flex items-center gap-1"
                      >
                        {replyMutation.isPending ? (
                          <Loader2 className="w-3 h-3 animate-spin" />
                        ) : (
                          <Send className="w-3 h-3" />
                        )}
                        Gửi Phản Hồi
                      </button>
                    </div>
                  </div>
                ) : (
                  <button
                    onClick={() => {
                      setReplyingReviewId(review.id);
                      setReplyText("");
                    }}
                    className="text-xs font-bold text-rose-600 hover:text-rose-700 flex items-center gap-1 hover:underline"
                  >
                    <MessageSquare className="w-3.5 h-3.5" />
                    Phản hồi đánh giá này
                  </button>
                )}
              </div>
            ) : null}

            {/* Moderator Action Buttons if in pending queue */}
            {isModerator && review.status === "Pending" && (
              <div className="mt-3 pt-3 border-t border-slate-200 flex items-center justify-end gap-2">
                <button
                  onClick={() => handleReject(review.id)}
                  disabled={actionPending}
                  className="px-3 py-1.5 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 text-xs font-bold transition"
                >
                  ✕ Từ Chối
                </button>
                <button
                  onClick={() => handleApprove(review.id)}
                  disabled={actionPending}
                  className="px-4 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold flex items-center gap-1 transition"
                >
                  {approveMutation.isPending ? (
                    <Loader2 className="w-3 h-3 animate-spin" />
                  ) : (
                    <CheckCircle2 className="w-3.5 h-3.5" />
                  )}
                  ✓ Phê Duyệt & Công Khai (FM-005)
                </button>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

export default ReviewList;
