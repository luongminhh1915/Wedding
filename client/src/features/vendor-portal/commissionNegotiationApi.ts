import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../services/axiosClient";

export const NegotiationStatus = {
  Submitted: "Submitted",
  InDiscussion: "InDiscussion",
  CounterOffered: "CounterOffered",
  Approved: "Approved",
  Rejected: "Rejected",
  Cancelled: "Cancelled",
} as const;
export type NegotiationStatus = (typeof NegotiationStatus)[keyof typeof NegotiationStatus];

export const NegotiationReasonCategory = {
  NewPartner: "NewPartner",
  HighValueOrder: "HighValueOrder",
  VolumeGrowth: "VolumeGrowth",
  MarginCost: "MarginCost",
  StrategicLongTerm: "StrategicLongTerm",
  Other: "Other",
} as const;
export type NegotiationReasonCategory = (typeof NegotiationReasonCategory)[keyof typeof NegotiationReasonCategory];

export const REASON_CATEGORY_LABELS: Record<NegotiationReasonCategory, string> = {
  NewPartner: "Đối tác mới gia nhập nền tảng",
  HighValueOrder: "Gói dịch vụ / đơn tiệc giá trị cao",
  VolumeGrowth: "Cam kết doanh số / số lượng lớn",
  MarginCost: "Chi phí nguyên vật liệu cao / biên lợi nhuận thấp",
  StrategicLongTerm: "Hợp tác chiến lược dài hạn",
  Other: "Lý do kinh doanh khác",
};

export const NEGOTIATION_STATUS_BADGES: Record<
  NegotiationStatus,
  { label: string; bg: string; text: string; border: string }
> = {
  Submitted: {
    label: "Chờ ban quản trị tiếp nhận",
    bg: "bg-amber-50",
    text: "text-amber-800",
    border: "border-amber-200",
  },
  InDiscussion: {
    label: "Đang trao đổi",
    bg: "bg-blue-50",
    text: "text-blue-800",
    border: "border-blue-200",
  },
  CounterOffered: {
    label: "Sàn đề xuất mức mới",
    bg: "bg-purple-50",
    text: "text-purple-800",
    border: "border-purple-200",
  },
  Approved: {
    label: "Đã chấp thuận",
    bg: "bg-emerald-50",
    text: "text-emerald-800",
    border: "border-emerald-200",
  },
  Rejected: {
    label: "Đã từ chối",
    bg: "bg-rose-50",
    text: "text-rose-800",
    border: "border-rose-200",
  },
  Cancelled: {
    label: "Đã hủy",
    bg: "bg-slate-100",
    text: "text-slate-600",
    border: "border-slate-200",
  },
};

export interface CommissionNegotiationMessageDto {
  id: string;
  negotiationRequestId: string;
  senderId: string;
  senderName: string;
  senderRole: string;
  message: string;
  proposedRateSnapshot?: number | null;
  attachmentUrl?: string | null;
  createdAt: string;
}

export interface CommissionNegotiationDto {
  id: string;
  vendorId: string;
  vendorName: string;
  scope: number; // 3: Vendor, 4: VendorCategory, 5: Service
  categoryId?: string | null;
  categoryName?: string | null;
  serviceItemId?: string | null;
  serviceItemName?: string | null;
  currentRate: number;
  proposedRate: number;
  counterRate?: number | null;
  approvedRate?: number | null;
  reasonCategory: NegotiationReasonCategory;
  reasonCategoryName: string;
  reasonDetail: string;
  cooperationPlan?: string | null;
  status: NegotiationStatus;
  createdById: string;
  createdByName: string;
  assignedModeratorId?: string | null;
  assignedModeratorName?: string | null;
  decidedById?: string | null;
  decidedByName?: string | null;
  decidedAt?: string | null;
  decisionReason?: string | null;
  resultingPolicyRuleId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  version: string;
  messages: CommissionNegotiationMessageDto[];
}

export interface CreateCommissionNegotiationPayload {
  scope: number;
  categoryId?: string | null;
  serviceItemId?: string | null;
  proposedRate: number;
  reasonCategory: NegotiationReasonCategory;
  reasonDetail: string;
  cooperationPlan?: string | null;
}

// Queries
export const useMyNegotiations = (status?: NegotiationStatus) =>
  useQuery({
    queryKey: ["my-commission-negotiations", status ?? "all"],
    queryFn: async () => {
      const { data } = await axiosClient.get<CommissionNegotiationDto[]>("/api/commission-negotiations/my", {
        params: status ? { status } : undefined,
      });
      return data;
    },
  });

export const useNegotiationDetail = (id?: string) =>
  useQuery({
    queryKey: ["commission-negotiation-detail", id],
    queryFn: async () => {
      if (!id) return null;
      const { data } = await axiosClient.get<CommissionNegotiationDto>(`/api/commission-negotiations/${id}`);
      return data;
    },
    enabled: Boolean(id),
  });

export const useAdminNegotiations = (filters?: {
  status?: NegotiationStatus;
  vendorId?: string;
  scope?: number;
}) =>
  useQuery({
    queryKey: ["admin-commission-negotiations", filters?.status, filters?.vendorId, filters?.scope],
    queryFn: async () => {
      const { data } = await axiosClient.get<CommissionNegotiationDto[]>("/api/commission-negotiations/admin", {
        params: filters,
      });
      return data;
    },
  });

// Mutations
export const useCreateNegotiation = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateCommissionNegotiationPayload) => {
      const { data } = await axiosClient.post<CommissionNegotiationDto>("/api/commission-negotiations", payload);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["my-commission-negotiations"] });
    },
  });
};

export const useSendNegotiationMessage = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      message,
      attachmentUrl,
    }: {
      id: string;
      message: string;
      attachmentUrl?: string | null;
    }) => {
      const { data } = await axiosClient.post<CommissionNegotiationDto>(
        `/api/commission-negotiations/${id}/messages`,
        { message, attachmentUrl }
      );
      return data;
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ["commission-negotiation-detail", data.id] });
      qc.invalidateQueries({ queryKey: ["my-commission-negotiations"] });
      qc.invalidateQueries({ queryKey: ["admin-commission-negotiations"] });
    },
  });
};

export const useCounterOfferNegotiation = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      expectedVersion,
      counterRate,
      message,
    }: {
      id: string;
      expectedVersion: string;
      counterRate: number;
      message: string;
    }) => {
      const { data } = await axiosClient.post<CommissionNegotiationDto>(
        `/api/commission-negotiations/${id}/counter-offer`,
        { expectedVersion, counterRate, message }
      );
      return data;
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ["commission-negotiation-detail", data.id] });
      qc.invalidateQueries({ queryKey: ["admin-commission-negotiations"] });
      qc.invalidateQueries({ queryKey: ["my-commission-negotiations"] });
    },
  });
};

export const useRespondToCounterOffer = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      expectedVersion,
      accept,
      message,
      newProposedRate,
    }: {
      id: string;
      expectedVersion: string;
      accept: boolean;
      message: string;
      newProposedRate?: number | null;
    }) => {
      const { data } = await axiosClient.post<CommissionNegotiationDto>(
        `/api/commission-negotiations/${id}/respond-counter`,
        { expectedVersion, accept, message, newProposedRate }
      );
      return data;
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ["commission-negotiation-detail", data.id] });
      qc.invalidateQueries({ queryKey: ["my-commission-negotiations"] });
      qc.invalidateQueries({ queryKey: ["admin-commission-negotiations"] });
    },
  });
};

export const useDecideNegotiation = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      expectedVersion,
      approve,
      approvedRate,
      reason,
      createPolicyRule = true,
    }: {
      id: string;
      expectedVersion: string;
      approve: boolean;
      approvedRate?: number | null;
      reason: string;
      createPolicyRule?: boolean;
    }) => {
      const { data } = await axiosClient.post<CommissionNegotiationDto>(
        `/api/commission-negotiations/${id}/decide`,
        { expectedVersion, approve, approvedRate, reason, createPolicyRule }
      );
      return data;
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ["commission-negotiation-detail", data.id] });
      qc.invalidateQueries({ queryKey: ["admin-commission-negotiations"] });
      qc.invalidateQueries({ queryKey: ["my-commission-negotiations"] });
      qc.invalidateQueries({ queryKey: ["commission-policy-rules"] });
    },
  });
};

export const useCancelNegotiation = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, reason }: { id: string; reason: string }) => {
      const { data } = await axiosClient.post<CommissionNegotiationDto>(
        `/api/commission-negotiations/${id}/cancel`,
        { reason }
      );
      return data;
    },
    onSuccess: (data) => {
      qc.invalidateQueries({ queryKey: ["commission-negotiation-detail", data.id] });
      qc.invalidateQueries({ queryKey: ["my-commission-negotiations"] });
      qc.invalidateQueries({ queryKey: ["admin-commission-negotiations"] });
    },
  });
};

