import { useEffect, useRef, type ReactNode } from "react";
import { X } from "lucide-react";
export function ComboDialog({ title, onClose, children, wide = false }: { title: string; onClose: () => void; children: ReactNode; wide?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => { const dialog = ref.current; dialog?.showModal(); const previous = document.body.style.overflow; document.body.style.overflow = "hidden"; return () => { dialog?.close(); document.body.style.overflow = previous; }; }, []);
  return <dialog ref={ref} className={`wm-combo-dialog wd ${wide ? "is-wide" : ""}`} aria-label={title} onCancel={onClose} onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}>
    <div className="combo-dialog-header"><h2>{title}</h2><button type="button" className="combo-icon-button" aria-label="Đóng" onClick={onClose}><X size={20} /></button></div>
    {children}
  </dialog>;
}
