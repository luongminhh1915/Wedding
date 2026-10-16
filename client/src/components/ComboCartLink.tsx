import { ShoppingBag } from "lucide-react";
import { Link } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import { useComboCartStore } from "../store/comboCartStore";
export default function ComboCartLink() {
  const user = useAuthStore((s) => s.user);
  const count = useComboCartStore((s) => s.carts[user?.id ?? "guest"]?.selections.length ?? 0);
  if (user && user.role !== "Customer") return null;
  return <Link className="wm-cart-link" to="/combo" aria-label={`Giỏ combo, ${count} dịch vụ`}><ShoppingBag size={18} /><span>{count}</span></Link>;
}
