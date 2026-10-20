import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "../../../services/axiosClient";
import { useAuthStore } from "../../../store/authStore";

export interface OrderGroupPage<T> { items: T[]; metadata: OrderJourneyItem[]; page: number; pageSize: number; totalCount: number; filter: string; focusFound: boolean }
export function useCustomerOrderPage<T>(kind: "lead" | "quote" | "contract", page: number, pageSize: number, filter: string, focusId?: string) {
  const user = useAuthStore(state => state.user);
  return useQuery({
    queryKey: ["customer-order-page", user?.id, kind, page, pageSize, filter, focusId],
    enabled: user?.role === "Customer",
    queryFn: async () => (await axios.get<OrderGroupPage<T>>("/api/customer-orders/page", { params: { kind, page, pageSize, filter, focusId } })).data,
  });
}

export type OrderKind = "combo" | "lead" | "quote" | "contract";
export interface OrderMediaItem {
  listingId: string;
  serviceItemId?: string | null;
  title: string;
  imageUrl?: string | null;
  galleryUrls: string[];
}
export interface OrderMedia {
  title: string;
  imageUrl?: string | null;
  galleryUrls: string[];
  vendorId?: string | null;
  vendorName?: string | null;
  vendorLogoUrl?: string | null;
  vendorCoverImageUrl?: string | null;
  listingId?: string | null;
  items?: OrderMediaItem[];
}
export interface OrderCapabilities {
  canEdit: boolean;
  canWithdraw: boolean;
  canArchive: boolean;
  canRestore: boolean;
  canRequestChanges: boolean;
  canStopPending: boolean;
  canRequestChange: boolean;
  canRequestCancellation: boolean;
  canConfirm?: boolean;
  canRespondQuote?: boolean;
  blockedReason?: string | null;
}
export interface OrderChangeRequest {
  id: string;
  kind: string;
  entityId: string;
  customerId: string;
  vendorId: string;
  type: "Change" | "Cancellation";
  reason: string;
  proposedChanges?: string | null;
  status: string;
  createdAt: string;
  vendorReviewedAt?: string | null;
  vendorResponse?: string | null;
  financeReviewedAt?: string | null;
  financeResponse?: string | null;
  requiresFinance: boolean;
}
export interface OrderJourneyItem {
  kind: OrderKind;
  id: string;
  isArchived: boolean;
  archivedAt?: string | null;
  revisionOfId?: string | null;
  supersededById?: string | null;
  comboOrderId?: string | null;
  comboOrderCode?: string | null;
  revisionReason?: string | null;
  withdrawalReason?: string | null;
  media: OrderMedia;
  capabilities: OrderCapabilities;
  requests: OrderChangeRequest[];
}
export type CustomerOrderAction = {
  kind: OrderKind;
  id: string;
  action:
    "archive" | "restore" | "withdraw" | "stop-pending" | "requests" | "revise";
  body?: Record<string, unknown>;
};
export const orderJourneyKey = (kind: OrderKind, id: string) => `${kind}:${id}`;

export function useCustomerOrderJourney(comboIds?: string[]) {
  const user = useAuthStore((state) => state.user);
  return useQuery({
    queryKey: ["customer-order-journey", user?.id, comboIds],
    enabled: user?.role === "Customer" && (comboIds === undefined || comboIds.length > 0),
    queryFn: async () =>
      (await axios.get<OrderJourneyItem[]>("/api/customer-orders", { params: { comboIds }, paramsSerializer: { indexes: null } })).data,
  });
}
export function useCustomerOrderAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ kind, id, action, body }: CustomerOrderAction) =>
      (
        await axios.post(
          `/api/customer-orders/${kind}/${id}/${action}`,
          body ?? {},
        )
      ).data,
    // A single lifecycle transition can affect quotes, contracts, the saved combo,
    // notifications and budget. Refresh all affected views after server success.
    onSuccess: () => queryClient.invalidateQueries(),
  });
}

export function useOrderRequestInbox(
  audience: "vendor" | "finance",
  enabled = true,
) {
  return useQuery({
    queryKey: ["order-request-inbox", audience],
    enabled,
    queryFn: async () =>
      (
        await axios.get<OrderChangeRequest[]>(
          `/api/customer-orders/requests/${audience}`,
        )
      ).data,
  });
}
export function useReviewOrderRequest(audience: "vendor" | "finance") {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      ...body
    }: {
      id: string;
      accept: boolean;
      response: string;
    }) =>
      (
        await axios.post(
          `/api/customer-orders/requests/${id}/${audience}-review`,
          body,
        )
      ).data,
    onSuccess: () => queryClient.invalidateQueries(),
  });
}
