import { useQuery } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import { readBusinessGroups, readVendorBusinessGroupPolicy } from "../businessGroupPolicy";

export function useBusinessGroups(enabled = true) {
  return useQuery({
    queryKey: ["business-groups"], enabled,
    queryFn: async () => readBusinessGroups((await axiosClient.get("/api/business-groups")).data),
    staleTime: 60_000,
  });
}

export function useVendorBusinessGroupPolicy() {
  const user = useAuthStore(state => state.user);
  return useQuery({
    queryKey: ["vendor-business-group-policy", user?.id], enabled: user?.role === "VendorOwner",
    queryFn: async () => readVendorBusinessGroupPolicy((await axiosClient.get("/api/business-groups/my")).data),
    staleTime: 60_000,
  });
}
