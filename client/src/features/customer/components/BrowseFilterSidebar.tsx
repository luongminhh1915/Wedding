import { useState, type FormEvent } from "react";
import { ArrowUpDown, MapPin, SlidersHorizontal } from "lucide-react";
import { MoneyInput } from "../../../components/MoneyInput";
import type { PublicListingsFilter } from "../../../types/category.types";
import { SERVICE_PRICE_LIMIT } from "../serviceFilterModel";

interface Props {
  filter: PublicListingsFilter;
  onChange: (filter: PublicListingsFilter) => void;
  onReset: () => void;
}

const sorts = [
  { value: "newest", label: "Mới nhất" },
  { value: "popular", label: "Phổ biến nhất" },
  { value: "price_asc", label: "Giá tăng dần" },
  { value: "price_desc", label: "Giá giảm dần" },
] as const;

const priceRanges = [
  { label: "Mọi mức giá", min: undefined, max: undefined },
  { label: "Dưới 10 triệu", min: undefined, max: 10_000_000 },
  { label: "10 – 30 triệu", min: 10_000_000, max: 30_000_000 },
  { label: "30 – 80 triệu", min: 30_000_000, max: 80_000_000 },
  { label: "Từ 80 triệu", min: 80_000_000, max: undefined },
];

export default function BrowseFilterSidebar({ filter, onChange, onReset }: Props) {
  const [draft, setDraft] = useState<PublicListingsFilter>(filter);
  const [isExpanded, setIsExpanded] = useState(false);
  const [error, setError] = useState("");
  const activeCount = [filter.location?.trim(), filter.minPrice != null, filter.maxPrice != null, filter.minRating != null]
    .filter(Boolean).length;

  const apply = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const next = {
      ...draft,
      location: draft.location?.trim() || undefined,
      page: 1,
    };
    if (next.minPrice != null && next.maxPrice != null && next.minPrice > next.maxPrice) {
      setError("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu.");
      return;
    }
    setError("");
    onChange(next);
    setIsExpanded(false);
  };

  return (
    <form className={`browse-filter-panel${isExpanded ? " is-expanded" : ""}`} onSubmit={apply} aria-label="Bộ lọc dịch vụ">
      <div className="browse-filter-header">
        <div><span className="browse-filter-icon"><SlidersHorizontal size={16} aria-hidden="true" /></span><h2>Bộ lọc</h2></div>
        <div className="browse-filter-controls">
          <label className="browse-filter-sort"><span><ArrowUpDown size={13} aria-hidden="true" /> Sắp xếp</span>
            <select aria-label="Sắp xếp dịch vụ" value={filter.sortBy ?? "newest"} onChange={event => onChange({ sortBy: event.target.value as PublicListingsFilter["sortBy"], page: 1 })}>
              {sorts.map(sort => <option key={sort.value} value={sort.value}>{sort.label}</option>)}
            </select>
          </label>
          <button type="button" className="browse-filter-toggle" aria-controls="browse-filter-content" aria-expanded={isExpanded} onClick={() => setIsExpanded(value => !value)}>
            <SlidersHorizontal size={14} aria-hidden="true" />
            {isExpanded ? "Thu gọn" : "Bộ lọc nâng cao"}
            {activeCount > 0 && <span>{activeCount}</span>}
          </button>
        </div>
      </div>

      <div id="browse-filter-content" className="browse-filter-content">
        <label className="browse-filter-field browse-filter-location"><span><MapPin size={14} aria-hidden="true" /> Địa điểm / khu vực</span>
          <input type="text" maxLength={100} value={draft.location ?? ""} onChange={event => setDraft({ ...draft, location: event.target.value })} placeholder="Ví dụ: Hà Nội, TP.HCM" />
        </label>
        <div className="browse-filter-price-grid">
          <label className="browse-filter-field"><span>Giá từ (đ)</span><MoneyInput aria-label="Giá dịch vụ tối thiểu" value={draft.minPrice ?? null} min={0} max={SERVICE_PRICE_LIMIT} fractionDigits={0} placeholder="Không giới hạn" onValueChange={value => setDraft({ ...draft, minPrice: value ?? undefined })} /></label>
          <label className="browse-filter-field"><span>Giá đến (đ)</span><MoneyInput aria-label="Giá dịch vụ tối đa" value={draft.maxPrice ?? null} min={0} max={SERVICE_PRICE_LIMIT} fractionDigits={0} placeholder="Không giới hạn" onValueChange={value => setDraft({ ...draft, maxPrice: value ?? undefined })} /></label>
        </div>
        <div className="browse-filter-field browse-filter-budget"><span>Ngân sách tham khảo</span>
          <div className="service-price-presets browse-price-presets" aria-label="Mức giá nhanh">
            {priceRanges.map(range => <button key={range.label} type="button" className={draft.minPrice === range.min && draft.maxPrice === range.max ? "is-selected" : ""}
              onClick={() => setDraft({ ...draft, minPrice: range.min, maxPrice: range.max })}>{range.label}</button>)}
          </div>
        </div>
        <label className="browse-filter-field browse-filter-rating"><span>Đánh giá nhà cung cấp</span>
          <select aria-label="Lọc đánh giá nhà cung cấp" value={draft.minRating ?? ""} onChange={event => setDraft({ ...draft, minRating: event.target.value ? Number(event.target.value) : undefined })}>
            <option value="">Tất cả nhà cung cấp</option><option value="4">Từ 4 sao</option><option value="4.5">Từ 4,5 sao</option><option value="5">5 sao</option>
          </select>
          <small>Chỉ áp dụng khi shop có ít nhất 3 đánh giá.</small>
        </label>
        {error && <p className="service-filter-error" role="alert">{error}</p>}
        <div className="browse-filter-actions">
          <button type="button" className="service-filter-reset" onClick={onReset}>Xóa bộ lọc</button>
          <button type="submit" className="service-filter-apply">Áp dụng</button>
        </div>
      </div>
    </form>
  );
}
