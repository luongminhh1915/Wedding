import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Heart, Sparkles, LogIn, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import CategoryBar from './components/CategoryBar';
import SearchFilterBar from './components/SearchFilterBar';
import ListingCard from './components/ListingCard';
import { usePublicCategories, usePublicListings } from './hooks/useBrowse';
import { useAuthStore } from '../../store/authStore';
import type { PublicListingsFilter } from '../../types/category.types';

export default function BrowsePage() {
  const { user, token } = useAuthStore();
  const [filter, setFilter] = useState<PublicListingsFilter>({
    sortBy: 'newest',
    page: 1,
    pageSize: 12,
  });

  const { data: categories = [], isLoading: catLoading } = usePublicCategories();
  const { data: result, isLoading: listLoading, isFetching } = usePublicListings(filter);

  const handleCategorySelect = (id: string | null) => {
    setFilter(f => ({ ...f, categoryId: id ?? undefined, page: 1 }));
  };

  const handleFilterChange = (newFilter: PublicListingsFilter) => {
    setFilter(f => ({ ...f, ...newFilter }));
  };

  const listings = result?.items ?? [];
  const totalCount = result?.totalCount ?? 0;
  const totalPages = result?.totalPages ?? 1;
  const currentPage = result?.page ?? 1;

  return (
    <div className="min-h-screen bg-[#fcfbf9]" style={{ fontFamily: "'Plus Jakarta Sans', sans-serif" }}>
      {/* ── HEADER ─────────────────────────────────────────────────────────── */}
      <header className="bg-white/95 backdrop-blur-md sticky top-0 z-50 border-b border-rose-100/80">
        <div className="max-w-7xl mx-auto px-5 h-16 flex items-center justify-between">
          <Link to="/" className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-full bg-gradient-to-br from-rose-400 to-amber-400 flex items-center justify-center text-white shadow-md shadow-rose-200">
              <Heart className="w-4 h-4 fill-current" />
            </div>
            <span className="text-lg font-bold text-slate-800" style={{ fontFamily: "'Playfair Display', serif" }}>
              Wedding Platform
            </span>
          </Link>

          <nav className="hidden md:flex items-center gap-7 text-sm font-medium text-slate-600">
            <Link to="/browse" className="text-rose-500 font-semibold">Khám Phá</Link>
            <Link to="/browse" className="hover:text-rose-500 transition">7 Ngành Cưới</Link>
            <Link to="/browse" className="hover:text-rose-500 transition">AI Stylist</Link>
          </nav>

          <div className="flex items-center gap-3">
            {token && user ? (
              <Link
                to={user.role === 'VendorOwner' ? '/vendor/dashboard' : user.role === 'Customer' ? '/customer/dashboard' : '/admin/dashboard'}
                className="flex items-center gap-2 px-4 py-2 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-sm font-semibold hover:bg-rose-100 transition"
              >
                <span className="w-5 h-5 rounded-full bg-rose-200 flex items-center justify-center text-xs font-bold">
                  {user.fullName.charAt(0)}
                </span>
                {user.fullName}
              </Link>
            ) : (
              <div className="flex items-center gap-2">
                <Link to="/login" className="flex items-center gap-1.5 px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-900 rounded-xl hover:bg-slate-100 transition">
                  <LogIn className="w-4 h-4" />
                  Đăng Nhập
                </Link>
                <Link to="/register" className="px-4 py-2 rounded-xl bg-rose-500 hover:bg-rose-600 text-white text-sm font-semibold transition shadow-sm shadow-rose-200">
                  Đăng Ký
                </Link>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* ── HERO BANNER ────────────────────────────────────────────────────── */}
      <section className="max-w-7xl mx-auto px-5 mt-8 mb-8">
        <div className="relative rounded-3xl overflow-hidden bg-gradient-to-br from-rose-50 via-white to-amber-50 border border-rose-100 p-10 text-center shadow-sm">
          {/* Decorative blobs */}
          <div className="absolute -top-10 -right-10 w-64 h-64 bg-rose-100 rounded-full blur-3xl opacity-40 pointer-events-none" />
          <div className="absolute -bottom-10 -left-10 w-48 h-48 bg-amber-100 rounded-full blur-3xl opacity-40 pointer-events-none" />

          <div className="relative">
            <div className="inline-flex items-center gap-2 px-4 py-1.5 bg-rose-100/70 text-rose-700 rounded-full text-xs font-bold uppercase tracking-wider mb-4">
              <Sparkles className="w-3.5 h-3.5" />
              Nền tảng dịch vụ cưới hàng đầu
            </div>
            <h1 className="text-4xl md:text-5xl font-bold text-slate-900 mb-4 leading-tight" style={{ fontFamily: "'Playfair Display', serif" }}>
              Khám Phá Dịch Vụ Cưới{' '}
              <span className="bg-gradient-to-r from-rose-500 to-amber-500 bg-clip-text text-transparent">
                Trong Mơ
              </span>
            </h1>
            <p className="text-slate-500 text-lg max-w-xl mx-auto mb-8">
              Hàng trăm nhà cung cấp uy tín — từ tiệc cưới, decor, đến váy cưới và wedding planner.
            </p>
          </div>
        </div>
      </section>

      <div className="max-w-7xl mx-auto px-5 pb-16 space-y-6">
        {/* ── CATEGORY BAR ──────────────────────────────────────────────────── */}
        {catLoading ? (
          <div className="flex gap-3">
            {[...Array(7)].map((_, i) => (
              <div key={i} className="h-11 w-32 bg-slate-100 rounded-full animate-pulse" />
            ))}
          </div>
        ) : (
          <CategoryBar
            categories={categories}
            selectedId={filter.categoryId ?? null}
            onSelect={handleCategorySelect}
          />
        )}

        {/* ── SEARCH & FILTER BAR ───────────────────────────────────────────── */}
        <SearchFilterBar filter={filter} onChange={handleFilterChange} />

        {/* ── RESULTS HEADER ────────────────────────────────────────────────── */}
        <div className="flex items-center justify-between">
          <p className="text-sm text-slate-500">
            {isFetching ? (
              <span className="flex items-center gap-1.5 text-rose-500">
                <Loader2 className="w-3.5 h-3.5 animate-spin" />
                Đang tải...
              </span>
            ) : (
              <>Tìm thấy <strong className="text-slate-900">{totalCount}</strong> gói dịch vụ</>
            )}
          </p>
        </div>

        {/* ── LISTING GRID ──────────────────────────────────────────────────── */}
        {listLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
            {[...Array(8)].map((_, i) => (
              <div key={i} className="bg-white rounded-2xl border border-slate-100 overflow-hidden animate-pulse">
                <div className="h-52 bg-slate-100" />
                <div className="p-4 space-y-3">
                  <div className="h-3 bg-slate-100 rounded w-3/4" />
                  <div className="h-4 bg-slate-100 rounded" />
                  <div className="h-4 bg-slate-100 rounded w-5/6" />
                  <div className="h-3 bg-slate-100 rounded w-1/2" />
                </div>
              </div>
            ))}
          </div>
        ) : listings.length === 0 ? (
          <div className="text-center py-20 bg-white rounded-2xl border border-dashed border-slate-200">
            <div className="text-5xl mb-4">💍</div>
            <h3 className="text-lg font-bold text-slate-700">Không tìm thấy kết quả</h3>
            <p className="text-sm text-slate-500 mt-1 mb-5">Thử thay đổi bộ lọc hoặc từ khóa tìm kiếm</p>
            <button
              onClick={() => setFilter({ sortBy: 'newest', page: 1, pageSize: 12 })}
              className="px-5 py-2 rounded-xl bg-rose-500 text-white text-sm font-semibold hover:bg-rose-600 transition"
            >
              Xóa bộ lọc
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
            {listings.map(listing => (
              <ListingCard key={listing.id} listing={listing} />
            ))}
          </div>
        )}

        {/* ── PAGINATION ────────────────────────────────────────────────────── */}
        {totalPages > 1 && (
          <div className="flex items-center justify-center gap-2 pt-4">
            <button
              onClick={() => setFilter(f => ({ ...f, page: Math.max(1, (f.page ?? 1) - 1) }))}
              disabled={currentPage <= 1}
              className="p-2 rounded-xl border border-slate-200 hover:bg-rose-50 hover:border-rose-300 transition disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>

            {[...Array(Math.min(totalPages, 7))].map((_, i) => {
              const p = i + 1;
              return (
                <button
                  key={p}
                  onClick={() => setFilter(f => ({ ...f, page: p }))}
                  className={`w-9 h-9 rounded-xl text-sm font-semibold transition ${
                    p === currentPage
                      ? 'bg-rose-500 text-white shadow-sm shadow-rose-200'
                      : 'border border-slate-200 hover:bg-rose-50 hover:border-rose-300 text-slate-700'
                  }`}
                >
                  {p}
                </button>
              );
            })}

            <button
              onClick={() => setFilter(f => ({ ...f, page: Math.min(totalPages, (f.page ?? 1) + 1) }))}
              disabled={currentPage >= totalPages}
              className="p-2 rounded-xl border border-slate-200 hover:bg-rose-50 hover:border-rose-300 transition disabled:opacity-40 disabled:cursor-not-allowed"
            >
              <ChevronRight className="w-4 h-4" />
            </button>

            <span className="text-sm text-slate-500 ml-2">
              Trang {currentPage} / {totalPages}
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
