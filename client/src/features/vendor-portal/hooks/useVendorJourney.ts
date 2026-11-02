import { useQuery } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import type { VendorJourneySummary } from "../vendorJourneyModel";

export function useVendorJourney(enabled: boolean) {
  const user = useAuthStore(state => state.user);
  const token = useAuthStore(state => state.token);
  return useQuery<VendorJourneySummary>({
    queryKey: ["vendor-journey", user?.id],
    enabled: enabled && !!token && user?.role === "VendorOwner",
    queryFn: async () => (await axiosClient.get<VendorJourneySummary>("/api/vendor-journey/me")).data,
    // Re-entering the overview must reflect work just saved in another module.
    staleTime: 0,
    retry: 1,
  });
}
