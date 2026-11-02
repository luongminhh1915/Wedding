import { useEffect, useLayoutEffect, useRef, useState, type InputHTMLAttributes } from "react";
import { formatMoneyInput, moneyDelete, moneyInputEdit, parseMoneyInput, type MoneyBounds } from "./moneyInputModel";

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, "value" | "defaultValue" | "onChange" | "type" | "min" | "max" | "step"> & MoneyBounds & {
  value: number | null;
  onValueChange: (value: number | null) => void;
};

/** Presentation-only control. Emits numeric amounts; server validation and all pricing rules remain authoritative. */
export function MoneyInput({ value, onValueChange, min = 0, max = 999_999_999_999, fractionDigits = 2, onBlur, onKeyDown, ...props }: Props) {
  const [text, setText] = useState(() => formatMoneyInput(value, fractionDigits));
  const input = useRef<HTMLInputElement>(null);
  const lastEmitted = useRef(value);
  const pendingCaret = useRef<number | null>(null);
  const [selectionRevision, setSelectionRevision] = useState(0);
  const composing = useRef(false);
  useEffect(() => {
    if (!Object.is(value, lastEmitted.current)) {
      if (value === 0 && lastEmitted.current === null && document.activeElement === input.current) return;
      lastEmitted.current = value;
      setText(formatMoneyInput(value, fractionDigits));
    }
  }, [value, fractionDigits]);
  useLayoutEffect(() => {
    if (pendingCaret.current != null) {
      input.current?.setSelectionRange(pendingCaret.current, pendingCaret.current);
      pendingCaret.current = null;
    }
  }, [text, selectionRevision]);
  const change = (raw: string, caret: number): boolean => {
    // Browser edits can leave an incomplete thousands group; dots come from our formatter.
    // Clipboard data is validated strictly before insertion instead of silently stripping punctuation.
    const next = moneyInputEdit(raw, caret, { min, max, fractionDigits });
    if (!next.valid) {
      input.current?.setCustomValidity("Nhập số tiền hợp lệ, dùng dấu chấm phân cách nghìn và dấu phẩy cho phần lẻ.");
      return false;
    }
    input.current?.setCustomValidity(next.complete ? "" : "Nhập đầy đủ số tiền trước khi lưu.");
    pendingCaret.current = next.caret;
    setText(next.text);
    // A leading zero or a separator edit can normalize to unchanged text. Still restore its caret.
    setSelectionRevision(revision => revision + 1);
    if (next.complete) {
      lastEmitted.current = next.value;
      onValueChange(next.value);
    }
    return true;
  };
  return <input {...props} ref={input} type="text" inputMode={fractionDigits ? "decimal" : "numeric"} value={text}
    onChange={event => {
      if (composing.current || (event.nativeEvent as InputEvent).isComposing) {
        pendingCaret.current = null;
        setText(event.target.value);
        return;
      }
      change(event.target.value, event.target.selectionStart ?? event.target.value.length);
    }}
    onCompositionStart={event => { composing.current = true; props.onCompositionStart?.(event); }}
    onCompositionEnd={event => {
      props.onCompositionEnd?.(event);
      if (!composing.current) return;
      composing.current = false;
      if (!change(event.currentTarget.value, event.currentTarget.selectionStart ?? event.currentTarget.value.length))
        setText(formatMoneyInput(value, fractionDigits));
    }}
    onPaste={event => {
      props.onPaste?.(event);
      if (event.defaultPrevented || composing.current) return;
      event.preventDefault();
      const pasted = parseMoneyInput(event.clipboardData.getData("text"), { min: Math.min(min, 0), max, fractionDigits });
      if (!pasted.valid || pasted.value == null) {
        event.currentTarget.setCustomValidity("Số tiền dán vào không hợp lệ. Dùng định dạng 1.000.000 hoặc 1.000.000,50.");
        return;
      }
      const start = event.currentTarget.selectionStart ?? 0;
      const end = event.currentTarget.selectionEnd ?? text.length;
      const inserted = pasted.text;
      change(text.slice(0, start) + inserted + text.slice(end), start + inserted.length);
    }}
    onKeyDown={event => {
      onKeyDown?.(event);
      if (event.defaultPrevented || composing.current || event.nativeEvent.isComposing || event.ctrlKey || event.metaKey || event.altKey || !["Backspace", "Delete"].includes(event.key)) return;
      const element = event.currentTarget;
      if (element.selectionStart !== element.selectionEnd || element.selectionStart == null) return;
      const deletion = moneyDelete(text, element.selectionStart, event.key as "Backspace" | "Delete");
      if (deletion) { event.preventDefault(); change(deletion.raw, deletion.caret); }
    }}
    onBlur={event => {
      composing.current = false;
      event.currentTarget.setCustomValidity("");
      setText(formatMoneyInput(value, fractionDigits));
      onBlur?.(event);
    }} />;
}
