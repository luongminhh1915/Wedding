import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  ChevronLeft,
  ChevronRight,
  Loader2,
  Search,
  RotateCcw,
  X,
} from "lucide-react";
import CategoryBar from "./components/CategoryBar";
import SearchFilterBar from "./components/SearchFilterBar";
import ListingCard from "./components/ListingCard";
import { usePublicCategories, usePublicListings } from "./hooks/useBrowse";
import type { PublicListingsFilter } from "../../types/category.types";
import { clearServiceFilterSearch, readServiceFilters, serviceFilterChips, serviceFilterSearch } from "./serviceFilterModel";
import "../../styles/service-search.css";

export default function BrowsePage() {
  const location = useLocation();
  const navigate = useNavigate();
  const filter = readServiceFilters(location.search);
  const updateFilter = (changes: PublicListingsFilter) => {
    const next = { ...filter, ...changes };
    navigate({ pathname: location.pathname, search: serviceFilterSearch(next, location.search) });
  };
  const resetFilter = () => {
    navigate({ pathname: location.pathname, search: clearServiceFilterSearch(location.search) });
  };
  const { data: categories = [], isLoading: catLoading, isError: catError, refetch: refetchCategories } =
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
  const chips = serviceFilterChips(filter, categories);
  const returnPath = `/browse${serviceFilterSearch(filter)}`;

  return (
    <main className="service-browse-page">
      <section className="page-intro service-browse-intro">
        <span className="wm-eyebrow">DỊCH VỤ CHO NGÀY VUI</span>
        <h1>
          Tìm người đồng hành,<br /><em>cùng viết ngày hạnh phúc.</em>
        </h1>
        <p>
          Tìm những dịch vụ phù hợp với câu chuyện, phong cách và ngân sách của
          hai bạn.
        </p>
      </section>
      <div className="browse-content space-y-6">
        <SearchFilterBar key={location.search} filter={filter} categories={categories} onChange={updateFilter} onReset={resetFilter} />
        {catError && <p role="alert" className="service-filter-error">Chưa tải được danh mục dịch vụ. Các bộ lọc khác vẫn dùng được. <button type="button" className="service-filter-reset" onClick={() => void refetchCategories()}>Thử lại danh mục</button></p>}
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
              updateFilter({ categoryId: id ?? undefined, page: 1 })
            }
          />
        )}
        {chips.length > 0 && <div className="service-applied-filters" aria-label="Bộ lọc đang áp dụng">
          <span>Đang lọc:</span>{chips.map(chip => <button type="button" key={chip.key} onClick={() => updateFilter({ ...chip.clear, page: 1 })} aria-label={`Bỏ bộ lọc ${chip.label}`}>{chip.label}<X size={13} aria-hidden="true" /></button>)}
          <button type="button" className="service-filter-reset" onClick={resetFilter}>Xóa tất cả</button>
        </div>}
        <div className="service-results-heading">
          <p>
            {isFetching ? (
              <span className="flex items-center gap-2">
                <Loader2 size={13} className="animate-spin" /> Đang tìm dịch vụ
                phù hợp...
              </span>
            ) : (
              <>
                Khám phá{" "}
                <strong>
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
        <section className="browse-combo-banner service-combo-banner">
          <div><h2>Muốn chọn nhiều dịch vụ cùng một nhà cung cấp?</h2><p>Khám phá combo riêng của shop, tùy chỉnh rồi gửi yêu cầu báo giá. Chưa thanh toán.</p></div>
          <Link to={`/combo${filter.categoryId ? `?category=${filter.categoryId}` : ""}`} className="combo-primary">Khám phá combo →</Link>
        </section>
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
              <ListingCard key={listing.id} listing={listing} returnPath={returnPath} />
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
                updateFilter({ page: currentPage - 1 })
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
                onClick={() => updateFilter({ page })}
                className={`w-9 h-9 rounded-xl text-xs ${page === currentPage ? "bg-[#25334d] text-white" : "border border-slate-200 text-slate-500"}`}
              >
                {page}
              </button>
            ))}
            <button
              aria-label="Trang sau"
              disabled={currentPage >= totalPages}
              onClick={() =>
                updateFilter({ page: currentPage + 1 })
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
