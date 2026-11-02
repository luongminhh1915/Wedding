import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "../../services/axiosClient";
import { useAuthStore } from "../../store/authStore";
import type { PrivateAgreementKind, PrivateAgreementWorkspace } from "./privateAgreementModel";
import { refreshPrivateAgreementQueries } from "./privateAgreementCacheModel";

export function usePrivateServiceAgreements(kind: PrivateAgreementKind, id: string, expanded: boolean, leadId?: string) {
  const { user, token } = useAuthStore();
  const queryKey = ["private-service-agreements", user?.id, user?.role, kind, id, leadId ?? null];
  const path = `/api/private-service-agreements/${kind}/${encodeURIComponent(id)}`;
  const queryPath = leadId ? `${path}?leadId=${encodeURIComponent(leadId)}` : path;
  const query = useQuery({ queryKey, enabled: expanded && !!id && !!token && ["Customer", "VendorOwner"].includes(user?.role ?? ""),
    retry: false, queryFn: async () => (await axios.get<PrivateAgreementWorkspace>(queryPath)).data });
  const cache = useQueryClient();
  const mutation = useMutation({ mutationFn: async ({ suffix = "", body }: { suffix?: string; body: unknown }) => (await axios.post<PrivateAgreementWorkspace>(`${path}${suffix}`, body)).data,
    onSuccess: async data => {
      cache.setQueryData(queryKey, data);
      await refreshPrivateAgreementQueries(cache, queryKey);
    } });
  return { query, mutation, role: user?.role };
}
