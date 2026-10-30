import { useState } from "react";
import { Search } from "lucide-react";
import type { CategoryDto, PublicListingsFilter } from "../../../types/category.types";
import { serviceCategoryLabel } from "../serviceFilterModel";

interface Props { filter: PublicListingsFilter; categories: CategoryDto[]; onChange: (filter: PublicListingsFilter) => void }
export default function SearchFilterBar({ filter, categories, onChange }: Props) {
  const [draft, setDraft] = useState<PublicListingsFilter>(filter);
  return <section className="service-search-panel" aria-label="Tìm và lọc dịch vụ">
    <form onSubmit={event => {
      event.preventDefault();
      onChange({ ...filter, categoryId: draft.categoryId || undefined, keyword: draft.keyword?.trim() || undefined, page: 1 });
    }}>
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
    </form>
  </section>;
}
