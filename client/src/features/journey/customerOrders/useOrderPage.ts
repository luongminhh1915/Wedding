import { useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { scrollToList, useResponsivePageSize } from "../../../components/Pagination";

/** A notification can locate an archived/off-page record once. Explicit navigation
 * leaves that focus so it cannot keep forcing the user back to the old page. */
export function useOrderPage(kind: string, initialFilter = "all") {
  const [params, setParams] = useSearchParams();
  const focusId = params.get("focusKind") === kind ? params.get("focus") ?? undefined : undefined;
  const [filter, setFilterValue] = useState(initialFilter);
  const pageSize = useResponsivePageSize();
  const key = JSON.stringify([filter, pageSize, focusId]);
  const [pagination, setPagination] = useState({ key, page: 1 });
  const listRef = useRef<HTMLElement>(null);
  const clearFocus = () => {
    if (!focusId) return;
    setParams(previous => { const next = new URLSearchParams(previous); next.delete("focus"); next.delete("focusKind"); return next; }, { replace: true });
  };
  return {
    filter, pageSize, focusId, listRef,
    page: pagination.key === key ? pagination.page : 1,
    setFilter: (next: string) => { clearFocus(); setFilterValue(next); },
    changePage: (next: number, effectiveFilter = filter) => {
      clearFocus();
      setFilterValue(effectiveFilter);
      const nextKey = JSON.stringify([effectiveFilter, pageSize, undefined]);
      setPagination({ key: focusId ? nextKey : key, page: next });
      scrollToList(listRef.current);
    },
  };
}
