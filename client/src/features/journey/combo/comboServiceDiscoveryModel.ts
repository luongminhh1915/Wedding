import type { CatalogPage, CatalogPageInput } from '../comboApi';
import type { ComboFilters } from './comboFilterModel';

/** The package page has category metadata, not service rows. Load one bounded
 * service preview explicitly; never use the unpaged builder or cart hydration as discovery. */
export function comboServicePreviewInput(
  catalog: CatalogPage | undefined,
  filters: ComboFilters,
  vendorId: string | null | undefined,
  guests: number,
): CatalogPageInput | null {
  if (!catalog || filters.listing || filters.category && filters.category !== 'templates') return null;
  const category = catalog.builder.categories.find(item => item.categoryId === filters.templateCategoryId) ??
    catalog.builder.categories.find(item => catalog.categoryCounts.some(count => count.categoryId === item.categoryId && count.count > 0));
  if (!category) return null;
  return {
    categoryId: category.categoryId, vendorId: vendorId ?? undefined,
    filterVendorId: filters.filterVendorId, search: filters.search,
    minPrice: filters.minPrice, maxPrice: filters.maxPrice, city: filters.city,
    region: filters.region, minRating: filters.minRating,
    sort: filters.sort === 'savings' ? 'recommended' : filters.sort,
    guests, page: 1, pageSize: 4, selectedItemIds: [],
  };
}
