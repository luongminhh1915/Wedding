import { useState } from "react";
import { Star } from "lucide-react";
import { useIndependentReviews } from "../../hooks/useReviews";
import { ReviewList } from "./ReviewList";
import { useAuthStore } from "../../../../store/authStore";

type ReviewTarget = "Service" | "Shop" | "Combo";

export function IndependentReviewPanel({ targetType, targetId, targetName }: {
  targetType: ReviewTarget;
  targetId: string;
  targetName: string;
}) {
  const [page, setPage] = useState(1);
  const [rating, setRating] = useState<number | null>(null);
  const [verifiedOnly, setVerifiedOnly] = useState<boolean | null>(null);
  const [sort, setSort] = useState("newest");
  const query = useIndependentReviews(targetType, targetId, { page, pageSize: 10, rating, verifiedOnly, sort });
  const data = query.data;
  const isVendorOwner = useAuthStore(state => state.user?.role === "VendorOwner");
  return (
    <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5">
      <header>
        <h3 className="flex items-center gap-2 font-semibold text-slate-900">
          <Star size={17} className="fill-amber-400 text-amber-500" />
          Đánh giá {targetType === "Service" ? "dịch vụ" : targetType === "Shop" ? "Shop" : "Combo"} · {targetName}
        </h3>
        {query.isSuccess && data && <p className="mt-1 text-sm text-slate-600">
          {data.summary.averageRating.toFixed(1)} / 5 · {data.summary.totalReviews} đánh giá
          {" · "}
          {Object.entries(data.summary.ratingDistribution).sort(([a], [b]) => Number(b) - Number(a))
            .map(([stars, count]) => `${stars}★ ${count}`).join(" · ")}
        </p>}
      </header>
      <div className="flex flex-wrap gap-2">
        <label className="text-xs text-slate-600">Số sao
          <select className="ml-2 rounded-lg border p-2" value={rating ?? ""}
            onChange={event => { setRating(event.target.value ? Number(event.target.value) : null); setPage(1); }}>
            <option value="">Tất cả</option>
            {[5, 4, 3, 2, 1].map(value => <option key={value} value={value}>{value} sao</option>)}
          </select>
        </label>
        <label className="text-xs text-slate-600">Người mua
          <select className="ml-2 rounded-lg border p-2" value={verifiedOnly === null ? "" : String(verifiedOnly)}
            onChange={event => { setVerifiedOnly(event.target.value === "" ? null : event.target.value === "true"); setPage(1); }}>
            <option value="">Tất cả</option><option value="true">Đã xác thực</option><option value="false">Khác</option>
          </select>
        </label>
        <label className="text-xs text-slate-600">Sắp xếp
          <select className="ml-2 rounded-lg border p-2" value={sort} onChange={event => { setSort(event.target.value); setPage(1); }}>
            <option value="newest">Mới nhất</option><option value="oldest">Cũ nhất</option>
            <option value="rating-high">Điểm cao</option><option value="rating-low">Điểm thấp</option>
          </select>
        </label>
      </div>
      {query.isSuccess && data && data.filteredCount === 0 && data.summary.totalReviews > 0
        ? <p className="rounded-xl border border-dashed p-5 text-center text-sm text-slate-500">Không có đánh giá phù hợp bộ lọc.</p>
        : <ReviewList reviews={data?.reviews ?? []} isLoading={query.isPending} isFetching={query.isFetching}
          isError={query.isError} error={query.error} onRetry={() => void query.refetch()}
          isVendorOwner={isVendorOwner} emptyMessage="Hãy là khách hàng đầu tiên chia sẻ trải nghiệm với đối tượng này." />}
      {query.isSuccess && data && data.totalPages > 1 && <nav className="flex items-center justify-between" aria-label="Phân trang đánh giá">
        <button className="rounded-lg border px-3 py-2 text-sm disabled:opacity-40" disabled={page <= 1}
          onClick={() => setPage(value => Math.max(1, value - 1))}>Trước</button>
        <span className="text-xs text-slate-500">Trang {data.page} / {data.totalPages}</span>
        <button className="rounded-lg border px-3 py-2 text-sm disabled:opacity-40" disabled={page >= data.totalPages}
          onClick={() => setPage(value => Math.min(data.totalPages, value + 1))}>Sau</button>
      </nav>}
    </section>
  );
}
