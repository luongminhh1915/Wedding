import type { CategoryDto } from "../../types/category.types";

/** Group active server categories by policy, never by a shop's historical membership or rate. */
export function vendorCategoryGroups(categories: readonly CategoryDto[]) {
  const groups = new Map<string, { id: string; label: string; categories: CategoryDto[] }>();
  for (const category of categories) {
    const id = category.businessGroupId ?? "unmapped";
    if (!groups.has(id)) groups.set(id, { id, label: category.businessGroupName || "Danh mục dịch vụ", categories: [] });
    groups.get(id)!.categories.push(category);
  }
  return [...groups.values()];
}

export function vendorCategoryError(categoryId: string, categories: readonly Pick<CategoryDto, "id">[] | undefined, pending: boolean, failed: boolean) {
  if (pending) return "Đang tải danh mục dịch vụ. Vui lòng chờ.";
  if (failed || !categories) return "Chưa tải được danh mục dịch vụ. Hãy thử lại trước khi tạo dịch vụ.";
  if (!categoryId) return "Vui lòng chọn danh mục dịch vụ.";
  if (!categories.some(category => category.id === categoryId)) return "Danh mục không tồn tại hoặc đã ngừng hoạt động. Vui lòng tải lại và chọn danh mục khác.";
  return null;
}
