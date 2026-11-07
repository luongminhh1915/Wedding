import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import axiosClient from "../../services/axiosClient";
import { useAuthStore } from "../../store/authStore";
import type { ModerationFilters, ModerationPage } from "./moderationQueueModel";
import { moderationActor, sameModerationActor, type BulkModerationResult, type ModerationActor } from "./bulkModerationModel";

export type AutoApproveResult = BulkModerationResult;

export function useModerationQueue(filters: ModerationFilters, options?: { refetchInterval?: number | false }) {
  const session = useAuthStore();
  const { user, token, sessionRevision } = session;
  const actor = moderationActor(session);
  return useQuery({
    queryKey: ["moderation-queue", user?.id, user?.role, sessionRevision, filters],
    enabled: !!actor,
    refetchInterval: options?.refetchInterval ?? false,
    queryFn: async ({ signal }) => {
      if (!actor || !sameModerationActor(actor, useAuthStore.getState())) throw new Error("Phiên kiểm duyệt đã thay đổi.");
      const result = await axiosClient.get<ModerationPage>("/api/moderation-queue", { params: filters, signal, headers: { Authorization: `Bearer ${token}` } });
      if (!sameModerationActor(actor, useAuthStore.getState())) throw new Error("Phiên kiểm duyệt đã thay đổi.");
      return result.data;
    },
  });
}

export function useAutoApproveAll() {
  const qc = useQueryClient();
  const { user, token, sessionRevision } = useAuthStore();
  const flight = useRef<AbortController | null>(null);
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; flight.current?.abort(); flight.current = null; };
  }, [user?.id, user?.role, token, sessionRevision]);
  return useMutation({
    retry: false,
    mutationFn: async ({ type, actor }: { type: string; actor: ModerationActor }) => {
      if (!mounted.current || !sameModerationActor(actor, useAuthStore.getState())) throw new Error("Phiên kiểm duyệt đã thay đổi. Xác nhận lại bằng tài khoản hiện tại.");
      if (flight.current) throw new Error("Đang xử lý một lượt duyệt.");
      const controller = new AbortController(); flight.current = controller;
      try {
        const { data } = await axiosClient.post<AutoApproveResult>("/api/moderation-queue/auto-approve-all", null, {
          params: { type, batchSize: 20 }, signal: controller.signal, headers: { Authorization: `Bearer ${actor.token}` },
        });
        if (!mounted.current || !sameModerationActor(actor, useAuthStore.getState())) throw new Error("Phiên kiểm duyệt đã thay đổi.");
        return data;
      } finally { if (flight.current === controller) flight.current = null; }
    },
    onSuccess: async (_data, request) => {
      if (!mounted.current || !sameModerationActor(request.actor, useAuthStore.getState())) return;
      await qc.invalidateQueries({ queryKey: ["moderation-queue"] });
      await qc.invalidateQueries({ queryKey: ["admin-listings"] });
      await qc.invalidateQueries({ queryKey: ["listings"] });
      await qc.invalidateQueries({ queryKey: ["combos"] });
      await qc.invalidateQueries({ queryKey: ["combo-templates"] });
      await qc.invalidateQueries({ queryKey: ["catalog"] });
    },
  });
}
