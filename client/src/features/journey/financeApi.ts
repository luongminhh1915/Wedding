import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axiosClient from "../../services/axiosClient";
import type { Settlement } from "./api";
import type { Quote, QuoteLine } from "./api";
export type FinancialQuote = Quote & {
  subtotal: number;
  vatRate: number;
  vatAmount: number;
};
export const useFinancialQuotes = (leadId?: string) =>
  useQuery({
    queryKey: ["quotes", leadId ?? "all"],
    queryFn: async () =>
      (
        await axiosClient.get<FinancialQuote[]>("/api/quotes", {
          params: { leadId },
        })
      ).data,
  });
export function useSendFinancialQuote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (b: {
      leadId: string;
      items: QuoteLine[];
      depositAmount: number;
      terms?: string;
      validDays: number;
      vatRate: number;
    }) => (await axiosClient.post<FinancialQuote>("/api/quotes", b)).data,
    onSuccess: () => qc.invalidateQueries(),
  });
}

export interface PremiumOrderInfo {
  id: string;
  isPremium: boolean;
  price: number;
  paymentRef: string;
  qrUrl: string;
  status: string;
  activatedAt?: string;
  expiresAt: string;
}
export interface PaymentReceipt {
  id: string;
  transactionId: string;
  amount: number;
  content: string;
  status: string;
  unmatchedReason?: string;
  settlementId?: string;
  premiumOrderId?: string;
  createdAt: string;
}
export interface CreditNote {
  id: string;
  contractId: string;
  vendorId: string;
  amount: number;
  appliedAmount: number;
  reason: string;
}
export const billingApi = {
  order: async () =>
    (await axiosClient.post<PremiumOrderInfo>("/api/billing/premium/order"))
      .data,
  me: async () =>
    (await axiosClient.get<PremiumOrderInfo | null>("/api/billing/me")).data,
};
export const useUnmatchedReceipts = () =>
  useQuery({
    queryKey: ["payment-unmatched"],
    queryFn: async () =>
      (await axiosClient.get<PaymentReceipt[]>("/api/billing/unmatched")).data,
  });
export const useCreditNotes = () =>
  useQuery({
    queryKey: ["commission-credits"],
    queryFn: async () =>
      (await axiosClient.get<CreditNote[]>("/api/settlements/credits")).data,
  });
export const useLedgerStatements = (period?: string) =>
  useQuery({
    queryKey: ["settlements", period ?? ""],
    queryFn: async () =>
      (
        await axiosClient.get<Settlement[]>("/api/settlements", {
          params: { period },
        })
      ).data,
  });
export function useRecordReceipt() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (b: {
      id: string;
      transactionId: string;
      amount: number;
      note: string;
    }) =>
      (
        await axiosClient.post<PaymentReceipt>(
          `/api/settlements/${b.id}/payment`,
          b,
        )
      ).data,
    onSuccess: () => qc.invalidateQueries(),
  });
}
export function useMatchReceipt() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (b: {
      id: string;
      settlementId?: string;
      premiumOrderId?: string;
      note: string;
    }) =>
      (
        await axiosClient.post<PaymentReceipt>(
          `/api/billing/unmatched/${b.id}/match`,
          b,
        )
      ).data,
    onSuccess: () => qc.invalidateQueries(),
  });
}
export function useRecordInvoice() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (b: {
      id: string;
      provider: string;
      invoiceNumber: string;
      lookupUrl: string;
      vatRate: number;
    }) => (await axiosClient.post(`/api/settlements/${b.id}/invoice`, b)).data,
    onSuccess: () => qc.invalidateQueries(),
  });
}
