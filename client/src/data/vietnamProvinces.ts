// Official provincial-unit directory, checked 18 October 2026: https://chinhphu.vn/p
// Plain names deliberately omit the changing province/municipality administrative prefix.
export const VIETNAM_PROVINCES = [
  "An Giang", "Bắc Ninh", "Cà Mau", "Cao Bằng", "Cần Thơ", "Đà Nẵng",
  "Đắk Lắk", "Điện Biên", "Đồng Nai", "Đồng Tháp", "Gia Lai", "Hà Nội",
  "Hà Tĩnh", "Hải Phòng", "Hồ Chí Minh", "Huế", "Hưng Yên", "Khánh Hòa",
  "Lai Châu", "Lạng Sơn", "Lào Cai", "Lâm Đồng", "Nghệ An", "Ninh Bình",
  "Phú Thọ", "Quảng Ngãi", "Quảng Ninh", "Quảng Trị", "Sơn La", "Tây Ninh",
  "Thái Nguyên", "Thanh Hóa", "Tuyên Quang", "Vĩnh Long",
] as const;

export function provinceSearchText(value: string): string {
  return value.normalize("NFD").replace(/\p{M}/gu, "").toLowerCase().replace(/đ/g, "d").replace(/[^a-z0-9]/g, "");
}

function provinceKey(value: string): string {
  const key = provinceSearchText(value).replace(/^(thanhpho|tinh|tp)/, "");
  return key === "hcm" || key === "saigon" ? "hochiminh" : key === "hn" ? "hanoi" : key;
}

/** Accept common spellings on input; never rewrites previously stored records. */
export function normalizeProvince(value: string): string | null {
  const key = provinceKey(value);
  return VIETNAM_PROVINCES.find((province) => provinceSearchText(province) === key) ?? null;
}

export function searchProvinces(query: string): readonly string[] {
  const key = provinceSearchText(query);
  if (!key) return VIETNAM_PROVINCES;
  const canonical = normalizeProvince(query);
  return VIETNAM_PROVINCES.filter((province) => province === canonical || provinceSearchText(province).includes(key));
}
