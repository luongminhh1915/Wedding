import { useEffect, useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import "./pagination.css";
import { paginationWindow } from "./paginationModel";

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** Smaller pages on phones; media changes do not discard filters or selections. */
export function useResponsivePageSize(desktop = 6, mobile = 4) {
  const [compact, setCompact] = useState(() =>
    typeof window !== "undefined" && window.matchMedia("(max-width: 600px)").matches,
  );
  useEffect(() => {
    const media = window.matchMedia("(max-width: 600px)");
    const update = () => setCompact(media.matches);
    media.addEventListener("change", update);
    return () => media.removeEventListener("change", update);
  }, []);
  return compact ? mobile : desktop;
}

export function Pagination({ page, pageSize, totalCount, onPageChange, isLoading = false, label = "Phân trang danh sách" }: {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
  isLoading?: boolean;
  label?: string;
}) {
  const { pages, current, first, last, hasPrevious, hasNext } = paginationWindow(page, pageSize, totalCount);
  if (totalCount === 0) return null;
  return (
    <nav className="wm-pagination" aria-label={label} aria-busy={isLoading}>
      <span className="wm-pagination-count">{first}–{last} / {totalCount}</span>
      <div>
        <button type="button" disabled={isLoading || !hasPrevious} onClick={() => onPageChange(current - 1)} aria-label="Trang trước"><ChevronLeft size={16} /><span>Trước</span></button>
        <span aria-live="polite" aria-atomic="true">Trang {current} / {pages}</span>
        <button type="button" disabled={isLoading || !hasNext} onClick={() => onPageChange(current + 1)} aria-label="Trang sau"><span>Sau</span><ChevronRight size={16} /></button>
      </div>
    </nav>
  );
}

export function scrollToList(element: HTMLElement | null) {
  if (!element) return;
  element.scrollIntoView({ block: "start", behavior: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth" });
}
