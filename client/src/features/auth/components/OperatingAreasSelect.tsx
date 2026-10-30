import { useState } from "react";
import { Globe2, Search, X } from "lucide-react";
import { searchProvinces } from "../../../data/vietnamProvinces";
import type { OperatingAreas } from "./operatingAreasModel";

type Props = OperatingAreas & {
  id: string;
  onChange: (value: OperatingAreas) => void;
  disabled?: boolean;
  error?: string;
  required?: boolean;
};

/** Native checkboxes support keyboard selection without submitting typed search text. */
export default function OperatingAreasSelect({ id, operatingRegions, nationwide, onChange, disabled, error, required = false }: Props) {
  const [query, setQuery] = useState("");
  const [expanded, setExpanded] = useState(false);
  const matches = searchProvinces(query);
  const toggleRegion = (province: string) => {
    onChange({ nationwide: false, operatingRegions: operatingRegions.includes(province) ? operatingRegions.filter(value => value !== province) : [...operatingRegions, province] });
  };
  return <fieldset id={id} disabled={disabled} aria-describedby={error ? `${id}-error` : `${id}-hint`} className="min-w-0 rounded-xl border border-[#eadbd4] bg-white p-3 sm:col-span-2">
    <legend className="px-1 text-xs font-semibold text-[#233a42]">Khu vực hoạt động{required ? " *" : " (tùy chọn)"}</legend>
    <label className="flex cursor-pointer items-center gap-2 rounded-lg bg-[#fcf6ea] p-2.5 text-sm font-semibold text-[#233a42]">
      <input type="checkbox" checked={nationwide} aria-invalid={error ? true : undefined} onChange={event => { onChange({ nationwide: event.target.checked, operatingRegions: [] }); setQuery(""); }} />
      <Globe2 size={17} aria-hidden="true" />Toàn quốc
    </label>
    {!nationwide && <>
      {!!operatingRegions.length && <div className="mt-3 flex flex-wrap gap-2" aria-label="Khu vực đã chọn">{operatingRegions.map(province => <button key={province} type="button" className="inline-flex items-center gap-1.5 rounded-full border border-[#eadbd4] bg-[#fcf6ea] px-2.5 py-1.5 text-xs text-[#233a42] disabled:opacity-50" aria-label={`Bỏ khu vực ${province}`} onClick={() => toggleRegion(province)}>{province}<X size={13} aria-hidden="true" /></button>)}</div>}
      <button type="button" className="mt-3 text-sm font-semibold text-[#80554d] underline underline-offset-4" aria-expanded={expanded} aria-controls={`${id}-options`} onClick={() => setExpanded(value => !value)}>{expanded ? "Thu gọn danh sách tỉnh / thành" : operatingRegions.length ? "Thêm / sửa khu vực" : "Chọn tỉnh / thành phố"}</button>
      {expanded && <div id={`${id}-options`} className="mt-3">
        <label htmlFor={`${id}-search`} className="relative block"><span className="sr-only">Tìm tỉnh / thành phố</span><Search size={16} aria-hidden="true" className="absolute left-3 top-3 text-slate-500" /><input id={`${id}-search`} type="search" value={query} onChange={event => setQuery(event.target.value)} placeholder="Tìm tỉnh / thành phố, có thể gõ không dấu" autoComplete="off" className="w-full rounded-lg border border-[#eadbd4] py-2 pl-9 pr-3 text-base text-[#233a42] sm:text-sm" /></label>
        <div className="mt-2 grid max-h-52 grid-cols-1 gap-1 overflow-y-auto rounded-lg border border-[#eadbd4] p-2 sm:grid-cols-2" role="group" aria-label="34 tỉnh / thành phố Việt Nam">{matches.map(province => <label key={province} className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-2 text-sm text-[#233a42] hover:bg-[#fcf6ea]"><input type="checkbox" checked={operatingRegions.includes(province)} onChange={() => toggleRegion(province)} />{province}</label>)}</div>
        {!matches.length && <p role="status" className="mt-2 text-xs text-slate-600">Không tìm thấy địa phương phù hợp.</p>}
      </div>}
    </>}
    <p id={`${id}-hint`} className="mt-2 text-xs leading-relaxed text-slate-600">{nationwide ? "NCC phục vụ trên toàn quốc. Bỏ chọn để chọn các tỉnh / thành cụ thể." : `${operatingRegions.length} khu vực đã chọn. Có thể chọn nhiều địa phương${required ? "." : "; để trống nếu chưa xác định."}`}</p>
    {error && <p id={`${id}-error`} role="alert" className="mt-2 text-xs text-red-700">{error}</p>}
  </fieldset>;
}
