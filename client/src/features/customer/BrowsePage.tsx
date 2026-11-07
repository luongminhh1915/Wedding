import { Link, useLocation, useNavigate } from "react-router-dom";
import {
  ChevronLeft,
  ChevronRight,
  Grid2X2,
  Loader2,
  Search,
  RotateCcw,
  ShieldCheck,
  X,
} from "lucide-react";
import CategoryBar from "./components/CategoryBar";
import SearchFilterBar from "./components/SearchFilterBar";
import BrowseFilterSidebar from "./components/BrowseFilterSidebar";
import ListingCard from "./components/ListingCard";
import { useCompareStore } from "./compareStore";
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
  const { items: compareItems, clear: clearCompare } = useCompareStore();
  const selectedCategory = filter.keyword?.toLowerCase() === "combo" ? "combo" : (filter.categoryId ?? null);
  const handleSelectCategory = (id: string | null) => {
    if (id === "combo") {
      updateFilter({ categoryId: undefined, keyword: "combo", page: 1 });
    } else {
      updateFilter({
        categoryId: id ?? undefined,
        keyword: filter.keyword?.toLowerCase() === "combo" ? undefined : filter.keyword,
        page: 1,
      });
    }
  };

  return (
    <main className="service-browse-page">
      <div className="marketplace-search-at-top browse-marketplace-search">
        <SearchFilterBar key={location.search} filter={filter} categories={categories} onChange={updateFilter} />
      </div>
      <section className="page-intro service-browse-intro">
        <div className="service-browse-intro-top">
          <div className="service-browse-intro-copy">
            <span className="wm-eyebrow">MARKETPLACE DỊCH VỤ CƯỚI</span>
            <h1>
              Khám phá dịch vụ cưới,<br /><em>chọn người đồng hành phù hợp.</em>
            </h1>
            <p>
              Tìm theo danh mục, khu vực và ngân sách. Xem hình ảnh cùng thông tin
              dịch vụ trước khi gửi yêu cầu tư vấn.
            </p>
          </div>
          <img
            className="service-browse-mascot"
            src="/wedding-match/be-hy-flowers.png"
            alt="Bé Hý vui vẻ cầm hoa chào bạn khám phá dịch vụ cưới"
            width={1254}
            height={1254}
            loading="lazy"
          />
        </div>
        <div className="service-browse-promises"><span><Search size={14} /> Lọc theo nhu cầu</span><span><ShieldCheck size={14} /> Dịch vụ công khai đã được duyệt</span><span>Không thanh toán ngay trên web</span></div>
      </section>
      <div className="browse-content space-y-6">
        <div className="browse-page-grid">
          <aside className="browse-sidebar" aria-label="Thanh bên danh mục dịch vụ">
            <div className="browse-sidebar-section browse-sidebar-categories">
              <div className="browse-sidebar-header">
                <span className="browse-filter-icon"><Grid2X2 size={16} aria-hidden="true" /></span>
                <h2>Danh mục dịch vụ</h2>
              </div>
              {catError && <p role="alert" className="service-filter-error">Chưa tải được danh mục dịch vụ. Các bộ lọc khác vẫn dùng được. <button type="button" className="service-filter-reset" onClick={() => void refetchCategories()}>Thử lại danh mục</button></p>}
              {catLoading ? (
                <div className="space-y-2">
                  {Array.from({ length: 6 }, (_, i) => (
                    <div
                      key={i}
                      className="h-10 w-full bg-slate-100 rounded-xl animate-pulse"
                    />
                  ))}
                </div>
              ) : (
                <CategoryBar
                  categories={categories}
                  selectedId={selectedCategory}
                  onSelect={handleSelectCategory}
                />
              )}
            </div>
          </aside>

          <BrowseFilterSidebar key={location.search} filter={filter} onChange={updateFilter} onReset={resetFilter} />

          <div className="browse-results-container">
            {chips.length > 0 && <div className="service-applied-filters mb-3" aria-label="Bộ lọc đang áp dụng">
              <span>Đang lọc:</span>{chips.map(chip => <button type="button" key={chip.key} onClick={() => updateFilter({ ...chip.clear, page: 1 })} aria-label={`Bỏ bộ lọc ${chip.label}`}>{chip.label}<X size={13} aria-hidden="true" /></button>)}
              <button type="button" className="service-filter-reset" onClick={resetFilter}>Xóa tất cả</button>
            </div>}
            <div className="service-results-heading">
              <p>
                {isFetching ? (
                  <span className="flex items-center gap-2">
                    <Loader2 size={13} className="animate-spin" /> Đang tìm dịch vụ phù hợp...
                  </span>
                ) : (
                  <>Khám phá <strong>{result?.totalCount ?? 0}</strong> gói dịch vụ cưới</>
                )}
              </p>
              <span className="hidden sm:block tracking-wider text-[9px] uppercase">
                Ngày cưới của bạn · Lựa chọn của bạn
              </span>
            </div>
            <div className="browse-results-main">
              {isError ? (
                <div className="overview-panel text-center py-16" role="alert">
                  <RotateCcw className="mx-auto mb-4 text-rose-400" size={28} />
                  <h2>Chưa thể tải dịch vụ</h2>
                  <p>Vui lòng kiểm tra kết nối rồi thử lại.</p>
                  <button className="wm-button small mt-6" onClick={() => refetch()}>Thử lại</button>
                </div>
              ) : isLoading ? (
                <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-4">
                  {Array.from({ length: 6 }, (_, i) => (
                    <div key={i} className="bg-white rounded-2xl border border-slate-100 overflow-hidden animate-pulse">
                      <div className="aspect-[16/10] bg-slate-100" />
                      <div className="p-4 space-y-3"><div className="h-3 bg-slate-100 rounded w-2/3" /><div className="h-4 bg-slate-100 rounded" /><div className="h-3 bg-slate-100 rounded w-1/2" /></div>
                    </div>
                  ))}
                </div>
              ) : !result?.items.length ? (
                <div className="overview-panel text-center py-16">
                  <Search className="mx-auto mb-4 text-rose-300" size={30} />
                  <h2>Chưa tìm thấy dịch vụ</h2>
                  <p>Thử một từ khóa khác hoặc mở rộng bộ lọc nhé.</p>
                  <button className="wm-button small mt-6" onClick={resetFilter}>Xóa bộ lọc</button>
                </div>
              ) : (
                <div className="browse-listing-grid">
                  {result.items.map((listing) => <ListingCard key={listing.id} listing={listing} returnPath={returnPath} />)}
                </div>
              )}
              {totalPages > 1 && (
                <nav aria-label="Phân trang dịch vụ" className="browse-pagination">
                  <button aria-label="Trang trước" disabled={currentPage <= 1} onClick={() => updateFilter({ page: currentPage - 1 })} className="browse-page-button"><ChevronLeft size={16} /></button>
                  {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => firstPage + i).map((page) => (
                    <button key={page} aria-current={page === currentPage ? "page" : undefined} onClick={() => updateFilter({ page })} className={`browse-page-number${page === currentPage ? " is-current" : ""}`}>{page}</button>
                  ))}
                  <button aria-label="Trang sau" disabled={currentPage >= totalPages} onClick={() => updateFilter({ page: currentPage + 1 })} className="browse-page-button"><ChevronRight size={16} /></button>
                </nav>
              )}
              <section className="browse-combo-banner service-combo-banner mt-6">
                <div><span className="wm-eyebrow">MỘT SHOP, NHIỀU DỊCH VỤ</span><h2>Muốn xem gói combo riêng của nhà cung cấp?</h2><p>Khám phá combo và dịch vụ lẻ trong cùng một hành trình. Bạn có thể chọn và thêm vào giỏ hàng ngay.</p></div>
                <button type="button" onClick={() => handleSelectCategory("combo")} className="combo-primary cursor-pointer">Xem các gói combo <ChevronRight size={16} /></button>
              </section>
            </div>
          </div>
        </div>
      </div>
      {compareItems.length > 0 && (
        <aside
          role="region"
          aria-label="Thanh so sánh dịch vụ nhanh"
          className="fixed bottom-6 right-6 z-40 bg-[#1c2d37] text-white px-5 py-3.5 rounded-2xl shadow-xl flex items-center gap-4 animate-in fade-in slide-in-from-bottom-4 duration-200 border border-slate-700"
        >
          <div className="flex items-center gap-2">
            <span className="w-6 h-6 rounded-full bg-rose-500 text-white font-bold text-xs flex items-center justify-center">
              {compareItems.length}
            </span>
            <span className="text-xs sm:text-sm font-semibold">
              Dịch vụ đang chọn so sánh
            </span>
          </div>
          <div className="flex items-center gap-2">
            <Link
              to="/compare"
              className="py-1.5 px-3 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 text-white font-bold text-xs transition"
            >
              So sánh ngay →
            </Link>
            <button
              type="button"
              onClick={clearCompare}
              className="text-xs text-slate-400 hover:text-white transition px-1 cursor-pointer"
              title="Xóa danh sách so sánh"
            >
              Xóa
            </button>
          </div>
        </aside>
      )}
    </main>
  );
}
