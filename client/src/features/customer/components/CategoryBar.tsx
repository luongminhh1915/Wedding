import type { CategoryDto } from '../../../types/category.types';

// Icon mapping cho 7 ngành hàng
const CATEGORY_ICONS: Record<string, string> = {
  'trung-tam-tiec-cuoi': '🏛️',
  'decor-hoa-cuoi': '💐',
  'quay-phim-chup-anh': '📸',
  'vay-cuoi-trang-phuc': '👗',
  'makeup-lam-dep': '💄',
  'thiep-cuoi-in-an': '💌',
  'wedding-planner': '📋',
};

const DEFAULT_ICON = '💍';

interface Props {
  categories: CategoryDto[];
  selectedId: string | null;
  onSelect: (id: string | null) => void;
}

export default function CategoryBar({ categories, selectedId, onSelect }: Props) {
  return (
    <div className="flex gap-3 overflow-x-auto pb-2 scrollbar-none" style={{ scrollbarWidth: 'none' }}>
      {/* Pill "Tất cả" */}
      <button
        onClick={() => onSelect(null)}
        className={`flex items-center gap-2 px-5 py-3 rounded-full text-sm font-semibold whitespace-nowrap transition-all duration-200 flex-shrink-0 ${
          selectedId === null
            ? 'bg-rose-500 text-white shadow-lg shadow-rose-200 scale-105'
            : 'bg-white border border-rose-100 text-slate-700 hover:border-rose-300 hover:text-rose-600'
        }`}
      >
        <span>💍</span>
        <span>Tất Cả</span>
        <span className={`text-xs font-bold ${selectedId === null ? 'text-rose-100' : 'text-slate-400'}`}>
          {categories.reduce((s, c) => s + c.listingCount, 0)}
        </span>
      </button>

      {categories.map(cat => {
        const icon = CATEGORY_ICONS[cat.slug] ?? DEFAULT_ICON;
        const isActive = selectedId === cat.id;
        return (
          <button
            key={cat.id}
            onClick={() => onSelect(isActive ? null : cat.id)}
            className={`flex items-center gap-2 px-5 py-3 rounded-full text-sm font-semibold whitespace-nowrap transition-all duration-200 flex-shrink-0 ${
              isActive
                ? 'bg-rose-500 text-white shadow-lg shadow-rose-200 scale-105'
                : 'bg-white border border-rose-100 text-slate-700 hover:border-rose-300 hover:text-rose-600 hover:shadow-sm'
            }`}
          >
            <span>{icon}</span>
            <span>{cat.name}</span>
            {cat.listingCount > 0 && (
              <span className={`text-xs font-bold px-1.5 py-0.5 rounded-full ${
                isActive ? 'bg-white/20 text-white' : 'bg-rose-50 text-rose-500'
              }`}>
                {cat.listingCount}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
