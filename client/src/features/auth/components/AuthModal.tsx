import { useEffect, useRef } from "react";
import { X } from "lucide-react";
import LoginForm from "./LoginForm";
export default function AuthModal({ onClose }: { onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  useEffect(() => { const element = dialog.current; element?.showModal(); return () => element?.close(); }, []);
  return <dialog ref={dialog} onCancel={onClose} onClick={(e) => { if (e.target === e.currentTarget) onClose(); }} className="wm-auth-dialog">
    <button className="wm-auth-close" type="button" aria-label="Đóng đăng nhập" onClick={onClose}><X size={20} /></button>
    <LoginForm embedded onComplete={onClose} />
  </dialog>;
}
