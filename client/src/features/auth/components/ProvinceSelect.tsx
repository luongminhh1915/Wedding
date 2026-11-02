import { useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import { Check, ChevronDown, Search } from "lucide-react";
import { searchProvinces } from "../../../data/vietnamProvinces";
import "./register-page.css";

type Props = {
  id: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  disabled?: boolean;
};

/** Editable ARIA combobox: typed text is not submitted until a real option is chosen. */
export default function ProvinceSelect({ id, value, onChange, error, disabled }: Props) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState(value);
  const [activeIndex, setActiveIndex] = useState(0);
  const input = useRef<HTMLInputElement>(null);
  const matches = searchProvinces(query);
  const active = matches[activeIndex];
  const optionId = (index: number) => `${id}-option-${index}`;
  const close = () => { setOpen(false); setQuery(value); setActiveIndex(0); };
  const select = (province: string) => {
    onChange(province); setQuery(province); setOpen(false); setActiveIndex(0); input.current?.focus();
  };
  const showAll = () => { setQuery(""); setActiveIndex(0); setOpen(true); };
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (!open) showAll();
      else if (matches.length) setActiveIndex((index) => event.key === "ArrowDown" ? (index + 1) % matches.length : (index - 1 + matches.length) % matches.length);
    } else if (event.key === "Enter" && open) {
      event.preventDefault();
      if (active) select(active);
    } else if (event.key === "Escape" && open) {
      event.preventDefault(); close();
    } else if (event.key === "Tab") close();
  };

  return <div className="wm-province-field" onBlur={(event) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) close();
  }}>
    <label htmlFor={id}>Tỉnh / thành phố hoạt động</label>
    <div className="wm-province-input">
      <Search size={17} aria-hidden="true" />
      <input ref={input} id={id} name="citySearch" role="combobox" type="text" autoComplete="off" spellCheck={false}
        aria-autocomplete="list" aria-expanded={open} aria-controls={`${id}-listbox`} aria-required="true"
        aria-activedescendant={open && active ? optionId(activeIndex) : undefined}
        aria-invalid={error ? true : undefined} aria-describedby={error ? `${id}-error` : `${id}-hint`}
        disabled={disabled} value={open ? query : value} placeholder="Tìm hoặc chọn tỉnh / thành phố"
        onFocus={showAll} onClick={() => { if (!open) showAll(); }} onKeyDown={onKeyDown}
        onChange={(event) => { setQuery(event.target.value); setActiveIndex(0); setOpen(true); if (value) onChange(""); }} />
      <ChevronDown size={16} aria-hidden="true" />
    </div>
    <input type="hidden" name="city" value={value} />
    {open && <div className="wm-province-menu">
      <ul id={`${id}-listbox`} role="listbox" aria-label="34 tỉnh / thành phố Việt Nam">
        {matches.map((province, index) => <li key={province} id={optionId(index)} role="option" aria-selected={province === value}
          data-active={index === activeIndex ? "true" : undefined}
          ref={(element) => { if (open && index === activeIndex) element?.scrollIntoView?.({ block: "nearest" }); }}
          onMouseDown={(event) => event.preventDefault()} onClick={() => select(province)}>
          <span>{province}</span>{province === value && <Check size={16} aria-hidden="true" />}
        </li>)}
      </ul>
      {!matches.length && <p role="status">Không tìm thấy địa phương. Thử tên không dấu, ví dụ “Da Nang”.</p>}
      <small>{matches.length}/34 địa phương · ↑ ↓ để chọn, Enter để xác nhận</small>
    </div>}
    <p id={`${id}-hint`} className="wm-province-hint">Danh sách 34 tỉnh / thành phố hiện hành. Có thể tìm bằng tên không dấu.</p>
    {error && <p id={`${id}-error`} className="wm-registration-error">{error}</p>}
  </div>;
}
