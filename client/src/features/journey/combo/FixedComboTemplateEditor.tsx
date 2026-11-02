import { useRef, useState } from "react";
import { useAuthStore } from "../../../store/authStore";
import axios from "../../../services/axiosClient";
import { MoneyInput } from "../../../components/MoneyInput";
import { apiError } from "../api";
import { useComboMutation, type CatalogItem, type CatalogTemplate } from "../comboApi";
import { comboEditorPayload, comboEditorValues, fixedComboTotals, isComboImage, isFixedComboService, newComboEditor } from "./comboTemplateEditorModel";
import { ComboPhoto } from "./ComboPhoto";
import { comboVnd, FixedComboServicePicker } from "./FixedComboServicePicker";

const utcInput = (value?: string | null) => { if (!value) return ""; const date = new Date(/(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? value : `${value}Z`); return Number.isNaN(date.getTime()) ? "" : date.toISOString().slice(0, 16); };
export function FixedComboTemplateEditor({ template, ownerVendorId, items, onClose, staff = false }: {
  template?: CatalogTemplate; ownerVendorId?: string; items: CatalogItem[]; onClose: () => void; maxDiscount?: number; staff?: boolean;
}) {
  const [form, setForm] = useState(() => template ? comboEditorValues(template) : newComboEditor(ownerVendorId));
  const [search, setSearch] = useState("");
  const [failure, setFailure] = useState("");
  const [uploading, setUploading] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);
  const role = useAuthStore(state => state.user?.role);
  const save = useComboMutation<CatalogTemplate>("/api/combos/templates");
  const busy = save.isPending || uploading;
  const owners = [...new Map(items.filter(item => isFixedComboService(item, item.vendorId)).map(item => [item.vendorId, item.vendorName])).entries()];
  const selected = form.items.map(slot => items.find(item => item.id === slot.serviceItemId)).filter((item): item is CatalogItem => !!item);
  const totals = fixedComboTotals(selected, form.comboPrice);
  const validSelection = form.items.length >= 2 && selected.length === form.items.length && selected.every(item => isFixedComboService(item, form.ownerVendorId));
  const submit = async (send: boolean) => {
    setFailure("");
    try { await save.mutateAsync(comboEditorPayload(form, items, send)); onClose(); }
    catch (error) { setFailure(error instanceof Error && !("response" in error) ? error.message : apiError(error)); }
  };
  const upload = async (files: FileList | null) => {
    if (!files?.length) return;
    if (form.content.galleryUrls.filter(url => url.trim()).length + files.length > 10) { setFailure("Gallery tối đa 10 ảnh. Xóa bớt ảnh trước khi tải thêm."); return; }
    setUploading(true); setFailure("");
    try {
      const body = new FormData(); Array.from(files).forEach(file => body.append("files", file));
      const response = await axios.post<{ urls: string[] }>("/api/upload/images", body, { headers: { "Content-Type": "multipart/form-data" } });
      const urls = response.data.urls;
      if (!Array.isArray(urls) || urls.length !== files.length || urls.some(url => !isComboImage(url))) throw new Error("Máy chủ chưa trả đủ ảnh đã tải lên hợp lệ.");
      setForm(previous => ({ ...previous, imageUrl: previous.imageUrl || urls[0], content: { ...previous.content, galleryUrls: [...previous.content.galleryUrls.filter(url => url.trim()), ...urls] } }));
    } catch (error) { setFailure(error instanceof Error && !("response" in error) ? error.message : apiError(error)); }
    finally { setUploading(false); if (fileInput.current) fileInput.current.value = ""; }
  };
  return <section className="mt-5 rounded-2xl border border-[#d9cfad] bg-[#fcf6ea] p-4 sm:p-5" aria-label={form.id ? "Chỉnh sửa combo cố định" : "Tạo combo mới"}>
    <div className="flex flex-wrap items-start justify-between gap-3"><div><h3 className="font-semibold text-[#233a42]">{form.id ? "Chỉnh sửa đề xuất combo" : "Tạo combo từ dịch vụ của shop"}</h3><p className="mt-2 text-xs leading-relaxed text-slate-600">Chọn các dịch vụ có sẵn và nhập giá bán cho toàn bộ gói. Khách gửi yêu cầu theo đúng thành phần; thay đổi riêng cho khách được trao đổi trong chat và xác nhận trên giao dịch.</p></div><button type="button" className="btn btn-outline" disabled={busy} onClick={onClose}>Đóng</button></div>
    <fieldset disabled={busy} className="mt-4 min-w-0 space-y-4"><legend className="sr-only">Nội dung combo cố định</legend>
      {staff && <label className="block"><span className="label">Nhà cung cấp sở hữu combo</span><select className="input" value={form.ownerVendorId ?? ""} disabled={!!template?.ownerVendorId} onChange={event => setForm({ ...form, ownerVendorId: event.target.value, items: [] })}><option value="">Chọn một NCC</option>{form.ownerVendorId && !owners.some(([id]) => id === form.ownerVendorId) && <option value={form.ownerVendorId}>{template?.ownerVendorName || "NCC hiện tại"}</option>}{owners.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>}
      <FixedComboServicePicker form={form} items={items} search={search} onSearch={setSearch} onChange={setForm} />
      <section aria-label="Cấu hình combo" className="rounded-xl border border-[#dedccf] bg-white p-4">
        <h4 className="text-sm font-semibold">B. Cấu hình combo</h4>
        <div className="mt-4 grid min-w-0 gap-3 sm:grid-cols-2">
          <label className="sm:col-span-2"><span className="label">Tên combo</span><input className="input" value={form.name} maxLength={150} onChange={event => setForm({ ...form, name: event.target.value })} /></label>
          <label><span className="label">Giá bán combo (VNĐ)</span><MoneyInput className="input" aria-label="Giá bán combo" min={0} max={100000000000} fractionDigits={0} value={form.comboPrice ?? null} onValueChange={comboPrice => setForm({ ...form, comboPrice })} /></label>
          <label><span className="label">Khu vực phục vụ</span><input className="input" maxLength={1000} value={form.region ?? ""} placeholder="* = toàn quốc" onChange={event => setForm({ ...form, region: event.target.value })} /></label>
          <label><span className="label">Phong cách</span><input className="input" value={form.style} maxLength={50} onChange={event => setForm({ ...form, style: event.target.value })} /></label>
          <label><span className="label">Phân khúc</span><select className="input" value={form.tier} onChange={event => setForm({ ...form, tier: event.target.value })}><option value="Saving">Tiết kiệm</option><option value="Popular">Phổ biến</option><option value="Luxury">Sang trọng</option></select></label>
        </div>
        <dl aria-live="polite" className="mt-4 grid gap-3 rounded-xl bg-[#fcf6ea] p-4 text-sm sm:grid-cols-3"><div><dt className="text-xs muted">Tổng giá bán lẻ</dt><dd className="mt-1 font-semibold">{comboVnd(totals.originalPrice)}</dd></div><div><dt className="text-xs muted">Giá bán combo</dt><dd className="mt-1 font-semibold">{totals.comboPrice == null ? "Chưa nhập giá" : comboVnd(totals.comboPrice)}</dd></div>{totals.savingsAmount > 0 && <div><dt className="text-xs muted">Khách tiết kiệm</dt><dd className="mt-1 font-semibold text-emerald-700">{comboVnd(totals.savingsAmount)} ({totals.savingsPercentage.toLocaleString("vi-VN", { maximumFractionDigits: 2 })}%)</dd></div>}</dl>
        <p className="mt-2 text-xs muted">Tổng bán lẻ được cộng từ giá hiện hành của từng dịch vụ đã chọn. Giá combo do shop nhập; máy chủ kiểm tra điều kiện và chính sách hiện hành khi lưu.</p>
        <label className="mt-4 block"><span className="label">Mô tả ngắn</span><textarea className="input" rows={2} maxLength={4000} value={form.description ?? ""} onChange={event => setForm({ ...form, description: event.target.value })} /></label>
        <label className="mt-3 block"><span className="label">Mô tả chi tiết</span><textarea className="input" rows={4} maxLength={4000} value={form.content.detailedDescription ?? ""} onChange={event => setForm({ ...form, content: { ...form.content, detailedDescription: event.target.value } })} /></label>
        <label className="mt-3 block"><span className="label">Điều kiện và ghi chú</span><textarea className="input" rows={3} maxLength={4000} value={form.content.terms} onChange={event => setForm({ ...form, content: { ...form.content, terms: event.target.value } })} /></label>
      </section>
      <details className="rounded-xl border border-[#dedccf] bg-white p-4"><summary className="cursor-pointer text-sm font-semibold">Ảnh đại diện & gallery ({form.content.galleryUrls.filter(url => url.trim()).length}/10)</summary>
        <label className="mt-3 block"><span className="label">Ảnh đại diện (URL hoặc ảnh đã tải lên)</span><input className="input" maxLength={1000} value={form.imageUrl ?? ""} onChange={event => setForm({ ...form, imageUrl: event.target.value })} /></label>
        {form.imageUrl && <ComboPhoto className="mt-3 h-36 w-full rounded-xl" sources={isComboImage(form.imageUrl) ? [form.imageUrl] : []} alt="Ảnh đại diện combo đang chỉnh" />}
        {role === "VendorOwner" && <div className="mt-3"><input ref={fileInput} type="file" accept="image/jpeg,image/png,image/webp" multiple className="sr-only" aria-label="Chọn ảnh combo từ thiết bị" onChange={event => void upload(event.target.files)} /><button type="button" className="btn btn-outline" disabled={busy || form.content.galleryUrls.filter(url => url.trim()).length >= 10} onClick={() => fileInput.current?.click()}>{uploading ? "Đang tải ảnh..." : "Tải ảnh từ thiết bị"}</button><p className="mt-2 text-xs muted">Tối đa 10 ảnh JPG/PNG/WebP, 8 MB mỗi ảnh.</p></div>}
        <label className="mt-3 block"><span className="label">Gallery · mỗi dòng một đường dẫn ảnh</span><textarea className="input" rows={3} maxLength={11000} value={form.content.galleryUrls.join("\n")} onChange={event => setForm({ ...form, content: { ...form.content, galleryUrls: event.target.value.split(/\r?\n/) } })} /></label>
        <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3">{form.content.galleryUrls.filter(url => url.trim()).map((url, index) => <div key={`${index}-${url}`} className="rounded-xl border border-[#dedccf] p-2"><ComboPhoto className="h-24 w-full rounded-lg" sources={isComboImage(url) ? [url] : []} alt={`Ảnh gallery ${index + 1}`} /><div className="mt-2 flex gap-3 text-xs"><button type="button" className="underline" onClick={() => setForm({ ...form, imageUrl: url })}>Làm ảnh bìa</button><button type="button" className="underline" onClick={() => setForm({ ...form, content: { ...form.content, galleryUrls: form.content.galleryUrls.filter((_, at) => at !== index) } })}>Bỏ ảnh</button></div></div>)}</div>
      </details>
      <details className="rounded-xl border border-[#dedccf] bg-white p-4"><summary className="cursor-pointer text-sm font-semibold">Thời gian áp dụng và hiển thị</summary><div className="mt-3 grid gap-3 sm:grid-cols-2"><label><span className="label">Bắt đầu áp dụng (UTC, tùy chọn)</span><input className="input" type="datetime-local" value={utcInput(form.content.validFromUtc)} onChange={event => setForm({ ...form, content: { ...form.content, validFromUtc: event.target.value ? `${event.target.value}:00Z` : null } })} /></label><label><span className="label">Kết thúc áp dụng (UTC, tùy chọn)</span><input className="input" type="datetime-local" value={utcInput(form.content.validUntilUtc)} onChange={event => setForm({ ...form, content: { ...form.content, validUntilUtc: event.target.value ? `${event.target.value}:00Z` : null } })} /></label><label><span className="label">Thứ tự hiển thị</span><input className="input" type="number" value={form.sortOrder} onChange={event => setForm({ ...form, sortOrder: Number(event.target.value) })} /></label></div></details>
      <p className="text-xs leading-relaxed muted">Lưu nháp chưa xuất bản. Combo phải được duyệt riêng trước khi hiển thị. Bản combo đã duyệt và giao dịch đã xác nhận giữ giá, thành phần đã chốt; đề xuất mới chỉ thay bản công khai sau khi được duyệt.</p>
      {failure && <p role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">{failure}</p>}
      {!validSelection && <p className="text-xs text-amber-900">Chọn ít nhất 2 dịch vụ đủ điều kiện trước khi lưu combo.</p>}
      <div className="flex flex-wrap justify-end gap-2"><button type="button" className="btn btn-outline" disabled={busy || !validSelection} onClick={() => void submit(false)}>Lưu nháp</button><button type="button" className="btn btn-primary" disabled={busy || !validSelection} onClick={() => void submit(true)}>{save.isPending ? "Đang lưu..." : "Lưu & gửi duyệt"}</button></div>
    </fieldset>
  </section>;
}
