import { useQuery } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";
import type { CategoryDto } from "../../../types/category.types";

export interface VendorCategoryOptions {
  categories: CategoryDto[];
  existingCategories: CategoryDto[];
  requiresMembershipReview: boolean;
  guidance: string | null;
  businessGroupIds: string[];
}

export function useVendorCategories() {
  const user = useAuthStore(state => state.user);
  const token = useAuthStore(state => state.token);
  return useQuery<VendorCategoryOptions>({
    queryKey: ["vendor-service-categories", user?.id],
    enabled: !!token && user?.role === "VendorOwner",
    queryFn: async () => (await axiosClient.get<VendorCategoryOptions>("/api/categories/my")).data,
    staleTime: 0,
  });
}
