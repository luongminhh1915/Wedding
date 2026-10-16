import type { User } from "../types/auth";

export function dashboardPath(user: User | null) {
  if (!user || user.role === "Customer") return "/customer/dashboard";
  return user.role === "VendorOwner" ? "/vendor/dashboard" : "/admin/dashboard";
}
