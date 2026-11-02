import type { CatalogTemplate } from "../comboApi.ts";

export const VENDOR_COMBO_STATUSES = ["Draft", "Pending", "Approved", "Rejected", "Inactive"] as const;
export function vendorComboPage(templates: CatalogTemplate[], keyword: string, status: string, requestedPage: number, pageSize: number) {
  const search = keyword.trim().toLocaleLowerCase("vi-VN");
  const filtered = templates.filter(template => {
    const proposal = template.latestRevision?.proposal;
    const name = proposal?.name ?? template.name;
    const moderation = template.latestRevision?.status ?? template.moderationStatus;
    return (!search || `${name} ${template.name} ${template.region ?? ""}`.toLocaleLowerCase("vi-VN").includes(search)) && (!status || (status === "Inactive" ? !template.isActive : moderation === status));
  });
  const safeSize = [6, 12, 24].includes(pageSize) ? pageSize : 6;
  const totalPages = Math.max(1, Math.ceil(filtered.length / safeSize));
  const page = Math.min(Math.max(1, Number.isSafeInteger(requestedPage) ? requestedPage : 1), totalPages);
  return { items: filtered.slice((page - 1) * safeSize, page * safeSize), totalCount: filtered.length, page, pageSize: safeSize };
}
