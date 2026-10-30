import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import type { WeddingProfile } from "../api";
import { useAuthStore } from "../../../store/authStore";

export interface ExtendedProfile extends WeddingProfile {
  engagementDate?: string | null; planningRole?: string; events?: string[]; priorities?: string[];
}
export interface Member { id: string; userId: string; name: string; email: string; role: "Owner" | "Editor" | "Viewer" }
export interface Members { myRole: Member["role"]; members: Member[] }
export interface GuestInput { id?: string; name: string; phone?: string; group: string; side: string; partySize: number; status: string; dietary?: string; notes?: string; tableId?: string | null }
export interface Guest extends GuestInput { id: string }
export interface SeatTable { id: string; name: string; capacity: number; assigned: number }
export interface GuestsOverview { myRole: string; guests: Guest[]; tables: SeatTable[] }
const get = async <T,>(path: string) => (await axiosClient.get<T>(path)).data;
export function useMembers() { const { user, token } = useAuthStore(); return useQuery({ queryKey: ["wedding-members", user?.id], enabled: !!token && user?.role === "Customer", queryFn: () => get<Members>("/api/wedding/members") }); }
export function useGuests() { const { user, token } = useAuthStore(); return useQuery({ queryKey: ["wedding-guests", user?.id], enabled: !!token && user?.role === "Customer", queryFn: () => get<GuestsOverview>("/api/wedding/guests") }); }
export function usePlannerAction() {
  const query = useQueryClient();
  return useMutation({
    mutationFn: async ({ path, method = "post", data }: { path: string; method?: "post" | "put" | "delete"; data?: unknown }) =>
      (await axiosClient.request({ url: `/api/wedding/${path}`, method, data })).data,
    onSuccess: () => query.invalidateQueries(),
  });
}
export function useSaveExtendedProfile() {
  const query = useQueryClient();
  return useMutation({ mutationFn: async (profile: Omit<ExtendedProfile, "id" | "daysLeft"> & { generatePlan: boolean }) =>
    (await axiosClient.put<ExtendedProfile>("/api/wedding/profile", profile)).data,
    onSuccess: () => query.invalidateQueries() });
}
