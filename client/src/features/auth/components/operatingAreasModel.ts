import { normalizeProvince } from "../../../data/vietnamProvinces.ts";

export type OperatingAreas = { operatingRegions: string[]; nationwide: boolean };

/** Keeps legacy single-city callers compatible; never turns missing coverage into nationwide. */
export function normalizeOperatingAreas(city: string, regions?: readonly string[], nationwide = false, required = false): OperatingAreas & { error?: string } {
  if (nationwide) return { operatingRegions: [], nationwide: true };
  const input = regions?.length ? regions : city.trim() ? [city] : [];
  const normalized: string[] = [];
  for (const value of input) {
    const province = typeof value === "string" ? normalizeProvince(value) : null;
    if (!province) return { operatingRegions: [], nationwide: false, error: "Chọn khu vực trong danh sách 34 tỉnh / thành phố hiện hành." };
    if (!normalized.includes(province)) normalized.push(province);
  }
  if (required && !normalized.length) return { operatingRegions: [], nationwide: false, error: "Chọn ít nhất một tỉnh / thành phố hoặc Toàn quốc." };
  return { operatingRegions: normalized, nationwide: false };
}

export function operatingAreaLabel(regions?: readonly string[] | null, nationwide?: boolean | null, city = ""): string {
  if (nationwide) return "Toàn quốc";
  return regions?.length ? regions.join(", ") : city || "Chưa cập nhật khu vực";
}
