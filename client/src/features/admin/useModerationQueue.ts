import { useQuery } from "@tanstack/react-query";
import axiosClient from "../../services/axiosClient";
import { useAuthStore } from "../../store/authStore";
import type { ModerationFilters, ModerationPage } from "./moderationQueueModel";

export function useModerationQueue(filters: ModerationFilters) {
  const { user, token } = useAuthStore();
  return useQuery({ queryKey: ["moderation-queue", user?.id, user?.role, filters], enabled: !!token && ["SuperAdmin", "Moderator"].includes(user?.role ?? ""),
    queryFn: async () => (await axiosClient.get<ModerationPage>("/api/moderation-queue", { params: filters })).data });
}
