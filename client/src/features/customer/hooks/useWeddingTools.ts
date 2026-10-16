import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import type {
  BudgetSummary,
  SaveBudgetItemPayload,
  ChecklistTask,
  SaveChecklistTaskPayload,
} from "../../../types/wedding-tools.types";

export function useWeddingBudget() {
  const queryClient = useQueryClient();

  const budgetQuery = useQuery({
    queryKey: ["wedding-budget"],
    queryFn: async (): Promise<BudgetSummary> => {
      const token = localStorage.getItem("token");
      const res = await fetch("/api/wedding-tools/budget", {
        headers: {
          "Content-Type": "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      });

      if (!res.ok) {
        throw new Error("Không thể tải ngân sách cưới. Vui lòng thử lại.");
      }

      return res.json();
    },
  });

  const saveItemMutation = useMutation({
    mutationFn: async (payload: SaveBudgetItemPayload) => {
      const token = localStorage.getItem("token");
      const res = await fetch("/api/wedding-tools/budget/items", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        throw new Error("Không thể lưu mục chi phí.");
      }
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["wedding-budget"] });
    },
  });

  const deleteItemMutation = useMutation({
    mutationFn: async (id: string) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/wedding-tools/budget/items/${id}`, {
        method: "DELETE",
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      });

      if (!res.ok) {
        throw new Error("Không thể xóa mục chi phí.");
      }
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["wedding-budget"] });
    },
  });

  return {
    budget: budgetQuery.data,
    isLoading: budgetQuery.isLoading,
    isError: budgetQuery.isError,
    error: budgetQuery.error,
    refetch: budgetQuery.refetch,
    saveItem: saveItemMutation.mutateAsync,
    isSaving: saveItemMutation.isPending,
    deleteItem: deleteItemMutation.mutateAsync,
    isDeleting: deleteItemMutation.isPending,
  };
}

export function useWeddingChecklist() {
  const queryClient = useQueryClient();

  const checklistQuery = useQuery({
    queryKey: ["wedding-checklist"],
    queryFn: async (): Promise<ChecklistTask[]> => {
      const token = localStorage.getItem("token");
      const res = await fetch("/api/wedding-tools/checklist", {
        headers: {
          "Content-Type": "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      });

      if (!res.ok) {
        throw new Error("Không thể tải danh sách công việc. Vui lòng thử lại.");
      }

      return res.json();
    },
  });

  const toggleTaskMutation = useMutation({
    mutationFn: async (id: string) => {
      const token = localStorage.getItem("token");
      const res = await fetch(
        `/api/wedding-tools/checklist/tasks/${id}/toggle`,
        {
          method: "PUT",
          headers: {
            ...(token ? { Authorization: `Bearer ${token}` } : {}),
          },
        },
      );

      if (!res.ok) {
        throw new Error("Không thể cập nhật trạng thái.");
      }
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["wedding-checklist"] });
    },
  });

  const saveTaskMutation = useMutation({
    mutationFn: async (payload: SaveChecklistTaskPayload) => {
      const token = localStorage.getItem("token");
      const res = await fetch("/api/wedding-tools/checklist/tasks", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        throw new Error("Không thể lưu công việc.");
      }
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["wedding-checklist"] });
    },
  });

  const deleteTaskMutation = useMutation({
    mutationFn: async (id: string) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/wedding-tools/checklist/tasks/${id}`, {
        method: "DELETE",
        headers: {
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      });

      if (!res.ok) {
        throw new Error("Không thể xóa công việc.");
      }
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["wedding-checklist"] });
    },
  });

  return {
    tasks: checklistQuery.data ?? [],
    isLoading: checklistQuery.isLoading,
    isError: checklistQuery.isError,
    refetch: checklistQuery.refetch,
    toggleTask: toggleTaskMutation.mutateAsync,
    saveTask: saveTaskMutation.mutateAsync,
    isSaving: saveTaskMutation.isPending,
    deleteTask: deleteTaskMutation.mutateAsync,
    isDeleting: deleteTaskMutation.isPending,
  };
}
