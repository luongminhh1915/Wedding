import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import { apiError } from "../../journey/api";
import type { WeddingInvitation, SaveInvitationRequest, GuestRsvp, PublicInvitation, SubmitRsvpRequest, RsvpResponse } from "../../../types/invitation.types";
import { useAuthStore } from "../../../store/authStore";

async function get<T>(url: string): Promise<T> { try { return (await axiosClient.get<T>(url)).data; } catch (e) { throw new Error(apiError(e)); } }
async function post<T>(url: string, data: unknown): Promise<T> { try { return (await axiosClient.post<T>(url, data)).data; } catch (e) { throw new Error(apiError(e)); } }
export function useMyInvitation() {
  const qc = useQueryClient();
  const { user, token } = useAuthStore();
  const query = useQuery({ queryKey: ["my-invitation", user?.id], enabled: !!token && user?.role === "Customer", queryFn: async () => (await get<WeddingInvitation | null>("/api/invitations/my-invitation")) || null });
  const save = useMutation({ mutationFn: (data: SaveInvitationRequest) => post<WeddingInvitation>("/api/invitations", data), onSuccess: saved => { qc.setQueryData(["my-invitation", user?.id], saved); void qc.invalidateQueries({ queryKey: ["public-invitation"] }); void qc.invalidateQueries({ queryKey: ["planner-overview"] }); } });
  return { invitation: query.data, isLoading: query.isLoading, isError: query.isError, error: query.error, refetch: query.refetch, saveInvitation: save.mutateAsync, isSaving: save.isPending };
}
export const useInvitationRsvps = (status?: string) => useQuery({ queryKey: ["my-rsvps", status], queryFn: () => get<GuestRsvp[]>(`/api/invitations/my-rsvps${status ? `?status=${encodeURIComponent(status)}` : ""}`) });
export const usePublicInvitation = (slug: string) => useQuery({ queryKey: ["public-invitation", slug], enabled: !!slug, queryFn: () => get<PublicInvitation>(`/api/invitations/${encodeURIComponent(slug)}`) });
export function useSubmitRsvp(slug: string) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (data: SubmitRsvpRequest) => post<RsvpResponse>(`/api/invitations/${encodeURIComponent(slug)}/rsvp`, data), onSuccess: () => qc.invalidateQueries() });
}
