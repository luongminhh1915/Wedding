import { normalizeOperatingAreas } from "../auth/components/operatingAreasModel.ts";
import { normalizeRegistration, validateRegistration } from "../auth/components/registrationValidation.ts";
import type { RegistrationErrors, RegistrationValues } from "../auth/components/registrationValidation.ts";

export const ADMIN_CREATABLE_ROLES = ["Customer", "VendorOwner", "Moderator", "Finance", "CustomerCare", "SuperAdmin"] as const;
export type AdminCreatableRole = typeof ADMIN_CREATABLE_ROLES[number];
export const adminRoleNames: Record<AdminCreatableRole, string> = {
  Customer: "Khách hàng", VendorOwner: "Nhà cung cấp", Moderator: "Kiểm duyệt",
  Finance: "Tài chính", CustomerCare: "Chăm sóc khách hàng", SuperAdmin: "SuperAdmin — toàn quyền quản trị",
};
export type AdminCreationErrors = RegistrationErrors & { role?: string; confirmSuperAdminCreation?: string };
export type AdminCreateUserRequest = {
  fullName: string; email: string; phoneNumber: string; password: string; role: AdminCreatableRole;
  brandName?: string; city?: string; operatingRegions?: string[]; nationwide?: boolean; confirmSuperAdminCreation?: boolean;
};
export type AdministrativeActorSnapshot = { id: string | undefined; token: string | null; revision: number };
type AdministrativeSession = { user: { id: string; role: string } | null; token: string | null; sessionRevision: number };

export function administrativeActorIsCurrent(actor: AdministrativeActorSnapshot, session: AdministrativeSession): boolean {
  return Boolean(actor.id && actor.token && session.user?.role === "SuperAdmin" && session.user.id === actor.id &&
    session.token === actor.token && session.sessionRevision === actor.revision);
}

export function validateAdminUserCreation(values: RegistrationValues, role: AdminCreatableRole, confirmSuperAdminCreation: boolean): AdminCreationErrors {
  const errors: AdminCreationErrors = validateRegistration(values, "customer");
  const fields = normalizeRegistration(values);
  if (!ADMIN_CREATABLE_ROLES.includes(role)) errors.role = "Chọn một vai trò hợp lệ.";
  if (role === "VendorOwner") {
    if (!fields.email) delete errors.email;
    if (!fields.phoneNumber) delete errors.phoneNumber;
    if (fields.brandName.length > 200) errors.brandName = "Tên thương hiệu cần tối đa 200 ký tự.";
    const areas = normalizeOperatingAreas(fields.city, fields.operatingRegions, fields.nationwide);
    if (areas.error) errors.city = areas.error;
  }
  if (role === "SuperAdmin") {
    if (fields.password.length < 12) errors.password = "Mật khẩu SuperAdmin cần ít nhất 12 ký tự và tối đa 72 byte.";
    if (!confirmSuperAdminCreation) errors.confirmSuperAdminCreation = "Cần xác nhận cấp toàn quyền SuperAdmin cho tài khoản mới.";
  }
  return errors;
}

/** Strict allowlist: no financial, verification, policy-consent or session fields. */
export function adminCreateUserRequest(values: RegistrationValues, role: AdminCreatableRole, confirmSuperAdminCreation: boolean): AdminCreateUserRequest {
  const errors = validateAdminUserCreation(values, role, confirmSuperAdminCreation);
  const firstError = Object.values(errors)[0];
  if (firstError) throw new Error(firstError);
  const fields = normalizeRegistration(values);
  const payload: AdminCreateUserRequest = {
    fullName: fields.fullName, email: fields.email, phoneNumber: fields.phoneNumber, password: fields.password, role,
  };
  if (role === "VendorOwner") {
    const areas = normalizeOperatingAreas(fields.city, fields.operatingRegions, fields.nationwide);
    payload.brandName = fields.brandName || fields.fullName;
    payload.city = areas.nationwide ? "" : areas.operatingRegions[0] ?? "";
    if (values.operatingRegions !== undefined || values.nationwide !== undefined) { payload.operatingRegions = areas.operatingRegions; payload.nationwide = areas.nationwide; }
  }
  if (role === "SuperAdmin") payload.confirmSuperAdminCreation = true;
  return payload;
}
