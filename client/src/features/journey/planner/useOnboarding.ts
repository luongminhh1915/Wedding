import { useQuery } from "@tanstack/react-query";
import { useAuthStore } from "../../../store/authStore";
import axiosClient from "../../../services/axiosClient";
export interface OnboardingStatus { completed: boolean; weddingProfileId?: string | null; role: "Owner" | "Editor" | "Viewer"; completedAt?: string | null }
export function useOnboardingStatus() {
  const { user, token } = useAuthStore();
  return useQuery({ queryKey: ["wedding-onboarding", user?.id], enabled: !!token && user?.role === "Customer", queryFn: async ({ signal }) => (await axiosClient.get<OnboardingStatus>("/api/wedding/onboarding-status", { signal })).data, staleTime: 0 });
}
