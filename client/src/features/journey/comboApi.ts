import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import axios from "../../services/axiosClient";
import type { ComboBuilder, ComboOption, ComboOrder, ComboTemplate } from "./api";
import type { PagedResult } from "../../components/Pagination";
import { useAuthStore } from "../../store/authStore";
import type { ServiceDetailsDto } from "../../types/listing.types";

export interface Variant { id?: string | null; name: string; priceDelta: number }
export interface AddOn { id?: string | null; name: string; price: number; perUnit: boolean }
export interface CatalogItem { id: string; listingId: string; categoryId: string; vendorId: string; vendorName: string; name: string; basePrice: number; unit: string; minGuests: number; maxGuests: number; regions: string; cancellationPolicy: string; moderationStatus: string; reviewNote?: string; reviewVersion?: string; isActive: boolean; maxBookingsPerDay: number; variants: Variant[]; addOns: AddOn[]; imageUrl?: string | null; categoryName?: string; listingStatus?: string; listingIsActive?: boolean; isCanonical?: boolean }
export interface CatalogOption extends ComboOption { serviceItemId: string; unit: string; minGuests: number; maxGuests: number; cancellationPolicy: string; variants: Variant[]; addOns: AddOn[]; galleryUrls?: string[]; description?: string }
export interface PublicComboVendor { id: string; name: string; slug: string; logoUrl?: string | null; coverImageUrl?: string | null; city: string; address?: string | null; bio?: string | null; isVerified: boolean; ratingAvg: number | null; totalReviews: number }
export interface ComboTemplateContent { galleryUrls: string[]; terms: string; validFromUtc: string | null; validUntilUtc: string | null; detailedDescription?: string | null }
export interface ComboComponent { serviceItemId: string; listingId: string; vendorId: string; categoryId: string; name: string; listingTitle: string; basePrice: number; unit: string; quantity: number; minGuests: number; maxGuests: number; regions: string; cancellationPolicy: string; description: string; imageUrl?: string | null; galleryUrls: string[]; serviceDetails?: ServiceDetailsDto | null; variants: Variant[]; addOns: AddOn[]; sourceVersion: string }
export interface FixedComboFields { pricingMode?: string; comboPrice?: number | null; originalPrice?: number | null; savingsAmount?: number; savingsPercentage?: number; components?: ComboComponent[]; requiresComponentReview?: boolean }
export interface ComboTemplateProposal extends FixedComboFields { name: string; tier: string; style: string; description?: string | null; imageUrl?: string | null; isActive: boolean; sortOrder: number; ownerVendorId?: string | null; region?: string | null; singleVendorDiscount?: number | null; items: CatalogTemplate["items"]; content?: ComboTemplateContent | null }
export interface ComboTemplateRevision { id: string; sequence: number; status: string; createdById?: string | null; createdByRole?: string | null; createdAt: string; submittedAt?: string | null; reviewedById?: string | null; reviewedByRole?: string | null; reviewedAt?: string | null; reviewNote?: string | null; proposal?: ComboTemplateProposal | null; supersededAt?: string | null; supersededFromStatus?: string | null }
export interface CatalogTemplate extends ComboTemplate, FixedComboFields { ownerVendorId?: string; ownerVendorName?: string; region?: string; moderationStatus: string; singleVendorDiscount?: number; sortOrder?: number; content?: ComboTemplateContent | null; latestRevision?: ComboTemplateRevision | null; revisionHistory?: ComboTemplateRevision[] | null; previewOptions?: CatalogOption[] | null; items: (ComboTemplate["items"][number] & { serviceItemId?: string; allowedItemIds?: string[] })[] }
export interface CatalogBuilder extends Omit<ComboBuilder, "categories" | "templates"> { categories: (Omit<ComboBuilder["categories"][number], "options"> & { options: CatalogOption[] })[]; templates: CatalogTemplate[]; vendors?: PublicComboVendor[]; previewGuestCount?: number }
export interface ComboSelection { serviceItemId: string; variantIds: string[]; addOnIds: string[]; hours: number }
export interface PriceInput { templateId?: string | null; region: string; weddingDate: string; guestCount: number; selections: ComboSelection[]; revisionOfId?: string }
export interface PriceLine { serviceItemId: string; listingId: string; vendorId: string; categoryId: string; vendorName: string; title: string; unit: string; quantity: number; price: number; discountShare: number; total: number; cancellationPolicy: string; availability: string }
export interface ComboPrice { subtotal: number; discountPercent: number; discountAmount: number; total: number; lines: PriceLine[]; warnings: string[]; plannerRemaining?: number; plannerReservedBeforeCart?: number; plannerReplacedEstimate?: number; plannerBudgetTotal?: number }
export interface SavedCombo extends ComboOrder { region: string; comboTemplateId?: string; priceLockUntil?: string; note?: string; revisionOfId?: string; revisionReason?: string; items: (ComboOrder["items"][number] & { serviceItemId?: string; categoryId: string; quantity: number; unit: string; cancellationPolicy: string; selectionJson: string; component?: ComboComponent | null })[] }
export const useSavedCombos = () => useQuery({ queryKey: ["my-combos"], queryFn: async () => (await axios.get<SavedCombo[]>("/api/combos/orders/my")).data });
export const useCatalogBuilder = (enabled = true) => useQuery({ queryKey: ["combo-builder"], enabled, queryFn: async () => (await axios.get<CatalogBuilder>("/api/combos/builder")).data });
export interface CatalogTemplateEstimate { templateId: string; subtotal: number; savings: number; total: number; discountPercent: number; requiredServiceCount: number; guests: number; shopRating: number | null }
export interface CatalogPage { builder: CatalogBuilder; selectedOptions: CatalogOption[]; selectedTemplate?: CatalogTemplate | null; page: number; pageSize: number; totalCount: number; categoryCounts: { categoryId: string; count: number }[]; estimates?: CatalogTemplateEstimate[]; categoryId?: string | null }
export interface CatalogPageInput { categoryId?: string; vendorId?: string; listingId?: string; search?: string; sort?: string; page: number; pageSize: number; selectedItemIds: string[]; templateId?: string | null; guests?: number; minPrice?: number; maxPrice?: number; city?: string; region?: string; minServices?: number; maxServices?: number; minSavings?: number; minRating?: number; templateCategoryId?: string; filterVendorId?: string }
export const useCatalogPage = (input: CatalogPageInput) => useQuery({ queryKey: ["combo-catalog-page", input], placeholderData: previous => previous, queryFn: async () => (await axios.get<CatalogPage>("/api/combos/catalog", { params: input, paramsSerializer: { indexes: null } })).data });
export function useSavedCombosPage(page: number, pageSize: number, filter: string, focusId?: string) {
  const user = useAuthStore(state => state.user);
  return useQuery({ queryKey: ["my-combos", user?.id, "page", page, pageSize, filter, focusId], enabled: user?.role === "Customer", queryFn: async () => (await axios.get<PagedResult<SavedCombo> & { filter: string; focusFound: boolean }>("/api/combos/orders/my/page", { params: { page, pageSize, filter, focusId } })).data });
}
export function useCatalog(admin = false) {
  const { user, token } = useAuthStore();
  return useQuery({ queryKey: ["combo-catalog", admin, user?.id], enabled: !!token && (admin ? ["Moderator", "SuperAdmin"].includes(user?.role ?? "") : user?.role === "VendorOwner"), queryFn: async () => (await axios.get<CatalogItem[]>(`/api/combos/${admin ? "items" : "vendor/items"}`)).data });
}
export function useParticipation() {
  const { user, token } = useAuthStore();
  return useQuery({ queryKey: ["combo-participation", user?.id], enabled: !!token && user?.role === "VendorOwner", queryFn: async () => (await axios.get<{ allowInCombos: boolean; maxComboDiscount: number }>("/api/combos/vendor/participation")).data });
}
export function useComboTemplates() {
  const { user, token } = useAuthStore();
  return useQuery({ queryKey: ["combo-templates-admin", user?.id], enabled: !!token && ["Moderator", "SuperAdmin"].includes(user?.role ?? ""), queryFn: async () => (await axios.get<CatalogTemplate[]>("/api/combos/templates")).data });
}
export function useVendorComboTemplates() {
  const { user, token } = useAuthStore();
  return useQuery({ queryKey: ["combo-templates-vendor", user?.id], enabled: !!token && user?.role === "VendorOwner", queryFn: async () => (await axios.get<CatalogTemplate[]>("/api/combos/vendor/templates")).data });
}
export function useComboTemplatePreview(id: string, revisionId: string | null, enabled: boolean) {
  const { user, token } = useAuthStore();
  return useQuery({ queryKey: ["combo-template-preview", user?.id, user?.role, id, revisionId], enabled: enabled && !!id && !!token && ["VendorOwner", "Moderator", "SuperAdmin"].includes(user?.role ?? ""), retry: false, queryFn: async () => (await axios.get<CatalogTemplate>(`/api/combos/templates/${id}/preview`, { params: { revisionId: revisionId ?? undefined } })).data });
}
export const useRules = () => useQuery({ queryKey: ["combo-rules"], queryFn: async () => (await axios.get<{ minItems: number; percent: number }[]>("/api/combos/bundle-rules")).data });
export function useComboMutation<T = unknown>(path: string) {
  const qc = useQueryClient();
  return useMutation({ mutationFn: async (b: unknown) => (await axios.post<T>(path, b)).data, onSuccess: () => qc.invalidateQueries() });
}
export const useServerPrice = (input: PriceInput, enabled: boolean) => useQuery({ queryKey: ["combo-price", input], enabled, retry: false, queryFn: async () => (await axios.post<ComboPrice>("/api/combos/price", input)).data });
export function useSubmitCatalogCombo() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: async (b: PriceInput & { phoneNumber?: string; note?: string; expectedTotal: number; saveAsDraft?: boolean; draftId?: string; revisionOfId?: string; revisionReason?: string }) => (await axios.post<ComboOrder>("/api/combos/orders", { ...b, listingIds: [] })).data, onSuccess: () => qc.invalidateQueries() });
}
