import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../services/axiosClient";

export const CommissionRuleScope = {
  System: 1,
  Category: 2,
  Vendor: 3,
  VendorCategory: 4,
  Service: 5,
  Combo: 6,
} as const;
export type CommissionRuleScope = (typeof CommissionRuleScope)[keyof typeof CommissionRuleScope];

export const CommissionBasisKind = {
  StandardService: 1,
  AzManagementFee: 2,
  ComboComponents: 3,
} as const;
export type CommissionBasisKind = (typeof CommissionBasisKind)[keyof typeof CommissionBasisKind];

export const CommissionPolicyStatus = {
  Draft: 1,
  Submitted: 2,
  Approved: 3,
  Rejected: 4,
} as const;
export type CommissionPolicyStatus = (typeof CommissionPolicyStatus)[keyof typeof CommissionPolicyStatus];

export const CommissionAdjustmentStatus = {
  Submitted: 1,
  Approved: 2,
  Rejected: 3,
} as const;
export type CommissionAdjustmentStatus = (typeof CommissionAdjustmentStatus)[keyof typeof CommissionAdjustmentStatus];

export interface CommissionPolicyRuleDto {
  id: string;
  scope: CommissionRuleScope;
  categoryId?: string | null;
  vendorId?: string | null;
  serviceItemId?: string | null;
  comboTemplateId?: string | null;
  comboTemplateRevisionId?: string | null;
  rate?: number | null;
  basisKind: CommissionBasisKind;
  policyVersion: string;
  reason: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  status: CommissionPolicyStatus;
  createdById: string;
  createdAt: string;
  submittedAt?: string | null;
  approvedById?: string | null;
  approvedAt?: string | null;
  rejectionReason?: string | null;
  version: string;
}

export interface CreateCommissionPolicyRuleCommand {
  scope: CommissionRuleScope;
  categoryId?: string | null;
  vendorId?: string | null;
  serviceItemId?: string | null;
  comboTemplateId?: string | null;
  comboTemplateRevisionId?: string | null;
  rate?: number | null;
  basisKind: CommissionBasisKind;
  policyVersion: string;
  reason: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export interface CommissionAdjustmentDto {
  id: string;
  contractId: string;
  commissionId: string;
  vendorId: string;
  requestedById: string;
  commissionVersionSnapshot: string;
  originalRateSnapshot: number;
  originalAmountSnapshot: number;
  outstandingAmountSnapshot: number;
  proposedRate: number;
  reason: string;
  status: CommissionAdjustmentStatus;
  decidedById?: string | null;
  decidedAt?: string | null;
  decisionReason?: string | null;
  createdAt: string;
  version: string;
}

export const useCommissionPolicyRules = (status?: CommissionPolicyStatus) =>
  useQuery({
    queryKey: ["commission-policy-rules", status ?? "all"],
    queryFn: async () => {
      const { data } = await axiosClient.get<CommissionPolicyRuleDto[]>("/api/commission-policies/rules", {
        params: status ? { status } : undefined,
      });
      return data;
    },
  });

export const useCreateCommissionPolicyRule = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (command: CreateCommissionPolicyRuleCommand) => {
      const { data } = await axiosClient.post<CommissionPolicyRuleDto>("/api/commission-policies/rules", command);
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["commission-policy-rules"] });
    },
  });
};

export const useSubmitCommissionPolicyRule = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, expectedVersion }: { id: string; expectedVersion: string }) => {
      const { data } = await axiosClient.post<CommissionPolicyRuleDto>(
        `/api/commission-policies/rules/${id}/submit`,
        { expectedVersion }
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["commission-policy-rules"] });
    },
  });
};

export const useReviewCommissionPolicyRule = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      expectedVersion,
      approve,
      reason,
    }: {
      id: string;
      expectedVersion: string;
      approve: boolean;
      reason?: string;
    }) => {
      const { data } = await axiosClient.post<CommissionPolicyRuleDto>(
        `/api/commission-policies/rules/${id}/review`,
        { expectedVersion, approve, reason }
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["commission-policy-rules"] });
    },
  });
};

export const useCommissionAdjustments = (status?: CommissionAdjustmentStatus) =>
  useQuery({
    queryKey: ["commission-adjustments", status ?? "all"],
    queryFn: async () => {
      const { data } = await axiosClient.get<CommissionAdjustmentDto[]>("/api/commission-adjustments", {
        params: status ? { status } : undefined,
      });
      return data;
    },
  });

export const useDecideCommissionAdjustment = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      expectedVersion,
      approve,
      reason,
    }: {
      id: string;
      expectedVersion: string;
      approve: boolean;
      reason: string;
    }) => {
      const { data } = await axiosClient.post<CommissionAdjustmentDto>(
        `/api/commission-adjustments/${id}/decision`,
        { expectedVersion, approve, reason }
      );
      return data;
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["commission-adjustments"] });
    },
  });
};
