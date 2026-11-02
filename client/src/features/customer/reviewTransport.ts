import { CanceledError, isAxiosError, isCancel, type AxiosInstance } from "axios";
import type {
  EligibleContract,
  EligibleReviewTarget,
  PendingReviewEdit,
  PublicReview,
  Review,
  ReviewPage,
  SubmitComboReviewRequest,
  SubmitReviewRequest,
  SubmitShopReviewRequest,
} from "../../types/review.types";

export interface ReviewSession {
  userId: string | null;
  role: string | null;
  token: string | null;
  revision: number;
}
export type ReviewQueryKind = "listing-reviews" | "vendor-reviews" | "eligible-contracts" |
  "eligible-targets" | "my-reviews" | "target-reviews" | "moderation-reviews" | "moderation-edits";
type ReviewAccess = "public" | "customer" | "moderator" | "vendor";

export function reviewQueryKey(kind: ReviewQueryKind, session: ReviewSession, target?: string) {
  const scope = [session.userId, session.role, session.revision];
  return target === undefined ? [kind, ...scope] : [kind, target, ...scope];
}

export function canRequestReviews(access: ReviewAccess, session: ReviewSession) {
  if (access === "public") return true;
  if (!session.userId || !session.token) return false;
  return access === "customer" ? session.role === "Customer" : access === "vendor" ? session.role === "VendorOwner" :
    session.role === "Moderator" || session.role === "SuperAdmin";
}

function sameSession(left: ReviewSession, right: ReviewSession) {
  return left.userId === right.userId && left.role === right.role && left.token === right.token && left.revision === right.revision;
}

export function reviewErrorMessage(error: unknown, fallback = "Không thể tải đánh giá. Vui lòng thử lại.") {
  if (isAxiosError(error)) {
    const data: unknown = error.response?.data;
    if (data && typeof data === "object") {
      const body = data as { message?: unknown; detail?: unknown };
      for (const message of [body.message, body.detail])
        if (typeof message === "string" && message.trim()) return message;
    }
    return fallback;
  }
  return error instanceof Error && error.message ? error.message : fallback;
}

export function reviewReadState(isLoading: boolean, isError: boolean, count: number) {
  return isError ? "error" : isLoading ? "loading" : count === 0 ? "empty" : "ready";
}

interface ReviewActionResponse { message: string; review: Review }

/** The injected client retains the application's configured API base and interceptors. Tokens never enter cache keys. */
export function createReviewTransport(client: AxiosInstance, currentSession: () => ReviewSession) {
  async function request<T>(session: ReviewSession, access: ReviewAccess, method: "get" | "post", url: string,
    fallback: string, data?: unknown, signal?: AbortSignal): Promise<T> {
    const checkSession = () => {
      if (signal?.aborted || !sameSession(session, currentSession())) throw new CanceledError("Phiên đánh giá đã thay đổi.");
    };
    checkSession();
    if (!canRequestReviews(access, session)) throw new Error("Phiên đăng nhập không có quyền thực hiện thao tác đánh giá này.");
    try {
      const response = await client.request<T>({ method, url, data, signal,
        // Pin the initiating identity, including anonymous reads, instead of allowing a queued request to use another session's token.
        headers: { Authorization: session.token ? `Bearer ${session.token}` : null } });
      checkSession();
      return response.data;
    } catch (error) {
      checkSession();
      if (isCancel(error)) throw error;
      throw new Error(reviewErrorMessage(error, fallback));
    }
  }
  async function list<T>(session: ReviewSession, access: ReviewAccess, url: string, fallback: string, signal?: AbortSignal): Promise<T[]> {
    const data = await request<unknown>(session, access, "get", url, fallback, undefined, signal);
    if (!Array.isArray(data)) throw new Error(fallback);
    return data as T[];
  }
  const postReview = <T>(session: ReviewSession, url: string, fallback: string, data: unknown) =>
    request<T>(session, "customer", "post", url, fallback, data);
  return {
    listing: (session: ReviewSession, id: string, signal?: AbortSignal) => list<PublicReview>(session, "public", `/api/reviews/listing/${encodeURIComponent(id)}`, "Không thể tải đánh giá dịch vụ. Vui lòng thử lại.", signal),
    vendor: (session: ReviewSession, id: string, signal?: AbortSignal) => list<PublicReview>(session, "public", `/api/reviews/vendor/${encodeURIComponent(id)}`, "Không thể tải đánh giá nhà cung cấp. Vui lòng thử lại.", signal),
    eligible: (session: ReviewSession, signal?: AbortSignal) => list<EligibleContract>(session, "customer", "/api/reviews/eligible-contracts", "Chưa tải được hợp đồng đủ điều kiện. Vui lòng thử lại.", signal),
    eligibleTargets: (session: ReviewSession, signal?: AbortSignal) => list<EligibleReviewTarget>(session, "customer", "/api/reviews/eligible-targets", "Chưa tải được giao dịch đủ điều kiện đánh giá. Vui lòng thử lại.", signal),
    mine: (session: ReviewSession, signal?: AbortSignal) => list<Review>(session, "customer", "/api/reviews/mine", "Chưa tải được đánh giá của bạn. Vui lòng thử lại.", signal),
    target: (session: ReviewSession, targetType: string, targetId: string, page: number, pageSize: number,
      rating: number | null, verifiedOnly: boolean | null, sort: string, signal?: AbortSignal) => {
      const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize), sort });
      if (rating !== null) query.set("rating", String(rating));
      if (verifiedOnly !== null) query.set("verifiedOnly", String(verifiedOnly));
      return request<ReviewPage>(session, "public", "get",
        `/api/reviews/target/${encodeURIComponent(targetType)}/${encodeURIComponent(targetId)}?${query}`,
        "Không thể tải đánh giá. Vui lòng thử lại.", undefined, signal);
    },
    moderation: (session: ReviewSession, signal?: AbortSignal) => list<Review>(session, "moderator", "/api/reviews/moderation/pending", "Không thể tải hàng chờ đánh giá. Vui lòng thử lại.", signal),
    pendingEdits: (session: ReviewSession, signal?: AbortSignal) => list<PendingReviewEdit>(session, "moderator", "/api/reviews/moderation/pending-edits", "Không thể tải yêu cầu chỉnh sửa đánh giá. Vui lòng thử lại.", signal),
    submit: (session: ReviewSession, payload: SubmitReviewRequest) => request<Review>(session, "customer", "post", "/api/reviews", "Không thể gửi đánh giá.", payload),
    submitShop: (session: ReviewSession, payload: SubmitShopReviewRequest) => postReview<Review>(session, "/api/reviews/shop", "Không thể gửi đánh giá Shop.", payload),
    submitCombo: (session: ReviewSession, payload: SubmitComboReviewRequest) => postReview<Review>(session, "/api/reviews/combo", "Không thể gửi đánh giá Combo.", payload),
    submitEdit: (session: ReviewSession, reviewId: string, payload: Omit<SubmitReviewRequest, "listingId" | "bookingContractId">) =>
      postReview<Review>(session, `/api/reviews/${encodeURIComponent(reviewId)}/edit-requests`, "Không thể gửi yêu cầu chỉnh sửa.", payload),
    approve: (session: ReviewSession, id: string) => request<ReviewActionResponse>(session, "moderator", "post", `/api/reviews/${encodeURIComponent(id)}/approve`, "Không thể duyệt đánh giá."),
    reject: (session: ReviewSession, id: string, reason?: string) => request<ReviewActionResponse>(session, "moderator", "post", `/api/reviews/${encodeURIComponent(id)}/reject`, "Không thể từ chối đánh giá.", { reason }),
    approveEdit: (session: ReviewSession, id: string) => request<Review>(session, "moderator", "post", `/api/reviews/moderation/edit-requests/${encodeURIComponent(id)}/approve`, "Không thể duyệt chỉnh sửa đánh giá."),
    rejectEdit: (session: ReviewSession, id: string, reason: string) => request<Review>(session, "moderator", "post", `/api/reviews/moderation/edit-requests/${encodeURIComponent(id)}/reject`, "Không thể từ chối chỉnh sửa đánh giá.", { reason }),
    reply: (session: ReviewSession, id: string, reply: string) => request<ReviewActionResponse>(session, "vendor", "post", `/api/reviews/${encodeURIComponent(id)}/reply`, "Không thể gửi phản hồi.", { reply }),
  };
}
