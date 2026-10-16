import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";

export interface InvitationTemplate { id: string; key: string; name: string; category: string; primaryColor: string; backgroundColor: string; motif: string; isPremium: boolean }
export const useInvitationTemplates = () => useQuery({ queryKey: ["invitation-templates"], queryFn: async () => (await axiosClient.get<InvitationTemplate[]>("/api/invitation-templates")).data, staleTime: 300000 });
export function useManageInvitation() {
  const query = useQueryClient();
  return useMutation({ mutationFn: async ({ id, published, remove }: { id: string; published?: boolean; remove?: boolean }) =>
    remove ? axiosClient.delete(`/api/invitations/${id}`) : axiosClient.put(`/api/invitations/${id}/publish`, { published }),
    onSuccess: () => query.invalidateQueries() });
}
export interface InvitationContent { welcome?: string; dressCode?: string; ogTitle?: string; ogDescription?: string; font?: string; album?: string[]; events?: { name: string; date: string; venue: string; address: string }[]; sections?: { story?: boolean; gallery?: boolean; wishes?: boolean } }
export function parseInvitationContent(json?: string): InvitationContent {
  try {
    const value = JSON.parse(json || "{}");
    if (!value || Array.isArray(value) || typeof value !== "object") return {};
    const text = (key: string) => typeof value[key] === "string" ? value[key].slice(0, 3000) : undefined;
    return { welcome: text("welcome"), dressCode: text("dressCode"), ogTitle: text("ogTitle"), ogDescription: text("ogDescription"), font: text("font"),
      album: Array.isArray(value.album) ? value.album.filter((v: unknown) => typeof v === "string" && /^https?:\/\//i.test(v)).slice(0, 12) : [],
      events: Array.isArray(value.events) ? value.events.filter((v: Record<string, unknown>) => v && typeof v.name === "string" && typeof v.date === "string" && typeof v.venue === "string" && typeof v.address === "string").slice(0, 10) : [],
      sections: value.sections && typeof value.sections === "object" ? { story: value.sections.story !== false, gallery: value.sections.gallery !== false, wishes: value.sections.wishes !== false } : {} };
  } catch { return {}; }
}
