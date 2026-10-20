import type { CategoryDto } from "../../types/category.types";

/** Group selectable server categories, never derive shop permission from a displayed rate. */
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
  if (pending) return "Đang tải danh mục được phép của shop. Vui lòng chờ.";
  if (failed || !categories) return "Chưa kiểm tra được danh mục của shop. Hãy tải lại trước khi tạo dịch vụ.";
  if (!categoryId) return "Vui lòng chọn danh mục dịch vụ.";
  if (!categories.some(category => category.id === categoryId)) return "Danh mục không thuộc phạm vi được phép của shop. Vui lòng chọn lại hoặc liên hệ Admin.";
  return null;
}
