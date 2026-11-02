import { Building2, CalendarHeart, Camera, Flower2, Grid2X2, Heart, Mail, PartyPopper, Smile, Sparkles, type LucideIcon } from "lucide-react";
import type { CategoryDto } from "../../../types/category.types";
import { serviceCategoryLabel } from "../serviceFilterModel";

// Identity/icon come from the API; unknown icons use one neutral fallback.
const icons: Record<string, LucideIcon> = { Building2, CalendarHeart, Camera, Flower2, Mail, PartyPopper, Smile, Sparkles };
const fallbackBySlug: Record<string, string> = {
  "tiem-cuoi-venues": "Building2", "trang-tri-decor": "Flower2", "quay-chup-photo-video": "Camera",
  "vay-cuoi-vest": "Sparkles", "trang-diem-makeup": "Smile", "thiep-cuoi-qua-tang": "Mail",
  "wedding-planner": "CalendarHeart", "tiec-su-kien-khac": "PartyPopper",
};
interface Props { categories: CategoryDto[]; selectedId: string | null; onSelect: (id: string | null) => void }
export default function CategoryBar({ categories, selectedId, onSelect }: Props) {
  const hasCounts = categories.length > 0 && categories.every(category => Number.isFinite(category.listingCount) && category.listingCount >= 0);
  return <nav className="service-category-rail" aria-label="Danh mục dịch vụ cưới">
    <button type="button" onClick={() => onSelect(null)} className={selectedId === null ? "is-selected" : ""} aria-pressed={selectedId === null}>
      <Grid2X2 size={23} aria-hidden="true" /><span>Tất cả dịch vụ</span>
      {hasCounts && <small>{categories.reduce((sum, category) => sum + category.listingCount, 0)}</small>}
    </button>
    {categories.map(category => {
      const Icon = icons[category.icon ?? ""] ?? icons[fallbackBySlug[category.slug]] ?? Heart;
      const selected = selectedId === category.id;
      return <button key={category.id} type="button" title={category.name} className={selected ? "is-selected" : ""}
        aria-pressed={selected} onClick={() => onSelect(selected ? null : category.id)}>
        <Icon size={23} aria-hidden="true" /><span>{serviceCategoryLabel(category)}</span>
        {Number.isFinite(category.listingCount) && category.listingCount >= 0 && <small>{category.listingCount}</small>}
      </button>;
    })}
  </nav>;
}
