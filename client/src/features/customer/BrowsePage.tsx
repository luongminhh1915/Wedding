import { useCallback, useState } from "react";
import { Link } from "react-router-dom";
import {
  ChevronLeft,
  ChevronRight,
  Loader2,
  Search,
  RotateCcw,
} from "lucide-react";
import CategoryBar from "./components/CategoryBar";
import SearchFilterBar from "./components/SearchFilterBar";
import ListingCard from "./components/ListingCard";
import { usePublicCategories, usePublicListings } from "./hooks/useBrowse";
import type { PublicListingsFilter } from "../../types/category.types";

const initialFilter: PublicListingsFilter = {
  sortBy: "newest",
  page: 1,
  pageSize: 12,
};

export default function BrowsePage() {
  const [filter, setFilter] = useState<PublicListingsFilter>(initialFilter);
  const [filterRevision, setFilterRevision] = useState(0);
  const updateFilter = useCallback(
    (changes: PublicListingsFilter) => setFilter((f) => ({ ...f, ...changes })),
    [],
  );
  const resetFilter = () => {
    setFilter(initialFilter);
    setFilterRevision((value) => value + 1);
  };
  const { data: categories = [], isLoading: catLoading } =
    usePublicCategories();
  const {
    data: result,
    isLoading,
    isFetching,
    isError,
    refetch,
  } = usePublicListings(filter);
  const totalPages = result?.totalPages ?? 1;
  const currentPage = result?.page ?? 1;
  const firstPage = Math.max(1, Math.min(currentPage - 3, totalPages - 6));

  return (
    <main>
      <section className="page-intro">
        <span className="wm-eyebrow">NHỮNG NGƯỜI BẠN ĐỒNG HÀNH</span>
        <h1>
          Vendor hợp gu,
          <br />
          <em>ngày cưới như mơ.</em>
        </h1>
        <p>
          Tìm những dịch vụ phù hợp với câu chuyện, phong cách và ngân sách của
          hai bạn.
        </p>
      </section>
      <div className="browse-content space-y-6">
        <section className="browse-combo-banner">
          <div><h2>Chọn combo riêng của nhà cung cấp</h2><p>Chọn combo của một NCC, tùy chỉnh dịch vụ trong cùng NCC rồi bấm “Bước tiếp theo” để kiểm tra và xin báo giá. Chưa thanh toán.</p></div>
          <Link to={`/combo${filter.categoryId ? `?category=${filter.categoryId}` : ""}`} className="combo-primary">Mở giỏ combo →</Link>
        </section>
        {catLoading ? (
          <div className="flex gap-3 overflow-hidden">
            {Array.from({ length: 6 }, (_, i) => (
              <div
                key={i}
                className="h-11 w-32 shrink-0 bg-slate-100 rounded-full animate-pulse"
              />
            ))}
          </div>
        ) : (
          <CategoryBar
            categories={categories}
            selectedId={filter.categoryId ?? null}
            onSelect={(id) =>
              setFilter((f) => ({ ...f, categoryId: id ?? undefined, page: 1 }))
            }
          />
        )}
        <SearchFilterBar
          key={`filters-${filterRevision}`}
          filter={filter}
          onChange={updateFilter}
        />
        <div className="flex items-center justify-between text-xs text-slate-400 py-2">
          <p>
            {isFetching ? (
              <span className="flex items-center gap-2">
                <Loader2 size={13} className="animate-spin" /> Đang tìm dịch vụ
                phù hợp...
              </span>
            ) : (
              <>
                Khám phá{" "}
                <strong className="text-rose-600">
                  {result?.totalCount ?? 0}
                </strong>{" "}
                gói dịch vụ cưới
              </>
            )}
          </p>
          <span className="hidden sm:block tracking-wider text-[9px] uppercase">
            Ngày cưới của bạn · Lựa chọn của bạn
          </span>
        </div>
        {isError ? (
          <div className="overview-panel text-center py-16" role="alert">
            <RotateCcw className="mx-auto mb-4 text-rose-400" size={28} />
            <h2>Chưa thể tải dịch vụ</h2>
            <p>Vui lòng kiểm tra kết nối rồi thử lại.</p>
            <button
              className="wm-button small mt-6"
              onClick={() => refetch()}
            >
              Thử lại
            </button>
          </div>
        ) : isLoading ? (
          <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {Array.from({ length: 6 }, (_, i) => (
              <div
                key={i}
                className="bg-white rounded-3xl border border-slate-100 overflow-hidden animate-pulse"
              >
                <div className="aspect-[4/3] bg-slate-100" />
                <div className="p-6 space-y-4">
                  <div className="h-3 bg-slate-100 rounded w-2/3" />
                  <div className="h-5 bg-slate-100 rounded" />
                  <div className="h-3 bg-slate-100 rounded w-1/2" />
                </div>
              </div>
            ))}
          </div>
        ) : !result?.items.length ? (
          <div className="overview-panel text-center py-16">
            <Search className="mx-auto mb-4 text-rose-300" size={30} />
            <h2>Chưa tìm thấy dịch vụ</h2>
            <p>Thử một từ khóa khác hoặc mở rộng bộ lọc nhé.</p>
            <button className="wm-button small mt-6" onClick={resetFilter}>
              Xóa bộ lọc
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            {result.items.map((listing) => (
              <ListingCard key={listing.id} listing={listing} />
            ))}
          </div>
        )}
        {totalPages > 1 && (
          <nav
            aria-label="Phân trang dịch vụ"
            className="flex items-center justify-center gap-2 pt-6"
          >
            <button
              aria-label="Trang trước"
              disabled={currentPage <= 1}
              onClick={() =>
                setFilter((f) => ({ ...f, page: currentPage - 1 }))
              }
              className="p-2 rounded-xl border border-slate-200 disabled:opacity-30"
            >
              <ChevronLeft size={16} />
            </button>
            {Array.from(
              { length: Math.min(totalPages, 7) },
              (_, i) => firstPage + i,
            ).map((page) => (
              <button
                key={page}
                aria-current={page === currentPage ? "page" : undefined}
                onClick={() => setFilter((f) => ({ ...f, page }))}
                className={`w-9 h-9 rounded-xl text-xs ${page === currentPage ? "bg-rose-500 text-white" : "border border-slate-200 text-slate-500"}`}
              >
                {page}
              </button>
            ))}
            <button
              aria-label="Trang sau"
              disabled={currentPage >= totalPages}
              onClick={() =>
                setFilter((f) => ({ ...f, page: currentPage + 1 }))
              }
              className="p-2 rounded-xl border border-slate-200 disabled:opacity-30"
            >
              <ChevronRight size={16} />
            </button>
          </nav>
        )}
      </div>
    </main>
  );
}
