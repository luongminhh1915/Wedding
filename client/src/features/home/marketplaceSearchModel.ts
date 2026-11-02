import type { PublicListingsFilter } from "../../types/category.types";

export interface MarketplaceSearchDraft {
  categoryId: string;
  keyword: string;
  location: string;
  maxPrice: string;
}

const allowedBudgetCeilings = new Set([10_000_000, 30_000_000, 80_000_000, 150_000_000, 300_000_000]);

/** Turns the homepage quick-search into the same URL-backed service filters used by /browse. */
export function marketplaceSearchFilters(draft: MarketplaceSearchDraft): PublicListingsFilter {
  const parsedBudget = Number(draft.maxPrice);
  return {
    categoryId: draft.categoryId || undefined,
    keyword: draft.keyword.trim().slice(0, 150) || undefined,
    location: draft.location.trim().slice(0, 100) || undefined,
    maxPrice: allowedBudgetCeilings.has(parsedBudget) ? parsedBudget : undefined,
    sortBy: "popular",
    page: 1,
    pageSize: 12,
  };
}
