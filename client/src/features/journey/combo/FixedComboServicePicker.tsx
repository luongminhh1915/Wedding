import { Link } from "react-router-dom";
import type { CatalogItem } from "../comboApi";
import { ComboPhoto } from "./ComboPhoto";
import { isFixedComboService, type ComboEditorValues } from "./comboTemplateEditorModel";

export const comboVnd = (value: number) => `${value.toLocaleString("vi-VN")} VNĐ`;
export function FixedComboServicePicker({ form, items, search, onSearch, onChange }: {
  form: ComboEditorValues; items: CatalogItem[]; search: string; onSearch: (value: string) => void; onChange: (value: ComboEditorValues) => void;
}) {
  const eligible = items.filter(item => isFixedComboService(item, form.ownerVendorId));
  const normalizedSearch = search.trim().toLocaleLowerCase("vi-VN");
  const filtered = eligible.filter(item => `${item.name} ${item.categoryName ?? ""}`.toLocaleLowerCase("vi-VN").includes(normalizedSearch));
  const toggle = (item: CatalogItem) => {
    const selected = form.items.some(slot => slot.serviceItemId === item.id);
    onChange({ ...form, items: selected ? form.items.filter(slot => slot.serviceItemId !== item.id) : [...form.items, { categoryId: item.categoryId, listingId: item.listingId, serviceItemId: item.id, isOptional: false, allowedItemIds: [] }] });
  };
  return <section aria-label="Chọn dịch vụ cho combo" className="rounded-xl border border-[#dedccf] bg-white p-4">
    <h4 className="text-sm font-semibold">A. Chọn dịch vụ ({form.items.length} đã chọn)</h4>
    <p className="mt-2 text-xs leading-relaxed muted">Chọn ít nhất 2 dịch vụ độc lập đã duyệt và đang hoạt động của shop. Bạn có thể chọn nhiều dịch vụ cùng danh mục; một dịch vụ có thể dùng trong nhiều combo.</p>
    <p className="mt-2 text-xs leading-relaxed muted">Mỗi thành phần gồm 1 gói theo giá bán lẻ đã lưu. Dịch vụ tính theo bàn, khách hoặc giờ chưa dùng trong combo cố định vì cần cấu hình số lượng riêng; mở Dịch vụ của tôi để thiết lập gói phù hợp khi cần.</p>
    <label className="mt-3 block"><span className="label">Tìm theo tên hoặc danh mục</span><input className="input" type="search" value={search} maxLength={150} onChange={event => onSearch(event.target.value)} placeholder="Tên dịch vụ, danh mục..." /></label>
    <div className="mt-3 grid max-h-96 gap-3 overflow-y-auto sm:grid-cols-2">
      {filtered.map(item => { const checked = form.items.some(slot => slot.serviceItemId === item.id); const duplicateSource = !checked && form.items.some(slot => slot.listingId === item.listingId); return <label key={item.id} className={`flex min-w-0 cursor-pointer items-start gap-3 rounded-xl border p-3 ${checked ? "border-[#233a42] bg-[#fcf6ea]" : "border-[#dedccf]"}`}>
        <input type="checkbox" aria-label={`Chọn ${item.name}`} checked={checked} disabled={duplicateSource || !checked && form.items.length >= 12} className="mt-1" onChange={() => toggle(item)} />
        <ComboPhoto className="h-16 w-16 shrink-0 rounded-lg" sources={[item.imageUrl]} alt={item.name} />
        <span className="min-w-0 text-sm"><strong className="block break-words">{item.name}</strong><span className="mt-1 block text-xs muted">{item.categoryName || "Dịch vụ của shop"}</span><span className="mt-1 block font-semibold">{comboVnd(item.basePrice)} <small className="font-normal">/ {item.unit === "Package" ? "gói" : item.unit}</small></span>{duplicateSource && <span className="block text-xs text-amber-800">Đã chọn dịch vụ gốc này</span>}</span>
      </label>; })}
    </div>
    {!filtered.length && <p className="mt-3 text-xs muted">{eligible.length ? "Không có dịch vụ phù hợp từ khóa." : "Bạn chưa có dịch vụ đủ điều kiện. Hãy tạo dịch vụ, thiết lập giá và gửi kiểm duyệt tại Dịch vụ của tôi."}</p>}
    {eligible.length < 2 && <p className="mt-3 text-xs text-amber-900">{eligible.length === 1 ? "Shop mới có 1 dịch vụ đủ điều kiện; cần thêm ít nhất 1 dịch vụ được duyệt và đang hoạt động." : "Dịch vụ đang chờ duyệt hoặc tạm ngừng chưa thể thêm vào combo."} <Link className="underline" to="/vendor/dashboard?tab=listings">Đi đến Dịch vụ của tôi</Link></p>}
    <div className="mt-4 border-t border-[#dedccf] pt-3"><h5 className="text-sm font-semibold">Dịch vụ đã chọn</h5><ul className="mt-2 space-y-2">{form.items.map((slot, index) => { const item = items.find(item => item.id === slot.serviceItemId); return <li key={slot.serviceItemId ?? index} className="flex items-start justify-between gap-3 rounded-lg bg-[#f8f7f2] p-2 text-xs"><span>{item?.name ?? "Dịch vụ cũ không còn khả dụng"}{item && <span className="mt-1 block muted">{item.categoryName || "Dịch vụ của shop"} · {comboVnd(item.basePrice)}</span>}{item && !isFixedComboService(item, form.ownerVendorId) && <span className="mt-1 block text-amber-900">Không còn đủ điều kiện, hãy bỏ hoặc cập nhật dịch vụ trước khi lưu.</span>}</span><button type="button" className="shrink-0 underline" onClick={() => onChange({ ...form, items: form.items.filter((_, at) => at !== index) })}>Bỏ dịch vụ</button></li>; })}</ul>{!form.items.length && <p className="mt-2 text-xs muted">Chưa chọn dịch vụ nào.</p>}</div>
  </section>;
}
