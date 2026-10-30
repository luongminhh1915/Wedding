import { MoneyInput } from "../../../components/MoneyInput";
import type { ListingPricingOption } from "../../../types/listing.types";
import { listingPricingValues, type ListingPricingEditor } from "../../vendor-portal/components/listingPricingModel";
import {
  Coins, Layers, Users, CalendarCheck, ShieldAlert, Sparkles, Plus, Trash2, Tag, Info
} from "lucide-react";

/** Pricing and legacy children share the parent service's save and approval. */
export function ServiceItemManager({ value, onChange, options = [], selectionRequired = false, disabled = false, error = "", open = false }: {
  value: ListingPricingEditor; onChange: (value: ListingPricingEditor) => void; options?: ListingPricingOption[];
  selectionRequired?: boolean; disabled?: boolean; error?: string; open?: boolean;
}) {
  return (
    <details open={open || !!error} className="rounded-2xl border border-[#dedccf] bg-[#fcf6ea] p-4 sm:p-5">
      <summary className="cursor-pointer text-sm font-semibold text-[#233a42] flex items-center justify-between">
        <span>Giá bán lẻ, biến thể và món kèm của dịch vụ</span>
        <span className="text-xs font-normal text-slate-500">Tùy chọn combo & bán lẻ</span>
      </summary>
      <p className="mt-2 text-xs leading-relaxed text-slate-600">
        Thiết lập một lần tại dịch vụ này. Sau khi dịch vụ được duyệt và đang hoạt động, bạn có thể chọn vào nhiều combo trong Combo của tôi.
      </p>

      {selectionRequired && (
        <div className="mt-3 bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs">
          <label className="block text-xs font-semibold text-slate-700 mb-1.5 flex items-center gap-1.5">
            <Tag className="w-3.5 h-3.5 text-rose-500" />
            <span>Chọn cấu hình giá cũ cần chỉnh sửa</span>
          </label>
          <select
            className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition"
            value={value.serviceItemId ?? ""}
            disabled={disabled}
            onChange={event => {
              const item = options.find(option => option.id === event.target.value);
              onChange(listingPricingValues(item ? { ...item, serviceItemId: item.id } : null));
            }}
          >
            <option value="">Giữ nguyên tất cả cấu hình cũ</option>
            {options.map(item => (
              <option key={item.id} value={item.id}>
                {item.name} · {item.basePrice.toLocaleString("vi-VN")} VNĐ / {item.unit}
              </option>
            ))}
          </select>
          <p className="mt-1.5 text-xs text-amber-900 flex items-center gap-1">
            <Info className="w-3.5 h-3.5 shrink-0" />
            <span>Chọn cấu hình sẽ dùng làm giá của dịch vụ. Các cấu hình cũ khác và giao dịch đã chốt được giữ nguyên.</span>
          </p>
        </div>
      )}

      <fieldset disabled={disabled || (selectionRequired && !value.serviceItemId)} className="mt-4 grid gap-3.5 sm:grid-cols-2 disabled:opacity-60">
        <legend className="sr-only">Cấu hình giá của dịch vụ</legend>

        {/* Ô 1: Giá bán lẻ chính xác (VNĐ) */}
        <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all flex flex-col justify-between">
          <div>
            <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 mb-1.5">
              <Coins className="w-3.5 h-3.5 text-rose-500" />
              <span>Giá bán lẻ chính xác (VNĐ)</span>
            </label>
            <MoneyInput
              aria-label="Giá bán lẻ chính xác của dịch vụ"
              className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition font-semibold text-rose-600"
              min={0}
              max={100000000000}
              fractionDigits={0}
              value={value.basePrice}
              placeholder="Vd: 5000000"
              onValueChange={basePrice => onChange({ ...value, basePrice })}
            />
          </div>
          <p className="mt-2 text-[11px] text-slate-400 leading-tight">Mức giá bán lẻ chính thức để tính toán đơn hàng và ghép combo.</p>
        </div>

        {/* Ô 2: Đơn vị tính giá */}
        <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all flex flex-col justify-between">
          <div>
            <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 mb-1.5">
              <Layers className="w-3.5 h-3.5 text-rose-500" />
              <span>Đơn vị tính giá</span>
            </label>
            <select
              className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition"
              value={value.unit}
              onChange={event => onChange({ ...value, unit: event.target.value })}
            >
              <option value="Package">Gói dịch vụ (trọn gói)</option>
              <option value="Table">Bàn tiệc (10 khách)</option>
              <option value="Guest">Khách (Người)</option>
              <option value="Hour">Giờ phục vụ</option>
            </select>
          </div>
          <p className="mt-2 text-[11px] text-slate-400 leading-tight">Đơn vị cơ sở để nhân hệ số hoặc tính số lượng đặt dịch vụ.</p>
        </div>

        {/* Ô 3: Quy mô số khách phục vụ */}
        <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all">
          <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 mb-1.5">
            <Users className="w-3.5 h-3.5 text-rose-500" />
            <span>Quy mô số khách phục vụ</span>
          </label>
          <div className="grid grid-cols-2 gap-2.5">
            <div>
              <span className="block text-[11px] text-slate-500 mb-1">Số khách tối thiểu</span>
              <input
                className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition"
                type="number"
                min={1}
                value={value.minGuests}
                onChange={event => onChange({ ...value, minGuests: Number(event.target.value) })}
              />
            </div>
            <div>
              <span className="block text-[11px] text-slate-500 mb-1">Số khách tối đa</span>
              <input
                className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition"
                type="number"
                min={1}
                value={value.maxGuests}
                onChange={event => onChange({ ...value, maxGuests: Number(event.target.value) })}
              />
            </div>
          </div>
        </div>

        {/* Ô 4: Công suất nhận đơn & Khu vực phục vụ */}
        <div className="bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all">
          <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 mb-1.5">
            <CalendarCheck className="w-3.5 h-3.5 text-rose-500" />
            <span>Công suất & Khu vực phục vụ</span>
          </label>
          <div className="grid grid-cols-2 gap-2.5">
            <div>
              <span className="block text-[11px] text-slate-500 mb-1">Số đơn tối đa / ngày</span>
              <input
                className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition"
                type="number"
                min={1}
                max={100}
                value={value.maxBookingsPerDay}
                onChange={event => onChange({ ...value, maxBookingsPerDay: Number(event.target.value) })}
              />
            </div>
            <div>
              <span className="block text-[11px] text-slate-500 mb-1">Khu vực phục vụ</span>
              <input
                className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition"
                maxLength={1000}
                placeholder="* = toàn quốc"
                value={value.regions}
                onChange={event => onChange({ ...value, regions: event.target.value })}
              />
            </div>
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-1">
            <span className="text-[10px] text-slate-400">Chọn nhanh:</span>
            {['*', 'TP. Hồ Chí Minh', 'Hà Nội', 'Đà Nẵng'].map(area => (
              <button
                key={area}
                type="button"
                onClick={() => onChange({ ...value, regions: area })}
                className="px-2 py-0.5 rounded text-[10px] bg-slate-100 hover:bg-rose-50 hover:text-rose-600 text-slate-600 transition"
              >
                {area === '*' ? 'Toàn quốc (*)' : area}
              </button>
            ))}
          </div>
        </div>

        {/* Ô 5: Chính sách hủy của gói (full-width) */}
        <div className="sm:col-span-2 bg-white rounded-xl p-3.5 border border-slate-200/90 shadow-2xs hover:border-rose-300 focus-within:border-rose-400 focus-within:ring-2 focus-within:ring-rose-100 transition-all">
          <label className="flex items-center gap-1.5 text-xs font-semibold text-slate-700 mb-1.5">
            <ShieldAlert className="w-3.5 h-3.5 text-rose-500" />
            <span>Chính sách hủy (để trống để giữ chính sách của dịch vụ)</span>
          </label>
          <textarea
            className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm bg-slate-50/50 focus:bg-white focus:outline-none transition resize-none"
            maxLength={4000}
            rows={2}
            placeholder="Nhập điều kiện hoặc mức hoàn trả nếu khách hủy gói bán lẻ này..."
            value={value.cancellationPolicy ?? ""}
            onChange={event => onChange({ ...value, cancellationPolicy: event.target.value })}
          />
        </div>

        {/* Ô 6: Biến thể và món kèm (full-width accordion card) */}
        <div className="sm:col-span-2 bg-white rounded-xl p-4 border border-slate-200/90 shadow-2xs">
          <details className="group">
            <summary className="cursor-pointer text-xs font-bold text-slate-800 flex items-center justify-between">
              <span className="flex items-center gap-2">
                <Sparkles className="w-4 h-4 text-amber-500" />
                <span>Biến thể ({value.variants.length}) và món kèm ({value.addOns.length})</span>
              </span>
              <span className="text-[11px] font-normal text-rose-600 group-open:rotate-180 transition-transform">▼</span>
            </summary>
            <p className="mt-2 text-xs text-slate-500">
              Thông tin đã có được giữ cùng mã định danh khi lưu. Chỉ bỏ mục khi bạn muốn xóa khỏi cấu hình hiện tại.
            </p>

            {/* Danh sách Biến thể */}
            <div className="mt-3.5 pt-3 border-t border-slate-100 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-slate-700">1. Danh sách biến thể dịch vụ</span>
                <span className="text-[11px] text-slate-400">{value.variants.length}/20</span>
              </div>
              {value.variants.map((variant, index) => (
                <div key={variant.id ?? index} className="p-2.5 rounded-lg bg-slate-50 border border-slate-200 grid gap-2 sm:grid-cols-[1fr_180px_auto] items-center">
                  <input
                    aria-label="Tên biến thể"
                    className="px-2.5 py-1.5 border border-slate-200 rounded text-xs bg-white focus:outline-none"
                    maxLength={200}
                    placeholder="Tên biến thể (vd: Gói nâng cao 500 khách)"
                    value={variant.name}
                    onChange={event => onChange({ ...value, variants: value.variants.map((item, at) => at === index ? { ...item, name: event.target.value } : item) })}
                  />
                  <div className="flex items-center gap-1">
                    <span className="text-[11px] text-slate-500 shrink-0">+/- VNĐ:</span>
                    <MoneyInput
                      aria-label="Giá chênh biến thể"
                      className="w-full px-2 py-1.5 border border-slate-200 rounded text-xs bg-white focus:outline-none font-medium"
                      fractionDigits={0}
                      min={-100000000000}
                      value={variant.priceDelta}
                      onValueChange={priceDelta => onChange({ ...value, variants: value.variants.map((item, at) => at === index ? { ...item, priceDelta: priceDelta ?? 0 } : item) })}
                    />
                  </div>
                  <button
                    type="button"
                    className="inline-flex items-center gap-1 text-xs text-red-600 hover:text-red-700 hover:bg-red-50 p-1.5 rounded transition"
                    onClick={() => onChange({ ...value, variants: value.variants.filter((_, at) => at !== index) })}
                    title="Xóa biến thể này"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                    <span>Xóa</span>
                  </button>
                </div>
              ))}
              <button
                type="button"
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-dashed border-slate-300 hover:border-rose-400 hover:text-rose-600 text-slate-600 text-xs font-semibold transition"
                disabled={value.variants.length >= 20}
                onClick={() => onChange({ ...value, variants: [...value.variants, { name: "", priceDelta: 0 }] })}
              >
                <Plus className="w-3.5 h-3.5" />
                <span>Thêm biến thể mới</span>
              </button>
            </div>

            {/* Danh sách Món kèm */}
            <div className="mt-4 pt-3 border-t border-slate-100 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-semibold text-slate-700">2. Danh sách món kèm / tiện ích thêm</span>
                <span className="text-[11px] text-slate-400">{value.addOns.length}/30</span>
              </div>
              {value.addOns.map((addOn, index) => (
                <div key={addOn.id ?? index} className="p-2.5 rounded-lg bg-slate-50 border border-slate-200 grid gap-2 sm:grid-cols-[1fr_160px_auto_auto] items-center">
                  <input
                    aria-label="Tên món kèm"
                    className="px-2.5 py-1.5 border border-slate-200 rounded text-xs bg-white focus:outline-none"
                    maxLength={200}
                    placeholder="Tên món kèm (vd: Thêm flycam quay tiệc)"
                    value={addOn.name}
                    onChange={event => onChange({ ...value, addOns: value.addOns.map((item, at) => at === index ? { ...item, name: event.target.value } : item) })}
                  />
                  <div className="flex items-center gap-1">
                    <span className="text-[11px] text-slate-500 shrink-0">Giá (VNĐ):</span>
                    <MoneyInput
                      aria-label="Giá món kèm"
                      className="w-full px-2 py-1.5 border border-slate-200 rounded text-xs bg-white focus:outline-none font-medium text-emerald-600"
                      min={0}
                      fractionDigits={0}
                      value={addOn.price}
                      onValueChange={price => onChange({ ...value, addOns: value.addOns.map((item, at) => at === index ? { ...item, price: price ?? 0 } : item) })}
                    />
                  </div>
                  <label className="text-xs text-slate-600 flex items-center gap-1.5 select-none">
                    <input
                      type="checkbox"
                      className="rounded text-rose-500"
                      checked={addOn.perUnit}
                      onChange={event => onChange({ ...value, addOns: value.addOns.map((item, at) => at === index ? { ...item, perUnit: event.target.checked } : item) })}
                    />
                    <span>Theo số lượng</span>
                  </label>
                  <button
                    type="button"
                    className="inline-flex items-center gap-1 text-xs text-red-600 hover:text-red-700 hover:bg-red-50 p-1.5 rounded transition"
                    onClick={() => onChange({ ...value, addOns: value.addOns.filter((_, at) => at !== index) })}
                    title="Xóa món kèm này"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                    <span>Xóa</span>
                  </button>
                </div>
              ))}
              <button
                type="button"
                className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-dashed border-slate-300 hover:border-rose-400 hover:text-rose-600 text-slate-600 text-xs font-semibold transition"
                disabled={value.addOns.length >= 30}
                onClick={() => onChange({ ...value, addOns: [...value.addOns, { name: "", price: 0, perUnit: false }] })}
              >
                <Plus className="w-3.5 h-3.5" />
                <span>Thêm món kèm mới</span>
              </button>
            </div>
          </details>
        </div>
      </fieldset>
      {error && <p role="alert" className="mt-3 text-xs text-red-700">{error}</p>}
    </details>
  );
}
