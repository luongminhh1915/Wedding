// API + hooks cho hành trình cặp đôi kiểu Weddy (P1–P6). Gọi backend .NET qua axiosClient sẵn có.
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../services/axiosClient";
import type { AuthResponse } from "../../types/auth";
import { useAuthStore } from "../../store/authStore";

const get = async <T,>(url: string) => (await axiosClient.get<T>(url)).data;
const post = async <T,>(url: string, body?: unknown) => (await axiosClient.post<T>(url, body ?? {})).data;
const put = async <T,>(url: string, body?: unknown) => (await axiosClient.put<T>(url, body ?? {})).data;

export function apiError(e: unknown, fallback = "Đã có lỗi xảy ra, vui lòng thử lại.") {
  const err = e as { response?: { data?: { message?: string; errors?: Record<string, string[]> } } };
  const errors = err.response?.data?.errors;
  if (errors) return Object.values(errors).flat()[0] ?? fallback;
  return err.response?.data?.message || fallback;
}

// ---------- P1: Auth OTP / Google ----------
export const authApi = {
  requestOtp: (target: string) => post<{ sent: boolean; expiresInSeconds: number }>("/api/auth/otp/request", { target }),
  verifyOtp: (target: string, code: string, fullName?: string) => post<AuthResponse>("/api/auth/otp/verify", { target, code, fullName }),
  google: (idToken: string) => post<AuthResponse>("/api/auth/google", { idToken }),
};

// ---------- P1: Planner ----------
export interface WeddingProfile {
  id: string; groomName: string; brideName: string; weddingDate?: string | null; city: string;
  totalBudget: number; guestCount: number; style: string; partnerEmail?: string | null; daysLeft?: number | null;
  engagementDate?: string | null; planningRole?: string; events?: string[]; priorities?: string[]; onboardingCompletedAt?: string | null;
}
export interface PlannerOverview {
  profile: WeddingProfile | null; checklistTotal: number; checklistDone: number; budgetPlanned: number; budgetActual: number;
  leadCount: number; contractCount: number; appointmentUpcoming: number; guestCount: number; rsvpAttending: number;
}
export type SaveProfileInput = Omit<WeddingProfile, "id" | "daysLeft"> & { generatePlan: boolean };

export function usePlannerOverview() {
  const { user, token } = useAuthStore();
  return useQuery({ queryKey: ["planner-overview", user?.id], enabled: !!token && user?.role === "Customer", queryFn: () => get<PlannerOverview>("/api/planner/overview") });
}
export function useSaveProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: SaveProfileInput) => put<WeddingProfile>("/api/planner/profile", input),
    onSuccess: () => qc.invalidateQueries(),
  });
}

// ---------- P2: Premium ----------
export interface PremiumInfo { isPremium: boolean; price: number; paymentRef: string; qrUrl: string; activatedAt?: string | null }
export const premiumApi = {
  quote: () => post<PremiumInfo>("/api/planner/invitation/premium?confirmPaid=false"),
  confirm: () => post<PremiumInfo>("/api/planner/invitation/premium?confirmPaid=true"),
};

// ---------- P3: Appointments ----------
export interface AppointmentType {
  id: string; vendorId: string; vendorName: string; name: string; description?: string; durationMinutes: number; capacity: number;
  workingDays: string; openTime: string; closeTime: string; depositAmount: number; requiresApproval: boolean; isActive: boolean;
}
export interface Slot { startAt: string; localTime: string; remaining: number }
export interface Appointment {
  id: string; code: string; appointmentTypeId: string; typeName: string; vendorId: string; vendorName: string; customerId: string;
  customerName: string; startAt: string; endAt: string; attendees: number; note?: string; status: AppointmentStatus;
  depositAmount: number; depositPaid: boolean; vendorNote?: string; depositQrUrl?: string | null;
}
export type AppointmentStatus = "AwaitingDeposit" | "Pending" | "Confirmed" | "Rejected" | "Cancelled" | "Completed" | "NoShow" | "Expired";

export const useVendorAppointmentTypes = (vendorId?: string) =>
  useQuery({ queryKey: ["appt-types", vendorId], enabled: !!vendorId, queryFn: () => get<AppointmentType[]>(`/api/appointments/vendors/${vendorId}/types`) });
export const useSlots = (typeId?: string, date?: string) =>
  useQuery({ queryKey: ["appt-slots", typeId, date], enabled: !!typeId && !!date, queryFn: () => get<Slot[]>(`/api/appointments/types/${typeId}/slots?date=${date}`) });
/** Tìm ngày gần nhất (trong danh sách ứng viên) NCC còn khung giờ trống – dùng gợi ý khi ngày đang chọn là ngày nghỉ. */
export const useNextAvailableDate = (typeId: string | undefined, candidates: string[], enabled: boolean) =>
  useQuery({
    queryKey: ["appt-next-date", typeId, candidates[0], candidates.length], enabled: enabled && !!typeId && candidates.length > 0,
    queryFn: async () => {
      for (const d of candidates) {
        const slots = await get<Slot[]>(`/api/appointments/types/${typeId}/slots?date=${d}`);
        if (slots.some((x) => x.remaining > 0)) return d;
      }
      return null;
    },
  });
export const useMyAppointments = (enabled = true) => useQuery({ queryKey: ["my-appointments"], enabled, queryFn: () => get<Appointment[]>("/api/appointments/my") });
export const useVendorAppointments = () => useQuery({ queryKey: ["vendor-appointments"], queryFn: () => get<Appointment[]>("/api/appointments/vendor") });
export const useMyAppointmentTypes = () => useQuery({ queryKey: ["my-appt-types"], queryFn: () => get<AppointmentType[]>("/api/appointments/vendor/types") });

export function useBookAppointment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (b: { appointmentTypeId: string; startAt: string; attendees: number; note?: string; leadId?: string | null }) =>
      post<Appointment>("/api/appointments", b),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["my-appointments"] }).then(() => qc.invalidateQueries({ queryKey: ["appt-slots"] })),
  });
}
export function useAppointmentAction() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, op, note, newStartAt }: { id: string; op: string; note?: string; newStartAt?: string }) =>
      post<Appointment>(`/api/appointments/${id}/${op}`, { note, newStartAt }),
    onSuccess: () => qc.invalidateQueries(),
  });
}
export function useSaveAppointmentType() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (b: Omit<Partial<AppointmentType>, "id"> & { id?: string | null }) => post<AppointmentType>("/api/appointments/vendor/types", b),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["my-appt-types"] }),
  });
}

// ---------- P5: Combo ----------
export interface ComboOption { listingId: string; vendorId: string; vendorName: string; vendorVerified: boolean; ratingAvg: number; title: string; price: number; maxPrice: number; location: string; imageUrl?: string | null }
export interface ComboCategory { categoryId: string; name: string; slug: string; icon?: string | null; options: ComboOption[] }
export interface ComboTemplate { id: string; name: string; tier: "Saving" | "Popular" | "Luxury" | string; style: string; description?: string; imageUrl?: string | null; isActive: boolean; estimatedTotal: number; items: { categoryId: string; listingId: string; isOptional: boolean }[] }
export interface ComboBuilder { categories: ComboCategory[]; templates: ComboTemplate[]; discountTiers: { minItems: number; percent: number }[] }
export interface ComboOrderItem { id: string; listingId: string; vendorId: string; vendorName: string; title: string; price: number; discountShare: number; leadId?: string | null; status: string; voucherCode?: string | null }
export interface ComboOrder { id: string; code: string; status: string; createdAt: string; weddingDate?: string | null; guestCount?: number | null; subtotal: number; discountPercent: number; discountAmount: number; total: number; items: ComboOrderItem[] }

export const useComboBuilder = () => useQuery({ queryKey: ["combo-builder"], queryFn: () => get<ComboBuilder>("/api/combos/builder") });
export const useMyCombos = () => useQuery({ queryKey: ["my-combos"], queryFn: () => get<ComboOrder[]>("/api/combos/orders/my") });
export function useSubmitCombo() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (b: { templateId?: string | null; listingIds: string[]; weddingDate?: string | null; guestCount?: number | null; phoneNumber?: string; note?: string }) =>
      post<ComboOrder>("/api/combos/orders", b),
    onSuccess: () => qc.invalidateQueries(),
  });
}
export function useCancelCombo() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (id: string) => post(`/api/combos/orders/${id}/cancel`), onSuccess: () => qc.invalidateQueries() });
}

// ---------- P4: Quotes + Settlements ----------
export interface QuoteLine { name: string; quantity: number; unitPrice: number }
export interface Quote { id: string; code: string; leadId: string; vendorId: string; vendorName: string; customerId: string; items: QuoteLine[]; totalAmount: number; depositAmount: number; terms?: string; validUntil: string; status: string; version: number; createdAt: string; contractId?: string | null }
export interface Settlement { id: string; vendorId: string; vendorName: string; period: string; commissionCount: number; totalAmount: number; paidAmount: number; status: string; dueDate: string; paymentReference: string; paidAt?: string | null; invoiceNumber?: string | null; qrUrl: string }

export const useQuotes = (leadId?: string) => useQuery({ queryKey: ["quotes", leadId ?? "all"], queryFn: () => get<Quote[]>(`/api/quotes${leadId ? `?leadId=${leadId}` : ""}`) });
export function useSendQuote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (b: { leadId: string; items: QuoteLine[]; depositAmount: number; terms?: string; validDays: number }) => post<Quote>("/api/quotes", b),
    onSuccess: () => qc.invalidateQueries(),
  });
}
export function useRespondQuote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, accept, note }: { id: string; accept: boolean; note?: string }) => post<Quote>(`/api/quotes/${id}/${accept ? "accept" : "reject"}`, { note }),
    onSuccess: () => qc.invalidateQueries(),
  });
}
export const useSettlements = (period?: string) => useQuery({ queryKey: ["settlements", period ?? ""], queryFn: () => get<Settlement[]>(`/api/settlements${period ? `?period=${period}` : ""}`) });
export function useRunSettlement() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (period: string) => post<Settlement[]>(`/api/settlements/run?period=${period}`), onSuccess: () => qc.invalidateQueries({ queryKey: ["settlements"] }) });
}
export function usePaySettlement() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (id: string) => post<Settlement>(`/api/settlements/${id}/mock-payment`, {}), onSuccess: () => qc.invalidateQueries() });
}

// ---------- P6: Governance ----------
export interface Verification { id: string; vendorId: string; vendorName: string; businessLicenseNumber: string; documentUrls: string[]; note?: string; status: string; reviewNote?: string; createdAt: string; reviewedAt?: string | null }
export interface Dispute { id: string; code: string; contractId: string; contractCode: string; openedByRole: string; reason: string; evidenceUrl?: string; status: string; resolution?: string; slaDeadline: string; createdAt: string; resolvedAt?: string | null; slaBreached: boolean }
export interface AdminKpi { customers: number; vendors: number; verifiedVendors: number; pendingVerifications: number; leadsThisMonth: number; leadsSlaBreached: number; contractsConfirmed: number; gmv: number; commissionPending: number; commissionPaid: number; openDisputes: number; comboOrders: number; upcomingAppointments: number; leadToContractRate: number }
export interface AuditLog { id: string; createdAt: string; actorId?: string; actorRole?: string; action: string; entityType: string; entityId?: string; dataJson?: string }
export interface Article { id: string; title: string; slug: string; excerpt: string; content: string; thumbnailUrl?: string; category: string; isPublished: boolean; publishedAt?: string; viewCount: number; listingIds?: string[] }

export const useMyVerifications = () => useQuery({ queryKey: ["my-verifications"], queryFn: () => get<Verification[]>("/api/verifications/my") });
export const useVerifications = () => useQuery({ queryKey: ["verifications"], queryFn: () => get<Verification[]>("/api/verifications") });
export function useSubmitVerification() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (b: { businessLicenseNumber: string; documentUrls: string[]; note?: string }) => post<Verification>("/api/verifications", b), onSuccess: () => qc.invalidateQueries() });
}
export function useReviewVerification() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: ({ id, approve, note }: { id: string; approve: boolean; note?: string }) => post<Verification>(`/api/verifications/${id}/review`, { approve, note }), onSuccess: () => qc.invalidateQueries() });
}
export const useMyDisputes = () => useQuery({ queryKey: ["my-disputes"], queryFn: () => get<Dispute[]>("/api/disputes/my") });
export const useDisputes = () => useQuery({ queryKey: ["disputes"], queryFn: () => get<Dispute[]>("/api/disputes") });
export function useOpenDispute() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (b: { contractId: string; reason: string; evidenceUrl?: string }) => post<Dispute>("/api/disputes", b), onSuccess: () => qc.invalidateQueries() });
}
export function useResolveDispute() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...b }: { id: string; inFavorOfCustomer: boolean; cancelContract: boolean; resolution: string; retainedDeposit?: number }) => post<Dispute>(`/api/disputes/${id}/resolve`, { id, ...b }),
    onSuccess: () => qc.invalidateQueries(),
  });
}
export const useAdminKpi = () => useQuery({ queryKey: ["admin-kpi"], queryFn: () => get<AdminKpi>("/api/admin/dashboard") });
export const useAuditLogs = (enabled: boolean) => useQuery({ queryKey: ["audit-logs"], enabled, queryFn: () => get<AuditLog[]>("/api/admin/audit-logs?take=50") });
export const useArticles = () => useQuery({ queryKey: ["articles"], queryFn: () => get<Article[]>("/api/articles") });

// ---------- Dữ liệu dùng chung ----------
export interface ContractLite { id: string; contractCode: string; vendorBrandName?: string; contractValue: number; status: string }
export const useMyContractsLite = () =>
  useQuery({
    queryKey: ["my-contracts-lite"],
    queryFn: async () => {
      const d = await get<ContractLite[] | { items: ContractLite[] }>("/api/contracts/my-contracts");
      return Array.isArray(d) ? d : d.items ?? [];
    },
  });
