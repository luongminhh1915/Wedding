import { normalizeProvince } from "../../../data/vietnamProvinces.ts";
import type { RegisterVendorRequest } from "../../../types/auth.ts";
import { normalizeOperatingAreas } from "./operatingAreasModel.ts";

export type RegistrationRole = "customer" | "vendor";

export type RegistrationValues = {
  fullName: string;
  email: string;
  phoneNumber: string;
  password: string;
  confirmPassword: string;
  brandName: string;
  city: string;
  operatingRegions?: string[];
  nationwide?: boolean;
  partnershipPolicyAccepted: boolean;
};

export type RegistrationErrors = Partial<Record<keyof RegistrationValues, string>>;

export const EMPTY_REGISTRATION: RegistrationValues = {
  fullName: "", email: "", phoneNumber: "", password: "", confirmPassword: "",
  brandName: "", city: "", partnershipPolicyAccepted: false,
};

export function normalizeRegistration(values: RegistrationValues): RegistrationValues {
  return {
    ...values,
    fullName: values.fullName.trim(),
    email: values.email.trim().toLowerCase(),
    phoneNumber: values.phoneNumber.trim(),
    brandName: values.brandName.trim(),
    city: normalizeProvince(values.city) ?? values.city.trim(),
  };
}

export function validateRegistration(values: RegistrationValues, role: RegistrationRole): RegistrationErrors {
  const fields = normalizeRegistration(values);
  const errors: RegistrationErrors = {};
  if (!fields.fullName || fields.fullName.length > 150) errors.fullName = "Họ và tên cần có từ 1 đến 150 ký tự.";
  if (!fields.email || fields.email.length > 150 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(fields.email)) {
    errors.email = "Nhập email hợp lệ, không quá 150 ký tự.";
  }
  if (!/^0\d{9}$/.test(fields.phoneNumber)) errors.phoneNumber = "Số điện thoại gồm 10 chữ số và bắt đầu bằng 0.";
  if (!fields.password.trim() || fields.password.length < 8 || new TextEncoder().encode(fields.password).length > 72) {
    errors.password = "Mật khẩu cần ít nhất 8 ký tự và tối đa 72 byte (ký tự có dấu có thể chiếm nhiều byte).";
  }
  if (fields.password !== fields.confirmPassword || !fields.confirmPassword) errors.confirmPassword = "Mật khẩu nhập lại chưa khớp.";
  if (role === "vendor") {
    if (!fields.brandName || fields.brandName.length > 200) errors.brandName = "Tên thương hiệu cần có từ 1 đến 200 ký tự.";
    const areas = normalizeOperatingAreas(fields.city, fields.operatingRegions, fields.nationwide, true);
    if (areas.error) errors.city = areas.error;
    if (!fields.partnershipPolicyAccepted) errors.partnershipPolicyAccepted = "Vui lòng xác nhận đã hiểu chính sách hoa hồng theo danh mục dịch vụ.";
  }
  return errors;
}

// Explicit allowlist: never send a rate, policy version, role or other client-controlled financial value.
export function vendorRegistrationRequest(values: RegistrationValues): RegisterVendorRequest {
  if (!values.partnershipPolicyAccepted) throw new Error("Cần xác nhận chính sách hợp tác trước khi đăng ký.");
  const fields = normalizeRegistration(values);
  const areas = normalizeOperatingAreas(fields.city, fields.operatingRegions, fields.nationwide, true);
  if (areas.error) throw new Error(areas.error);
  return {
    fullName: fields.fullName, email: fields.email, phoneNumber: fields.phoneNumber, password: fields.password,
    brandName: fields.brandName, city: areas.nationwide ? "" : areas.operatingRegions[0], partnershipPolicyAccepted: true,
    ...(values.operatingRegions !== undefined || values.nationwide !== undefined ? { operatingRegions: areas.operatingRegions, nationwide: areas.nationwide } : {}),
  };
}

// Supports both ASP.NET validation dictionaries and the API's message arrays.
export function registrationApiError(error: unknown): string {
  const response = (error as { response?: { data?: unknown } } | null)?.response?.data;
  if (response && typeof response === "object") {
    const data = response as { message?: unknown; errors?: unknown };
    const messages = new Set<string>();
    const collect = (value: unknown): void => {
      if (typeof value === "string" && value.trim()) messages.add(value.trim());
      else if (Array.isArray(value)) value.forEach(collect);
      else if (value && typeof value === "object") {
        const item = value as { errorMessage?: unknown; message?: unknown };
        if (typeof item.errorMessage === "string") collect(item.errorMessage);
        else if (typeof item.message === "string") collect(item.message);
        else Object.values(value).forEach(collect);
      }
    };
    collect(data.errors);
    if (messages.size) return [...messages].join(" ");
    if (typeof data.message === "string" && data.message.trim()) return data.message;
  }
  return "Chưa thể tạo tài khoản. Vui lòng kiểm tra kết nối và thử lại.";
}
