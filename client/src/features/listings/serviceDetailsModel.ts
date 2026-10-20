import type { FullServiceDetailsDto, ServiceDetailsDto } from "../../types/listing.types.ts";

type TextKey = Exclude<keyof ServiceDetailsDto, "portfolioUrls" | "videoUrl" | "fullService">;
export interface ServiceTextField { key: TextKey; label: string; maxLength: number; }
export const SERVICE_DETAIL_SECTIONS: { title: string; fields: ServiceTextField[] }[] = [
  { title: "Giới thiệu & cách niêm yết giá", fields: [
    { key: "shortDescription", label: "Mô tả ngắn", maxLength: 500 },
    { key: "priceUnit", label: "Đơn vị / cách niêm yết giá", maxLength: 80 },
    { key: "highlights", label: "Điểm nổi bật", maxLength: 4000 },
  ] },
  { title: "Nội dung & quy trình dịch vụ", fields: [
    { key: "includedItems", label: "Các hạng mục bao gồm", maxLength: 4000 },
    { key: "excludedItems", label: "Các hạng mục không bao gồm", maxLength: 4000 },
    { key: "duration", label: "Thời gian thực hiện", maxLength: 4000 },
    { key: "process", label: "Quy trình cung cấp", maxLength: 4000 },
    { key: "serviceOptions", label: "Tùy chọn / gói dịch vụ", maxLength: 4000 },
  ] },
  { title: "Điều kiện & lưu ý", fields: [
    { key: "additionalFees", label: "Mô tả phụ phí dự kiến", maxLength: 4000 },
    { key: "terms", label: "Điều kiện sử dụng dịch vụ", maxLength: 4000 },
    { key: "reschedulePolicy", label: "Điều kiện thay đổi lịch", maxLength: 4000 },
    { key: "customerRequirements", label: "Yêu cầu dành cho khách hàng", maxLength: 4000 },
    { key: "notes", label: "Ghi chú đặc biệt", maxLength: 4000 },
  ] },
];
export const FULL_SERVICE_FIELDS: { key: keyof FullServiceDetailsDto; label: string }[] = [
  { key: "scope", label: "Phạm vi tổ chức & trách nhiệm điều phối" },
  { key: "capacity", label: "Quy mô đám cưới có thể phục vụ" },
  { key: "stages", label: "Các giai đoạn thực hiện" },
  { key: "outsourcing", label: "Dịch vụ thuê ngoài / khoản chi hộ" },
  { key: "managementFeeDescription", label: "Mô tả phí tổ chức / quản lý / điều phối" },
  { key: "packagePriceDescription", label: "Mô tả tổng giá gói nếu có" },
  { key: "extraCostTerms", label: "Điều kiện phát sinh chi phí" },
  { key: "paymentTerms", label: "Tiến độ & điều kiện thanh toán dự kiến" },
];

export function emptyFullServiceDetails(): FullServiceDetailsDto {
  return { scope: "", capacity: "", stages: "", outsourcing: "", managementFeeDescription: "", packagePriceDescription: "", extraCostTerms: "", paymentTerms: "" };
}
export function emptyServiceDetails(): ServiceDetailsDto {
  return { shortDescription: "", priceUnit: "", includedItems: "", excludedItems: "", duration: "", process: "", serviceOptions: "", additionalFees: "", terms: "", reschedulePolicy: "", portfolioUrls: [], videoUrl: null, highlights: "", customerRequirements: "", notes: "", fullService: null };
}

/** Whitelist informational metadata only; never transport financial snapshots, rates or cancellation state. */
export function normalizeServiceDetails(value?: Partial<ServiceDetailsDto> | null): ServiceDetailsDto {
  const result = emptyServiceDetails();
  for (const section of SERVICE_DETAIL_SECTIONS) for (const field of section.fields)
    result[field.key] = typeof value?.[field.key] === "string" ? value[field.key]!.trim() : "";
  result.portfolioUrls = Array.isArray(value?.portfolioUrls) ? value.portfolioUrls.map(url => typeof url === "string" ? url.trim() : "").filter(Boolean) : [];
  result.videoUrl = typeof value?.videoUrl === "string" && value.videoUrl.trim() ? value.videoUrl.trim() : null;
  if (value?.fullService) {
    const full = emptyFullServiceDetails();
    for (const field of FULL_SERVICE_FIELDS) full[field.key] = typeof value.fullService[field.key] === "string" ? value.fullService[field.key].trim() : "";
    if (Object.values(full).some(Boolean)) result.fullService = full;
  }
  return result;
}

export function isServiceDetailsLink(reference: string): boolean {
  const value = reference.trim();
  if (!value || value.length > 1000 || /\s/.test(value) || !/^https?:\/\//i.test(value)) return false;
  let decoded = value;
  for (let depth = 0; depth < 5; depth++) {
    if (decoded.includes("\\") || Array.from(decoded).some(character => { const code = character.charCodeAt(0); return code < 32 || code >= 127 && code <= 159; }) || /(^|\/)\.{1,2}(\/|[?#]|$)/.test(decoded)) return false;
    let next: string;
    try { next = decodeURIComponent(decoded); } catch { return false; }
    if (next === decoded) break;
    if (depth === 4) return false;
    decoded = next;
  }
  try { const url = new URL(value); return !!url.hostname && !url.username && !url.password; }
  catch { return false; }
}

export function validateServiceDetails(value: ServiceDetailsDto): Record<string, string> {
  const normalized = normalizeServiceDetails(value);
  const errors: Record<string, string> = {};
  for (const section of SERVICE_DETAIL_SECTIONS) for (const field of section.fields)
    if (normalized[field.key].length > field.maxLength) errors[`serviceDetails.${field.key}`] = `${field.label} tối đa ${field.maxLength} ký tự.`;
  if (normalized.portfolioUrls.length > 10 || normalized.portfolioUrls.some(url => !isServiceDetailsLink(url))) errors["serviceDetails.portfolioUrls"] = "Tối đa 10 liên kết portfolio HTTP(S) hợp lệ, mỗi liên kết tối đa 1000 ký tự, không chứa tài khoản hoặc đường dẫn bất thường.";
  if (normalized.videoUrl && !isServiceDetailsLink(normalized.videoUrl)) errors["serviceDetails.videoUrl"] = "Dùng liên kết video HTTP(S) hợp lệ, tối đa 1000 ký tự; không nhập mã nhúng.";
  if (normalized.fullService) for (const field of FULL_SERVICE_FIELDS)
    if (normalized.fullService[field.key].length > 4000) errors[`serviceDetails.fullService.${field.key}`] = `${field.label} tối đa 4000 ký tự.`;
  if (JSON.stringify(normalized).length > 60000) errors.serviceDetails = "Nội dung bổ sung quá dài. Vui lòng rút gọn trước khi lưu.";
  return errors;
}

export function serviceDetailsUpdate(value: ServiceDetailsDto, original?: ServiceDetailsDto | null): ServiceDetailsDto | undefined {
  const normalized = normalizeServiceDetails(value);
  if (JSON.stringify(normalized) === JSON.stringify(normalizeServiceDetails(original))) return undefined;
  const errors = validateServiceDetails(normalized);
  if (Object.keys(errors).length) throw new Error(Object.values(errors)[0]);
  return normalized;
}
