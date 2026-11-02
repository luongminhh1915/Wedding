export interface MoneyBounds {
  min?: number;
  max?: number;
  fractionDigits?: 0 | 1 | 2;
}

export function formatMoneyInput(value: number | null | undefined, fractionDigits: 0 | 1 | 2 = 2): string {
  if (value == null || !Number.isFinite(value)) return "";
  return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: fractionDigits }).format(value);
}

/** Vietnamese grouping and decimal separator only. Never strip arbitrary text into a different amount. */
export function parseMoneyInput(raw: string, { min = 0, max = 999_999_999_999, fractionDigits = 2 }: MoneyBounds = {}):
  { valid: true; value: number | null; text: string } | { valid: false } {
  const text = raw.trim().replace(/\s*(?:₫|đ|VNĐ|VND)$/iu, "").trim();
  if (!text) return { valid: true, value: null, text: "" };
  if (!Number.isFinite(min) || !Number.isFinite(max) || min > max) return { valid: false };
  const sign = min < 0 ? "-?" : "";
  const grouping = `(?:\\d+|[1-9]\\d{0,2}(?:\\.\\d{3})+|[1-9]\\d{0,2}(?:[ \\u00a0\\u202f]\\d{3})+)`;
  const decimal = fractionDigits ? `(?:,\\d{0,${fractionDigits}})?` : "";
  if (!new RegExp(`^${sign}${grouping}${decimal}$`).test(text)) return { valid: false };
  const normalized = text.replace(/[. \u00a0\u202f]/g, "").replace(",", ".");
  const value = Number(normalized);
  // Converting to JS numbers must not silently lose any integer VND or decimal digit.
  const [whole, fraction = ""] = normalized.replace(/^-/, "").split(".");
  if (!Number.isFinite(value) || !Number.isSafeInteger(Number(whole + fraction)) || value < min || value > max)
    return { valid: false };
  const formattedWhole = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 0 }).format(Number(whole));
  return { valid: true, value, text: `${normalized.startsWith("-") ? "-" : ""}${formattedWhole}${text.includes(",") ? `,${fraction}` : ""}` };
}

/** Map a logical caret (digits/sign/decimal, excluding separators) into the formatted input. */
export function moneyCaret(raw: string, caret: number, formatted: string): number {
  const logical = raw.slice(0, caret).replace(/[^\d,-]/g, "").length;
  if (!logical) return 0;
  let count = 0;
  for (let index = 0; index < formatted.length; index++) {
    if (/[\d,-]/.test(formatted[index])) count++;
    if (count === logical) return index + 1;
  }
  return formatted.length;
}

/** Browser edit only: formatter-generated dots may be incomplete. Validate pasted text strictly first. */
export function moneyInputEdit(raw: string, caret: number, bounds: MoneyBounds = {}):
  { valid: true; value: number | null; text: string; caret: number; complete: boolean } | { valid: false } {
  if (raw === "-" && (bounds.min ?? 0) < 0)
    return { valid: true, value: null, text: "-", caret: 1, complete: false };
  const next = parseMoneyInput(raw.replaceAll(".", ""), bounds);
  if (!next.valid) return next;
  return { ...next, caret: moneyCaret(raw, caret, next.text), complete: true };
}

/** Backspace/Delete next to a thousands separator must delete a digit, not get stuck on the dot. */
export function moneyDelete(raw: string, caret: number, key: "Backspace" | "Delete"): { raw: string; caret: number } | null {
  if (key === "Backspace" && raw[caret - 1] === "." && /\d/.test(raw[caret - 2] ?? "")) {
    return { raw: raw.slice(0, caret - 2) + raw.slice(caret), caret: caret - 2 };
  }
  if (key === "Delete" && raw[caret] === "." && /\d/.test(raw[caret + 1] ?? "")) {
    return { raw: raw.slice(0, caret) + raw.slice(caret + 2), caret };
  }
  return null;
}
