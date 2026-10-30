export function paginationWindow(page: number, pageSize: number, totalCount: number) {
  const size = Math.max(1, Math.floor(pageSize) || 1);
  const total = Math.max(0, Math.floor(totalCount) || 0);
  const pages = Math.max(1, Math.ceil(total / size));
  const current = Math.max(1, Math.min(Math.floor(page) || 1, pages));
  return { pages, current, first: total ? (current - 1) * size + 1 : 0, last: Math.min(current * size, total), hasPrevious: current > 1, hasNext: current < pages };
}
