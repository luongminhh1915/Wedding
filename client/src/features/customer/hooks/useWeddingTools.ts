import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../../services/axiosClient";
import type { BudgetSummary, SaveBudgetItemPayload, ChecklistTask, SaveChecklistTaskPayload } from "../../../types/wedding-tools.types";

export function useWeddingBudget() {
  const qc = useQueryClient(); const refresh = () => qc.invalidateQueries();
  const query = useQuery({ queryKey: ["wedding-budget"], queryFn: async () => (await axiosClient.get<BudgetSummary>("/api/wedding-tools/budget")).data });
  const save = useMutation({ mutationFn: async (payload: SaveBudgetItemPayload) => (await axiosClient.post("/api/wedding-tools/budget/items", payload)).data, onSuccess: refresh });
  const remove = useMutation({ mutationFn: async (id: string) => axiosClient.delete(`/api/wedding-tools/budget/items/${id}`), onSuccess: refresh });
  return { budget: query.data, isLoading: query.isLoading, isError: query.isError, error: query.error, refetch: query.refetch, saveItem: save.mutateAsync, isSaving: save.isPending, deleteItem: remove.mutateAsync, isDeleting: remove.isPending };
}
export function useWeddingChecklist() {
  const qc = useQueryClient(); const refresh = () => qc.invalidateQueries();
  const query = useQuery({ queryKey: ["wedding-checklist"], queryFn: async () => (await axiosClient.get<ChecklistTask[]>("/api/wedding-tools/checklist")).data });
  const toggle = useMutation({ mutationFn: async (id: string) => axiosClient.put(`/api/wedding-tools/checklist/tasks/${id}/toggle`), onSuccess: refresh });
  const save = useMutation({ mutationFn: async (payload: SaveChecklistTaskPayload) => (await axiosClient.post("/api/wedding-tools/checklist/tasks", payload)).data, onSuccess: refresh });
  const remove = useMutation({ mutationFn: async (id: string) => axiosClient.delete(`/api/wedding-tools/checklist/tasks/${id}`), onSuccess: refresh });
  return { tasks: query.data ?? [], isLoading: query.isLoading, isError: query.isError, refetch: query.refetch, toggleTask: toggle.mutateAsync, saveTask: save.mutateAsync, isSaving: save.isPending, deleteTask: remove.mutateAsync, isDeleting: remove.isPending };
}
