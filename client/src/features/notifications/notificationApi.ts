import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "../../services/axiosClient";
import { useAuthStore } from "../../store/authStore";
import type { NotificationPage } from "./notificationModel";

export function useNotifications(page = 1, pageSize = 8, unreadOnly = false) {
  const { user, token, validated } = useAuthStore();
  return useQuery({
    queryKey: ["notifications", user?.id, page, pageSize, unreadOnly],
    enabled: !!user && !!token && validated,
    queryFn: async ({ signal }) => (await axios.get<NotificationPage>("/api/notifications", {
      params: { page, pageSize, unreadOnly }, signal, headers: { Authorization: `Bearer ${token}` },
    })).data,
    refetchInterval: 60000,
  });
}

export function useReadNotifications() {
  const token = useAuthStore(s => s.token);
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string | null) => axios.post(id ? `/api/notifications/${id}/read` : "/api/notifications/read-all", {},
      { headers: { Authorization: `Bearer ${token}` } }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["notifications"] }),
  });
}
