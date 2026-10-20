import { useState } from "react";
import { Search, SlidersHorizontal, X } from "lucide-react";
import type { CatalogBuilder, PublicComboVendor } from "../comboApi";
import { MoneyInput } from "../../../components/MoneyInput";
import { ComboDialog } from "./ComboDialog";
import { COMBO_PRICE_LIMIT, comboFilterChips, type ComboFilters } from "./comboFilterModel";

interface Props {
  filters: ComboFilters; categories: CatalogBuilder["categories"]; vendors: PublicComboVendor[];
  guests: number; templateMode: boolean; lockedVendorName?: string;
  onChange: (changes: Partial<ComboFilters>) => void; onReset: () => void;
}
export function ComboCatalogFilters({ filters, categories, vendors, guests, templateMode, lockedVendorName, onChange, onReset }: Props) {
  const [search, setSearch] = useState(filters.search ?? "");
  const [open, setOpen] = useState(false);
  const chips = comboFilterChips(filters, categories, vendors);
  return <div className="combo-discovery-filters">
    <div className="combo-discovery-toolbar">
      <form className="combo-discovery-search" onSubmit={event => { event.preventDefault(); onChange({ search: search.trim() || undefined }); }}>
        <label><Search size={16} aria-hidden="true" /><span className="sr-only">Tìm combo, dịch vụ hoặc nhà cung cấp</span><input type="search" value={search} maxLength={150} onChange={event => setSearch(event.target.value)} placeholder="Tìm combo, dịch vụ hoặc NCC..." /></label>
        <button type="submit">Tìm</button>
      </form>
      <button type="button" className="combo-secondary combo-discovery-open" aria-haspopup="dialog" aria-expanded={open} onClick={() => setOpen(true)}><SlidersHorizontal size={15} />Bộ lọc{chips.length > 0 && <span>{chips.length}</span>}</button>
      <label className="combo-discovery-sort"><span className="sr-only">Sắp xếp combo và dịch vụ</span><select aria-label="Sắp xếp combo và dịch vụ" value={filters.sort} onChange={event => onChange({ sort: event.target.value as ComboFilters["sort"] })}>
        <option value="recommended">Đề xuất</option><option value="price-asc">Giá tăng dần</option><option value="price-desc">Giá giảm dần</option><option value="newest">Mới nhất</option><option value="rating">Shop được đánh giá cao</option><option value="savings">Tiết kiệm nhiều nhất</option>
      </select></label>
    </div>
    <p className="combo-discovery-price-note">{templateMode ? "Giá combo dự kiến sau ưu đãi" : "Giá dịch vụ dự kiến, chưa áp dụng ưu đãi combo"} cho {guests.toLocaleString("vi-VN")} khách · dịch vụ theo giờ tính 1 giờ; chưa gồm tùy chọn thêm.</p>
    {chips.length > 0 && <div className="combo-discovery-chips" aria-label="Bộ lọc combo đang áp dụng">{chips.map(chip => <button key={chip.key} type="button" onClick={() => onChange(chip.clear)} aria-label={`Bỏ bộ lọc ${chip.label}`}>{chip.label}<X size={12} /></button>)}<button type="button" className="combo-discovery-reset" onClick={onReset}>Xóa tất cả bộ lọc</button></div>}
    {open && <ComboDialog title="Lọc combo & dịch vụ" onClose={() => setOpen(false)}>
      <AdvancedFilters filters={{ ...filters, search: search.trim() || undefined }} categories={categories} vendors={vendors} templateMode={templateMode} lockedVendorName={lockedVendorName}
        onApply={changes => { setOpen(false); onChange(changes); }} onReset={() => { setOpen(false); onReset(); }} />
    </ComboDialog>}
  </div>;
}

function AdvancedFilters({ filters, categories, vendors, templateMode, lockedVendorName, onApply, onReset }: {
  filters: ComboFilters; categories: CatalogBuilder["categories"]; vendors: PublicComboVendor[]; templateMode: boolean;
  lockedVendorName?: string; onApply: (changes: Partial<ComboFilters>) => void; onReset: () => void;
}) {
  const [draft, setDraft] = useState(filters);
  const [error, setError] = useState("");
  const set = (changes: Partial<ComboFilters>) => { setDraft(current => ({ ...current, ...changes })); setError(""); };
  return <form className="combo-advanced-form" onSubmit={event => {
    event.preventDefault();
    if (draft.minPrice != null && draft.maxPrice != null && draft.minPrice > draft.maxPrice) { setError("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu."); return; }
    if (draft.minServices != null && draft.maxServices != null && draft.minServices > draft.maxServices) { setError("Số dịch vụ tối đa phải lớn hơn hoặc bằng tối thiểu."); return; }
    onApply({ ...draft, city: draft.city?.trim() || undefined, region: draft.region?.trim() || undefined });
  }}>
    <p className="combo-advanced-copy">Các điều kiện được áp dụng đồng thời. Lọc danh sách không thay đổi dịch vụ trong giỏ.</p>
    {lockedVendorName && <p className="combo-notice">Giỏ đang thuộc {lockedVendorName}. Chọn lọc một shop khác sẽ không đổi NCC hoặc xóa giỏ hiện tại.</p>}
    <div className="combo-advanced-grid">
      <label>Giá từ (đ)<MoneyInput value={draft.minPrice ?? null} onValueChange={value => set({ minPrice: value ?? undefined })} min={0} max={COMBO_PRICE_LIMIT} fractionDigits={0} placeholder="Không giới hạn" aria-label="Giá combo tối thiểu" /></label>
      <label>Giá đến (đ)<MoneyInput value={draft.maxPrice ?? null} onValueChange={value => set({ maxPrice: value ?? undefined })} min={0} max={COMBO_PRICE_LIMIT} fractionDigits={0} placeholder="Không giới hạn" aria-label="Giá combo tối đa" /></label>
      <label>Địa điểm của shop<input type="text" maxLength={100} value={draft.city ?? ""} onChange={event => set({ city: event.target.value })} placeholder="Ví dụ: Hà Nội" /><small>Thành phố đăng ký của nhà cung cấp.</small></label>
      <label>Khu vực cần phục vụ<input type="text" maxLength={100} value={draft.region ?? ""} onChange={event => set({ region: event.target.value })} placeholder="Ví dụ: Hồ Chí Minh" /><small>Tất cả dịch vụ có sẵn phải phục vụ khu vực này.</small></label>
      <label className="combo-advanced-wide">Nhà cung cấp<select value={draft.filterVendorId ?? ""} onChange={event => set({ filterVendorId: event.target.value || undefined })}><option value="">Tất cả NCC phù hợp giỏ</option>{vendors.map(vendor => <option key={vendor.id} value={vendor.id}>{vendor.name}</option>)}</select></label>
      {templateMode && <>
        <label className="combo-advanced-wide">Combo có danh mục<select value={draft.templateCategoryId ?? ""} onChange={event => set({ templateCategoryId: event.target.value || undefined })}><option value="">Tất cả danh mục dịch vụ</option>{categories.map(category => <option key={category.categoryId} value={category.categoryId}>{category.name}</option>)}</select></label>
        <label>Số dịch vụ từ<select value={draft.minServices ?? ""} onChange={event => set({ minServices: event.target.value ? Number(event.target.value) : undefined })}><option value="">Không giới hạn</option>{Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{i + 1} dịch vụ</option>)}</select></label>
        <label>Số dịch vụ đến<select value={draft.maxServices ?? ""} onChange={event => set({ maxServices: event.target.value ? Number(event.target.value) : undefined })}><option value="">Không giới hạn</option>{Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{i + 1} dịch vụ</option>)}</select></label>
        <label className="combo-advanced-wide">Tiết kiệm ít nhất (đ)<MoneyInput value={draft.minSavings ?? null} onValueChange={value => set({ minSavings: value ?? undefined })} min={0} max={COMBO_PRICE_LIMIT} fractionDigits={0} placeholder="Không giới hạn" /><small>Theo dịch vụ có sẵn, chưa gồm các hạng mục tùy chọn.</small></label>
      </>}
      <label className="combo-advanced-wide">Đánh giá nhà cung cấp<select value={draft.minRating ?? ""} onChange={event => set({ minRating: event.target.value ? Number(event.target.value) : undefined })}><option value="">Tất cả nhà cung cấp</option><option value="4">Từ 4 sao</option><option value="4.5">Từ 4,5 sao</option><option value="5">5 sao</option></select><small>Điểm của shop có ít nhất 3 đánh giá; không phải điểm riêng của combo.</small></label>
    </div>
    {error && <p role="alert" className="co-error">{error}</p>}
    <div className="combo-advanced-actions"><button type="button" className="combo-text-button" onClick={onReset}>Xóa bộ lọc</button><button type="submit" className="combo-primary">Áp dụng bộ lọc</button></div>
  </form>;
}
