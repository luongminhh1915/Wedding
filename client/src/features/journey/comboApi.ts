import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "../../services/axiosClient";
import type { ComboBuilder, ComboOption, ComboOrder, ComboTemplate } from "./api";

export interface Variant { id?: string | null; name: string; priceDelta: number }
export interface AddOn { id?: string | null; name: string; price: number; perUnit: boolean }
export interface CatalogItem { id: string; listingId: string; categoryId: string; vendorId: string; vendorName: string; name: string; basePrice: number; unit: string; minGuests: number; maxGuests: number; regions: string; cancellationPolicy: string; moderationStatus: string; reviewNote?: string; isActive: boolean; maxBookingsPerDay: number; variants: Variant[]; addOns: AddOn[] }
export interface CatalogOption extends ComboOption { serviceItemId: string; unit: string; minGuests: number; maxGuests: number; cancellationPolicy: string; variants: Variant[]; addOns: AddOn[] }
export interface CatalogTemplate extends ComboTemplate { ownerVendorId?: string; ownerVendorName?: string; region?: string; moderationStatus: string; singleVendorDiscount?: number; items: (ComboTemplate["items"][number] & { serviceItemId?: string; allowedItemIds?: string[] })[] }
export interface CatalogBuilder extends Omit<ComboBuilder, "categories" | "templates"> { categories: (Omit<ComboBuilder["categories"][number], "options"> & { options: CatalogOption[] })[]; templates: CatalogTemplate[] }
export interface ComboSelection { serviceItemId: string; variantIds: string[]; addOnIds: string[]; hours: number }
export interface PriceInput { templateId?: string | null; region: string; weddingDate: string; guestCount: number; selections: ComboSelection[] }
export interface PriceLine { serviceItemId: string; listingId: string; vendorId: string; categoryId: string; vendorName: string; title: string; unit: string; quantity: number; price: number; discountShare: number; total: number; cancellationPolicy: string; availability: string }
export interface ComboPrice { subtotal: number; discountPercent: number; discountAmount: number; total: number; lines: PriceLine[]; warnings: string[]; plannerRemaining?: number }
export interface SavedCombo extends ComboOrder { region: string; comboTemplateId?: string; priceLockUntil?: string; items: (ComboOrder["items"][number] & { serviceItemId?: string; categoryId: string; quantity: number; unit: string; cancellationPolicy: string; selectionJson: string })[] }
export const useSavedCombos = () => useQuery({ queryKey: ["my-combos"], queryFn: async () => (await axios.get<SavedCombo[]>("/api/combos/orders/my")).data });
export const useCatalogBuilder = () => useQuery({ queryKey: ["combo-builder"], queryFn: async () => (await axios.get<CatalogBuilder>("/api/combos/builder")).data });
export const useCatalog = (admin = false) => useQuery({ queryKey: ["combo-catalog", admin], queryFn: async () => (await axios.get<CatalogItem[]>(`/api/combos/${admin ? "items" : "vendor/items"}`)).data });
export const useParticipation = () => useQuery({ queryKey: ["combo-participation"], queryFn: async () => (await axios.get<{ allowInCombos: boolean; maxComboDiscount: number }>("/api/combos/vendor/participation")).data });
export const useComboTemplates = () => useQuery({ queryKey: ["combo-templates-admin"], queryFn: async () => (await axios.get<CatalogTemplate[]>("/api/combos/templates")).data });
export const useVendorComboTemplates = () => useQuery({ queryKey: ["combo-templates-vendor"], queryFn: async () => (await axios.get<CatalogTemplate[]>("/api/combos/vendor/templates")).data });
export const useRules = () => useQuery({ queryKey: ["combo-rules"], queryFn: async () => (await axios.get<{ minItems: number; percent: number }[]>("/api/combos/bundle-rules")).data });
export function useComboMutation<T = unknown>(path: string) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: async (b: unknown) => (await axios.post<T>(path, b)).data, onSuccess: () => qc.invalidateQueries() });
}
export const useServerPrice = (input: PriceInput, enabled: boolean) => useQuery({ queryKey: ["combo-price", input], enabled, retry: false, queryFn: async () => (await axios.post<ComboPrice>("/api/combos/price", input)).data });
export function useSubmitCatalogCombo() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: async (b: PriceInput & { phoneNumber?: string; note?: string; expectedTotal: number; saveAsDraft?: boolean; draftId?: string }) => (await axios.post<ComboOrder>("/api/combos/orders", { ...b, listingIds: [] })).data, onSuccess: () => qc.invalidateQueries() });
}
