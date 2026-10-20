import { useState } from "react";
import { ArrowUpDown, MapPin, Search, SlidersHorizontal } from "lucide-react";
import type { CategoryDto, PublicListingsFilter } from "../../../types/category.types";
import { MoneyInput } from "../../../components/MoneyInput";
import { SERVICE_PRICE_LIMIT, serviceCategoryLabel } from "../serviceFilterModel";

interface Props { filter: PublicListingsFilter; categories: CategoryDto[]; onChange: (filter: PublicListingsFilter) => void; onReset: () => void }
const sorts = [
  { value: "newest", label: "Mới nhất" }, { value: "popular", label: "Phổ biến nhất" },
  { value: "price_asc", label: "Giá tăng dần" }, { value: "price_desc", label: "Giá giảm dần" },
] as const;
const priceRanges = [
  { label: "Mọi mức giá", min: undefined, max: undefined },
  { label: "Dưới 10 triệu", min: undefined, max: 10_000_000 },
  { label: "10 – 30 triệu", min: 10_000_000, max: 30_000_000 },
  { label: "30 – 80 triệu", min: 30_000_000, max: 80_000_000 },
  { label: "Từ 80 triệu", min: 80_000_000, max: undefined },
];
export default function SearchFilterBar({ filter, categories, onChange, onReset }: Props) {
  const [draft, setDraft] = useState<PublicListingsFilter>(filter);
  const [error, setError] = useState("");
  const hasAdvanced = !!(filter.location || filter.minPrice != null || filter.maxPrice != null || filter.minRating != null);
  const apply = () => {
    const next = { ...draft, keyword: draft.keyword?.trim() || undefined, location: draft.location?.trim() || undefined, page: 1 };
    if (next.minPrice != null && next.maxPrice != null && next.minPrice > next.maxPrice) { setError("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu."); return; }
    setError(""); onChange(next);
  };
  return <section className="service-search-panel" aria-label="Tìm và lọc dịch vụ">
    <form onSubmit={event => { event.preventDefault(); apply(); }}>
      <div className="service-search-heading"><span>TÌM DỊCH VỤ CƯỚI</span><p>Đúng dịch vụ, đúng người đồng hành.</p></div>
      <div className="service-search-main">
        <label className="service-category-select"><span>Danh mục dịch vụ</span>
          <select aria-label="Chọn danh mục dịch vụ" value={draft.categoryId ?? ""} onChange={event => setDraft({ ...draft, categoryId: event.target.value || undefined })}>
            <option value="">Tất cả dịch vụ</option>
            {categories.map(category => <option key={category.id} value={category.id}>{serviceCategoryLabel(category)}</option>)}
          </select>
        </label>
        <label className="service-keyword-input"><Search size={20} aria-hidden="true" /><span className="sr-only">Từ khóa dịch vụ hoặc tên nhà cung cấp</span>
          <input type="search" maxLength={150} value={draft.keyword ?? ""} onChange={event => setDraft({ ...draft, keyword: event.target.value })} placeholder="Bạn đang tìm dịch vụ hay nhà cung cấp nào?" />
        </label>
        <button type="submit" className="service-search-submit"><Search size={18} aria-hidden="true" />Tìm kiếm</button>
      </div>
      <div className="service-search-lower">
        <details className="service-advanced" open={hasAdvanced || undefined}>
          <summary><SlidersHorizontal size={16} aria-hidden="true" />Bộ lọc chi tiết{hasAdvanced && <span>Đang áp dụng</span>}</summary>
          <div className="service-advanced-content">
            <label><span><MapPin size={14} aria-hidden="true" /> Địa điểm / khu vực</span>
              <input type="text" maxLength={100} value={draft.location ?? ""} onChange={event => setDraft({ ...draft, location: event.target.value })} placeholder="Ví dụ: Hà Nội, Hồ Chí Minh..." />
            </label>
            <label><span>Giá từ (đ)</span><MoneyInput aria-label="Giá dịch vụ tối thiểu" value={draft.minPrice ?? null} min={0} max={SERVICE_PRICE_LIMIT} fractionDigits={0} placeholder="Không giới hạn" onValueChange={value => setDraft({ ...draft, minPrice: value ?? undefined })} /></label>
            <label><span>Giá đến (đ)</span><MoneyInput aria-label="Giá dịch vụ tối đa" value={draft.maxPrice ?? null} min={0} max={SERVICE_PRICE_LIMIT} fractionDigits={0} placeholder="Không giới hạn" onValueChange={value => setDraft({ ...draft, maxPrice: value ?? undefined })} /></label>
            <label><span>Đánh giá nhà cung cấp</span>
              <select aria-label="Lọc đánh giá nhà cung cấp" value={draft.minRating ?? ""} onChange={event => setDraft({ ...draft, minRating: event.target.value ? Number(event.target.value) : undefined })}>
                <option value="">Tất cả nhà cung cấp</option><option value="4">Từ 4 sao</option><option value="4.5">Từ 4,5 sao</option><option value="5">5 sao</option>
              </select><small>Chỉ áp dụng khi shop có ít nhất 3 đánh giá.</small>
            </label>
            <div className="service-price-presets" aria-label="Mức giá nhanh">
              {priceRanges.map(range => <button key={range.label} type="button" className={draft.minPrice === range.min && draft.maxPrice === range.max ? "is-selected" : ""}
                onClick={() => setDraft({ ...draft, minPrice: range.min, maxPrice: range.max })}>{range.label}</button>)}
            </div>
            <div className="service-advanced-actions"><button type="button" className="service-filter-reset" onClick={onReset}>Xóa toàn bộ bộ lọc</button><button type="submit" className="service-filter-apply">Áp dụng bộ lọc</button></div>
          </div>
        </details>
        <label className="service-sort"><ArrowUpDown size={15} aria-hidden="true" /><span>Sắp xếp</span>
          <select aria-label="Sắp xếp dịch vụ" value={filter.sortBy ?? "newest"} onChange={event => onChange({ sortBy: event.target.value as PublicListingsFilter["sortBy"], page: 1 })}>
            {sorts.map(sort => <option key={sort.value} value={sort.value}>{sort.label}</option>)}
          </select>
        </label>
      </div>
      {error && <p className="service-filter-error" role="alert">{error}</p>}
    </form>
  </section>;
}
