import { useState, useEffect } from 'react';
import { Search, MapPin, DollarSign, ArrowUpDown, X } from 'lucide-react';
import type { PublicListingsFilter } from '../../../types/category.types';

interface Props {
  filter: PublicListingsFilter;
  onChange: (filter: PublicListingsFilter) => void;
}

const SORT_OPTIONS = [
  { value: 'newest', label: 'Mới nhất' },
  { value: 'popular', label: 'Phổ biến nhất' },
  { value: 'price_asc', label: 'Giá tăng dần' },
  { value: 'price_desc', label: 'Giá giảm dần' },
] as const;

const PRICE_RANGES = [
  { label: 'Tất cả mức giá', min: undefined, max: undefined },
  { label: 'Dưới 10 triệu', min: undefined, max: 10_000_000 },
  { label: '10 – 30 triệu', min: 10_000_000, max: 30_000_000 },
  { label: '30 – 80 triệu', min: 30_000_000, max: 80_000_000 },
  { label: 'Trên 80 triệu', min: 80_000_000, max: undefined },
];

export default function SearchFilterBar({ filter, onChange }: Props) {
  const [keyword, setKeyword] = useState(filter.keyword ?? '');
  const [location, setLocation] = useState(filter.location ?? '');

  // Debounce search
  useEffect(() => {
    const t = setTimeout(() => {
      onChange({ ...filter, keyword: keyword || undefined, page: 1 });
    }, 400);
    return () => clearTimeout(t);
  }, [keyword]);

  useEffect(() => {
    const t = setTimeout(() => {
      onChange({ ...filter, location: location || undefined, page: 1 });
    }, 400);
    return () => clearTimeout(t);
  }, [location]);

  const handlePriceRange = (min?: number, max?: number) => {
    onChange({ ...filter, minPrice: min, maxPrice: max, page: 1 });
  };

  const handleSort = (sortBy: PublicListingsFilter['sortBy']) => {
    onChange({ ...filter, sortBy, page: 1 });
  };

  const hasFilters = !!(filter.keyword || filter.location || filter.minPrice || filter.maxPrice);

  const clearAll = () => {
    setKeyword('');
    setLocation('');
    onChange({ sortBy: 'newest', page: 1, pageSize: 12 });
  };

  return (
    <div className="bg-white rounded-2xl border border-rose-100 shadow-sm p-4 space-y-4">
      {/* Search row */}
      <div className="flex gap-3">
        {/* Keyword search */}
        <div className="flex-1 relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            value={keyword}
            onChange={e => setKeyword(e.target.value)}
            placeholder="Tìm gói dịch vụ, tên NCC..."
            className="w-full pl-9 pr-3 py-2.5 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition"
          />
          {keyword && (
            <button onClick={() => setKeyword('')} className="absolute right-3 top-1/2 -translate-y-1/2">
              <X className="w-3.5 h-3.5 text-slate-400" />
            </button>
          )}
        </div>

        {/* Location */}
        <div className="w-52 relative">
          <MapPin className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
          <input
            type="text"
            value={location}
            onChange={e => setLocation(e.target.value)}
            placeholder="Khu vực..."
            className="w-full pl-9 pr-3 py-2.5 border border-slate-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-rose-300 transition"
          />
        </div>
      </div>

      {/* Filter chips row */}
      <div className="flex items-center gap-3 flex-wrap">
        {/* Price range chips */}
        <div className="flex items-center gap-1.5">
          <DollarSign className="w-3.5 h-3.5 text-slate-400" />
          <span className="text-xs text-slate-500 font-semibold mr-1">Mức giá:</span>
          {PRICE_RANGES.map(r => {
            const isActive = filter.minPrice === r.min && filter.maxPrice === r.max;
            return (
              <button
                key={r.label}
                onClick={() => handlePriceRange(r.min, r.max)}
                className={`px-3 py-1 rounded-full text-xs font-semibold transition ${
                  isActive
                    ? 'bg-rose-500 text-white'
                    : 'bg-slate-100 text-slate-600 hover:bg-rose-50 hover:text-rose-600'
                }`}
              >
                {r.label}
              </button>
            );
          })}
        </div>

        {/* Divider */}
        <div className="w-px h-5 bg-slate-200" />

        {/* Sort */}
        <div className="flex items-center gap-1.5">
          <ArrowUpDown className="w-3.5 h-3.5 text-slate-400" />
          <span className="text-xs text-slate-500 font-semibold mr-1">Sắp xếp:</span>
          <select
            value={filter.sortBy ?? 'newest'}
            onChange={e => handleSort(e.target.value as PublicListingsFilter['sortBy'])}
            className="text-xs font-semibold text-slate-700 bg-slate-100 border-none rounded-lg px-2 py-1 focus:outline-none focus:ring-2 focus:ring-rose-300 cursor-pointer"
          >
            {SORT_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>

        {/* Clear all */}
        {hasFilters && (
          <button
            onClick={clearAll}
            className="ml-auto flex items-center gap-1 text-xs text-slate-500 hover:text-red-500 transition font-medium"
          >
            <X className="w-3 h-3" />
            Xóa bộ lọc
          </button>
        )}
      </div>
    </div>
  );
}
