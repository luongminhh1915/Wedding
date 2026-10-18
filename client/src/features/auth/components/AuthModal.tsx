import { useEffect, useRef } from "react";
import { X } from "lucide-react";
import { useLocation } from "react-router-dom";
import LoginForm from "./LoginForm";
export default function AuthModal({ onClose }: { onClose: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const location = useLocation();
  const initialLocation = useRef(location.key);
  useEffect(() => { const element = dialog.current; element?.showModal(); return () => element?.close(); }, []);
  useEffect(() => { if (location.key !== initialLocation.current) onClose(); }, [location.key, onClose]);
  return <dialog ref={dialog} onCancel={onClose} onClick={(e) => { if (e.target === e.currentTarget) onClose(); }} className="wm-auth-dialog">
    <button className="wm-auth-close" type="button" aria-label="Đóng đăng nhập" onClick={onClose}><X size={20} /></button>
    <LoginForm embedded onComplete={onClose} />
  </dialog>;
}
